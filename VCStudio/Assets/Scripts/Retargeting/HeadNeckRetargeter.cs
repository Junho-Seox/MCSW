using UnityEngine;

public class HeadNeckRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;

    [Header("Automatic Head Alignment")]
    [Tooltip("ON: automatic reference (no Z key). OFF: original Z-key calibration.")]
    public bool useAutomaticAlignment = true;

    [Tooltip("Mapper captured before retargeting; must use same Animator.")]
    public AvatarRigMapper rigMapper;

    [Tooltip("Usually ON with Step 1A/B mapping where face right-left points toward -X.")]
    public bool sourceFaceRightIsNegativeX = true;

    [Header("Precision - Camera Facing Setup")]
    [Tooltip("For a fixed FRONT-facing webcam seat: choose face-right sign from stable initial frames and lock until next Play. Turn OFF if starting while looking away.")]
    public bool autoResolveFrontalFaceRightAtStartup = true;
    [Range(3, 30)] public int frontalSetupConsecutiveFrames = 8;
    [Range(15f, 80f)] public float frontalSetupMaxAngle = 60f;
    [Tooltip("Keep the previous face orientation briefly if eye/mouth landmarks disappear.")]
    [Range(0f, 0.5f)] public float trackingLossGraceSeconds = 0.20f;

    [Tooltip("Static pitch correction in degrees for a persistent frontal face-frame offset. Start at zero.")]
    [Range(-35f, 35f)] public float neutralFacePitchOffsetDegrees = 0f;

    [Tooltip("Static roll correction for frontal eye-line bias. Does not change tracking coordinates. Start at zero; test approximately -15 for the supplied frontal log.")]
    [Range(-45f, 45f)] public float neutralFaceRollOffsetDegrees = 0f;

    [Tooltip("How strongly the head follows absolute tracked world orientation. 1 = fully follow; 0 = inherit torso only.")]
    [Range(0f, 1f)] public float automaticHeadWorldFollow = 1f;

    [Tooltip("Clamp head rotation relative to the torso-inherited head pose, for neck safety.")]
    [Range(0f, 180f)] public float maxHeadRelativeToTorsoAngle = 80f;

    [Tooltip("Reject implausible face frames before applying head rotation.")]
    [Range(90f, 180f)] public float maximumSourceDeviation = 115f;

    public bool logFirstValidAutomaticPose = true;

    [Header("Optional Head Rotation Stabilization")]
    [Tooltip("Smooth the source head rotation delta, NOT the torso compensation. Disable for instant A/B comparison.")]
    public bool enableAdaptiveRotationSmoothing = true;

    [Tooltip("Smoothing cutoff in Hz for small angular errors. Lower values reduce jitter but add delay.")]
    [Range(0.5f, 30f)] public float stillRotationCutoffHz = 4f;

    [Tooltip("Smoothing cutoff in Hz for larger angular errors, to preserve responsiveness.")]
    [Range(0.5f, 30f)] public float movingRotationCutoffHz = 16f;

    [Tooltip("Angular error below which stillRotationCutoffHz is used.")]
    [Range(0f, 15f)] public float motionStartAngleDegrees = 0.6f;

    [Tooltip("Angular error above which movingRotationCutoffHz is used.")]
    [Range(0.1f, 30f)] public float motionFullAngleDegrees = 6f;

    [Tooltip("Optional low-frequency log of the instantaneous raw/filtered rotation gap.")]
    public bool logSmoothingDiagnostics = false;

    [Header("Legacy Coordinate Mapping (only if input is not normalized)")]
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Tracking")]
    [Range(0f, 1f)] public float minVisibility = 0.5f;
    [Tooltip("Mouth landmarks may be less confident than eyes; used only in automatic mode.")]
    [Range(0f, 1f)] public float minMouthVisibility = 0.35f;

    [Header("Rotation Distribution")]
    [Range(0f, 1f)] public float neckWeight = 0.25f;
    [Range(0f, 1f)] public float headWeight = 0.65f;

    [Header("Rotation Safety")]
    [Range(0f, 180f)] public float maxRotationAngle = 65f;

    private Transform neck;
    private Transform head;
    private Quaternion neckRestLocal;
    private Quaternion headRestLocal;

    private Quaternion currentRotation;
    private Quaternion referenceRotation;
    private Quaternion currentAutomaticSourceRotation = Quaternion.identity;
    private Vector3 lastFaceRight;
    private Vector3 lastFaceUp;
    private Vector3 lastFaceForward;

    private bool initialized;
    private bool hasPose;
    private bool calibrated;
    private bool inputIsNormalized;
    private bool loggedAutomaticPose;
    private bool warnedMissingMapper;
    private bool warnedSourceFrame;
    private bool warnedMissingFacePoints;
    private Quaternion filteredRootLocalDelta = Quaternion.identity;
    private bool hasFilteredRootLocalDelta;
    private float nextSmoothingDiagnosticTime;
    private bool frontalConventionLocked;
    private int frontalCandidate = -1;
    private int frontalConsecutiveFrames;
    private bool warnedFrontalSetup;
    private float lastValidPoseRealtime = -100f;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.LogError("[Head/Step2C] Valid Humanoid Animator required.", this);
            return;
        }

        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
        {
            Debug.LogError("[Head/Step2C] Head bone not found.", this);
            return;
        }

        headRestLocal = head.localRotation;
        if (neck != null)
            neckRestLocal = neck.localRotation;

        if (rigMapper == null)
            rigMapper = animator.GetComponent<AvatarRigMapper>();
        if (rigMapper == null)
            rigMapper = animator.GetComponentInChildren<AvatarRigMapper>();

        initialized = true;
        Debug.Log("[Head/Step2C] Ready. Mode=" +
            (useAutomaticAlignment ? "Automatic (no Z key)" : "Legacy (Z key)"), this);
    }

    private Vector3 Convert(PoseLandmark p)
    {
        if (inputIsNormalized)
            return new Vector3(p.x, p.y, p.z);

        return new Vector3(
            invertX ? -p.x : p.x,
            invertY ? -p.y : p.y,
            invertZ ? -p.z : p.z);
    }

    private bool Visible(PoseLandmark[] landmarks, int index, float threshold)
    {
        PoseLandmark p = landmarks[index];
        return p != null && !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z) &&
            !float.IsNaN(p.visibility) && p.visibility >= threshold;
    }

    public void SetPose(PoseLandmark[] landmarks)
    {
        SetPose(landmarks, false);
    }

    public void SetPose(PoseLandmark[] landmarks, bool isNormalized)
    {
        inputIsNormalized = isNormalized;
        hasPose = false;
        if (!initialized || landmarks == null || landmarks.Length != 33)
            return;

        if (useAutomaticAlignment)
        {
            SetAutomaticPose(landmarks);
            return;
        }

        hasFilteredRootLocalDelta = false;

        if (!Visible(landmarks, 2, minVisibility) ||
            !Visible(landmarks, 5, minVisibility) ||
            !Visible(landmarks, 0, minVisibility))
            return;

        Vector3 left = Convert(landmarks[2]);
        Vector3 right = Convert(landmarks[5]);
        if (Visible(landmarks, 7, minVisibility) &&
            Visible(landmarks, 8, minVisibility))
        {
            left = Convert(landmarks[7]);
            right = Convert(landmarks[8]);
        }

        Vector3 horizontal = right - left;
        Vector3 eyeCenter = (Convert(landmarks[2]) + Convert(landmarks[5])) * 0.5f;
        Vector3 forwardHint = Convert(landmarks[0]) - eyeCenter;
        if (horizontal.sqrMagnitude < 1e-6f || forwardHint.sqrMagnitude < 1e-6f)
            return;

        horizontal.Normalize();
        Vector3 forward = Vector3.ProjectOnPlane(forwardHint, horizontal);
        if (forward.sqrMagnitude < 1e-6f)
            return;
        forward.Normalize();
        Vector3 up = Vector3.Cross(forward, horizontal);
        if (up.sqrMagnitude < 1e-6f)
            return;
        currentRotation = Quaternion.LookRotation(forward, up.normalized);
        hasPose = true;
    }

    private void SetAutomaticPose(PoseLandmark[] landmarks)
    {
        if (!Visible(landmarks, 2, minVisibility) ||
            !Visible(landmarks, 5, minVisibility) ||
            !Visible(landmarks, 9, minMouthVisibility) ||
            !Visible(landmarks, 10, minMouthVisibility))
        {
            if (!warnedMissingFacePoints)
            {
                Debug.LogWarning("[Head/Step2C] Face eyes/mouth not confident. " +
                    "Auto head pose temporarily unavailable (does not affect torso/arms).", this);
                warnedMissingFacePoints = true;
            }
            return;
        }
        warnedMissingFacePoints = false;

        Vector3 eyeLeft = Convert(landmarks[2]);
        Vector3 eyeRight = Convert(landmarks[5]);
        Vector3 eyeCenter = (eyeLeft + eyeRight) * 0.5f;
        Vector3 mouthCenter = (Convert(landmarks[9]) + Convert(landmarks[10])) * 0.5f;

        Vector3 right = eyeRight - eyeLeft;
        if (sourceFaceRightIsNegativeX)
            right = -right;
        if (right.sqrMagnitude < 1e-8f)
            return;
        right.Normalize();

        Vector3 up = Vector3.ProjectOnPlane(eyeCenter - mouthCenter, right);
        if (up.sqrMagnitude < 1e-8f)
            return;
        up.Normalize();
        Vector3 forward = Vector3.Cross(right, up);
        if (forward.sqrMagnitude < 1e-8f)
            return;
        forward.Normalize();
        up = Vector3.Cross(forward, right).normalized;

        Quaternion faceOffset = Quaternion.Euler(
            neutralFacePitchOffsetDegrees, 0f, neutralFaceRollOffsetDegrees);
        Quaternion sourceFrame = Quaternion.LookRotation(forward, up);
        Quaternion correctedFrame = sourceFrame * faceOffset;

        if (autoResolveFrontalFaceRightAtStartup && !frontalConventionLocked)
        {
            Quaternion flippedFrame = Quaternion.LookRotation(-forward, up) * faceOffset;
            float primaryAngle = Quaternion.Angle(Quaternion.identity, correctedFrame);
            float flippedAngle = Quaternion.Angle(Quaternion.identity, flippedFrame);
            int candidate = flippedAngle < primaryAngle ? 1 : 0;
            if (Mathf.Min(primaryAngle, flippedAngle) <= frontalSetupMaxAngle)
            {
                frontalConsecutiveFrames = candidate == frontalCandidate
                    ? frontalConsecutiveFrames + 1 : 1;
                frontalCandidate = candidate;
                if (frontalConsecutiveFrames >= frontalSetupConsecutiveFrames)
                {
                    if (candidate == 1)
                    {
                        sourceFaceRightIsNegativeX = !sourceFaceRightIsNegativeX;
                        right = -right;
                        forward = -forward;
                        correctedFrame = flippedFrame;
                    }
                    frontalConventionLocked = true;
                    Debug.Log($"[Head/Alignment] FRONT convention locked. signNegativeX={sourceFaceRightIsNegativeX}, " +
                        $"sourceAngle={Quaternion.Angle(Quaternion.identity, correctedFrame):F1} deg, flipped={candidate == 1}.", this);
                }
            }
            else
            {
                frontalCandidate = -1;
                frontalConsecutiveFrames = 0;
                if (!warnedFrontalSetup)
                {
                    Debug.LogWarning($"[Head/Alignment] Neither face convention is front-facing " +
                        $"(angles {primaryAngle:F1}/{flippedAngle:F1}). Face webcam, or disable Auto Resolve Frontal Face Right At Startup.", this);
                    warnedFrontalSetup = true;
                }
            }
            if (!frontalConventionLocked) return;
        }

        lastFaceRight = right;
        lastFaceUp = up;
        lastFaceForward = forward;
        currentAutomaticSourceRotation = correctedFrame;
        hasPose = true;
        lastValidPoseRealtime = Time.realtimeSinceStartup;
    }

    public void Calibrate()
    {
        if (useAutomaticAlignment)
        {
            Debug.Log("[Head/Step2C] Z calibration ignored: automatic alignment enabled.", this);
            return;
        }
        if (!hasPose)
        {
            Debug.LogWarning("[Head/Step2C] No valid face pose for manual calibration.", this);
            return;
        }
        referenceRotation = currentRotation;
        calibrated = true;
        Debug.Log("[Head/Step2C] Legacy Z-key calibration complete.", this);
    }

    public void ApplyPose()
    {
        if (!initialized)
            return;

        if (neck != null)
            neck.localRotation = neckRestLocal;
        head.localRotation = headRestLocal;

        bool holdRecentPose = useAutomaticAlignment &&
            lastValidPoseRealtime >= 0f &&
            Time.realtimeSinceStartup - lastValidPoseRealtime <= trackingLossGraceSeconds;
        if (!hasPose && !holdRecentPose)
        {
            hasFilteredRootLocalDelta = false;
            return;
        }

        if (useAutomaticAlignment)
            ApplyAutomaticPose();
        else if (calibrated)
            ApplyLegacyPose();
    }

    private void ApplyAutomaticPose()
    {
        if (rigMapper == null || !rigMapper.IsReady || rigMapper.animator != animator ||
            !rigMapper.TryGetBoneSnapshot(HumanBodyBones.Head, out AvatarRigMapper.BoneSnapshot headSnapshot))
        {
            if (!warnedMissingMapper)
            {
                Debug.LogWarning("[Head/Step2C] Automatic mode needs a READY " +
                    "AvatarRigMapper on the SAME Animator. Head rotation skipped.", this);
                warnedMissingMapper = true;
            }
            return;
        }
        warnedMissingMapper = false;

        float rawSourceAngle = Quaternion.Angle(Quaternion.identity,
            Quaternion.LookRotation(lastFaceForward, lastFaceUp));
        float sourceAngle = Quaternion.Angle(Quaternion.identity, currentAutomaticSourceRotation);
        if (!loggedAutomaticPose && logFirstValidAutomaticPose)
        {
            Debug.Log("[Head/Step2C] Source face-right=" + lastFaceRight.ToString("F3") +
                ", face-up=" + lastFaceUp.ToString("F3") +
                ", face-forward=" + lastFaceForward.ToString("F3") +
                ", raw angle=" + rawSourceAngle.ToString("F1") +
                " deg, corrected angle=" + sourceAngle.ToString("F1") +
                " deg, pitch offset=" + neutralFacePitchOffsetDegrees.ToString("F1") +
                " deg, roll offset=" + neutralFaceRollOffsetDegrees.ToString("F1") +
                " deg. These are static frame corrections, not a calibrated face model.", this);
            loggedAutomaticPose = true;
        }

        if (sourceAngle > maximumSourceDeviation)
        {
            if (!warnedSourceFrame)
            {
                Debug.LogWarning("[Head/Step2C] Source face frame " +
                    sourceAngle.ToString("F1") + " deg exceeds Maximum Source Deviation; " +
                    "head rotation skipped.", this);
                warnedSourceFrame = true;
            }
            return;
        }
        warnedSourceFrame = false;

        Quaternion rootLocalTorsoFrame = rigMapper.RootLocalTorsoFrame;
        Quaternion rootLocalDelta = rootLocalTorsoFrame * currentAutomaticSourceRotation *
            Quaternion.Inverse(rootLocalTorsoFrame);
        rootLocalDelta = Quaternion.RotateTowards(
            Quaternion.identity, rootLocalDelta, maxRotationAngle);

        Quaternion stableRootLocalDelta = StabilizeSourceDelta(rootLocalDelta);

        Quaternion rootWorld = animator.transform.rotation;
        Quaternion desiredHeadWorld = rootWorld * stableRootLocalDelta * headSnapshot.rootLocalRotation;

        Quaternion headInheritedWorld = head.rotation;
        desiredHeadWorld = Quaternion.RotateTowards(
            headInheritedWorld, desiredHeadWorld, maxHeadRelativeToTorsoAngle);

        if (neck != null &&
            rigMapper.TryGetBoneSnapshot(HumanBodyBones.Neck, out AvatarRigMapper.BoneSnapshot neckSnapshot))
        {
            Quaternion desiredNeckWorld = rootWorld * stableRootLocalDelta * neckSnapshot.rootLocalRotation;
            neck.rotation = Quaternion.Slerp(neck.rotation, desiredNeckWorld, neckWeight);
        }

        head.rotation = Quaternion.Slerp(head.rotation, desiredHeadWorld, automaticHeadWorldFollow);
    }

    private Quaternion StabilizeSourceDelta(Quaternion rawDelta)
    {
        if (!enableAdaptiveRotationSmoothing || !hasFilteredRootLocalDelta)
        {
            filteredRootLocalDelta = rawDelta;
            hasFilteredRootLocalDelta = true;
            return rawDelta;
        }

        float errorDegrees = Quaternion.Angle(filteredRootLocalDelta, rawDelta);
        float fullAngle = Mathf.Max(motionStartAngleDegrees + 0.01f,
            motionFullAngleDegrees);
        float motion = Mathf.InverseLerp(motionStartAngleDegrees,
            fullAngle, errorDegrees);
        float cutoffHz = Mathf.Lerp(
            Mathf.Max(0.5f, stillRotationCutoffHz),
            Mathf.Max(0.5f, movingRotationCutoffHz), motion);

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
        float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz * dt);
        filteredRootLocalDelta = Quaternion.Slerp(
            filteredRootLocalDelta, rawDelta, alpha);

        if (logSmoothingDiagnostics && Time.unscaledTime >= nextSmoothingDiagnosticTime)
        {
            Debug.Log("[Head/Step2D] raw-filter gap=" +
                Quaternion.Angle(filteredRootLocalDelta, rawDelta).ToString("F2") +
                " deg, cutoff=" + cutoffHz.ToString("F1") +
                " Hz, alpha=" + alpha.ToString("F2"), this);
            nextSmoothingDiagnosticTime = Time.unscaledTime + 2f;
        }
        return filteredRootLocalDelta;
    }

    private void ApplyLegacyPose()
    {
        Quaternion delta = currentRotation * Quaternion.Inverse(referenceRotation);
        delta = Quaternion.RotateTowards(Quaternion.identity, delta, maxRotationAngle);
        Quaternion headBaseWorld = head.rotation;
        if (neck != null)
        {
            Quaternion neckDelta = Quaternion.Slerp(Quaternion.identity, delta, neckWeight);
            neck.rotation = neckDelta * neck.rotation;
        }
        Quaternion headDelta = Quaternion.Slerp(Quaternion.identity, delta, headWeight);
        head.rotation = headDelta * headBaseWorld;
    }
}

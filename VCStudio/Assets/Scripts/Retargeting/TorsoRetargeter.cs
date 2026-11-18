using UnityEngine;

public class TorsoRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;

    [Header("Automatic Torso Alignment")]
    [Tooltip("ON: no X-key calibration needed. OFF: legacy X-key reference mode.")]
    public bool useAutomaticAlignment = true;

    [Tooltip("Assign the AvatarRigMapper on the same VRM Animator.")]
    public AvatarRigMapper rigMapper;

    [Tooltip("Enable only if the source shoulder-right vector points toward -X when facing front. See diagnostic log.")]
    public bool sourceShoulderRightIsNegativeX = false;

    [Header("Precision - Camera Facing Setup")]
    [Tooltip("For a fixed FRONT-facing webcam seat: choose the shoulder axis sign from stable initial frames, then lock it. Disable when starting with your back to the camera.")]
    public bool autoResolveFrontalRightAtStartup = true;
    [Range(3, 30)] public int frontalSetupConsecutiveFrames = 8;
    [Range(15f, 80f)] public float frontalSetupMaxAngle = 55f;
    [Tooltip("Retain the last valid torso orientation briefly during landmark occlusion.")]
    [Range(0f, 0.5f)] public float trackingLossGraceSeconds = 0.20f;

    [Header("Precision - Automatic Torso Rotation Filter")]
    public bool enableAutomaticRotationFiltering = true;
    [Range(0.5f, 20f)] public float stillRotationCutoffHz = 4f;
    [Range(0.5f, 30f)] public float movingRotationCutoffHz = 14f;

    [Tooltip("Reject unexpectedly large source rotations (often caused by an inverted shoulder-right convention).")]
    [Range(90f, 180f)] public float maximumSourceDeviation = 110f;

    [Tooltip("Clamp automatic torso rotation to avoid extreme poses during initial validation.")]
    [Range(0f, 180f)] public float maxAutomaticRotation = 65f;

    public bool logFirstValidAutoPose = true;

    [Header("Legacy Coordinate Mapping (only when input is not normalized)")]
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Upper Body MVP")]
    [Tooltip("Automatic mode intentionally leaves the seated hips unchanged.")]
    public bool enableHipRotation = false;
    public bool useShoulderOnly = true;
    [Range(0f, 1f)] public float shoulderOnlyWeight = 0.5f;

    [Header("Rotation Weights")]
    [Range(0f, 1f)] public float hipsWeight = 0.5f;
    [Range(0f, 1f)] public float spineWeight = 0.35f;
    [Range(0f, 1f)] public float chestWeight = 0.35f;

    [Header("Tracking")]
    [Range(0f, 1f)] public float minVisibility = 0.5f;

    private Transform hips;
    private Transform spine;
    private Transform chest;
    private Quaternion hipsRestLocal;
    private Quaternion spineRestLocal;
    private Quaternion chestRestLocal;

    private Quaternion currentHipRotation;
    private Quaternion currentShoulderRotation;
    private Quaternion referenceHipRotation;
    private Quaternion referenceShoulderRotation;

    private Quaternion currentAutoSourceRotation = Quaternion.identity;
    private Vector3 lastSourceShoulderRight;
    private bool initialized;
    private bool hasPose;
    private bool calibrated;
    private bool inputIsNormalized;
    private bool warnedMissingMapper;
    private bool warnedSourceOrientation;
    private bool loggedAutoPose;
    private bool warnedHipMode;
    private bool frontalConventionLocked;
    private int frontalCandidate = -1;
    private int frontalConsecutiveFrames;
    private bool warnedFrontalSetup;
    private float lastValidPoseRealtime = -100f;
    private Quaternion filteredRootLocalDelta = Quaternion.identity;
    private bool hasFilteredRootLocalDelta;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.LogError("Valid Humanoid Animator required.", this);
            return;
        }

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);

        if (hips == null || spine == null)
        {
            Debug.LogError("Hips or Spine bone missing.", this);
            return;
        }

        hipsRestLocal = hips.localRotation;
        spineRestLocal = spine.localRotation;
        if (chest != null)
            chestRestLocal = chest.localRotation;

        if (rigMapper == null)
            rigMapper = animator.GetComponent<AvatarRigMapper>();
        if (rigMapper == null)
            rigMapper = animator.GetComponentInChildren<AvatarRigMapper>();

        initialized = true;
        Debug.Log("Ready. Mode=" +
            (useAutomaticAlignment ? "Automatic (no X key)" : "Legacy (X key)"), this);
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

    private static bool TryBuildRotation(Vector3 right, Vector3 up, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        if (right.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f)
            return false;

        up.Normalize();
        right = Vector3.ProjectOnPlane(right, up);
        if (right.sqrMagnitude < 1e-8f)
            return false;
        right.Normalize();

        Vector3 forward = Vector3.Cross(right, up);
        if (forward.sqrMagnitude < 1e-8f)
            return false;
        rotation = Quaternion.LookRotation(forward.normalized, up);
        return true;
    }

    private static bool TryBuildShoulderRotation(Vector3 shoulderRight, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        if (shoulderRight.sqrMagnitude < 1e-8f)
            return false;
        Vector3 right = shoulderRight.normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, right);
        return TryBuildRotation(right, up, out rotation);
    }

    public void SetPose(PoseLandmark[] landmarks)
    {
        SetPose(landmarks, false);
    }

    public void SetPose(PoseLandmark[] landmarks, bool isNormalized)
    {
        inputIsNormalized = isNormalized;
        hasPose = false;

        if (!initialized || landmarks == null || landmarks.Length != 33 ||
            landmarks[11] == null || landmarks[12] == null ||
            landmarks[11].visibility < minVisibility ||
            landmarks[12].visibility < minVisibility)
            return;

        Vector3 ls = Convert(landmarks[11]);
        Vector3 rs = Convert(landmarks[12]);
        Vector3 shoulderRight = rs - ls;
        if (shoulderRight.sqrMagnitude < 1e-8f)
            return;

        Vector3 torsoUp = Vector3.up;
        if (!useShoulderOnly)
        {
            if (landmarks[23] == null || landmarks[24] == null ||
                landmarks[23].visibility < minVisibility ||
                landmarks[24].visibility < minVisibility)
                return;

            Vector3 lh = Convert(landmarks[23]);
            Vector3 rh = Convert(landmarks[24]);
            Vector3 shoulderCenter = (ls + rs) * 0.5f;
            Vector3 hipCenter = (lh + rh) * 0.5f;
            torsoUp = shoulderCenter - hipCenter;
            Vector3 hipRight = rh - lh;
            if (!TryBuildRotation(hipRight, torsoUp, out currentHipRotation))
                return;
        }
        else
        {
            currentHipRotation = Quaternion.identity;
        }

        if (!TryBuildRotation(shoulderRight, torsoUp, out currentShoulderRotation))
            return;

        Vector3 autoRight = sourceShoulderRightIsNegativeX ? -shoulderRight : shoulderRight;
        Quaternion currentChoice;
        if (!TryBuildRotation(autoRight, torsoUp, out currentChoice))
            return;

        if (useAutomaticAlignment && autoResolveFrontalRightAtStartup && !frontalConventionLocked)
        {
            Quaternion flippedChoice;
            if (!TryBuildRotation(-autoRight, torsoUp, out flippedChoice)) return;
            float primaryAngle = Quaternion.Angle(Quaternion.identity, currentChoice);
            float flippedAngle = Quaternion.Angle(Quaternion.identity, flippedChoice);
            int candidate = flippedAngle < primaryAngle ? 1 : 0;
            float bestAngle = Mathf.Min(primaryAngle, flippedAngle);
            if (bestAngle <= frontalSetupMaxAngle)
            {
                frontalConsecutiveFrames = candidate == frontalCandidate
                    ? frontalConsecutiveFrames + 1 : 1;
                frontalCandidate = candidate;
                if (frontalConsecutiveFrames >= frontalSetupConsecutiveFrames)
                {
                    if (candidate == 1)
                    {
                        sourceShoulderRightIsNegativeX = !sourceShoulderRightIsNegativeX;
                        currentChoice = flippedChoice;
                    }
                    frontalConventionLocked = true;
                    Debug.Log($"[Torso/Alignment] FRONT convention locked. signNegativeX={sourceShoulderRightIsNegativeX}, " +
                        $"sourceAngle={Quaternion.Angle(Quaternion.identity, currentChoice):F1} deg, flipped={candidate == 1}.", this);
                }
            }
            else
            {
                frontalCandidate = -1;
                frontalConsecutiveFrames = 0;
                if (!warnedFrontalSetup)
                {
                    Debug.LogWarning($"[Torso/Alignment] Neither shoulder convention is front-facing " +
                        $"(angles {primaryAngle:F1}/{flippedAngle:F1}). Sit facing the webcam, or disable Auto Resolve Frontal Right At Startup.", this);
                    warnedFrontalSetup = true;
                }
            }
            if (!frontalConventionLocked) return;
        }

        currentAutoSourceRotation = currentChoice;
        lastSourceShoulderRight = shoulderRight.normalized;
        hasPose = true;
        lastValidPoseRealtime = Time.realtimeSinceStartup;
    }

    public void Calibrate()
    {
        if (useAutomaticAlignment)
        {
            Debug.Log("X calibration ignored: automatic alignment is enabled.", this);
            return;
        }
        if (!hasPose)
        {
            Debug.LogWarning("No valid pose for manual calibration.", this);
            return;
        }

        referenceHipRotation = currentHipRotation;
        referenceShoulderRotation = currentShoulderRotation;
        calibrated = true;
        Debug.Log("Legacy X-key calibration complete.", this);
    }

    public void ApplyPose()
    {
        if (!initialized)
            return;

        hips.localRotation = hipsRestLocal;
        spine.localRotation = spineRestLocal;
        if (chest != null)
            chest.localRotation = chestRestLocal;

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
        if (rigMapper == null || !rigMapper.IsReady || rigMapper.animator != animator)
        {
            if (!warnedMissingMapper)
            {
                Debug.LogWarning("Automatic mode requires a READY AvatarRigMapper " +
                    "on the SAME Animator. Torso rotation skipped.", this);
                warnedMissingMapper = true;
            }
            return;
        }
        warnedMissingMapper = false;

        if (enableHipRotation && !warnedHipMode)
        {
            Debug.LogWarning("Automatic mode keeps seated hips fixed. " +
                "Disable Enable Hip Rotation for this step.", this);
            warnedHipMode = true;
        }

        float sourceAngle = Quaternion.Angle(Quaternion.identity, currentAutoSourceRotation);
        if (!loggedAutoPose && logFirstValidAutoPose)
        {
            Debug.Log("Source shoulder-right=" +
                lastSourceShoulderRight.ToString("F3") +
                ", source angle from canonical=" + sourceAngle.ToString("F1") +
                " deg, avatar rest forward=" +
                (rigMapper.RootLocalTorsoFrame * Vector3.forward).ToString("F3") +
                ". If front-facing source angle is ~180 deg, toggle " +
                "Source Shoulder Right Is Negative X.", this);
            loggedAutoPose = true;
        }

        if (sourceAngle > maximumSourceDeviation)
        {
            if (!warnedSourceOrientation)
            {
                Debug.LogWarning("Source rotation " +
                    sourceAngle.ToString("F1") + " deg exceeds Maximum Source Deviation. " +
                    "If facing front, toggle Source Shoulder Right Is Negative X. " +
                    "Torso rotation skipped for safety.", this);
                warnedSourceOrientation = true;
            }
            return;
        }
        warnedSourceOrientation = false;

        Quaternion avatarRestFrame = rigMapper.RootLocalTorsoFrame;
        Quaternion rootLocalDelta = avatarRestFrame * currentAutoSourceRotation *
            Quaternion.Inverse(avatarRestFrame);
        rootLocalDelta = Quaternion.RotateTowards(
            Quaternion.identity, rootLocalDelta, maxAutomaticRotation);

        if (enableAutomaticRotationFiltering)
        {
            if (!hasFilteredRootLocalDelta)
            {
                filteredRootLocalDelta = rootLocalDelta;
                hasFilteredRootLocalDelta = true;
            }
            else
            {
                float error = Quaternion.Angle(filteredRootLocalDelta, rootLocalDelta);
                float cutoff = Mathf.Lerp(stillRotationCutoffHz, movingRotationCutoffHz,
                    Mathf.InverseLerp(0.75f, 8f, error));
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
                float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(0.1f, cutoff) * dt);
                filteredRootLocalDelta = Quaternion.Slerp(filteredRootLocalDelta, rootLocalDelta, alpha);
            }
            rootLocalDelta = filteredRootLocalDelta;
        }
        else hasFilteredRootLocalDelta = false;

        Quaternion rootWorld = animator.transform.rotation;
        Quaternion worldDelta = rootWorld * rootLocalDelta * Quaternion.Inverse(rootWorld);

        float shoulderFactor = useShoulderOnly ? shoulderOnlyWeight : 1f;
        Quaternion spineDelta = Quaternion.Slerp(
            Quaternion.identity, worldDelta, spineWeight * shoulderFactor);
        Quaternion chestDelta = Quaternion.Slerp(
            Quaternion.identity, worldDelta, chestWeight * shoulderFactor);

        spine.rotation = spineDelta * spine.rotation;
        if (chest != null)
            chest.rotation = chestDelta * chest.rotation;
    }

    private void ApplyLegacyPose()
    {
        Quaternion hipDelta = currentHipRotation * Quaternion.Inverse(referenceHipRotation);
        Quaternion shoulderDelta = currentShoulderRotation *
            Quaternion.Inverse(referenceShoulderRotation);

        Quaternion relativeDelta;
        if (useShoulderOnly)
        {
            relativeDelta = Quaternion.Slerp(
                Quaternion.identity, shoulderDelta, shoulderOnlyWeight);
        }
        else
        {
            relativeDelta = enableHipRotation
                ? Quaternion.Inverse(hipDelta) * shoulderDelta
                : shoulderDelta;
        }

        if (enableHipRotation)
        {
            Quaternion weightedHip = Quaternion.Slerp(
                Quaternion.identity, hipDelta, hipsWeight);
            hips.rotation = weightedHip * hips.rotation;
        }

        Quaternion weightedSpine = Quaternion.Slerp(
            Quaternion.identity, relativeDelta, spineWeight);
        spine.rotation = weightedSpine * spine.rotation;
        if (chest != null)
        {
            Quaternion weightedChest = Quaternion.Slerp(
                Quaternion.identity, relativeDelta, chestWeight);
            chest.rotation = weightedChest * chest.rotation;
        }
    }
}

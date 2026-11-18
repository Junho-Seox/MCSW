using UnityEngine;

public class BothArmsRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;

    [Header("Mapping - preserve verified settings")]
    public bool swapLeftRight = false;
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Tracking")]
    [Range(0f, 1f)] public float minVisibility = 0.5f;

    [Header("Arm Rotation")]
    [Range(0f, 1f)] public float upperArmWeight = 1f;
    [Range(0f, 1f)] public float lowerArmWeight = 1f;

    [Header("Rotation Smoothing - IK mode")]
    [Range(1f, 30f)] public float rotationSpeed = 15f;

    [Header("Safety")]
    [Range(0f, 180f)] public float maxUpperArmAngle = 150f;
    [Range(0f, 180f)] public float maxLowerArmAngle = 150f;

    [Header("Two Bone IK - opt in")]
    [Tooltip("OFF = previous direction-based behavior; ON = wrist-position IK")]
    public bool enablePositionIK = false;

    [Tooltip("Pose coordinates are interpreted in avatar-root axes. For a rotated avatar root, leave enabled.")]
    public bool useAvatarRootRotation = true;

    [Range(0.5f, 1.5f)]
    public float positionScaleMultiplier = 1f;

    [Range(0f, 30f)]
    public float wristTargetSmoothingSpeed = 12f;

    [Range(0f, 20f)]
    public float sourceArmLengthSmoothingSpeed = 4f;

    [Header("Preserve source arm reach geometry")]
    [Tooltip("ON: use geometric shoulder-to-wrist / full source-arm reach ratio instead of a warmed-up left/right scale. Prevents unequal IK reach from an early arm-length lock.")]
    public bool useSourceReachRatioIKTargets = true;
    [Tooltip("ON: preserve both wrist target direction and reach magnitude when limiting and smoothing IK input.")]
    public bool preserveReachDuringIKFiltering = true;

    [Header("Shield - Calibrated Front Reach (Step 6 - opt in)")]
    [Tooltip("Uses a consciously demonstrated front-extended pose to compensate monocular foreshortening. Has no effect until calibrated via the Gesture B key.")]
    public bool enableCalibratedFrontReachAssist = true;
    [Range(0.90f, 0.995f)] public float frontReachTargetRatio = 0.985f;
    [Tooltip("When GestureRecognizer sees a still, forward-facing two-palm shield pose, allow a short-lived reach correction without B calibration. The pose recognizer enforces the safety gate.")]
    public bool enableShieldIntentReachAssist = true;
    [Range(0f, 1f)] public float frontReachAssistStrength = 1f;
    [Range(0.45f, 0.95f)] public float frontReachActivationDepthFraction = 0.72f;
    [Range(0.60f, 0.98f)] public float frontReachMinimumSourceRatioFraction = 0.82f;
    [Range(0.6f, 1.8f)] public float frontReachMaxPlanarDistanceShoulders = 1.25f;
    [Range(4, 25)] public int frontReachCalibrationMinimumPackets = 6;
    [Tooltip("Print source elbow angle, source reach ratio, final IK reach ratio, and avatar elbow angle once per second.")]
    public bool logFrontReachDiagnostics = true;

    [Header("Precision - Fixed Seat / Stable Source Scale")]
    [Tooltip("After enough valid Pose packets, freeze per-arm human-to-avatar length scale. Useful with a fixed demo seat/camera. Disable for dynamic camera setups.")]
    public bool lockSourceArmLengthAfterWarmup = true;
    [Range(10, 120)] public int sourceArmLengthWarmupPackets = 30;
    [Range(0.1f, 0.6f)] public float warmupLengthOutlierFraction = 0.30f;

    [Header("Precision - IK Input / Joint Stability")]
    [Tooltip("Limit raw wrist-target displacement ONCE PER NEW Pose packet. Prevents a single bad depth frame from throwing the arm.")]
    public bool limitWristTargetInputSpeed = true;
    [Range(1f, 12f)] public float maxWristTargetArmLengthsPerSecond = 5f;
    [Tooltip("Cap the final upper/lower-arm rotation per rendered frame, including near-antiparallel IK solutions.")]
    public bool limitIKJointAngularSpeed = true;
    [Range(90f, 1080f)] public float maxIKJointDegreesPerSecond = 480f;
    [Tooltip("Require a few consecutive tracking packets before accepting a large HIGH-confidence elbow-pole reversal.")]
    public bool confirmStrongPoleReversals = true;
    [Range(2, 8)] public int strongPoleReverseConfirmPackets = 3;
    [Range(90f, 170f)] public float strongPoleReverseMinAngle = 115f;

    [Header("Stable Elbow Pole (opt in)")]
    [Tooltip("ON = stabilize the elbow bend plane against noisy/occluded landmarks. OFF = original Step 3A pole behavior.")]
    public bool enableStableElbowPole = false;
    [Range(0.05f, 0.75f)] public float minElbowPoleConfidence = 0.22f;
    [Range(1f, 30f)] public float elbowPoleResponse = 16f;
    [Range(30f, 720f)] public float maxElbowPoleDegreesPerSecond = 260f;
    [Tooltip("Weak, near-opposite elbow hints are treated as occlusion artifacts.")]
    public bool rejectWeakPoleFlips = true;
    [Range(0.2f, 0.95f)] public float weakPoleFlipConfidence = 0.50f;
    [Range(90f, 180f)] public float weakPoleFlipAngle = 115f;
    [Tooltip("Avatar-root-local fallback direction, used only when both tracking and history are unusable.")]
    public Vector3 fallbackElbowPoleLocal = new Vector3(0f, 0f, -1f);

    [Header("Occlusion Recovery - Frame Re-entry (Step 3D)")]
    [Tooltip("Re-acquire an arm only after several consistent NEW tracking packets. Does not change normal in-view IK.")]
    public bool stabilizeArmReentry = true;
    [Tooltip("Wait for PoseTrackingProcessor's independent elbow/wrist recovery to settle (its default recoveryDuration is 0.25 s).")]
    [Range(0.10f, 0.80f)] public float armReentryHoldSeconds = 0.32f;
    [Range(2, 20)] public int armReentryMinimumPackets = 6;
    [Range(0f, 1f)] public float armReentryMinVisibility = 0.70f;
    [Tooltip("If the elbow bending plane is ambiguous, reuse the previous good plane instead of trusting a near-straight first frame.")]
    [Range(0.02f, 0.5f)] public float armReentryMinPoleConfidence = 0.12f;
    [Range(20f, 150f)] public float armReentryMaxPoleChangeDegrees = 65f;
    [Tooltip("Keep the last valid elbow-bend plane while the arm is temporarily invisible.")]
    public bool preserveElbowPoleOnLoss = true;
    [Tooltip("Limit how fast upper/lower arm local rotations change after an occluded arm reappears.")]
    [Range(60f, 720f)] public float armReentryMaxJointDegreesPerSecond = 220f;
    [Range(0.1f, 1.5f)] public float armReentrySlowdownSeconds = 0.45f;
    public bool logArmReentry = true;

    [Header("Optional Wrist Target Correction")]
    [Tooltip("OFF by default. A small final lower-arm correction can reduce wrist-to-IK-target residual, but may alter the elbow pose.")]
    public bool enableFinalWristCorrection = false;
    [Range(0f, 1f)] public float finalWristCorrectionWeight = 0.35f;
    [Range(0f, 25f)] public float maxFinalWristCorrectionDegrees = 8f;

    [Header("Diagnostics")]
    public bool logStep3CDiagnostics = false;
    public bool drawElbowPole = false;

    [Header("Tracking Loss - optional")]
    [Tooltip("Enable only after validating the arm rest pose. Disabled preserves prior behavior.")]
    public bool returnToRestOnTrackingLoss = false;

    [Min(0f)] public float lostTrackingDelay = 0.3f;
    [Min(0.1f)] public float poseStaleTimeout = 0.5f;
    [Min(0f)] public float returnSpeed = 5f;

    [Header("IK Debug")]
    public bool drawIKTargets = true;
    public bool logIKError = false;
    [Min(0.005f)] public float targetSphereRadius = 0.025f;

    private class Arm
    {
        public Transform upper;
        public Transform lower;
        public Transform hand;
        public Quaternion upperRest;
        public Quaternion lowerRest;


        public Vector3 targetUpper;
        public Vector3 targetLower;


        public Vector3 shoulderToElbow;
        public Vector3 shoulderToWrist;
        public float measuredSourceLength;
        public float smoothedSourceLength;
        public bool sourceLengthReady;
        public bool sourceScaleLocked;
        public int sourceLengthWarmupCount;
        public float sourceLengthWarmupSum;
        public float lockedSourceLength;

        public float avatarUpperLength;
        public float avatarLowerLength;
        public bool valid;
        public float lastSeen = -100f;

        public Vector3 smoothedTargetOffsetWorld;
        public bool targetReady;
        public Vector3 targetWorld;
        public float wristErrorMeters;
        public Vector3 lastBendDirection;
        public bool hasBendDirection;
        public float poleInputConfidence;
        public float poleChangeDegrees;
        public bool poleHintRejected;
        public bool finalCorrectionUsed;
        public bool pendingPoseOffset;
        public Vector3 lastAcceptedOffsetWorld;
        public bool hasAcceptedOffsetWorld;
        public float lastAcceptedOffsetTime = -100f;
        public bool hasNewPoleSample;
        public int oppositePolePacketCount;
        public Vector3 lastOppositePoleCandidate;

        public bool hasEverTracked;
        public bool awaitingReentry;
        public float reentryCandidateStartedAt = -100f;
        public int reentryCandidatePackets;
        public bool hasReentryCandidatePole;
        public Vector3 lastReentryCandidatePole;
        public float reentrySlowdownUntil = -100f;
        public float nextReentryStatusLogAt = -100f;


        public float sourceElbowDegrees;
        public float sourceReachRatio;
        public float sourceFrontDepthShoulders;
        public float sourcePlanarDistanceShoulders;
        public float resultingIKReachRatio;
        public float avatarElbowDegrees;
        public float assistApplied;
        public bool frontPoseEvidence;
        public bool frontReachCalibrated;
        public float calibratedFrontSourceRatio;
        public float calibratedFrontDepthShoulders;
        public float calibratedFrontDepthSign;
        public int frontCapturePackets;
        public float frontCaptureRatioSum;
        public float frontCaptureDepthSum;
        public float frontCaptureElbowSum;
    }

    private readonly Arm left = new Arm();
    private readonly Arm right = new Arm();
    private bool initialized;
    private float lastDebugLogTime;
    private bool previousIKEnabled;
    private bool previousStablePoleEnabled;
    private bool inputIsNormalized;
    private bool collectingFrontReach;
    private float shieldIntentExpiresAt = -100f;

    public bool IsShieldReachIntentActive
    {
        get { return enableShieldIntentReachAssist && Time.realtimeSinceStartup < shieldIntentExpiresAt; }
    }

    public void ReportShieldFrontReachIntent(float validForSeconds = 0.28f)
    {
        if (!enableShieldIntentReachAssist) return;
        shieldIntentExpiresAt = Time.realtimeSinceStartup + Mathf.Clamp(validForSeconds, 0.06f, 0.45f);
    }

    private float currentSourceShoulderWidth;
    private float lastFrontDiagnosticTime = -100f;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.avatar == null ||
            !animator.avatar.isHuman || !animator.avatar.isValid)
        {
            Debug.LogError("BothArmsRetargeter: 유효한 Humanoid Avatar가 없습니다.");
            enabled = false;
            return;
        }

        SetupArm(left, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        SetupArm(right, HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);

        initialized = left.upper != null && left.lower != null && left.hand != null &&
                      right.upper != null && right.lower != null && right.hand != null &&
                      left.avatarUpperLength > 0.0001f && left.avatarLowerLength > 0.0001f &&
                      right.avatarUpperLength > 0.0001f && right.avatarLowerLength > 0.0001f;

        if (!initialized)
        {
            Debug.LogError("BothArmsRetargeter: 팔 뼈 또는 길이를 확인하세요.");
            enabled = false;
            return;
        }

        previousIKEnabled = enablePositionIK;
        previousStablePoleEnabled = enableStableElbowPole;
        Debug.Log("BothArmsRetargeter: 양팔 초기화 완료 (IK 선택 가능)");
    }

    private void SetupArm(Arm arm, HumanBodyBones upperBone,
        HumanBodyBones lowerBone, HumanBodyBones handBone)
    {
        arm.upper = animator.GetBoneTransform(upperBone);
        arm.lower = animator.GetBoneTransform(lowerBone);
        arm.hand = animator.GetBoneTransform(handBone);
        if (arm.upper == null || arm.lower == null || arm.hand == null)
            return;

        arm.upperRest = arm.upper.localRotation;
        arm.lowerRest = arm.lower.localRotation;
        arm.avatarUpperLength = Vector3.Distance(arm.upper.position, arm.lower.position);
        arm.avatarLowerLength = Vector3.Distance(arm.lower.position, arm.hand.position);
    }

    private Vector3 ConvertPosition(PoseLandmark p)
    {
        if (inputIsNormalized)
            return new Vector3(p.x, p.y, p.z);

        return new Vector3(invertX ? -p.x : p.x,
                           invertY ? -p.y : p.y,
                           invertZ ? -p.z : p.z);
    }

    private Vector3 ToWorldDirection(Vector3 converted)
    {
        return useAvatarRootRotation
            ? animator.transform.TransformDirection(converted)
            : converted;
    }

    public void SetPose(PoseLandmark[] landmarks)
    {
        SetPose(landmarks, false);
    }

    public void SetPose(PoseLandmark[] landmarks, bool isNormalized)
    {
        inputIsNormalized = isNormalized;
        if (landmarks == null || landmarks.Length != 33)
        {
            MarkTrackingLost(left, "missing Pose array");
            MarkTrackingLost(right, "missing Pose array");
            return;
        }


        currentSourceShoulderWidth = 0f;
        if (landmarks[11] != null && landmarks[12] != null)
        {
            Vector3 a = ConvertPosition(landmarks[11]);
            Vector3 b = ConvertPosition(landmarks[12]);
            if (FiniteVector(a) && FiniteVector(b))
                currentSourceShoulderWidth = Vector3.Distance(a, b);
        }


        if (!inputIsNormalized && swapLeftRight)
        {
            SetArmTarget(left, landmarks, 12, 14, 16);
            SetArmTarget(right, landmarks, 11, 13, 15);
        }
        else
        {
            SetArmTarget(left, landmarks, 11, 13, 15);
            SetArmTarget(right, landmarks, 12, 14, 16);
        }
    }

    private void SetArmTarget(Arm arm, PoseLandmark[] landmarks,
        int shoulderId, int elbowId, int wristId)
    {
        PoseLandmark shoulder = landmarks[shoulderId];
        PoseLandmark elbow = landmarks[elbowId];
        PoseLandmark wrist = landmarks[wristId];

        if (!IsValid(shoulder) || !IsValid(elbow) || !IsValid(wrist))
        {
            MarkTrackingLost(arm, "low visibility or invalid joint");
            return;
        }

        Vector3 s = ConvertPosition(shoulder);
        Vector3 e = ConvertPosition(elbow);
        Vector3 w = ConvertPosition(wrist);
        Vector3 upper = e - s;
        Vector3 lower = w - e;

        if (upper.sqrMagnitude < 1e-8f || lower.sqrMagnitude < 1e-8f)
        {
            MarkTrackingLost(arm, "degenerate arm geometry");
            return;
        }

        bool justReacquired = false;

        arm.sourceElbowDegrees = Vector3.Angle(s - e, w - e);
        arm.sourceReachRatio = Mathf.Clamp01((w - s).magnitude /
            Mathf.Max(upper.magnitude + lower.magnitude, 1e-6f));
        arm.sourceFrontDepthShoulders = currentSourceShoulderWidth > .05f
            ? (w.z - s.z) / currentSourceShoulderWidth : 0f;
        arm.sourcePlanarDistanceShoulders = currentSourceShoulderWidth > .05f
            ? new Vector2(w.x - s.x, w.y - s.y).magnitude / currentSourceShoulderWidth
            : float.PositiveInfinity;
        UpdateFrontReachEvidence(arm);
        if (collectingFrontReach && currentSourceShoulderWidth > .05f &&
            Time.realtimeSinceStartup >= 0f)
            CaptureFrontReachSample(arm, shoulder, elbow, wrist);

        if (stabilizeArmReentry && arm.awaitingReentry)
        {
            if (!TryFinishArmReentry(arm, shoulder, elbow, wrist, s, e, w))
            {
                arm.valid = false;
                arm.pendingPoseOffset = false;
                arm.hasNewPoleSample = false;
                return;
            }
            justReacquired = true;
            arm.reentrySlowdownUntil = Time.time + armReentrySlowdownSeconds;
        }


        if (Time.time - arm.lastSeen > Mathf.Max(0.1f, poseStaleTimeout))
        {
            arm.targetReady = false;
            arm.sourceLengthReady = false;

            if (!(stabilizeArmReentry && preserveElbowPoleOnLoss && justReacquired))
                arm.hasBendDirection = false;
            arm.hasAcceptedOffsetWorld = false;
            arm.oppositePolePacketCount = 0;
        }

        arm.targetUpper = upper.normalized;
        arm.targetLower = lower.normalized;
        arm.shoulderToElbow = upper;
        arm.shoulderToWrist = w - s;
        arm.measuredSourceLength = upper.magnitude + lower.magnitude;
        arm.valid = true;
        arm.hasEverTracked = true;
        arm.lastSeen = Time.time;
        arm.pendingPoseOffset = true;
        arm.hasNewPoleSample = true;
    }

    public void BeginFrontReachCapture()
    {
        collectingFrontReach = true;
        ClearCapture(left);
        ClearCapture(right);
        Debug.Log("[ArmIK/FrontCal] Capturing both arms for shield pose. Keep both elbows extended and palms forward.", this);
    }

    public void CancelFrontReachCapture()
    {
        collectingFrontReach = false;
        ClearCapture(left);
        ClearCapture(right);
    }

    public bool CommitFrontReachCapture()
    {
        collectingFrontReach = false;
        float lRatio, lDepth, lAngle, rRatio, rDepth, rAngle;
        bool okL = TrySummarizeFrontCapture(left, out lRatio, out lDepth, out lAngle);
        bool okR = TrySummarizeFrontCapture(right, out rRatio, out rDepth, out rAngle);
        if (!okL || !okR || Mathf.Sign(lDepth) != Mathf.Sign(rDepth))
        {
            Debug.LogWarning(string.Format(
                "[ArmIK/FrontCal] FAILED L={0} samples={1} R={2} samples={3}. " +
                "Need >= {4} steady, visible Pose frames on BOTH sides, with matching forward depth. " +
                "Old reach calibration is left unchanged.",
                okL, left.frontCapturePackets, okR, right.frontCapturePackets,
                frontReachCalibrationMinimumPackets), this);
            ClearCapture(left);
            ClearCapture(right);
            return false;
        }
        ApplyFrontCapture(left, lRatio, lDepth);
        ApplyFrontCapture(right, rRatio, rDepth);
        Debug.Log(string.Format(
            "[ArmIK/FrontCal] SUCCESS L ratio={0:F3} depth={1:F2} elbow={2:F1}deg | " +
            "R ratio={3:F3} depth={4:F2} elbow={5:F1}deg. " +
            "Compensation active ONLY near this front-facing reach pose.",
            lRatio, lDepth, lAngle, rRatio, rDepth, rAngle), this);
        ClearCapture(left);
        ClearCapture(right);
        return true;
    }

    public bool IsCalibratedFrontReachPose(bool isLeft)
    {
        Arm a = isLeft ? left : right;
        return initialized && a.valid && a.frontPoseEvidence &&
            a.frontReachCalibrated && Time.time - a.lastSeen <= poseStaleTimeout;
    }

    public bool HasBothFrontReachCalibrated
    {
        get { return left.frontReachCalibrated && right.frontReachCalibrated; }
    }

    private static bool FiniteVector(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    private static void ClearCapture(Arm arm)
    {
        arm.frontCapturePackets = 0;
        arm.frontCaptureRatioSum = 0f;
        arm.frontCaptureDepthSum = 0f;
        arm.frontCaptureElbowSum = 0f;
    }

    private void CaptureFrontReachSample(Arm arm, PoseLandmark shoulder,
        PoseLandmark elbow, PoseLandmark wrist)
    {
        float vis = Mathf.Min(shoulder.visibility, Mathf.Min(elbow.visibility, wrist.visibility));
        if (vis < Mathf.Max(.50f, minVisibility) ||
            arm.sourceElbowDegrees < 75f || arm.sourceReachRatio < .50f ||
            Mathf.Abs(arm.sourceFrontDepthShoulders) < .35f ||
            arm.sourcePlanarDistanceShoulders > frontReachMaxPlanarDistanceShoulders * 1.15f)
            return;
        arm.frontCapturePackets++;
        arm.frontCaptureRatioSum += arm.sourceReachRatio;
        arm.frontCaptureDepthSum += arm.sourceFrontDepthShoulders;
        arm.frontCaptureElbowSum += arm.sourceElbowDegrees;
    }

    private bool TrySummarizeFrontCapture(Arm arm, out float ratio, out float depth, out float angle)
    {
        ratio = depth = angle = 0f;
        if (arm.frontCapturePackets < frontReachCalibrationMinimumPackets)
            return false;
        float n = arm.frontCapturePackets;
        ratio = arm.frontCaptureRatioSum / n;
        depth = arm.frontCaptureDepthSum / n;
        angle = arm.frontCaptureElbowSum / n;
        return ratio >= .50f && ratio <= 1.001f &&
            Mathf.Abs(depth) >= .35f && angle >= 75f;
    }

    private static void ApplyFrontCapture(Arm arm, float ratio, float depth)
    {
        arm.frontReachCalibrated = true;
        arm.calibratedFrontSourceRatio = ratio;
        arm.calibratedFrontDepthShoulders = Mathf.Abs(depth);
        arm.calibratedFrontDepthSign = Mathf.Sign(depth);
    }

    private void UpdateFrontReachEvidence(Arm arm)
    {
        arm.frontPoseEvidence = false;
        if (!arm.frontReachCalibrated || currentSourceShoulderWidth <= .05f)
            return;
        float depth = arm.sourceFrontDepthShoulders * arm.calibratedFrontDepthSign;
        arm.frontPoseEvidence =
            depth >= arm.calibratedFrontDepthShoulders * frontReachActivationDepthFraction &&
            arm.sourceReachRatio >= arm.calibratedFrontSourceRatio * frontReachMinimumSourceRatioFraction &&
            arm.sourcePlanarDistanceShoulders <= frontReachMaxPlanarDistanceShoulders;
    }

    private void MarkTrackingLost(Arm arm, string reason)
    {
        if (stabilizeArmReentry && arm.hasEverTracked && !arm.awaitingReentry)
        {
            arm.awaitingReentry = true;
            if (logArmReentry)
                Debug.Log($"[ArmIK/Reentry] {ArmSide(arm)} LOST: {reason}. " +
                          "Holding IK until stable Pose packets return.", this);
        }
        arm.reentryCandidatePackets = 0;
        arm.reentryCandidateStartedAt = -100f;
        arm.hasReentryCandidatePole = false;
        arm.valid = false;
        arm.frontPoseEvidence = false;
        arm.assistApplied = 0f;
        arm.pendingPoseOffset = false;
        arm.hasNewPoleSample = false;
    }


    private bool TryFinishArmReentry(Arm arm, PoseLandmark shoulder,
        PoseLandmark elbow, PoseLandmark wrist, Vector3 s, Vector3 e, Vector3 w)
    {
        float visibility = Mathf.Min(shoulder.visibility,
            Mathf.Min(elbow.visibility, wrist.visibility));
        if (visibility < armReentryMinVisibility)
        {
            ResetReentryCandidate(arm);
            LogReentryWait(arm, $"visibility={visibility:F2} < {armReentryMinVisibility:F2}");
            return false;
        }

        Vector3 wristAxis = w - s;
        Vector3 upper = e - s;
        if (wristAxis.sqrMagnitude < 1e-8f || upper.sqrMagnitude < 1e-8f)
        {
            ResetReentryCandidate(arm);
            return false;
        }

        Vector3 pole = Vector3.ProjectOnPlane(upper, wristAxis.normalized);
        float poleConfidence = pole.magnitude / upper.magnitude;
        bool goodPole = poleConfidence >= armReentryMinPoleConfidence &&
                        pole.sqrMagnitude > 1e-8f;


        if (!goodPole && !(preserveElbowPoleOnLoss && arm.hasBendDirection))
        {
            ResetReentryCandidate(arm);
            LogReentryWait(arm, $"elbow pole ambiguous ({poleConfidence:F2})");
            return false;
        }

        Vector3 candidate = goodPole ? pole.normalized : Vector3.zero;
        if (goodPole && arm.hasReentryCandidatePole &&
            Vector3.Angle(arm.lastReentryCandidatePole, candidate) >
                armReentryMaxPoleChangeDegrees)
        {

            ResetReentryCandidate(arm);
        }

        if (arm.reentryCandidatePackets == 0)
            arm.reentryCandidateStartedAt = Time.time;
        arm.reentryCandidatePackets++;
        if (goodPole)
        {
            arm.lastReentryCandidatePole = candidate;
            arm.hasReentryCandidatePole = true;
        }

        if (arm.reentryCandidatePackets < armReentryMinimumPackets ||
            Time.time - arm.reentryCandidateStartedAt < armReentryHoldSeconds)
        {
            LogReentryWait(arm, $"stablePackets={arm.reentryCandidatePackets}/" +
                $"{armReentryMinimumPackets}, stableSeconds=" +
                $"{Time.time - arm.reentryCandidateStartedAt:F2}/{armReentryHoldSeconds:F2}");
            return false;
        }

        arm.awaitingReentry = false;
        arm.reentryCandidatePackets = 0;
        arm.hasReentryCandidatePole = false;
        if (logArmReentry)
            Debug.Log($"[ArmIK/Reentry] {ArmSide(arm)} READY after stable tracking. " +
                      $"visibility={visibility:F2}, poleConfidence={poleConfidence:F2}, " +
                      $"retainedPole={arm.hasBendDirection}", this);
        return true;
    }

    private void LogReentryWait(Arm arm, string reason)
    {
        if (!logArmReentry || Time.time < arm.nextReentryStatusLogAt)
            return;
        arm.nextReentryStatusLogAt = Time.time + 1f;
        Debug.Log($"[ArmIK/Reentry] {ArmSide(arm)} WAIT: {reason}", this);
    }

    private static void ResetReentryCandidate(Arm arm)
    {
        arm.reentryCandidatePackets = 0;
        arm.reentryCandidateStartedAt = -100f;
        arm.hasReentryCandidatePole = false;
    }

    private string ArmSide(Arm arm)
    {
        return object.ReferenceEquals(arm, left) ? "L" : "R";
    }

    private bool IsValid(PoseLandmark p)
    {
        return p != null && p.visibility >= minVisibility &&
               !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
               !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }


    public void ApplyPose()
    {
        if (!initialized)
            return;

        if (previousIKEnabled != enablePositionIK)
        {
            left.targetReady = false;
            right.targetReady = false;
            left.hasBendDirection = false;
            right.hasBendDirection = false;
            previousIKEnabled = enablePositionIK;
        }
        if (previousStablePoleEnabled != enableStableElbowPole)
        {

            left.hasBendDirection = false;
            right.hasBendDirection = false;
            previousStablePoleEnabled = enableStableElbowPole;
        }


        if (left.valid && Time.time - left.lastSeen > poseStaleTimeout)
            MarkTrackingLost(left, "no fresh Pose packet");
        if (right.valid && Time.time - right.lastSeen > poseStaleTimeout)
            MarkTrackingLost(right, "no fresh Pose packet");

        ApplyArm(left);
        ApplyArm(right);

        if (enablePositionIK && logFrontReachDiagnostics &&
            Time.unscaledTime - lastFrontDiagnosticTime >= 1f)
        {
            lastFrontDiagnosticTime = Time.unscaledTime;
            Debug.Log(string.Format(
                "[ArmIK/Reach] L: rawAngle={0:F1} R={1:F3} ikR={2:F3} avatarAngle={3:F1} " +
                "assist={4:F2} cal={5} visible={6} | " +
                "R: rawAngle={7:F1} R={8:F3} ikR={9:F3} avatarAngle={10:F1} " +
                "assist={11:F2} cal={12} visible={13} shieldIntent={14} ratioMode={15}",
                left.sourceElbowDegrees, left.sourceReachRatio, left.resultingIKReachRatio,
                left.avatarElbowDegrees, left.assistApplied, left.frontReachCalibrated, left.valid,
                right.sourceElbowDegrees, right.sourceReachRatio, right.resultingIKReachRatio,
                right.avatarElbowDegrees, right.assistApplied, right.frontReachCalibrated, right.valid,
                IsShieldReachIntentActive, useSourceReachRatioIKTargets), this);
        }

        if (enablePositionIK && (logIKError || logStep3CDiagnostics) &&
            Time.unscaledTime - lastDebugLogTime >= 1f)
        {
            lastDebugLogTime = Time.unscaledTime;
            if (logStep3CDiagnostics)
            {
                Debug.Log($"[ArmIK/Step3C] stablePole={enableStableElbowPole} " +
                    $"L: error={left.wristErrorMeters:F3}u poleConfidence={left.poleInputConfidence:F2} " +
                    $"poleTurn={left.poleChangeDegrees:F1}deg rejected={left.poleHintRejected} " +
                    $"corrected={left.finalCorrectionUsed} | " +
                    $"R: error={right.wristErrorMeters:F3}u poleConfidence={right.poleInputConfidence:F2} " +
                    $"poleTurn={right.poleChangeDegrees:F1}deg rejected={right.poleHintRejected} " +
                    $"corrected={right.finalCorrectionUsed}");
            }
            else
            {
                Debug.Log($"[Arm IK] Left={left.wristErrorMeters:F3}m " +
                          $"Right={right.wristErrorMeters:F3}m (estimated target, not GT)");
            }
        }
    }

    private void ApplyArm(Arm arm)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null)
            return;

        if (!arm.valid)
        {
            if (returnToRestOnTrackingLoss &&
                Time.time - arm.lastSeen > lostTrackingDelay)
                RestoreArm(arm);
            return;
        }

        if (enablePositionIK)
            ApplyPositionIK(arm);
        else
            ApplyOriginalDirectionSolver(arm);
    }


    private void ApplyOriginalDirectionSolver(Arm arm)
    {
        arm.upper.localRotation = arm.upperRest;
        arm.lower.localRotation = arm.lowerRest;

        Vector3 currentUpper = arm.lower.position - arm.upper.position;
        if (currentUpper.sqrMagnitude < 1e-8f)
            return;

        Quaternion upperDelta = Quaternion.FromToRotation(
            currentUpper.normalized, arm.targetUpper);
        upperDelta = Quaternion.RotateTowards(Quaternion.identity,
            upperDelta, maxUpperArmAngle);
        upperDelta = Quaternion.Slerp(Quaternion.identity,
            upperDelta, upperArmWeight);
        arm.upper.rotation = upperDelta * arm.upper.rotation;

        Vector3 currentLower = arm.hand.position - arm.lower.position;
        if (currentLower.sqrMagnitude < 1e-8f)
            return;

        Quaternion lowerDelta = Quaternion.FromToRotation(
            currentLower.normalized, arm.targetLower);
        lowerDelta = Quaternion.RotateTowards(Quaternion.identity,
            lowerDelta, maxLowerArmAngle);
        lowerDelta = Quaternion.Slerp(Quaternion.identity,
            lowerDelta, lowerArmWeight);
        arm.lower.rotation = lowerDelta * arm.lower.rotation;
    }


    private static Vector3 SmoothDirectionAndReach(Vector3 current, Vector3 wanted, float t)
    {
        if (current.sqrMagnitude < 1e-10f) return wanted;
        if (wanted.sqrMagnitude < 1e-10f) return Vector3.zero;
        float length = Mathf.Lerp(current.magnitude, wanted.magnitude, Mathf.Clamp01(t));
        Vector3 direction = Vector3.Slerp(current.normalized, wanted.normalized, Mathf.Clamp01(t));
        return direction.sqrMagnitude > 1e-10f ? direction.normalized * length : wanted.normalized * length;
    }


    private static Vector3 StepDirectionAndReach(Vector3 current, Vector3 wanted, float maxStep)
    {
        if (current.sqrMagnitude < 1e-10f || wanted.sqrMagnitude < 1e-10f)
            return Vector3.MoveTowards(current, wanted, maxStep);
        float lengthDifference = Mathf.Abs(current.magnitude - wanted.magnitude);
        float radians = Vector3.Angle(current, wanted) * Mathf.Deg2Rad;

        float maxPath = lengthDifference + Mathf.Max(current.magnitude, wanted.magnitude) * radians;
        float t = maxPath < 1e-8f ? 1f : Mathf.Clamp01(Mathf.Max(0f, maxStep) / maxPath);
        return SmoothDirectionAndReach(current, wanted, t);
    }

    private void ApplyPositionIK(Arm arm)
    {
        float avatarLength = arm.avatarUpperLength + arm.avatarLowerLength;
        if (arm.measuredSourceLength < 1e-5f || avatarLength < 1e-5f)
            return;


        if (!arm.sourceLengthReady)
        {
            arm.smoothedSourceLength = arm.measuredSourceLength;
            arm.sourceLengthReady = true;
        }
        else
        {
            float lengthT = Damp(sourceArmLengthSmoothingSpeed);
            arm.smoothedSourceLength = Mathf.Lerp(arm.smoothedSourceLength,
                arm.measuredSourceLength, lengthT);
        }

        if (lockSourceArmLengthAfterWarmup && !arm.sourceScaleLocked && arm.pendingPoseOffset)
        {
            float measured = arm.measuredSourceLength;
            float mean = arm.sourceLengthWarmupCount > 0
                ? arm.sourceLengthWarmupSum / arm.sourceLengthWarmupCount
                : measured;
            if (arm.sourceLengthWarmupCount == 0 ||
                Mathf.Abs(measured - mean) <= mean * warmupLengthOutlierFraction)
            {
                arm.sourceLengthWarmupSum += measured;
                arm.sourceLengthWarmupCount++;
                if (arm.sourceLengthWarmupCount >= sourceArmLengthWarmupPackets)
                {
                    arm.lockedSourceLength = arm.sourceLengthWarmupSum / arm.sourceLengthWarmupCount;
                    arm.sourceScaleLocked = true;
                    Debug.Log($"[ArmIK/Scale] {(object.ReferenceEquals(arm, left) ? "L" : "R")} " +
                        $"source length locked={arm.lockedSourceLength:F3} (Pose World units), " +
                        $"avatar length={avatarLength:F3} (Unity units)", this);
                }
            }
        }
        float effectiveSourceLength = lockSourceArmLengthAfterWarmup && arm.sourceScaleLocked
            ? arm.lockedSourceLength : arm.smoothedSourceLength;
        float scale = avatarLength /
            Mathf.Max(effectiveSourceLength, 1e-5f) * positionScaleMultiplier;

        Vector3 mappedOffset = ToWorldDirection(arm.shoulderToWrist);
        float targetLength = useSourceReachRatioIKTargets
            ? arm.sourceReachRatio * avatarLength * positionScaleMultiplier
            : mappedOffset.magnitude * scale;
        Vector3 rawOffsetWorld = mappedOffset.sqrMagnitude > 1e-8f
            ? mappedOffset.normalized * targetLength : Vector3.zero;
        if (rawOffsetWorld.sqrMagnitude < 1e-8f)
            return;

        arm.assistApplied = 0f;
        bool shieldIntentActive = IsShieldReachIntentActive && arm.valid;
        if (enableCalibratedFrontReachAssist && arm.valid &&
            (shieldIntentActive || (arm.frontPoseEvidence && arm.frontReachCalibrated)))
        {

            float weight;
            if (shieldIntentActive)
            {

                weight = Mathf.Clamp01(frontReachAssistStrength);
            }
            else
            {
                float d = arm.sourceFrontDepthShoulders * arm.calibratedFrontDepthSign;
                float depthT = Mathf.InverseLerp(
                    arm.calibratedFrontDepthShoulders * frontReachActivationDepthFraction,
                    arm.calibratedFrontDepthShoulders * .95f, d);
                float ratioT = Mathf.InverseLerp(
                    arm.calibratedFrontSourceRatio * frontReachMinimumSourceRatioFraction,
                    arm.calibratedFrontSourceRatio * .98f, arm.sourceReachRatio);
                weight = Mathf.Clamp01(Mathf.Min(depthT, ratioT) * frontReachAssistStrength);
            }
            float measuredRatio = Mathf.Clamp01(rawOffsetWorld.magnitude / avatarLength);
            float correctedRatio = Mathf.Lerp(measuredRatio,
                Mathf.Max(measuredRatio, frontReachTargetRatio), weight);
            rawOffsetWorld = rawOffsetWorld.normalized *
                Mathf.Max(rawOffsetWorld.magnitude, correctedRatio * avatarLength);
            arm.assistApplied = weight;
        }


        if (arm.pendingPoseOffset)
        {
            float now = Time.realtimeSinceStartup;
            if (limitWristTargetInputSpeed && arm.hasAcceptedOffsetWorld &&
                now - arm.lastAcceptedOffsetTime <= Mathf.Max(0.1f, poseStaleTimeout))
            {
                float inputDt = Mathf.Clamp(now - arm.lastAcceptedOffsetTime, 1f / 120f, 0.20f);
                float maxStep = avatarLength * maxWristTargetArmLengthsPerSecond * inputDt;
                rawOffsetWorld = preserveReachDuringIKFiltering
                    ? StepDirectionAndReach(arm.lastAcceptedOffsetWorld, rawOffsetWorld, maxStep)
                    : Vector3.MoveTowards(arm.lastAcceptedOffsetWorld, rawOffsetWorld, maxStep);
            }
            arm.lastAcceptedOffsetWorld = rawOffsetWorld;
            arm.hasAcceptedOffsetWorld = true;
            arm.lastAcceptedOffsetTime = now;
            arm.pendingPoseOffset = false;
        }
        else if (arm.hasAcceptedOffsetWorld)
        {
            rawOffsetWorld = arm.lastAcceptedOffsetWorld;
        }

        if (!arm.targetReady)
        {
            arm.smoothedTargetOffsetWorld = rawOffsetWorld;
            arm.targetReady = true;
        }
        else
        {
            arm.smoothedTargetOffsetWorld = preserveReachDuringIKFiltering
                ? SmoothDirectionAndReach(arm.smoothedTargetOffsetWorld, rawOffsetWorld,
                    Damp(wristTargetSmoothingSpeed))
                : Vector3.Lerp(arm.smoothedTargetOffsetWorld, rawOffsetWorld,
                    Damp(wristTargetSmoothingSpeed));
        }


        Quaternion previousUpper = arm.upper.localRotation;
        Quaternion previousLower = arm.lower.localRotation;
        arm.upper.localRotation = arm.upperRest;
        arm.lower.localRotation = arm.lowerRest;

        Vector3 shoulderWorld = arm.upper.position;
        Vector3 wristOffset = arm.smoothedTargetOffsetWorld;
        float reach = wristOffset.magnitude;
        if (reach < 1e-6f)
        {
            arm.upper.localRotation = previousUpper;
            arm.lower.localRotation = previousLower;
            return;
        }

        float upperLength = arm.avatarUpperLength;
        float lowerLength = arm.avatarLowerLength;
        float minReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        float maxReach = upperLength + lowerLength - 0.001f;
        if (maxReach < minReach)
            maxReach = minReach;

        float clampedReach = Mathf.Clamp(reach, minReach, maxReach);
        arm.resultingIKReachRatio = clampedReach / avatarLength;
        Vector3 wristDirection = wristOffset / reach;
        arm.targetWorld = shoulderWorld + wristDirection * clampedReach;

        float along = (upperLength * upperLength - lowerLength * lowerLength +
                       clampedReach * clampedReach) / (2f * clampedReach);
        float height = Mathf.Sqrt(Mathf.Max(0f,
            upperLength * upperLength - along * along));

        Vector3 bendDirection;
        if (enableStableElbowPole)
        {
            bendDirection = ComputeStableElbowPole(arm, wristDirection);
        }
        else
        {

            Vector3 elbowHint = ToWorldDirection(arm.shoulderToElbow);
            bendDirection = Vector3.ProjectOnPlane(elbowHint, wristDirection);

            if (bendDirection.sqrMagnitude < 1e-6f && arm.hasBendDirection)
                bendDirection = Vector3.ProjectOnPlane(arm.lastBendDirection, wristDirection);

            if (bendDirection.sqrMagnitude < 1e-6f)
            {
                Vector3 restElbowDirection = arm.lower.position - shoulderWorld;
                bendDirection = Vector3.ProjectOnPlane(restElbowDirection, wristDirection);
            }
            if (bendDirection.sqrMagnitude < 1e-6f)
            {
                Vector3 fallback = Mathf.Abs(Vector3.Dot(wristDirection, Vector3.up)) < 0.9f
                    ? Vector3.up : Vector3.right;
                bendDirection = Vector3.ProjectOnPlane(fallback, wristDirection);
            }

            bendDirection.Normalize();
            arm.lastBendDirection = bendDirection;
            arm.hasBendDirection = true;

            arm.poleInputConfidence = 0f;
            arm.poleChangeDegrees = 0f;
            arm.poleHintRejected = false;
        }

        Vector3 desiredElbow = shoulderWorld + wristDirection * along +
                               bendDirection * height;


        Vector3 currentUpper = arm.lower.position - arm.upper.position;
        Vector3 desiredUpper = desiredElbow - arm.upper.position;
        if (currentUpper.sqrMagnitude < 1e-8f || desiredUpper.sqrMagnitude < 1e-8f)
        {
            arm.upper.localRotation = previousUpper;
            arm.lower.localRotation = previousLower;
            return;
        }

        Quaternion upperDelta = Quaternion.FromToRotation(
            currentUpper.normalized, desiredUpper.normalized);
        upperDelta = Quaternion.RotateTowards(Quaternion.identity,
            upperDelta, maxUpperArmAngle);
        upperDelta = Quaternion.Slerp(Quaternion.identity,
            upperDelta, upperArmWeight);
        arm.upper.rotation = upperDelta * arm.upper.rotation;
        Quaternion solvedUpper = arm.upper.localRotation;
        Quaternion blendedUpper = Quaternion.Slerp(previousUpper, solvedUpper, Damp(rotationSpeed));
        float jointSpeed = EffectiveIKJointSpeed(arm);
        arm.upper.localRotation = (limitIKJointAngularSpeed ||
                                   (stabilizeArmReentry && Time.time < arm.reentrySlowdownUntil))
            ? Quaternion.RotateTowards(previousUpper, blendedUpper,
                jointSpeed * Time.deltaTime)
            : blendedUpper;


        Vector3 currentLower = arm.hand.position - arm.lower.position;
        Vector3 desiredLower = arm.targetWorld - arm.lower.position;
        if (currentLower.sqrMagnitude < 1e-8f || desiredLower.sqrMagnitude < 1e-8f)
        {
            arm.lower.localRotation = previousLower;
            return;
        }

        Quaternion lowerDelta = Quaternion.FromToRotation(
            currentLower.normalized, desiredLower.normalized);
        lowerDelta = Quaternion.RotateTowards(Quaternion.identity,
            lowerDelta, maxLowerArmAngle);
        lowerDelta = Quaternion.Slerp(Quaternion.identity,
            lowerDelta, lowerArmWeight);
        arm.lower.rotation = lowerDelta * arm.lower.rotation;
        Quaternion solvedLower = arm.lower.localRotation;
        Quaternion blendedLower = Quaternion.Slerp(previousLower, solvedLower, Damp(rotationSpeed));
        arm.lower.localRotation = (limitIKJointAngularSpeed ||
                                   (stabilizeArmReentry && Time.time < arm.reentrySlowdownUntil))
            ? Quaternion.RotateTowards(previousLower, blendedLower,
                jointSpeed * Time.deltaTime)
            : blendedLower;

        arm.wristErrorMeters = Vector3.Distance(arm.hand.position, arm.targetWorld);
        arm.finalCorrectionUsed = false;
        if (enableFinalWristCorrection && finalWristCorrectionWeight > 0f &&
            maxFinalWristCorrectionDegrees > 0f &&
            arm.wristErrorMeters > avatarLength * 0.005f)
        {

            Quaternion before = arm.lower.localRotation;
            Vector3 current = arm.hand.position - arm.lower.position;
            Vector3 wanted = arm.targetWorld - arm.lower.position;
            if (current.sqrMagnitude > 1e-8f && wanted.sqrMagnitude > 1e-8f)
            {
                Quaternion delta = Quaternion.FromToRotation(current, wanted);
                delta = Quaternion.RotateTowards(Quaternion.identity, delta,
                    maxFinalWristCorrectionDegrees);
                delta = Quaternion.Slerp(Quaternion.identity, delta,
                    finalWristCorrectionWeight);
                arm.lower.rotation = delta * arm.lower.rotation;
                float afterError = Vector3.Distance(arm.hand.position, arm.targetWorld);
                if (afterError < arm.wristErrorMeters - 1e-6f)
                {
                    arm.wristErrorMeters = afterError;
                    arm.finalCorrectionUsed = true;
                }
                else
                {
                    arm.lower.localRotation = before;
                }
            }
        }
        Vector3 avatarUpper = arm.upper.position - arm.lower.position;
        Vector3 avatarLower = arm.hand.position - arm.lower.position;
        if (avatarUpper.sqrMagnitude > 1e-8f && avatarLower.sqrMagnitude > 1e-8f)
            arm.avatarElbowDegrees = Vector3.Angle(avatarUpper, avatarLower);
    }

    private float EffectiveIKJointSpeed(Arm arm)
    {
        float speed = limitIKJointAngularSpeed
            ? maxIKJointDegreesPerSecond : 1080f;
        if (stabilizeArmReentry && Time.time < arm.reentrySlowdownUntil)
            speed = Mathf.Min(speed, armReentryMaxJointDegreesPerSecond);
        return speed;
    }

    private Vector3 ComputeStableElbowPole(Arm arm, Vector3 wristDirection)
    {
        Vector3 elbowHint = ToWorldDirection(arm.shoulderToElbow);
        Vector3 projected = Vector3.ProjectOnPlane(elbowHint, wristDirection);
        float hintLength = elbowHint.magnitude;
        float confidence = hintLength > 1e-6f
            ? Mathf.Clamp01(projected.magnitude / hintLength) : 0f;
        arm.poleInputConfidence = confidence;
        arm.poleHintRejected = false;
        arm.poleChangeDegrees = 0f;

        Vector3 previous = Vector3.zero;
        bool hasPrevious = arm.hasBendDirection;
        if (hasPrevious)
        {
            previous = Vector3.ProjectOnPlane(arm.lastBendDirection, wristDirection);
            hasPrevious = previous.sqrMagnitude > 1e-8f;
            if (hasPrevious) previous.Normalize();
        }

        bool hintUsable = confidence >= minElbowPoleConfidence &&
            projected.sqrMagnitude > 1e-8f;
        Vector3 candidate = hintUsable ? projected.normalized : Vector3.zero;

        if (hintUsable && hasPrevious && rejectWeakPoleFlips &&
            confidence < weakPoleFlipConfidence &&
            Vector3.Angle(previous, candidate) > weakPoleFlipAngle)
        {

            hintUsable = false;
            arm.poleHintRejected = true;
        }

        if (confirmStrongPoleReversals && hintUsable && hasPrevious &&
            Vector3.Angle(previous, candidate) >= strongPoleReverseMinAngle)
        {
            if (arm.hasNewPoleSample)
            {
                if (arm.oppositePolePacketCount > 0 &&
                    Vector3.Angle(arm.lastOppositePoleCandidate, candidate) < 35f)
                    arm.oppositePolePacketCount++;
                else
                    arm.oppositePolePacketCount = 1;
                arm.lastOppositePoleCandidate = candidate;
            }
            if (arm.oppositePolePacketCount < strongPoleReverseConfirmPackets)
            {
                hintUsable = false;
                arm.poleHintRejected = true;
            }
        }
        else if (arm.hasNewPoleSample)
        {
            arm.oppositePolePacketCount = 0;
        }
        arm.hasNewPoleSample = false;

        if (!hintUsable)
        {
            if (hasPrevious)
                candidate = previous;
            else
            {
                Vector3 rest = Vector3.ProjectOnPlane(
                    arm.lower.position - arm.upper.position, wristDirection);
                if (rest.sqrMagnitude > 1e-6f)
                    candidate = rest.normalized;
                else
                {
                    Vector3 local = fallbackElbowPoleLocal.sqrMagnitude > 1e-8f
                        ? fallbackElbowPoleLocal : Vector3.back;
                    Vector3 fallback = useAvatarRootRotation
                        ? animator.transform.TransformDirection(local.normalized)
                        : local.normalized;
                    candidate = Vector3.ProjectOnPlane(fallback, wristDirection);
                    if (candidate.sqrMagnitude < 1e-8f)
                    {
                        Vector3 secondary = Mathf.Abs(Vector3.Dot(wristDirection, Vector3.up)) < 0.9f
                            ? Vector3.up : Vector3.right;
                        candidate = Vector3.ProjectOnPlane(secondary, wristDirection);
                    }
                    candidate.Normalize();
                }
            }
        }

        Vector3 result = candidate;
        if (hasPrevious)
        {
            float maxRadians = maxElbowPoleDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
            Vector3 limited = Vector3.RotateTowards(previous, candidate, maxRadians, 0f);
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, elbowPoleResponse) * Time.deltaTime);
            result = Vector3.Slerp(previous, limited, t);
            if (result.sqrMagnitude < 1e-8f) result = previous;
            result.Normalize();
            arm.poleChangeDegrees = Vector3.Angle(previous, result);
        }

        arm.lastBendDirection = result;
        arm.hasBendDirection = true;
        return result;
    }

    private void RestoreArm(Arm arm)
    {
        float t = Damp(returnSpeed);
        arm.upper.localRotation = Quaternion.Slerp(
            arm.upper.localRotation, arm.upperRest, t);
        arm.lower.localRotation = Quaternion.Slerp(
            arm.lower.localRotation, arm.lowerRest, t);
        arm.targetReady = false;
        arm.sourceLengthReady = false;
        if (!(stabilizeArmReentry && preserveElbowPoleOnLoss && arm.awaitingReentry))
            arm.hasBendDirection = false;
        arm.hasAcceptedOffsetWorld = false;
        arm.pendingPoseOffset = false;
        arm.hasNewPoleSample = false;
        arm.oppositePolePacketCount = 0;
        arm.poleInputConfidence = 0f;
        arm.poleChangeDegrees = 0f;
        arm.poleHintRejected = false;
        arm.finalCorrectionUsed = false;
    }

    private static float Damp(float speed)
    {
        if (speed <= 0f)
            return 1f;
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    [ContextMenu("Reset IK Source Length Calibration")]
    public void ResetSourceLengthCalibration()
    {
        ResetArmSourceScale(left);
        ResetArmSourceScale(right);
        Debug.Log("[ArmIK/Scale] Source arm-length calibration reset.", this);
    }

    private static void ResetArmSourceScale(Arm arm)
    {
        arm.sourceScaleLocked = false;
        arm.sourceLengthWarmupCount = 0;
        arm.sourceLengthWarmupSum = 0f;
        arm.lockedSourceLength = 0f;
        arm.sourceLengthReady = false;
        arm.targetReady = false;
        arm.hasAcceptedOffsetWorld = false;
    }

    public bool TryGetArmDirections(bool isLeft,
        out Vector3 upperDirection, out Vector3 lowerDirection)
    {
        Arm arm = isLeft ? left : right;
        upperDirection = Vector3.zero;
        lowerDirection = Vector3.zero;
        if (!initialized || !arm.valid)
            return false;

        upperDirection = arm.targetUpper;
        lowerDirection = arm.targetLower;
        return true;
    }

    public bool TryGetIKTarget(bool isLeft,
        out Vector3 targetWorld, out float wristErrorMeters)
    {
        Arm arm = isLeft ? left : right;
        targetWorld = arm.targetWorld;
        wristErrorMeters = arm.wristErrorMeters;
        return initialized && enablePositionIK && arm.valid && arm.targetReady &&
               Time.time - arm.lastSeen <= poseStaleTimeout;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !enablePositionIK || !drawIKTargets)
            return;

        DrawArmTarget(left, Color.cyan);
        DrawArmTarget(right, Color.magenta);
    }

    private void DrawArmTarget(Arm arm, Color color)
    {
        if (!arm.valid || !arm.targetReady || arm.hand == null)
            return;
        Gizmos.color = color;
        Gizmos.DrawSphere(arm.targetWorld, targetSphereRadius);
        Gizmos.DrawLine(arm.hand.position, arm.targetWorld);
        if (drawElbowPole && arm.hasBendDirection && arm.upper != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(arm.upper.position,
                arm.upper.position + arm.lastBendDirection * arm.avatarUpperLength * 0.65f);
        }
    }
}

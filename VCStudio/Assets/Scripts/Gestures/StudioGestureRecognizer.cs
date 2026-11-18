using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class StudioGestureRecognizer : MonoBehaviour
{
    public enum Gesture { HorizontalSwipe, DownSwipe, Shield, Headset }
    public event Action<Gesture> GestureTriggered;

    [Header("References")]
    public PoseWebSocketClient trackingClient;
    [Header("Optional - shared arm reach calibration")]
    [Tooltip("Assign the SAME BothArmsRetargeter used by UpperBodyRetargeter. The B shield calibration captures a straight-ahead arm reach here too.")]
    public BothArmsRetargeter armsRetargeter;
    public bool calibrateArmReachTogetherWithShield = true;

    [Header("Recognition gate")]
    public bool enableRecognition = true;
    [Range(0.1f, 1f)] public float minPoseVisibility = 0.50f;
    [Range(0.05f, 0.6f)] public float poseFreshnessSeconds = 0.25f;

    [Header("Dynamic swipe: measured in shoulder widths")]
    [Range(0.15f, 0.9f)] public float swipeWindowSeconds = 0.38f;
    [Range(0.1f, 2f)] public float swipeDistanceShoulders = 0.70f;
    [Range(0.1f, 6f)] public float swipeSpeedShouldersPerSecond = 1.7f;
    [Range(1f, 5f)] public float minimumDirectionDominance = 1.6f;
    [Range(0.5f, 1f)] public float swipeTriggerScore = 0.82f;
    [Range(0.25f, 5f)] public float swipeCooldownSeconds = 1.25f;
    [Range(0.1f, 2f)] public float swipeReleaseSpeed = 0.40f;
    [Range(0.05f, 1f)] public float swipeReleaseSeconds = 0.22f;
    [Tooltip("On a mirrored webcam the normalized X-positive direction is typically viewer-right. Reverse if needed.")]
    public bool horizontalRightIsPositiveX = true;
    [Tooltip("CoordinateNormalizer default has Y-up. Disable if your coordinate signs differ.")]
    public bool downwardIsNegativeY = true;

    [Header("Static gestures")]
    [Tooltip("Legacy setting retained to preserve old Inspector scenes. Step 6-G6 uses the dedicated hold durations below.")]
    [Range(0.1f, 2f)] public float staticHoldSeconds = 0.50f;
    [Header("Step 6-G6 - Deliberate static poses")]
    [Tooltip("Shield must remain recognized and hands sufficiently still for at least this long.")]
    [Range(1f, 3f)] public float shieldHoldSeconds = 1.10f;
    [Tooltip("Headset must remain recognized and hands sufficiently still for at least this long.")]
    [Range(1f, 3f)] public float headsetHoldSeconds = 1.10f;
    [Tooltip("Max hand motion in shoulder widths/second, averaged over ~0.2s. This prevents down-swipes from turning into headset gestures.")]
    [Range(0.3f, 2.5f)] public float staticMaxHandSpeedShouldersPerSecond = 1.0f;
    [Range(0.3f, 1f)] public float staticTriggerScore = 0.78f;
    [Range(0f, 0.9f)] public float staticReleaseScore = 0.45f;
    [Range(0.1f, 2f)] public float postEffectCooldownSeconds = 1.75f;
    [Min(0.5f)] public float shieldDurationSeconds = 5f;
    [Min(0.5f)] public float headsetDurationSeconds = 10f;

    [Header("Shield pose thresholds")]
    [Range(90f, 179f)] public float straightElbowDegrees = 135f;
    [Range(0f, 1f)] public float forwardReachInShoulders = 0.15f;
    [Tooltip("Facing scores require both Hand World frames; if these signs are wrong, inspect palmDot L/R first.")]
    public bool requireFacingPalms = true;
    [Range(-1f, 1f)] public float leftPalmSign = 1f;
    [Range(-1f, 1f)] public float rightPalmSign = -1f;
    [Range(-1f, 1f)] public float minimumPalmFacingDot = 0.55f;

    [Header("Step 6-G4 - Shield input calibration")]
    [Tooltip("Press B while holding the actual shield pose for ~0.7s. Solves forward depth sign and L/R palm signs without changing CoordinateNormalizer or retargeters.")]
    public KeyCode shieldCalibrationKey = KeyCode.B;
    public bool requireShieldCalibration = true;
    [Tooltip("Manual fallback if calibration is disabled: normalized Pose wrist-minus-shoulder Z points toward the webcam when positive.")]
    public bool forwardReachIsPositiveZ = true;
    [Range(0.02f, 0.5f)] public float palmFreshnessSeconds = 0.40f;
    [Range(0.1f, 0.8f)] public float minShieldPoseVisibility = 0.30f;
    [Tooltip("If an elbow is occluded but both palms + forward depth are VERY strong, allow a guarded substitute. Disable to demand reliable elbow landmarks every time.")]
    public bool allowShieldElbowOcclusionFallback = true;
    [Header("Step 6-G6.1 - Guarded frontal elbow fallback")]
    [Tooltip("Only after successful B calibration, permit a strongly confirmed palms-forward / hands-forward pose to substitute for an unreliable elbow angle. Requires 1.1s still-hold as usual. Disable for strict elbow-only comparison.")]
    public bool enableGuardedFrontalElbowFallback = true;
    [Tooltip("Minimum forward reach of EACH wrist, in shoulder widths. Prevents bent arms close to the torso from passing the substitute test.")]
    [Range(0.55f, 1.5f)] public float guardedFallbackMinimumDepthShoulders = 0.75f;
    [Tooltip("Maximum planar shoulder-to-wrist distance for an arm pointing toward the camera, in shoulder widths.")]
    [Range(0.5f, 1.6f)] public float guardedFallbackMaximumPlanarShoulders = 1.10f;
    [Range(0.02f, 0.7f)] public float shieldReachFullScoreShoulders = 0.32f;
    [Tooltip("Show why shield recognition is blocked; does not change trigger thresholds.")]
    public bool showShieldDiagnostics = true;

    [Header("Step 6-G8 - Frontal shield auto recognition")]
    [Tooltip("Recognize a strongly confirmed two-palm frontal shield even when no B reference was recorded. B calibration remains optional for finer adaptation.")]
    public bool enableUncalibratedFrontalShield = true;
    [Range(0.50f, 1.20f)] public float autoShieldMinEachDepthShoulders = 0.68f;
    [Range(0.70f, 1.60f)] public float autoShieldMinAverageDepthShoulders = 0.86f;
    [Range(0.60f, 1.60f)] public float autoShieldMaxPlanarDistanceShoulders = 1.10f;
    [Range(0.75f, 1f)] public float autoShieldRequiredPalmScore = 0.88f;
    [Range(0.1f, 1.3f)] public float autoShieldMaxDepthDifferenceShoulders = 0.70f;
    [Tooltip("Wait briefly before asking arm IK to straighten, rather than extending it from one spurious palm frame.")]
    [Range(0.10f, 0.60f)] public float shieldArmAssistIntentHoldSeconds = 0.25f;
    public bool AutoShieldFrontalReady { get; private set; }
    private float autoShieldIntentCandidateSince = -1f;

    [Header("Step 6-G5 - Headset palm-to-ear contact")]
    [Tooltip("Use Pose fingertip landmarks 17/19/21 to estimate the contact point of the palm. Does not mix Pose World with Hand World coordinates.")]
    public bool usePosePalmContact = true;
    [Range(0.20f, 1.20f)] public float maxWristToEarShoulders = 0.78f;
    [Range(0.08f, 0.7f)] public float headsetFullMatchDistanceShoulders = 0.26f;
    [Range(0f, 1f)] public float headsetContactAlongFingers = 0.62f;
    [Range(0.1f, 0.6f)] public float headsetFingerVisibility = 0.27f;
    [Range(0.1f, 0.8f)] public float headsetEarVisibility = 0.30f;
    [Tooltip("When a hand covers an ear, use a position derived from eye landmarks if the ear landmark is missing.")]
    public bool headsetEyeBasedEarFallback = true;
    [Range(0.01f, 0.5f)] public float headsetScoreSmoothingSeconds = 0.09f;
    public float HeadsetDistanceLeft { get; private set; } = float.PositiveInfinity;
    public float HeadsetDistanceRight { get; private set; } = float.PositiveInfinity;
    public string HeadsetContactMode { get; private set; } = "NO EAR";

    [Header("Diagnostics — sample-based, not FPS-based")]
    public bool showOnScreenMonitor = true;
    [Tooltip("OFF = minimal bottom-right demo HUD. ON = expanded developer readout, also bottom right.")]
    public bool showDetailedMonitor = false;
    [Range(0.75f, 1.5f)] public float monitorScale = 1f;
    public bool logScores = true;
    [Range(0.25f, 10f)] public float logIntervalSeconds = 1.0f;
    public bool logGestureEvents = true;

    public float HorizontalScore { get; private set; }
    public float DownScore { get; private set; }
    public float ShieldScore { get; private set; }
    public float HeadsetScore { get; private set; }
    public float ShieldElbowScore { get; private set; }
    public float ShieldReachScore { get; private set; }
    public float ShieldPalmScore { get; private set; }
    public float ShieldGateScore { get; private set; }
    public float ShieldRawReachLeft { get; private set; }
    public float ShieldRawReachRight { get; private set; }
    public float ShieldElbowLeftDegrees { get; private set; }
    public float ShieldElbowRightDegrees { get; private set; }
    public float RawPalmZLeft { get; private set; } = float.NaN;
    public float RawPalmZRight { get; private set; } = float.NaN;
    public float HandAgeLeft { get; private set; } = float.PositiveInfinity;
    public float HandAgeRight { get; private set; } = float.PositiveInfinity;
    public string ShieldBlockReason { get; private set; } = "WAITING FOR POSE";
    public bool ShieldCalibrated { get; private set; }
    public float PalmDotLeft { get; private set; } = float.NaN;
    public float PalmDotRight { get; private set; } = float.NaN;
    public float PoseAgeSeconds { get; private set; } = float.PositiveInfinity;
    public long EvaluatedPackets { get; private set; }
    public bool TrackingValid { get; private set; }
    public bool IsGloballyLocked { get { return activeGesture.HasValue || Time.realtimeSinceStartup < lockedUntil; } }
    public string CurrentMode { get { return activeGesture.HasValue ? "ACTIVE " + activeGesture.Value : (Time.realtimeSinceStartup < lockedUntil ? "COOLDOWN" : "READY"); } }
    public string LastEvent { get; private set; } = "None";

    struct Sample
    {
        public float time;
        public Vector2 left;
        public Vector2 right;
    }

    private readonly List<Sample> swipeSamples = new List<Sample>(32);
    private readonly List<Sample> staticMotionSamples = new List<Sample>(16);
    private float staticHandSpeed = float.PositiveInfinity;
    private bool hasStableMotionWindow;
    private float lastFireTime = -100f;
    private float lastPosePacket = -1f;
    private float nextScoreLog;
    private float lockedUntil;
    private Gesture? activeGesture;
    private float activeUntil;
    private bool swipeArmed = true;
    private bool shieldArmed = true;
    private bool headsetArmed = true;
    private float swipeIdleSince = -1f;
    private float shieldReleaseSince = -1f;
    private float headsetReleaseSince = -1f;
    private float shieldCandidateSince = -1f;
    private float headsetCandidateSince = -1f;
    private float shieldLastGoodTime = -100f, headsetLastGoodTime = -100f;
    private int shieldHoldSamples, shieldGoodSamples, headsetHoldSamples, headsetGoodSamples;
    private const float TransientPoseGapSeconds = 0.12f;
    private const float MinimumPoseEvidenceFraction = 0.85f;
    private float previousCenterTime = -1f;
    private Vector2 previousCenter;
    private float currentVelocity;
    private GUIStyle statusStyle;
    private GUIStyle smallStatusStyle;
    private GUIStyle headingStyle;

    private Vector3 cachedPalmNormalLeft, cachedPalmNormalRight;
    private bool cachedPalmValidLeft, cachedPalmValidRight;
    private Vector3 shieldPalmReferenceLeft, shieldPalmReferenceRight;
    private bool shieldPalmReferenceReady;
    private Vector3 calibrationPalmNormalSumLeft, calibrationPalmNormalSumRight;
    private float cachedPalmRawLeft = float.NaN, cachedPalmRawRight = float.NaN;
    private float cachedPalmTimeLeft = -100f, cachedPalmTimeRight = -100f;
    private bool calibratingShield;
    private float calibrationUntil;
    private int calibrationCount;
    private float calibDepthLeft, calibDepthRight, calibPalmLeft, calibPalmRight;
    private float calibElbowLeft, calibElbowRight;
    private int calibElbowCount;
    private float calibratedElbowFullAngle = -1f;
    private float headsetPreviousSampleTime = -1f;
    private float lastCalibrationHandPairStamp = -1f;
    private float calibrationPalmFullScore = 0.55f;
    private bool shieldPoseValid;
    public bool ShieldElbowFallbackUsed { get; private set; }

    [ContextMenu("Begin shield calibration (hold pose)")]
    public void BeginShieldCalibration()
    {
        calibratingShield = true;
        calibrationUntil = Time.realtimeSinceStartup + .70f;
        calibrationCount = 0;
        lastCalibrationHandPairStamp = -1f;
        calibDepthLeft = calibDepthRight = calibPalmLeft = calibPalmRight = 0f;
        calibrationPalmNormalSumLeft = calibrationPalmNormalSumRight = Vector3.zero;
        calibElbowLeft = calibElbowRight = 0f;
        calibElbowCount = 0;
        ResetShieldCandidate();
        if (calibrateArmReachTogetherWithShield)
        {
            if (armsRetargeter == null) armsRetargeter = FindObjectOfType<BothArmsRetargeter>();
            if (armsRetargeter != null) armsRetargeter.BeginFrontReachCapture();
            else Debug.LogWarning("[Gesture/ShieldCal] No BothArmsRetargeter assigned. Shield can calibrate, but avatar arm-reach assist will remain unavailable.", this);
        }
        Debug.Log("[Gesture/ShieldCal] Hold BOTH arms forward, elbows extended, BOTH palms toward camera for 0.7s.", this);
    }

    private void FinishShieldCalibration()
    {
        calibratingShield = false;
        if (calibrationCount < 6)
        {
            Debug.LogWarning("[Gesture/ShieldCal] Failed: only " + calibrationCount + " complete Pose + BOTH Hand packets. Keep both hands within frame and retry B.", this);
            if (armsRetargeter != null) armsRetargeter.CancelFrontReachCapture();
            return;
        }
        float dzL = calibDepthLeft / calibrationCount, dzR = calibDepthRight / calibrationCount;
        float pL = calibPalmLeft / calibrationCount, pR = calibPalmRight / calibrationCount;

        Vector3 nL = calibrationPalmNormalSumLeft / calibrationCount;
        Vector3 nR = calibrationPalmNormalSumRight / calibrationCount;
        if (Mathf.Sign(dzL) != Mathf.Sign(dzR) || Mathf.Min(Mathf.Abs(dzL), Mathf.Abs(dzR)) < .035f ||
            nL.magnitude < .65f || nR.magnitude < .65f)
        {
            Debug.LogWarning(string.Format("[Gesture/ShieldCal] Ambiguous frame: depth={0:F2}/{1:F2}, palmConsistency={2:F2}/{3:F2}. Keep BOTH hands visible and still, then retry B.", dzL, dzR, nL.magnitude, nR.magnitude), this);
            if (armsRetargeter != null) armsRetargeter.CancelFrontReachCapture();
            return;
        }
        forwardReachIsPositiveZ = dzL + dzR > 0f;
        leftPalmSign = Mathf.Sign(Mathf.Abs(pL) > .001f ? pL : nL.z);
        rightPalmSign = Mathf.Sign(Mathf.Abs(pR) > .001f ? pR : nR.z);
        shieldPalmReferenceLeft = nL.normalized;
        shieldPalmReferenceRight = nR.normalized;
        shieldPalmReferenceReady = true;
        float weakDepth = Mathf.Min(Mathf.Abs(dzL), Mathf.Abs(dzR));

        shieldReachFullScoreShoulders = Mathf.Clamp(weakDepth * .56f, .035f, .45f);

        calibrationPalmFullScore = Mathf.Clamp(Mathf.Min(Mathf.Abs(pL), Mathf.Abs(pR)) * .62f, .18f, .77f);
        calibratedElbowFullAngle = -1f;
        if (calibElbowCount >= 4)
        {
            float weakerElbow = Mathf.Min(calibElbowLeft, calibElbowRight) / calibElbowCount;

            if (weakerElbow >= 108f)
                calibratedElbowFullAngle = Mathf.Clamp(weakerElbow - 7f, 112f, 158f);
            else Debug.LogWarning("[Gesture/ShieldCal] Observed elbows were significantly bent (" + weakerElbow.ToString("F1") + " deg); keeping conservative elbow threshold.", this);
        }
        ShieldCalibrated = true;
        if (calibrateArmReachTogetherWithShield && armsRetargeter != null)
        {
            if (!armsRetargeter.CommitFrontReachCapture())
                Debug.LogWarning("[Gesture/ShieldCal] Shield reference captured but avatar arm-reach reference failed. Check [ArmIK/FrontCal] diagnostics and retry B.", this);
        }
        Debug.Log(string.Format("[Gesture/ShieldCal] SUCCESS samples={0}, +Zforward={1}, palm normal reference L/R=({2:F2},{3:F2},{4:F2})/({5:F2},{6:F2},{7:F2}), depthFull={8:F2}, elbowFull={9:F1} deg. References stay fixed until next B.",
            calibrationCount, forwardReachIsPositiveZ,
            shieldPalmReferenceLeft.x, shieldPalmReferenceLeft.y, shieldPalmReferenceLeft.z,
            shieldPalmReferenceRight.x, shieldPalmReferenceRight.y, shieldPalmReferenceRight.z,
            shieldReachFullScoreShoulders, calibratedElbowFullAngle), this);
    }

    private void Start()
    {

        if (armsRetargeter == null) armsRetargeter = FindObjectOfType<BothArmsRetargeter>();
        if (armsRetargeter == null)
            Debug.LogWarning("[Gesture/G8] Arm retargeter missing: the shield can still fire, but IK reach assist will be unavailable.", this);
    }

    private void Reset()
    {
        trackingClient = FindObjectOfType<PoseWebSocketClient>();
    }

    private void OnDisable()
    {
        ClearSamples();
        TrackingValid = false;
        activeGesture = null;
        lockedUntil = 0f;
        shieldArmed = headsetArmed = swipeArmed = true;
        calibratingShield = false;
        if (armsRetargeter != null) armsRetargeter.CancelFrontReachCapture();
        ShieldCalibrated = false;
        cachedPalmTimeLeft = cachedPalmTimeRight = -100f;
        cachedPalmValidLeft = cachedPalmValidRight = false;
        shieldPalmReferenceReady = false;
        headsetPreviousSampleTime = -1f;
        HeadsetScore = 0f;
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (Input.GetKeyDown(shieldCalibrationKey)) BeginShieldCalibration();
        if (calibratingShield && now >= calibrationUntil) FinishShieldCalibration();
        if (activeGesture.HasValue && now >= activeUntil)
        {
            Gesture ended = activeGesture.Value;
            activeGesture = null;
            lockedUntil = now + postEffectCooldownSeconds;
            if (logGestureEvents) Debug.Log("[Gesture/END] " + ended + " -> COOLDOWN " + postEffectCooldownSeconds.ToString("F1") + "s", this);
        }

        PoseAgeSeconds = trackingClient == null || trackingClient.LastGesturePoseTime < 0f
            ? float.PositiveInfinity : now - trackingClient.LastGesturePoseTime;

        bool ready = enableRecognition && trackingClient != null && trackingClient.IsReceiving &&
                     trackingClient.HasPoseTracking && trackingClient.LatestGesturePose != null &&
                     PoseAgeSeconds <= poseFreshnessSeconds;
        if (!ready)
        {
            TrackingValid = false;
            HorizontalScore = DownScore = ShieldScore = HeadsetScore = 0f;
            PalmDotLeft = PalmDotRight = float.NaN;
            ShieldBlockReason = "NO FRESH POSE";
            ResetShieldScores();
            ClearSamples();
            return;
        }

        if (trackingClient.LastGesturePoseTime == lastPosePacket) return;
        lastPosePacket = trackingClient.LastGesturePoseTime;
        EvaluatedPackets++;

        PoseLandmark[] pose = trackingClient.LatestGesturePose;

        if (pose.Length != 33 || !HasLandmarks(pose, Mathf.Min(minShieldPoseVisibility, minPoseVisibility), 11, 12, 15, 16))
        {
            TrackingValid = false;
            HorizontalScore = DownScore = ShieldScore = HeadsetScore = 0f;
            PalmDotLeft = PalmDotRight = float.NaN;
            ShieldBlockReason = "SHOULDER/WRIST LOST";
            ResetShieldScores();
            ClearSamples();
            return;
        }
        Vector3 lShoulder = P(pose, 11), rShoulder = P(pose, 12);
        Vector3 lElbow = P(pose, 13), rElbow = P(pose, 14);
        Vector3 lWrist = P(pose, 15), rWrist = P(pose, 16);
        float s = Vector3.Distance(lShoulder, rShoulder);
        if (s < 0.07f || float.IsNaN(s) || float.IsInfinity(s))
        {
            TrackingValid = false;
            HorizontalScore = DownScore = ShieldScore = HeadsetScore = 0f;
            PalmDotLeft = PalmDotRight = float.NaN;
            ShieldBlockReason = "INVALID SHOULDER SCALE";
            ResetShieldScores();
            ClearSamples();
            return;
        }
        TrackingValid = true;
        Vector3 shoulderCenter = (lShoulder + rShoulder) * 0.5f;
        Sample sample = new Sample
        {
            time = now,
            left = new Vector2((lWrist.x - shoulderCenter.x) / s, (lWrist.y - shoulderCenter.y) / s),
            right = new Vector2((rWrist.x - shoulderCenter.x) / s, (rWrist.y - shoulderCenter.y) / s)
        };

        UpdateStaticMotion(sample, now);
        if (HasLandmarks(pose, minPoseVisibility, 11, 12, 15, 16)) UpdateSwipe(sample, now);
        else { HorizontalScore = DownScore = 0f; swipeSamples.Clear(); previousCenterTime = -1f; }

        shieldPoseValid = HasLandmarks(pose, minShieldPoseVisibility, 11, 12, 13, 14, 15, 16);
        ShieldElbowLeftDegrees = shieldPoseValid ? Vector3.Angle(lShoulder - lElbow, lWrist - lElbow) : 0f;
        ShieldElbowRightDegrees = shieldPoseValid ? Vector3.Angle(rShoulder - rElbow, rWrist - rElbow) : 0f;
        ShieldRawReachLeft = (lWrist.z - lShoulder.z) / s;
        ShieldRawReachRight = (rWrist.z - rShoulder.z) / s;
        ShieldElbowFallbackUsed = false;
        float elbowFull = ShieldCalibrated && calibratedElbowFullAngle > 0f
            ? calibratedElbowFullAngle : straightElbowDegrees + 10f;
        float elbowZero = ShieldCalibrated && calibratedElbowFullAngle > 0f
            ? Mathf.Max(92f, elbowFull - 34f) : straightElbowDegrees - 25f;
        ShieldElbowScore = shieldPoseValid ? Mathf.Min(
            Ramp(ShieldElbowLeftDegrees, elbowZero, elbowFull),
            Ramp(ShieldElbowRightDegrees, elbowZero, elbowFull)) : 0f;
        float depthLeft = ShieldRawReachLeft * (forwardReachIsPositiveZ ? 1f : -1f);
        float depthRight = ShieldRawReachRight * (forwardReachIsPositiveZ ? 1f : -1f);
        float fullReach = ShieldCalibrated ? shieldReachFullScoreShoulders : Mathf.Max(.10f, forwardReachInShoulders + .12f);
        ShieldReachScore = Mathf.Min(Ramp(depthLeft, .01f, fullReach), Ramp(depthRight, .01f, fullReach));
        ShieldPalmScore = PalmFacingScore(now);

        float leftXYAuto = Vector2.Distance(new Vector2(lWrist.x, lWrist.y),
                                          new Vector2(lShoulder.x, lShoulder.y)) / s;
        float rightXYAuto = Vector2.Distance(new Vector2(rWrist.x, rWrist.y),
                                           new Vector2(rShoulder.x, rShoulder.y)) / s;
        AutoShieldFrontalReady = enableUncalibratedFrontalShield && requireFacingPalms &&
            !calibratingShield &&
            HasLandmarks(pose, minShieldPoseVisibility, 11, 12, 15, 16) &&
            HandAgeLeft <= palmFreshnessSeconds && HandAgeRight <= palmFreshnessSeconds &&
            ShieldPalmScore >= autoShieldRequiredPalmScore &&
            ShieldReachScore >= .96f &&
            depthLeft >= autoShieldMinEachDepthShoulders &&
            depthRight >= autoShieldMinEachDepthShoulders &&
            (depthLeft + depthRight) * .5f >= autoShieldMinAverageDepthShoulders &&
            Mathf.Abs(depthLeft - depthRight) <= autoShieldMaxDepthDifferenceShoulders &&
            leftXYAuto <= autoShieldMaxPlanarDistanceShoulders &&
            rightXYAuto <= autoShieldMaxPlanarDistanceShoulders;

        if (AutoShieldFrontalReady && ShieldElbowScore < .88f)
        {
            ShieldElbowScore = .88f;
            ShieldElbowFallbackUsed = true;
        }

        if (allowShieldElbowOcclusionFallback && enableGuardedFrontalElbowFallback &&
            ShieldCalibrated && requireFacingPalms && ShieldElbowScore < staticTriggerScore &&
            ShieldReachScore >= .97f && ShieldPalmScore >= .97f &&
            HandAgeLeft <= palmFreshnessSeconds && HandAgeRight <= palmFreshnessSeconds &&
            HasLandmarks(pose, minShieldPoseVisibility, 11, 12, 15, 16) &&
            depthLeft >= guardedFallbackMinimumDepthShoulders &&
            depthRight >= guardedFallbackMinimumDepthShoulders)
        {
            float leftXY = Vector2.Distance(new Vector2(lWrist.x, lWrist.y),
                                            new Vector2(lShoulder.x, lShoulder.y)) / s;
            float rightXY = Vector2.Distance(new Vector2(rWrist.x, rWrist.y),
                                             new Vector2(rShoulder.x, rShoulder.y)) / s;
            bool calibratedFrontEvidence = armsRetargeter != null &&
                armsRetargeter.HasBothFrontReachCalibrated &&
                armsRetargeter.IsCalibratedFrontReachPose(true) &&
                armsRetargeter.IsCalibratedFrontReachPose(false);
            if ((leftXY <= guardedFallbackMaximumPlanarShoulders &&
                 rightXY <= guardedFallbackMaximumPlanarShoulders) ||
                calibratedFrontEvidence)
            {
                ShieldElbowScore = .86f; 
                ShieldElbowFallbackUsed = true;
            }
        }
        float usablePalm = requireFacingPalms ? ShieldPalmScore : 1f;

        ShieldScore = Mathf.Clamp01(.30f * ShieldElbowScore + .35f * ShieldReachScore + .35f * usablePalm);
        ShieldGateScore = Mathf.Min(ShieldElbowScore, ShieldReachScore, usablePalm);
        ShieldBlockReason = ShieldFailureReason();
        if (calibratingShield && HasLandmarks(pose, minShieldPoseVisibility, 11, 12, 15, 16) &&
            HandAgeLeft <= palmFreshnessSeconds && HandAgeRight <= palmFreshnessSeconds &&
            !float.IsNaN(RawPalmZLeft) && !float.IsNaN(RawPalmZRight) &&
            Mathf.Min(trackingClient.LastLeftHandTime, trackingClient.LastRightHandTime) > lastCalibrationHandPairStamp)
        {
            lastCalibrationHandPairStamp = Mathf.Min(trackingClient.LastLeftHandTime, trackingClient.LastRightHandTime);
            calibrationCount++;
            calibDepthLeft += ShieldRawReachLeft;
            calibDepthRight += ShieldRawReachRight;
            calibPalmLeft += RawPalmZLeft;
            calibPalmRight += RawPalmZRight;
            calibrationPalmNormalSumLeft += cachedPalmNormalLeft;
            calibrationPalmNormalSumRight += cachedPalmNormalRight;
            if (shieldPoseValid && ShieldElbowLeftDegrees >= 90f && ShieldElbowRightDegrees >= 90f)
            {
                calibElbowLeft += ShieldElbowLeftDegrees;
                calibElbowRight += ShieldElbowRightDegrees;
                calibElbowCount++;
            }
        }

        ComputeHeadsetScore(pose, lWrist, rWrist, s, now);

        UpdateReleaseState(now);

        bool handsStillForIntent = hasStableMotionWindow &&
            staticHandSpeed <= staticMaxHandSpeedShouldersPerSecond &&
            DownScore < .50f && HorizontalScore < .50f;
        bool shieldActive = activeGesture.HasValue && activeGesture.Value == Gesture.Shield;
        if (AutoShieldFrontalReady && handsStillForIntent && (!IsGloballyLocked || shieldActive))
        {
            if (autoShieldIntentCandidateSince < 0f) autoShieldIntentCandidateSince = now;
            if (now - autoShieldIntentCandidateSince >= shieldArmAssistIntentHoldSeconds &&
                armsRetargeter != null)
                armsRetargeter.ReportShieldFrontReachIntent(0.28f);
        }
        else autoShieldIntentCandidateSince = -1f;

        if (!IsGloballyLocked && !calibratingShield) SelectAndTrigger(now);
        else ResetStaticCandidates();

        if (logScores && now >= nextScoreLog)
        {
            nextScoreLog = now + Mathf.Max(.25f, logIntervalSeconds);
            Debug.Log(string.Format("[Gesture/G4] N={0} age={1:F2}s H={2:F2} D={3:F2} Shield={4:F2} Gate={5:F2} Headset={6:F2} | E={7:F2} Z={8:F2} P={9:F2} | rawDepth={10:F2}/{11:F2} rawPalm={12:F2}/{13:F2} palmAge={14:F2}/{15:F2}s | block={16} mode={17}",
                EvaluatedPackets, PoseAgeSeconds, HorizontalScore, DownScore, ShieldScore, ShieldGateScore, HeadsetScore,
                ShieldElbowScore, ShieldReachScore, ShieldPalmScore, ShieldRawReachLeft, ShieldRawReachRight,
                RawPalmZLeft, RawPalmZRight, HandAgeLeft, HandAgeRight, ShieldBlockReason, CurrentMode) +
                string.Format(" | ElbowAngle L/R={0:F1}/{1:F1} vis={2:F2}/{3:F2} calibrated={4} fallback={5} autoFront={6}",
                    ShieldElbowLeftDegrees, ShieldElbowRightDegrees, pose[13].visibility, pose[14].visibility,
                    ShieldCalibrated, ShieldElbowFallbackUsed, AutoShieldFrontalReady) +
                (armsRetargeter != null ?
                    string.Format(" | ArmCal={0} frontL/R={1}/{2}",
                        armsRetargeter.HasBothFrontReachCalibrated,
                        armsRetargeter.IsCalibratedFrontReachPose(true),
                        armsRetargeter.IsCalibratedFrontReachPose(false)) :
                    " | ArmCal=NO REF") +
                string.Format(" | Headset dL/dR={0:F2}/{1:F2} {2}", HeadsetDistanceLeft, HeadsetDistanceRight, HeadsetContactMode), this);
        }
    }

    private static Vector3 P(PoseLandmark[] p, int i) { return new Vector3(p[i].x, p[i].y, p[i].z); }
    private bool HasLandmarks(PoseLandmark[] p, params int[] ids)
    {
        return HasLandmarks(p, minPoseVisibility, ids);
    }
    private static bool HasLandmarks(PoseLandmark[] p, float minVisibility, params int[] ids)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            PoseLandmark lm = p[ids[i]];
            if (lm == null || float.IsNaN(lm.x) || float.IsNaN(lm.y) || float.IsNaN(lm.z) ||
                float.IsInfinity(lm.x) || float.IsInfinity(lm.y) || float.IsInfinity(lm.z) ||
                lm.visibility < minVisibility) return false;
        }
        return true;
    }
    private void ResetShieldScores()
    {
        AutoShieldFrontalReady = false;
        autoShieldIntentCandidateSince = -1f;
        ShieldElbowScore = ShieldReachScore = ShieldPalmScore = ShieldGateScore = 0f;
        ShieldElbowFallbackUsed = false;
    }
    private string ShieldFailureReason()
    {
        if (calibratingShield) return "CALIBRATING";
        if (requireShieldCalibration && !ShieldCalibrated && !AutoShieldFrontalReady)
            return enableUncalibratedFrontalShield ? "FRONT PALMS OR PRESS B" : "PRESS B IN SHIELD POSE";
        if (!shieldPoseValid && !ShieldElbowFallbackUsed) return "ELBOW VISIBILITY LOW";
        if (ShieldElbowScore < staticTriggerScore) return "ELBOW NOT EXTENDED";
        if (ShieldReachScore < staticTriggerScore) return "DEPTH NOT FORWARD";
        if (requireFacingPalms && (float.IsNaN(PalmDotLeft) || float.IsNaN(PalmDotRight))) return "BOTH HANDS REQUIRED";
        if (requireFacingPalms && ShieldPalmScore < staticTriggerScore) return "PALM NOT FACING CAMERA";
        return ShieldElbowFallbackUsed ? "READY / HOLD (ELBOW FALLBACK)" : "READY / HOLD";
    }
    private static float Ramp(float x, float start, float end)
    {
        return Mathf.Clamp01((x - start) / Mathf.Max(.0001f, end - start));
    }

    private void UpdateSwipe(Sample current, float now)
    {
        swipeSamples.Add(current);
        float minTime = now - Mathf.Max(.15f, swipeWindowSeconds);
        while (swipeSamples.Count > 2 && swipeSamples[0].time < minTime) swipeSamples.RemoveAt(0);
        Vector2 center = (current.left + current.right) * .5f;
        if (previousCenterTime >= 0f && now > previousCenterTime)
        {
            float dt = now - previousCenterTime;

            currentVelocity = Vector2.Distance(center, previousCenter) / Mathf.Max(dt, .01f);
        }
        previousCenterTime = now;
        previousCenter = center;
        if (currentVelocity <= swipeReleaseSpeed)
        {
            if (swipeIdleSince < 0f) swipeIdleSince = now;
            else if (now - swipeIdleSince >= swipeReleaseSeconds && !IsGloballyLocked) swipeArmed = true;
        }
        else swipeIdleSince = -1f;

        HorizontalScore = DownScore = 0f;
        if (swipeSamples.Count < 4) return;
        Sample first = swipeSamples[0];
        float dtWindow = now - first.time;
        if (dtWindow < .13f) return;
        Vector2 l = current.left - first.left;
        Vector2 r = current.right - first.right;
        Vector2 avg = (l + r) * .5f;
        float horizontal = horizontalRightIsPositiveX ? avg.x : -avg.x;
        float vertical = downwardIsNegativeY ? -avg.y : avg.y;
        float sameHorizontal = Mathf.Min(horizontalRightIsPositiveX ? l.x : -l.x, horizontalRightIsPositiveX ? r.x : -r.x);
        float sameVertical = Mathf.Min(downwardIsNegativeY ? -l.y : l.y, downwardIsNegativeY ? -r.y : r.y);
        if (horizontal > 0f && sameHorizontal > 0f)
        {
            float dominance = horizontal / Mathf.Max(.025f, Mathf.Abs(avg.y));
            HorizontalScore = Mathf.Min(Ramp(horizontal, swipeDistanceShoulders * .55f, swipeDistanceShoulders * 1.35f),
                Ramp(horizontal / dtWindow, swipeSpeedShouldersPerSecond * .55f, swipeSpeedShouldersPerSecond * 1.3f),
                Ramp(dominance, minimumDirectionDominance * .65f, minimumDirectionDominance * 1.35f),
                Ramp(sameHorizontal, swipeDistanceShoulders * .2f, swipeDistanceShoulders * .55f));
        }
        if (vertical > 0f && sameVertical > 0f)
        {
            float dominance = vertical / Mathf.Max(.025f, Mathf.Abs(avg.x));
            DownScore = Mathf.Min(Ramp(vertical, swipeDistanceShoulders * .55f, swipeDistanceShoulders * 1.35f),
                Ramp(vertical / dtWindow, swipeSpeedShouldersPerSecond * .55f, swipeSpeedShouldersPerSecond * 1.3f),
                Ramp(dominance, minimumDirectionDominance * .65f, minimumDirectionDominance * 1.35f),
                Ramp(sameVertical, swipeDistanceShoulders * .2f, swipeDistanceShoulders * .55f));
        }
    }

    private float PalmFacingScore(float now)
    {
        PalmDotLeft = PalmDotRight = RawPalmZLeft = RawPalmZRight = float.NaN;
        HandAgeLeft = HandAgeRight = float.PositiveInfinity;
        if (trackingClient == null || !trackingClient.enableHandTracking ||
            trackingClient.coordinateNormalizer == null || !trackingClient.coordinateNormalizer.isActiveAndEnabled)
            return 0f;

        HandData[] hands = trackingClient.LatestHands;
        if (hands != null)
        {
            foreach (HandData hand in hands)
            {
                if (hand == null || hand.landmarks == null || hand.landmarks.Length != 21) continue;
                bool isLeft;
                if (!trackingClient.coordinateNormalizer.TryGetAvatarHandSide(hand.label, out isLeft)) continue;
                HandLandmark w0 = hand.landmarks[0], i5 = hand.landmarks[5], p17 = hand.landmarks[17];
                if (object.ReferenceEquals(w0, null) || object.ReferenceEquals(i5, null) || object.ReferenceEquals(p17, null)) continue;
                Vector3 w = H(hand, 0), index = H(hand, 5), pinky = H(hand, 17);
                if (!Finite(w) || !Finite(index) || !Finite(pinky)) continue;
                Vector3 n = Vector3.Cross(index - w, pinky - w);
                if (!Finite(n) || n.sqrMagnitude < 1e-10f) continue;
                n.Normalize();
                float packetTime = isLeft ? trackingClient.LastLeftHandTime : trackingClient.LastRightHandTime;
                if (packetTime < 0f || now - packetTime > palmFreshnessSeconds) continue;
                if (isLeft)
                {
                    cachedPalmRawLeft = n.z;
                    cachedPalmNormalLeft = n;
                    cachedPalmValidLeft = true;
                    cachedPalmTimeLeft = packetTime;
                }
                else
                {
                    cachedPalmRawRight = n.z;
                    cachedPalmNormalRight = n;
                    cachedPalmValidRight = true;
                    cachedPalmTimeRight = packetTime;
                }
            }
        }
        HandAgeLeft = now - cachedPalmTimeLeft;
        HandAgeRight = now - cachedPalmTimeRight;
        if (HandAgeLeft <= palmFreshnessSeconds && cachedPalmValidLeft)
        {
            RawPalmZLeft = cachedPalmRawLeft;
            PalmDotLeft = shieldPalmReferenceReady
                ? Vector3.Dot(cachedPalmNormalLeft, shieldPalmReferenceLeft)
                : RawPalmZLeft * leftPalmSign;
        }
        if (HandAgeRight <= palmFreshnessSeconds && cachedPalmValidRight)
        {
            RawPalmZRight = cachedPalmRawRight;
            PalmDotRight = shieldPalmReferenceReady
                ? Vector3.Dot(cachedPalmNormalRight, shieldPalmReferenceRight)
                : RawPalmZRight * rightPalmSign;
        }
        if (float.IsNaN(PalmDotLeft) || float.IsNaN(PalmDotRight)) return 0f;
        if (shieldPalmReferenceReady)
        {

            return Mathf.Min(Ramp(PalmDotLeft, .43f, .83f), Ramp(PalmDotRight, .43f, .83f));
        }
        float palmFull = Mathf.Clamp(minimumPalmFacingDot + .20f, .20f, .85f);
        return Mathf.Min(Ramp(PalmDotLeft, .07f, palmFull), Ramp(PalmDotRight, .07f, palmFull));
    }
    private static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
    private Vector3 H(HandData hand, int i)
    {
        var p = hand.landmarks[i];
        return trackingClient.coordinateNormalizer.NormalizeHandWorldPosition(new Vector3(p.x, p.y, p.z));
    }

    private void ComputeHeadsetScore(PoseLandmark[] pose, Vector3 leftWrist, Vector3 rightWrist, float shoulderWidth, float now)
    {
        Vector2 leftEar, rightEar;
        bool lReal = HasLandmarks(pose, headsetEarVisibility, 7);
        bool rReal = HasLandmarks(pose, headsetEarVisibility, 8);
        bool eyeFallback = false;
        if (lReal) leftEar = XY(P(pose, 7));
        else leftEar = Vector2.zero;
        if (rReal) rightEar = XY(P(pose, 8));
        else rightEar = Vector2.zero;
        if ((!lReal || !rReal) && headsetEyeBasedEarFallback &&
            HasLandmarks(pose, headsetEarVisibility, 2, 5))
        {
            Vector2 lEye = XY(P(pose, 2)), rEye = XY(P(pose, 5));

            Vector2 eyeSeparation = rEye - lEye;
            Vector2 earDrop = Vector2.down * (0.09f * shoulderWidth);
            if (!lReal) { leftEar = lEye - eyeSeparation * .27f + earDrop; lReal = true; eyeFallback = true; }
            if (!rReal) { rightEar = rEye + eyeSeparation * .27f + earDrop; rReal = true; eyeFallback = true; }
        }
        if (!lReal || !rReal)
        {
            HeadsetScore = 0f;
            HeadsetDistanceLeft = HeadsetDistanceRight = float.PositiveInfinity;
            HeadsetContactMode = "EAR LANDMARK LOST";
            headsetPreviousSampleTime = now;
            return;
        }
        bool lPalm, rPalm;
        Vector2 lContact = GetPoseHandContact(pose, 15, 17, 19, 21, shoulderWidth, out lPalm);
        Vector2 rContact = GetPoseHandContact(pose, 16, 18, 20, 22, shoulderWidth, out rPalm);
        float dL = Vector2.Distance(lContact, leftEar) / shoulderWidth;
        float dR = Vector2.Distance(rContact, rightEar) / shoulderWidth;

        float wristL = Vector2.Distance(XY(leftWrist), leftEar) / shoulderWidth;
        float wristR = Vector2.Distance(XY(rightWrist), rightEar) / shoulderWidth;
        if (lPalm) dL = Mathf.Min(dL, Mathf.Max(0f, wristL - .12f));
        if (rPalm) dR = Mathf.Min(dR, Mathf.Max(0f, wristR - .12f));
        HeadsetDistanceLeft = dL;
        HeadsetDistanceRight = dR;
        HeadsetContactMode = (lPalm && rPalm ? "PALM" : "WRIST ASSIST") + (eyeFallback ? "+EYE FALLBACK" : "+EARS");
        float raw = Mathf.Min(1f - Ramp(dL, headsetFullMatchDistanceShoulders, maxWristToEarShoulders),
                              1f - Ramp(dR, headsetFullMatchDistanceShoulders, maxWristToEarShoulders));

        float dt = headsetPreviousSampleTime < 0f ? 0.1f : Mathf.Clamp(now - headsetPreviousSampleTime, .01f, .15f);
        headsetPreviousSampleTime = now;
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(.01f, headsetScoreSmoothingSeconds));
        HeadsetScore = Mathf.Lerp(HeadsetScore, raw, alpha);
    }

    private Vector2 GetPoseHandContact(PoseLandmark[] pose, int wristIndex, int pinkyIndex, int indexIndex, int thumbIndex,
        float shoulderWidth, out bool usedFingers)
    {
        Vector2 wrist = XY(P(pose, wristIndex));
        usedFingers = false;
        if (!usePosePalmContact) return wrist;
        Vector2 sum = Vector2.zero;
        int count = 0;
        int[] ids = { pinkyIndex, indexIndex, thumbIndex };
        for (int i = 0; i < ids.Length; i++)
        {
            if (!HasLandmarks(pose, headsetFingerVisibility, ids[i])) continue;
            Vector2 tip = XY(P(pose, ids[i]));

            if (Vector2.Distance(tip, wrist) > shoulderWidth * .85f) continue;
            sum += tip;
            count++;
        }
        if (count < 2) return wrist;
        usedFingers = true;
        Vector2 direction = sum / count - wrist;
        direction = Vector2.ClampMagnitude(direction, shoulderWidth * .48f);
        return wrist + direction * headsetContactAlongFingers;
    }

    private static Vector2 XY(Vector3 v) { return new Vector2(v.x, v.y); }

    private void UpdateStaticMotion(Sample sample, float now)
    {
        staticMotionSamples.Add(sample);

        while (staticMotionSamples.Count > 2 && staticMotionSamples[0].time < now - .22f)
            staticMotionSamples.RemoveAt(0);
        hasStableMotionWindow = false;
        if (staticMotionSamples.Count < 3) return;
        Sample first = staticMotionSamples[0];
        float dt = now - first.time;
        if (dt < .14f) return;
        staticHandSpeed = Mathf.Max(Vector2.Distance(sample.left, first.left),
                                    Vector2.Distance(sample.right, first.right)) / dt;
        hasStableMotionWindow = true;
    }

    private void UpdateReleaseState(float now)
    {
        if (ShieldGateScore <= staticReleaseScore)
        {
            if (shieldReleaseSince < 0f) shieldReleaseSince = now;
            if (now - shieldReleaseSince > .2f) shieldArmed = true;
        }
        else shieldReleaseSince = -1f;
        if (HeadsetScore <= staticReleaseScore)
        {
            if (headsetReleaseSince < 0f) headsetReleaseSince = now;
            if (now - headsetReleaseSince > .2f) headsetArmed = true;
        }
        else headsetReleaseSince = -1f;
    }

    private void ResetShieldCandidate()
    {
        shieldCandidateSince = -1f;
        shieldHoldSamples = shieldGoodSamples = 0;
        shieldLastGoodTime = -100f;
    }
    private void ResetHeadsetCandidate()
    {
        headsetCandidateSince = -1f;
        headsetHoldSamples = headsetGoodSamples = 0;
        headsetLastGoodTime = -100f;
    }
    private void ResetStaticCandidates()
    {
        ResetShieldCandidate();
        ResetHeadsetCandidate();
    }
    private void SelectAndTrigger(float now)
    {

        bool handsStill = hasStableMotionWindow && staticHandSpeed <= staticMaxHandSpeedShouldersPerSecond;
        bool staticAllowed = handsStill && DownScore < .50f && HorizontalScore < .50f;
        if (!staticAllowed) ResetStaticCandidates();
        else
        {
            if (shieldArmed && !calibratingShield &&
                (!requireShieldCalibration || ShieldCalibrated || AutoShieldFrontalReady))
            {
                if (ShieldGateScore >= staticTriggerScore)
                {
                    if (shieldCandidateSince < 0f) shieldCandidateSince = now;
                    shieldLastGoodTime = now;
                    shieldHoldSamples++;
                    shieldGoodSamples++;
                }
                else if (shieldCandidateSince >= 0f && now - shieldLastGoodTime <= TransientPoseGapSeconds)
                {

                    shieldHoldSamples++;
                }
                else ResetShieldCandidate();
            }
            else ResetShieldCandidate();

            if (headsetArmed)
            {
                if (HeadsetScore >= staticTriggerScore)
                {
                    if (headsetCandidateSince < 0f) headsetCandidateSince = now;
                    headsetLastGoodTime = now;
                    headsetHoldSamples++;
                    headsetGoodSamples++;
                }
                else if (headsetCandidateSince >= 0f && now - headsetLastGoodTime <= TransientPoseGapSeconds)
                {
                    headsetHoldSamples++;
                }
                else ResetHeadsetCandidate();
            }
            else ResetHeadsetCandidate();
        }

        bool shieldReady = shieldCandidateSince >= 0f && now - shieldCandidateSince >= shieldHoldSeconds &&
            ShieldGateScore >= staticTriggerScore && shieldHoldSamples > 0 &&
            (float)shieldGoodSamples / shieldHoldSamples >= MinimumPoseEvidenceFraction;
        bool headsetReady = headsetCandidateSince >= 0f && now - headsetCandidateSince >= headsetHoldSeconds &&
            HeadsetScore >= staticTriggerScore && headsetHoldSamples > 0 &&
            (float)headsetGoodSamples / headsetHoldSamples >= MinimumPoseEvidenceFraction;
        if (shieldReady || headsetReady)
        {
            if (shieldReady && (!headsetReady || ShieldGateScore > HeadsetScore + .08f)) Fire(Gesture.Shield, now, ShieldGateScore);
            else if (headsetReady && (!shieldReady || HeadsetScore > ShieldGateScore + .08f)) Fire(Gesture.Headset, now, HeadsetScore);

            return;
        }
        if (shieldCandidateSince >= 0f || headsetCandidateSince >= 0f) return;
        if (!swipeArmed) return;
        if (HorizontalScore >= swipeTriggerScore && HorizontalScore >= DownScore + .12f)
            Fire(Gesture.HorizontalSwipe, now, HorizontalScore);
        else if (DownScore >= swipeTriggerScore && DownScore >= HorizontalScore + .12f)
            Fire(Gesture.DownSwipe, now, DownScore);
    }

    private void Fire(Gesture type, float now, float score)
    {
        LastEvent = type.ToString();
        lastFireTime = now;
        if (type == Gesture.Shield || type == Gesture.Headset)
        {
            activeGesture = type;
            activeUntil = now + (type == Gesture.Shield ? shieldDurationSeconds : headsetDurationSeconds);
            if (type == Gesture.Shield) shieldArmed = false;
            else headsetArmed = false;
        }
        else
        {
            swipeArmed = false;
            lockedUntil = now + swipeCooldownSeconds;
        }
        ResetStaticCandidates();
        if (logGestureEvents)
            Debug.Log(string.Format("[Gesture/FIRE] {0} score={1:F2} sample={2} lock={3} time={4:F2}",
                type, score, EvaluatedPackets, CurrentMode, now), this);
        try { if (GestureTriggered != null) GestureTriggered(type); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    private void ClearSamples()
    {
        swipeSamples.Clear();
        staticMotionSamples.Clear();
        staticHandSpeed = float.PositiveInfinity;
        hasStableMotionWindow = false;
        lastPosePacket = -1f;
        previousCenterTime = -1f;
        swipeIdleSince = -1f;
        currentVelocity = 0f;
        ResetStaticCandidates();
    }

    private void OnGUI()
    {
        if (!showOnScreenMonitor || !Application.isPlaying) return;
        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 12;
            statusStyle.normal.textColor = Color.white;
            smallStatusStyle = new GUIStyle(statusStyle);
            smallStatusStyle.fontSize = 11;
            smallStatusStyle.normal.textColor = new Color(.80f, .88f, .93f);
            headingStyle = new GUIStyle(statusStyle);
            headingStyle.fontStyle = FontStyle.Bold;
            headingStyle.fontSize = 13;
        }

        float scale = Mathf.Clamp(monitorScale, .75f, 1.5f);

        float panelW = showDetailedMonitor ? 396f : 238f;
        float panelH = showDetailedMonitor ? (showShieldDiagnostics ? 277f : 176f) : 80f;
        float margin = 15f;
        float x = Mathf.Max(2f, Screen.width - panelW * scale - margin);
        float y = Mathf.Max(2f, Screen.height - panelH * scale - margin);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = oldMatrix * Matrix4x4.TRS(new Vector3(x, y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        try
        {
            Rect panel = new Rect(0f, 0f, panelW, panelH);
            Color before = GUI.color;
            GUI.color = new Color(.035f, .065f, .10f, .87f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = before;
            if (showDetailedMonitor) DrawDetailedMonitor();
            else DrawDemoMonitor();
        }
        finally { GUI.matrix = oldMatrix; }
    }

    private void DrawDemoMonitor()
    {
        string mode = !TrackingValid ? "TRACKING LOST" :
            activeGesture.HasValue ? "EFFECT ACTIVE" : IsGloballyLocked ? "COOLDOWN" : "LIVE";
        GUI.Label(new Rect(11f, 7f, 100f, 18f), "GESTURE", headingStyle);
        GUI.Label(new Rect(117f, 8f, 113f, 18f), mode, smallStatusStyle);

        float now = Time.realtimeSinceStartup;
        string name = "Ready";
        float value = 0f;
        if (activeGesture.HasValue)
        {
            name = GestureName(activeGesture.Value);
            value = 1f;
        }
        else if (now - lastFireTime < 1.8f && LastEvent != "None")
        {
            name = GestureNameFromString(LastEvent);
            value = 1f;
        }
        else if (TrackingValid)
        {

            if (shieldCandidateSince >= 0f)
            {
                name = "Shield hold";
                value = Mathf.Clamp01((now - shieldCandidateSince) / Mathf.Max(1f, shieldHoldSeconds));
            }
            else if (headsetCandidateSince >= 0f)
            {
                name = "Headset hold";
                value = Mathf.Clamp01((now - headsetCandidateSince) / Mathf.Max(1f, headsetHoldSeconds));
            }
            else
            {
                value = Mathf.Max(HorizontalScore, DownScore, ShieldGateScore, HeadsetScore);
                if (value < .12f) { name = "Ready"; value = 0f; }
                else if (value == HorizontalScore) name = "Background";
                else if (value == DownScore) name = "Lighting";
                else if (value == ShieldGateScore) name = "Shield";
                else name = "Headset";
            }
        }
        GUI.Label(new Rect(11f, 30f, 177f, 19f), name, statusStyle);
        GUI.Label(new Rect(195f, 30f, 38f, 19f), (Mathf.Clamp01(value) * 100f).ToString("F0") + "%", smallStatusStyle);
        DrawMonitorBar(new Rect(11f, 58f, 216f, 7f), value);
    }

    private static string GestureName(Gesture gesture)
    {
        switch (gesture)
        {
            case Gesture.HorizontalSwipe: return "Background";
            case Gesture.DownSwipe: return "Lighting";
            case Gesture.Shield: return "Shield";
            default: return "Headset";
        }
    }
    private static string GestureNameFromString(string gesture)
    {
        Gesture parsed;
        return Enum.TryParse(gesture, out parsed) ? GestureName(parsed) : gesture;
    }

    private void DrawDetailedMonitor()
    {
        GUI.Label(new Rect(12f, 8f, 360f, 20f), "Gesture monitor | developer", headingStyle);
        GUI.Label(new Rect(12f, 30f, 373f, 19f), (TrackingValid ? "POSE OK" : "NO FRESH POSE") + "   " + CurrentMode, statusStyle);
        DrawDetailedScore(0, "Swipe right", HorizontalScore);
        DrawDetailedScore(1, "Swipe down", DownScore);
        DrawDetailedScore(2, "Shield GATE", ShieldGateScore);
        DrawDetailedScore(3, "Headset", HeadsetScore);
        GUI.Label(new Rect(12f, 147f, 375f, 19f), "Last: " + LastEvent + " | N=" + EvaluatedPackets, smallStatusStyle);
        if (showShieldDiagnostics)
        {
            GUI.Label(new Rect(12f, 171f, 375f, 19f), string.Format("Shield: E={0:F2}  Depth={1:F2}  Palm={2:F2}  Gate={3:F2}",
                ShieldElbowScore, ShieldReachScore, ShieldPalmScore, ShieldGateScore), smallStatusStyle);
            GUI.Label(new Rect(12f, 192f, 375f, 19f), string.Format("Z={0:F2}/{1:F2} | normalDot={2:F2}/{3:F2}",
                ShieldRawReachLeft, ShieldRawReachRight, PalmDotLeft, PalmDotRight), smallStatusStyle);
            GUI.Label(new Rect(12f, 213f, 375f, 19f), "BLOCK: " + ShieldBlockReason, smallStatusStyle);
            GUI.Label(new Rect(12f, 234f, 375f, 18f), ShieldCalibrated ? "Shield calibrated | B = redo" : "Hold shield pose & press B", smallStatusStyle);
            GUI.Label(new Rect(12f, 253f, 375f, 18f), string.Format("Headset dL/R {0:F2}/{1:F2}", HeadsetDistanceLeft, HeadsetDistanceRight), smallStatusStyle);
        }
    }
    private void DrawDetailedScore(int index, string title, float value)
    {
        float y = 54f + index * 22f;
        GUI.Label(new Rect(12f, y, 115f, 19f), title, smallStatusStyle);
        DrawMonitorBar(new Rect(129f, y + 5f, 208f, 9f), value);
        GUI.Label(new Rect(343f, y, 42f, 18f), value.ToString("F2"), smallStatusStyle);
    }
    private static void DrawMonitorBar(Rect rect, float value)
    {
        Color old = GUI.color;
        GUI.color = new Color(.15f, .23f, .31f, .95f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = value >= .78f ? new Color(.35f, .92f, .75f, .98f) : new Color(.37f, .70f, .96f, .98f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

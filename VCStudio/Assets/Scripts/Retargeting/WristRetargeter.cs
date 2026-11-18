using System;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class WristRetargeter : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PoseWebSocketClient poseClient;
    public BothArmsRetargeter armsRetargeter;

    [Header("Hand Mapping - preserve existing values")]
    public bool swapHands = true;

    [Header("Coordinate Conversion - preserve existing values")]
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Wrist Rotation")]
    public bool enableWristRotation = true;
    [Range(0f, 1f)] public float rotationWeight = 1f;
    [Range(0f, 180f)] public float maxRotationAngle = 145f;
    [Range(1f, 30f)] public float rotationSpeed = 15f;
    [Range(30f, 720f)] public float maxDegreesPerSecond = 270f;

    [Header("Direct Palm Alignment (opt in)")]
    [Tooltip("OFF = original first-frame forearm-relative solver. ON = use normalized Hand World palm orientation directly in avatar-root axes; requires camera/rig axes to agree.")]
    public bool useDirectPalmWorldAlignment = false;

    [Tooltip("Ignore abrupt Hand World palm orientation jumps between successive tracking packets. Direct mode only.")]
    public bool rejectAbruptPalmInput = true;
    [Range(15f, 150f)] public float maxPalmJumpPerPacketDegrees = 65f;
    [Range(0.1f, 1f)] public float inputContinuityTimeout = 0.3f;

    [Tooltip("Limit wrist rotation around the forearm axis and its remaining swing separately. Direct mode only.")]
    public bool useWristSwingTwistLimits = true;
    [Range(5f, 120f)] public float maxWristSwingDegrees = 65f;
    [Range(5f, 150f)] public float maxWristTwistDegrees = 80f;

    [Header("Tracking Loss")]
    [Min(0f)] public float lostTrackingDelay = 0.3f;
    [Min(0f)] public float returnSpeed = 5f;
    [Min(0f)] public float recoverySpeed = 9f;
    [Min(0f)] public float recoveryDuration = 0.25f;

    [Header("Diagnostics")]
    public bool drawPalmDirections = true;
    public bool logPalmDiagnostics = false;
    [Range(0.03f, 0.3f)] public float debugAxisLength = 0.12f;
    [Tooltip("Remaining angle between desired and actual avatar palm after wrist update (degrees).")]
    public float leftPalmErrorDegrees;
    public float rightPalmErrorDegrees;
    [Tooltip("Angle between left and right actual palm normals; near 180 degrees when palms face each other.")]
    public float palmNormalSeparationDegrees;
    [Tooltip("Rejected tracking packets with sudden palm rotation jumps (since Play start).")]
    public int leftRejectedPalmPackets;
    public int rightRejectedPalmPackets;
    [Tooltip("Requested wrist rotation from the current forearm-relative neutral, before safety limits.")]
    public float leftRequestedWristAngle;
    public float rightRequestedWristAngle;

    private struct PalmFrame
    {
        public Quaternion rotation;
        public Vector3 normal;
        public bool valid;
    }

    private sealed class HandState
    {
        public Transform wrist;
        public Transform elbow;
        public Transform indexBase;
        public Transform middleBase;
        public Transform pinkyBase;
        public Quaternion restLocal;

        public PalmFrame trackedPalm;
        public float lastHandSeen = -100f;
        public float lastUsable = -100f;
        public float recoveryUntil = -100f;

        public bool calibrated;
        public Quaternion palmFrameOffset = Quaternion.identity;

        public Vector3 trackingForward;
        public Vector3 trackingUp;
        public bool trackingFrameReady;
        public Vector3 avatarForward;
        public Vector3 avatarUp;
        public bool avatarFrameReady;

        public Quaternion desiredPalm = Quaternion.identity;
        public bool hasDesiredPalm;
        public float palmError;
        public PalmFrame lastAcceptedInputPalm;
        public bool hasAcceptedInputPalm;
        public float lastAcceptedInputTime = -100f;
        public int rejectedPackets;
        public float requestedWristAngle;
    }

    private readonly HandState left = new HandState();
    private readonly HandState right = new HandState();
    private float lastProcessedMessageTime = -1f;
    private float lastLogTime = -100f;
    private bool previousDirectMode;

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (armsRetargeter == null) armsRetargeter = GetComponentInChildren<BothArmsRetargeter>();
        if (poseClient == null) poseClient = GetComponentInChildren<PoseWebSocketClient>();

        if (animator == null || animator.avatar == null ||
            !animator.avatar.isHuman || !animator.avatar.isValid ||
            armsRetargeter == null || poseClient == null)
        {
            Debug.LogError("Animator / BothArmsRetargeter / PoseWebSocketClient 참조를 확인하세요.");
            enabled = false;
            return;
        }

        if (!Setup(left, true) || !Setup(right, false))
        {
            Debug.LogError("VRM 손목 또는 손가락 첫 관절을 찾지 못했습니다. Humanoid finger bones를 확인하세요.");
            enabled = false;
            return;
        }

        previousDirectMode = useDirectPalmWorldAlignment;
        Debug.Log("Ready. DirectPalm=" + useDirectPalmWorldAlignment +
                  ". Legacy mode still calibrates from the first valid hand frame.");
    }

    private bool Setup(HandState hand, bool isLeft)
    {
        hand.wrist = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        hand.elbow = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        hand.indexBase = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        hand.middleBase = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        hand.pinkyBase = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);

        if (hand.wrist == null || hand.elbow == null || hand.indexBase == null ||
            hand.middleBase == null || hand.pinkyBase == null)
            return false;

        hand.restLocal = hand.wrist.localRotation;
        return true;
    }

    private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

    private CoordinateNormalizer ActiveNormalizer
    {
        get
        {
            CoordinateNormalizer n = poseClient != null
                ? poseClient.coordinateNormalizer : null;
            return n != null && n.isActiveAndEnabled ? n : null;
        }
    }

    private Vector3 ConvertHand(Vector3 p)
    {
        CoordinateNormalizer n = ActiveNormalizer;
        if (n != null)
            return n.NormalizeHandWorldPosition(p);

        return new Vector3(invertX ? -p.x : p.x,
                           invertY ? -p.y : p.y,
                           invertZ ? -p.z : p.z);
    }

    private bool TryGetAvatarHandSide(string label, out bool avatarIsLeft)
    {
        CoordinateNormalizer n = ActiveNormalizer;
        if (n != null)
            return n.TryGetAvatarHandSide(label, out avatarIsLeft);

        avatarIsLeft = false;
        if (string.Equals(label, "Left", StringComparison.OrdinalIgnoreCase))
        {
            avatarIsLeft = !swapHands;
            return true;
        }
        if (string.Equals(label, "Right", StringComparison.OrdinalIgnoreCase))
        {
            avatarIsLeft = swapHands;
            return true;
        }
        return false;
    }

    private static bool MakePalmFrame(Vector3 wrist, Vector3 index,
        Vector3 middle, Vector3 pinky, out PalmFrame frame)
    {
        frame = new PalmFrame();
        Vector3 forward = middle - wrist;
        Vector3 across = pinky - index;
        if (forward.sqrMagnitude < 1e-9f || across.sqrMagnitude < 1e-9f)
            return false;
        forward.Normalize();
        Vector3 normal = Vector3.Cross(across, forward);
        if (normal.sqrMagnitude < 1e-9f)
            return false;
        normal.Normalize();
        frame.rotation = Quaternion.LookRotation(forward, normal);
        frame.normal = normal;
        frame.valid = true;
        return true;
    }

    private bool TryTrackingPalm(HandLandmark[] landmarks, out PalmFrame frame)
    {
        frame = new PalmFrame();
        if (landmarks == null || landmarks.Length != 21)
            return false;

        HandLandmark w = landmarks[0], i = landmarks[5], m = landmarks[9], p = landmarks[17];
        if (object.ReferenceEquals(w, null) || object.ReferenceEquals(i, null) ||
            object.ReferenceEquals(m, null) || object.ReferenceEquals(p, null) ||
            !Finite(w.x) || !Finite(w.y) || !Finite(w.z) ||
            !Finite(i.x) || !Finite(i.y) || !Finite(i.z) ||
            !Finite(m.x) || !Finite(m.y) || !Finite(m.z) ||
            !Finite(p.x) || !Finite(p.y) || !Finite(p.z))
            return false;
        Vector3 wrist = ConvertHand(new Vector3(w.x, w.y, w.z));
        Vector3 index = ConvertHand(new Vector3(i.x, i.y, i.z));
        Vector3 middle = ConvertHand(new Vector3(m.x, m.y, m.z));
        Vector3 pinky = ConvertHand(new Vector3(p.x, p.y, p.z));
        if (useDirectPalmWorldAlignment)
        {
            Vector3 forward = middle - wrist;
            Vector3 across = pinky - index;
            if (forward.sqrMagnitude < 1e-9f || across.sqrMagnitude < 1e-9f ||
                Vector3.Cross(forward.normalized, across.normalized).sqrMagnitude < 0.04f)
                return false;
        }
        return MakePalmFrame(wrist, index, middle, pinky, out frame);
    }

    private static bool TryAvatarPalm(HandState hand, out PalmFrame frame)
    {
        frame = new PalmFrame();
        return hand.wrist != null && hand.indexBase != null &&
               hand.middleBase != null && hand.pinkyBase != null &&
               MakePalmFrame(hand.wrist.position, hand.indexBase.position,
                             hand.middleBase.position, hand.pinkyBase.position, out frame);
    }

    private static bool TryForearmFrame(Vector3 direction,
        ref Vector3 lastForward, ref Vector3 lastUp, ref bool ready,
        out Quaternion frame)
    {
        frame = Quaternion.identity;
        if (direction.sqrMagnitude < 1e-10f) return false;
        Vector3 forward = direction.normalized;
        Vector3 up;
        if (ready)
        {
            Quaternion transport = Quaternion.FromToRotation(lastForward, forward);
            up = Vector3.ProjectOnPlane(transport * lastUp, forward);
        }
        else
        {
            Vector3 seed = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.9f
                ? Vector3.up : Vector3.forward;
            up = Vector3.ProjectOnPlane(seed, forward);
        }
        if (up.sqrMagnitude < 1e-8f)
        {
            Vector3 seed = Mathf.Abs(Vector3.Dot(forward, Vector3.right)) < 0.9f
                ? Vector3.right : Vector3.forward;
            up = Vector3.ProjectOnPlane(seed, forward);
            if (up.sqrMagnitude < 1e-8f) return false;
        }
        up.Normalize();
        lastForward = forward;
        lastUp = up;
        ready = true;
        frame = Quaternion.LookRotation(forward, up);
        return true;
    }

    private void Update()
    {
        if (poseClient == null || !poseClient.IsReceiving ||
            poseClient.LastMessageTime < 0f ||
            poseClient.LastMessageTime <= lastProcessedMessageTime)
            return;

        if (previousDirectMode != useDirectPalmWorldAlignment)
            ResetModeState();

        lastProcessedMessageTime = poseClient.LastMessageTime;
        left.trackedPalm.valid = false;
        right.trackedPalm.valid = false;

        HandData[] hands = poseClient.LatestHands;
        if (hands == null) return;

        foreach (HandData hand in hands)
        {
            if (hand == null) continue;
            bool targetLeft;
            if (!TryGetAvatarHandSide(hand.label, out targetLeft))
                continue;

            PalmFrame frame;
            if (!TryTrackingPalm(hand.landmarks, out frame)) continue;
            HandState state = targetLeft ? left : right;
            float now = Time.realtimeSinceStartup;
            if (useDirectPalmWorldAlignment && rejectAbruptPalmInput &&
                state.hasAcceptedInputPalm &&
                now - state.lastAcceptedInputTime <= inputContinuityTimeout &&
                Quaternion.Angle(state.lastAcceptedInputPalm.rotation, frame.rotation) >
                    maxPalmJumpPerPacketDegrees)
            {

                state.rejectedPackets++;
                continue;
            }

            state.trackedPalm = frame;
            state.lastHandSeen = now;
            state.lastAcceptedInputPalm = frame;
            state.hasAcceptedInputPalm = true;
            state.lastAcceptedInputTime = now;
        }
    }

    public void CalibrateNeutralPose() { }
    public void ResetWristCalibration() { }

    private void ResetModeState()
    {
        ResetHandModeState(left);
        ResetHandModeState(right);
        previousDirectMode = useDirectPalmWorldAlignment;
        Debug.Log("Mode switched: DirectPalm=" + useDirectPalmWorldAlignment);
    }

    private static void ResetHandModeState(HandState state)
    {
        state.calibrated = false;
        state.palmFrameOffset = Quaternion.identity;
        state.trackingFrameReady = false;
        state.avatarFrameReady = false;
        state.hasAcceptedInputPalm = false;
        state.trackedPalm.valid = false;
        state.hasDesiredPalm = false;
        state.lastUsable = -100f;
        state.lastHandSeen = -100f;
    }

    private static Quaternion LimitSwingTwist(Quaternion deltaWorld,
        Vector3 forearmAxisWorld, float swingLimit, float twistLimit)
    {
        if (forearmAxisWorld.sqrMagnitude < 1e-8f)
            return Quaternion.identity;
        Vector3 axis = forearmAxisWorld.normalized;

        if (deltaWorld.w < 0f)
            deltaWorld = new Quaternion(-deltaWorld.x, -deltaWorld.y,
                                        -deltaWorld.z, -deltaWorld.w);
        Vector3 xyz = new Vector3(deltaWorld.x, deltaWorld.y, deltaWorld.z);
        Vector3 projection = Vector3.Project(xyz, axis);
        Quaternion twist = new Quaternion(projection.x, projection.y,
                                          projection.z, deltaWorld.w);
        float length = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y +
                                  twist.z * twist.z + twist.w * twist.w);
        if (length < 1e-6f)
            twist = Quaternion.identity;
        else
            twist = new Quaternion(twist.x / length, twist.y / length,
                                   twist.z / length, twist.w / length);
        Quaternion swing = deltaWorld * Quaternion.Inverse(twist);
        Quaternion limitedSwing = Quaternion.RotateTowards(
            Quaternion.identity, swing, swingLimit);
        Quaternion limitedTwist = Quaternion.RotateTowards(
            Quaternion.identity, twist, twistLimit);
        return limitedSwing * limitedTwist;
    }

    private void LateUpdate()
    {
        if (previousDirectMode != useDirectPalmWorldAlignment)
            ResetModeState();
        if (!enableWristRotation) return;
        Apply(left, true);
        Apply(right, false);

        leftPalmErrorDegrees = left.palmError;
        rightPalmErrorDegrees = right.palmError;
        leftRejectedPalmPackets = left.rejectedPackets;
        rightRejectedPalmPackets = right.rejectedPackets;
        leftRequestedWristAngle = left.requestedWristAngle;
        rightRequestedWristAngle = right.requestedWristAngle;
        PalmFrame lp, rp;
        if (TryAvatarPalm(left, out lp) && TryAvatarPalm(right, out rp))
            palmNormalSeparationDegrees = Vector3.Angle(lp.normal, rp.normal);

        if (logPalmDiagnostics && Time.realtimeSinceStartup - lastLogTime >= 1f)
        {
            lastLogTime = Time.realtimeSinceStartup;
            Debug.Log($"direct={useDirectPalmWorldAlignment} " +
                      $"palmError L={leftPalmErrorDegrees:F1} R={rightPalmErrorDegrees:F1} " +
                      $"requestAngle L={leftRequestedWristAngle:F1} R={rightRequestedWristAngle:F1} " +
                      $"rejected L={leftRejectedPalmPackets} R={rightRejectedPalmPackets} " +
                      $"legacyCalibrated L={left.calibrated} R={right.calibrated}");
        }
    }

    private void Apply(HandState state, bool isLeft)
    {
        if (state.wrist == null) return;
        float now = Time.realtimeSinceStartup;
        if (!state.trackedPalm.valid || !poseClient.IsReceiving ||
            now - state.lastHandSeen > lostTrackingDelay)
        {
            Restore(state, now);
            return;
        }

        PalmFrame avatarPalm;
        if (!TryAvatarPalm(state, out avatarPalm))
        {
            Restore(state, now);
            return;
        }

        if (useDirectPalmWorldAlignment)
        {
            state.desiredPalm = animator.transform.rotation * state.trackedPalm.rotation;
            state.hasDesiredPalm = true;
        }
        else
        {

            Vector3 upperDir, lowerDir;
            if (!armsRetargeter.TryGetArmDirections(isLeft, out upperDir, out lowerDir))
            {
                Restore(state, now);
                return;
            }

            Quaternion trackingForearm;
            if (!TryForearmFrame(lowerDir, ref state.trackingForward,
                    ref state.trackingUp, ref state.trackingFrameReady,
                    out trackingForearm))
            {
                Restore(state, now);
                return;
            }

            Quaternion avatarForearm;
            if (!TryForearmFrame(state.wrist.position - state.elbow.position,
                    ref state.avatarForward, ref state.avatarUp,
                    ref state.avatarFrameReady, out avatarForearm))
            {
                Restore(state, now);
                return;
            }

            Quaternion trackedRelative = Quaternion.Inverse(trackingForearm) * state.trackedPalm.rotation;
            Quaternion avatarRelative = Quaternion.Inverse(avatarForearm) * avatarPalm.rotation;

            if (!state.calibrated)
            {
                state.palmFrameOffset = Quaternion.Inverse(trackedRelative) * avatarRelative;
                state.calibrated = true;
                state.lastUsable = now;
                state.hasDesiredPalm = false;
                Debug.Log(isLeft ? "LEFT palm calibrated" : "RIGHT palm calibrated");
                return;
            }

            state.desiredPalm = avatarForearm * trackedRelative * state.palmFrameOffset;
            state.hasDesiredPalm = true;
        }

        Quaternion rawTargetWrist = state.desiredPalm *
            Quaternion.Inverse(avatarPalm.rotation) * state.wrist.rotation;

        Quaternion neutralWorld = state.wrist.parent.rotation * state.restLocal;
        Quaternion fromNeutral = rawTargetWrist * Quaternion.Inverse(neutralWorld);
        state.requestedWristAngle = Quaternion.Angle(Quaternion.identity, fromNeutral);
        if (useDirectPalmWorldAlignment && useWristSwingTwistLimits)
        {
            fromNeutral = LimitSwingTwist(fromNeutral,
                state.wrist.position - state.elbow.position,
                maxWristSwingDegrees, maxWristTwistDegrees);
        }
        Quaternion limited = Quaternion.RotateTowards(Quaternion.identity,
                                                      fromNeutral, maxRotationAngle);
        Quaternion weighted = Quaternion.Slerp(Quaternion.identity, limited, rotationWeight);
        Quaternion targetWorld = weighted * neutralWorld;

        if (now - state.lastUsable > lostTrackingDelay)
            state.recoveryUntil = now + recoveryDuration;

        float speed = now < state.recoveryUntil ? recoverySpeed : rotationSpeed;
        float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Time.deltaTime);
        Quaternion smoothed = Quaternion.Slerp(state.wrist.rotation, targetWorld, alpha);
        state.wrist.rotation = Quaternion.RotateTowards(state.wrist.rotation,
            smoothed, maxDegreesPerSecond * Time.deltaTime);

        PalmFrame updatedPalm;
        if (TryAvatarPalm(state, out updatedPalm))
            state.palmError = Quaternion.Angle(updatedPalm.rotation, state.desiredPalm);

        state.lastUsable = now;
    }

    private void Restore(HandState state, float now)
    {
        if (now - state.lastUsable <= lostTrackingDelay) return;
        float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, returnSpeed) * Time.deltaTime);
        state.wrist.localRotation = Quaternion.Slerp(state.wrist.localRotation,
                                                    state.restLocal, alpha);
        state.hasDesiredPalm = false;
        state.palmError = 0f;
    }

    private void OnDrawGizmos()
    {
        if (!drawPalmDirections || !Application.isPlaying) return;
        DrawHand(left);
        DrawHand(right);
    }

    private void DrawHand(HandState hand)
    {
        if (hand.wrist == null) return;
        PalmFrame actual;
        if (!TryAvatarPalm(hand, out actual)) return;
        Vector3 origin = hand.wrist.position;
        Gizmos.color = Color.green; 
        Gizmos.DrawLine(origin, origin + actual.normal * debugAxisLength);
        if (hand.hasDesiredPalm)
        {
            Gizmos.color = Color.cyan; 
            Gizmos.DrawLine(origin,
                origin + (hand.desiredPalm * Vector3.up) * debugAxisLength);
        }
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(origin, 0.009f);
    }
}

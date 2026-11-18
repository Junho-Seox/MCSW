using UnityEngine;

/// <summary>
/// Step 3A: read-only diagnostics for the EXISTING BothArmsRetargeter position IK.
/// This script never writes to Transform, Animator, or IK settings.
/// Attach to any active GameObject; assign the same Humanoid Animator as the arm retargeter.
/// LateUpdate runs after the arm retargeter and wrist retargeter.
/// </summary>
[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public class ArmIKStep3ADiagnostics : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public BothArmsRetargeter bothArms;

    [Header("Logging")]
    public bool logOncePerSecond = true;
    [Min(0.2f)] public float logIntervalSeconds = 1f;

    [Header("Scene View Gizmos")]
    public bool drawTargets = true;
    public bool drawHeadDistances = false;
    [Min(0.002f)] public float markerRadius = 0.018f;

    private Transform leftShoulder, leftElbow, leftHand;
    private Transform rightShoulder, rightElbow, rightHand;
    private Transform head;
    private bool ready;
    private float nextLogTime;
    private bool warnedIKDisabled;

    private struct ArmStatus
    {
        public bool valid;
        public Vector3 target;
        public Vector3 handPosition;
        public float wristError;
        public float reachRatio;
        public float elbowBendDegrees;
        public float distanceToHead;
        public bool crossedMidline;
    }

    private ArmStatus leftStatus;
    private ArmStatus rightStatus;

    private void Start()
    {
        if (bothArms == null)
            bothArms = GetComponent<BothArmsRetargeter>();
        if (bothArms == null)
            bothArms = GetComponentInChildren<BothArmsRetargeter>();
        if (bothArms == null)
            bothArms = GetComponentInParent<BothArmsRetargeter>();

        if (animator == null && bothArms != null)
            animator = bothArms.animator;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (bothArms == null || animator == null || animator.avatar == null ||
            !animator.avatar.isHuman || !animator.avatar.isValid)
        {
            Debug.LogError("[ArmIK/Step3A] Assign BothArmsRetargeter and a valid Humanoid Animator.", this);
            enabled = false;
            return;
        }

        leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        head = animator.GetBoneTransform(HumanBodyBones.Head);

        ready = leftShoulder != null && leftElbow != null && leftHand != null &&
                rightShoulder != null && rightElbow != null && rightHand != null;
        if (!ready)
        {
            Debug.LogError("[ArmIK/Step3A] One or more arm bones are missing.", this);
            enabled = false;
            return;
        }

        Debug.Log("[ArmIK/Step3A] Read-only diagnostics ready. Enable Position IK on BothArmsRetargeter to test targets.", this);
    }

    private void LateUpdate()
    {
        if (!ready || bothArms == null)
            return;

        if (!bothArms.enablePositionIK)
        {
            leftStatus.valid = false;
            rightStatus.valid = false;
            if (!warnedIKDisabled)
            {
                Debug.Log("[ArmIK/Step3A] Position IK is OFF. Existing direction-based solver is active; diagnostics are waiting.", this);
                warnedIKDisabled = true;
            }
            return;
        }
        warnedIKDisabled = false;

        Vector3 mid = (leftShoulder.position + rightShoulder.position) * 0.5f;
        Vector3 rightAxis = rightShoulder.position - leftShoulder.position;
        if (rightAxis.sqrMagnitude > 1e-8f)
            rightAxis.Normalize();
        else
            rightAxis = animator.transform.right;

        leftStatus = Evaluate(true, leftShoulder, leftElbow, leftHand, mid, rightAxis);
        rightStatus = Evaluate(false, rightShoulder, rightElbow, rightHand, mid, rightAxis);

        if (logOncePerSecond && Time.unscaledTime >= nextLogTime)
        {
            nextLogTime = Time.unscaledTime + Mathf.Max(0.2f, logIntervalSeconds);
            Debug.Log("[ArmIK/Step3A] " + FormatArm("L", leftStatus) + " | " +
                      FormatArm("R", rightStatus) +
                      " | errors are distances to estimated IK targets, NOT ground-truth accuracy.", this);
        }
    }

    private ArmStatus Evaluate(bool isLeft, Transform shoulder, Transform elbow,
                               Transform hand, Vector3 mid, Vector3 rightAxis)
    {
        ArmStatus status = new ArmStatus();
        Vector3 target;
        float reportedError;
        if (!bothArms.TryGetIKTarget(isLeft, out target, out reportedError))
            return status;

        status.valid = true;
        status.target = target;
        status.handPosition = hand.position;
        status.wristError = Vector3.Distance(hand.position, target);

        float upperLength = Vector3.Distance(shoulder.position, elbow.position);
        float lowerLength = Vector3.Distance(elbow.position, hand.position);
        float totalLength = upperLength + lowerLength;
        status.reachRatio = totalLength > 1e-6f
            ? Vector3.Distance(shoulder.position, target) / totalLength : 0f;

        Vector3 upperDirection = elbow.position - shoulder.position;
        Vector3 lowerDirection = hand.position - elbow.position;
        status.elbowBendDegrees = upperDirection.sqrMagnitude > 1e-8f &&
                                  lowerDirection.sqrMagnitude > 1e-8f
            ? Vector3.Angle(upperDirection, lowerDirection) : 0f;

        status.crossedMidline = isLeft
            ? Vector3.Dot(target - mid, rightAxis) > 0f
            : Vector3.Dot(target - mid, rightAxis) < 0f;
        status.distanceToHead = head != null
            ? Vector3.Distance(hand.position, head.position) : -1f;
        return status;
    }

    private static string FormatArm(string label, ArmStatus s)
    {
        if (!s.valid)
            return label + "=no valid target";
        return string.Format(
            "{0}: wristError={1:F3}u, reach={2:F2}, elbowBend={3:F1}deg, " +
            "targetCrossesMidline={4}, handToHeadBone={5:F3}u",
            label, s.wristError, s.reachRatio, s.elbowBendDegrees,
            s.crossedMidline, s.distanceToHead);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !drawTargets || !ready)
            return;
        DrawStatus(leftStatus, Color.cyan);
        DrawStatus(rightStatus, Color.magenta);
    }

    private void DrawStatus(ArmStatus s, Color color)
    {
        if (!s.valid)
            return;
        Gizmos.color = color;
        Gizmos.DrawSphere(s.target, markerRadius);
        Gizmos.DrawLine(s.handPosition, s.target);
        if (drawHeadDistances && head != null)
            Gizmos.DrawLine(s.handPosition, head.position);
    }
}

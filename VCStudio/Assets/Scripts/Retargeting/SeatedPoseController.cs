using UnityEngine;

[DefaultExecutionOrder(10000)]
public class SeatedPoseController : MonoBehaviour
{
    [Header("Humanoid Avatar")]
    [SerializeField] private Animator avatarAnimator;

    [Header("Seated Pose")]
    [Tooltip("Enable this to hold the legs in the seated pose.")]
    [SerializeField] private bool seated = true;

    [Tooltip("Turn on if the avatar's actual front is opposite its Animator transform forward.")]
    [SerializeField] private bool reverseForward = false;

    [Tooltip("Angle of the thighs below horizontal. 0 means horizontal thighs.")]
    [Range(0f, 30f)]
    [SerializeField] private float thighDownAngle = 8f;

    [Tooltip("Positive values point the shins slightly backward instead of straight down.")]
    [Range(-20f, 20f)]
    [SerializeField] private float shinBackwardAngle = 0f;

    [Tooltip("Small outward separation of the knees, relative to the forward direction.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float kneeSpread = 0.06f;

    [Tooltip("Preserve each foot's original orientation after bending the legs.")]
    [SerializeField] private bool keepFeetLevel = true;

    private sealed class Leg
    {
        public Transform upper;
        public Transform lower;
        public Transform foot;

        public Quaternion upperRest;
        public Quaternion lowerRest;
        public Quaternion footRest;

        public Quaternion upperSeated;
        public Quaternion lowerSeated;
        public Quaternion footSeated;
    }

    private Leg left;
    private Leg right;
    private bool initialized;
    private bool lastSeated;

    private void Reset()
    {
        avatarAnimator = GetComponent<Animator>();
        if (avatarAnimator == null)
            avatarAnimator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (avatarAnimator == null)
            avatarAnimator = GetComponent<Animator>();
        if (avatarAnimator == null)
            avatarAnimator = GetComponentInChildren<Animator>();

        if (avatarAnimator == null || !avatarAnimator.isHuman)
        {
            Debug.LogError("[SeatedPose] A valid Humanoid Animator is required.", this);
            enabled = false;
            return;
        }

        left = BindLeg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        right = BindLeg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

        if (left == null || right == null)
        {
            Debug.LogError("[SeatedPose] The Humanoid rig is missing one or more leg bones.", this);
            enabled = false;
            return;
        }

        CaptureRest(left);
        CaptureRest(right);
        initialized = true;
    }

    private void Start()
    {
        if (initialized)
            RebuildSeatedPose();
    }

    private Leg BindLeg(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones footBone)
    {
        Transform upper = avatarAnimator.GetBoneTransform(upperBone);
        Transform lower = avatarAnimator.GetBoneTransform(lowerBone);
        Transform foot = avatarAnimator.GetBoneTransform(footBone);

        if (upper == null || lower == null || foot == null)
            return null;

        return new Leg { upper = upper, lower = lower, foot = foot };
    }

    private static void CaptureRest(Leg leg)
    {
        leg.upperRest = leg.upper.localRotation;
        leg.lowerRest = leg.lower.localRotation;
        leg.footRest = leg.foot.localRotation;
    }

    private static void RestoreRest(Leg leg)
    {
        leg.upper.localRotation = leg.upperRest;
        leg.lower.localRotation = leg.lowerRest;
        leg.foot.localRotation = leg.footRest;
    }

    [ContextMenu("Rebuild Seated Pose (Play Mode)")]
    public void RebuildSeatedPose()
    {
        if (!initialized)
            return;

        RestoreRest(left);
        RestoreRest(right);

        Vector3 forward = avatarAnimator.transform.forward * (reverseForward ? -1f : 1f);
        Vector3 up = avatarAnimator.transform.up;
        Vector3 rightAxis = avatarAnimator.transform.right;

        BuildLegPose(left, forward, up, rightAxis, -1f);
        BuildLegPose(right, forward, up, rightAxis, 1f);

        RestoreRest(left);
        RestoreRest(right);

        if (seated)
            ApplySeatedPose();

        lastSeated = seated;
    }

    private void BuildLegPose(Leg leg, Vector3 forward, Vector3 up, Vector3 rightAxis, float side)
    {
        Quaternion originalFootWorld = leg.foot.rotation;

        float thighRadians = thighDownAngle * Mathf.Deg2Rad;
        Vector3 thighTarget =
            forward * Mathf.Cos(thighRadians) -
            up * Mathf.Sin(thighRadians) +
            rightAxis * (side * kneeSpread);
        thighTarget.Normalize();

        Vector3 currentThigh = leg.lower.position - leg.upper.position;
        if (currentThigh.sqrMagnitude > 0.000001f)
        {
            Quaternion rotateThigh = Quaternion.FromToRotation(currentThigh.normalized, thighTarget);
            leg.upper.rotation = rotateThigh * leg.upper.rotation;
        }

        float shinRadians = shinBackwardAngle * Mathf.Deg2Rad;
        Vector3 shinTarget = -up * Mathf.Cos(shinRadians) - forward * Mathf.Sin(shinRadians);
        shinTarget.Normalize();

        Vector3 currentShin = leg.foot.position - leg.lower.position;
        if (currentShin.sqrMagnitude > 0.000001f)
        {
            Quaternion rotateShin = Quaternion.FromToRotation(currentShin.normalized, shinTarget);
            leg.lower.rotation = rotateShin * leg.lower.rotation;
        }

        if (keepFeetLevel)
            leg.foot.rotation = originalFootWorld;

        leg.upperSeated = leg.upper.localRotation;
        leg.lowerSeated = leg.lower.localRotation;
        leg.footSeated = leg.foot.localRotation;
    }

    private void ApplySeatedPose()
    {
        ApplyLeg(left);
        ApplyLeg(right);
    }

    private void ApplyLeg(Leg leg)
    {
        leg.upper.localRotation = leg.upperSeated;
        leg.lower.localRotation = leg.lowerSeated;
        if (keepFeetLevel)
            leg.foot.localRotation = leg.footSeated;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        if (lastSeated != seated)
        {
            if (!seated)
            {
                RestoreRest(left);
                RestoreRest(right);
            }
            lastSeated = seated;
        }

        if (seated)
            ApplySeatedPose();
    }

    public void SetSeated(bool value)
    {
        seated = value;
        if (!initialized)
            return;

        if (seated)
            ApplySeatedPose();
        else
        {
            RestoreRest(left);
            RestoreRest(right);
        }
        lastSeated = seated;
    }

    private void OnDisable()
    {
        if (initialized)
        {
            RestoreRest(left);
            RestoreRest(right);
        }
    }
}


using UnityEngine;

public class BothLegsRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;

    [Header("Coordinate Mapping")]
    public bool swapLeftRight = false;
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Tracking")]
    [Range(0f, 1f)]
    public float minVisibility = 0.5f;

    [Header("Rotation")]
    [Range(0f, 1f)]
    public float rotationWeight = 1.0f;

    private class Leg
    {
        public Transform upper;
        public Transform lower;
        public Transform foot;

        public Quaternion upperRestLocal;
        public Quaternion lowerRestLocal;

        public Vector3 targetUpper;
        public Vector3 targetLower;

        public bool valid;
    }

    private readonly Leg left = new Leg();
    private readonly Leg right = new Leg();

    private bool initialized;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null ||
            animator.avatar == null ||
            !animator.avatar.isValid ||
            !animator.avatar.isHuman)
        {
            Debug.LogError(
                "BothLegsRetargeter: Humanoid Animator 오류");
            return;
        }

        SetupLeg(
            left,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        );

        SetupLeg(
            right,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot
        );

        initialized =
            left.upper != null &&
            left.lower != null &&
            left.foot != null &&
            right.upper != null &&
            right.lower != null &&
            right.foot != null;

        if (initialized)
        {
            Debug.Log("양다리 리타게팅 준비 완료");
        }
        else
        {
            Debug.LogError(
                "양다리 Humanoid 뼈 매핑 실패");
        }
    }

    private void SetupLeg(
        Leg leg,
        HumanBodyBones upperBone,
        HumanBodyBones lowerBone,
        HumanBodyBones footBone)
    {
        leg.upper = animator.GetBoneTransform(upperBone);
        leg.lower = animator.GetBoneTransform(lowerBone);
        leg.foot = animator.GetBoneTransform(footBone);

        if (leg.upper == null ||
            leg.lower == null ||
            leg.foot == null)
        {
            return;
        }

        leg.upperRestLocal = leg.upper.localRotation;
        leg.lowerRestLocal = leg.lower.localRotation;
    }

    private Vector3 ConvertPosition(PoseLandmark p)
    {
        return new Vector3(
            invertX ? -p.x : p.x,
            invertY ? -p.y : p.y,
            invertZ ? -p.z : p.z
        );
    }

    public void SetPose(PoseLandmark[] landmarks)
    {
        if (!initialized ||
            landmarks == null ||
            landmarks.Length != 33)
        {
            left.valid = false;
            right.valid = false;
            return;
        }

        if (swapLeftRight)
        {
            SetLegTarget(left, landmarks, 24, 26, 28);
            SetLegTarget(right, landmarks, 23, 25, 27);
        }
        else
        {
            SetLegTarget(left, landmarks, 23, 25, 27);
            SetLegTarget(right, landmarks, 24, 26, 28);
        }
    }

    private void SetLegTarget(
        Leg leg,
        PoseLandmark[] landmarks,
        int hipId,
        int kneeId,
        int ankleId)
    {
        PoseLandmark hip = landmarks[hipId];
        PoseLandmark knee = landmarks[kneeId];
        PoseLandmark ankle = landmarks[ankleId];

        if (hip.visibility < minVisibility ||
            knee.visibility < minVisibility ||
            ankle.visibility < minVisibility)
        {
            leg.valid = false;
            return;
        }

        Vector3 h = ConvertPosition(hip);
        Vector3 k = ConvertPosition(knee);
        Vector3 a = ConvertPosition(ankle);

        Vector3 upperDirection = k - h;
        Vector3 lowerDirection = a - k;

        if (upperDirection.sqrMagnitude < 0.000001f ||
            lowerDirection.sqrMagnitude < 0.000001f)
        {
            leg.valid = false;
            return;
        }

        leg.targetUpper = upperDirection.normalized;
        leg.targetLower = lowerDirection.normalized;
        leg.valid = true;
    }

    public void ApplyPose()
    {
        if (!initialized)
            return;

        ApplyLeg(left);
        ApplyLeg(right);
    }

    private void ApplyLeg(Leg leg)
    {
        if (!leg.valid)
            return;

        // 이전 프레임의 회전 누적 방지
        leg.upper.localRotation = leg.upperRestLocal;
        leg.lower.localRotation = leg.lowerRestLocal;

        // 허벅지 방향 보정
        Vector3 currentUpper =
            leg.lower.position - leg.upper.position;

        if (currentUpper.sqrMagnitude < 0.000001f)
            return;

        Quaternion upperDelta =
            Quaternion.FromToRotation(
                currentUpper.normalized,
                leg.targetUpper
            );

        Quaternion weightedUpper =
            Quaternion.Slerp(
                Quaternion.identity,
                upperDelta,
                rotationWeight
            );

        leg.upper.rotation =
            weightedUpper * leg.upper.rotation;

        // 종아리 방향 보정
        Vector3 currentLower =
            leg.foot.position - leg.lower.position;

        if (currentLower.sqrMagnitude < 0.000001f)
            return;

        Quaternion lowerDelta =
            Quaternion.FromToRotation(
                currentLower.normalized,
                leg.targetLower
            );

        Quaternion weightedLower =
            Quaternion.Slerp(
                Quaternion.identity,
                lowerDelta,
                rotationWeight
            );

        leg.lower.rotation =
            weightedLower * leg.lower.rotation;
    }
}

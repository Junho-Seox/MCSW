
using UnityEngine;

public class LeftArmRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;

    [Header("Coordinate Mapping")]
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Tracking")]
    [Range(0f, 1f)]
    public float minVisibility = 0.5f;

    private Transform upperArm;
    private Transform lowerArm;
    private Transform hand;

    private Quaternion upperRestLocal;
    private Quaternion lowerRestLocal;

    private Vector3 targetUpper;
    private Vector3 targetLower;

    private bool hasPose;
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
            Debug.LogError("Humanoid Animator 설정 오류");
            return;
        }

        upperArm = animator.GetBoneTransform(
            HumanBodyBones.LeftUpperArm);

        lowerArm = animator.GetBoneTransform(
            HumanBodyBones.LeftLowerArm);

        hand = animator.GetBoneTransform(
            HumanBodyBones.LeftHand);

        if (upperArm == null ||
            lowerArm == null ||
            hand == null)
        {
            Debug.LogError("왼팔 뼈 매핑 실패");
            return;
        }

        upperRestLocal = upperArm.localRotation;
        lowerRestLocal = lowerArm.localRotation;

        initialized = true;
        Debug.Log("왼팔 리타게팅 준비 완료");
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
        if (landmarks == null || landmarks.Length != 33)
        {
            hasPose = false;
            return;
        }

        PoseLandmark shoulder = landmarks[11];
        PoseLandmark elbow = landmarks[13];
        PoseLandmark wrist = landmarks[15];

        if (shoulder.visibility < minVisibility ||
            elbow.visibility < minVisibility ||
            wrist.visibility < minVisibility)
        {
            hasPose = false;
            return;
        }

        Vector3 s = ConvertPosition(shoulder);
        Vector3 e = ConvertPosition(elbow);
        Vector3 w = ConvertPosition(wrist);

        Vector3 upper = e - s;
        Vector3 lower = w - e;

        if (upper.sqrMagnitude < 0.000001f ||
            lower.sqrMagnitude < 0.000001f)
        {
            hasPose = false;
            return;
        }

        targetUpper = upper.normalized;
        targetLower = lower.normalized;

        hasPose = true;
    }

    private void LateUpdate()
    {
        if (!initialized || !hasPose)
            return;

        // 매 프레임 기준 자세로 복원
        upperArm.localRotation = upperRestLocal;
        lowerArm.localRotation = lowerRestLocal;

        // 1. 위팔 회전
        Vector3 currentUpper =
            (lowerArm.position - upperArm.position).normalized;

        Quaternion upperDelta =
            Quaternion.FromToRotation(
                currentUpper,
                targetUpper
            );

        upperArm.rotation =
            upperDelta * upperArm.rotation;

        // 2. 위팔 회전 후 아래팔 방향 재계산
        Vector3 currentLower =
            (hand.position - lowerArm.position).normalized;

        Quaternion lowerDelta =
            Quaternion.FromToRotation(
                currentLower,
                targetLower
            );

        lowerArm.rotation =
            lowerDelta * lowerArm.rotation;
    }
}

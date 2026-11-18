
using UnityEngine;

public class FeetRetargeter : MonoBehaviour
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

    [Header("Rotation")]
    [Range(0f, 1f)]
    public float rotationWeight = 0.7f;

    private class Foot
    {
        public Transform bone;
        public Quaternion restLocal;

        public Quaternion currentRotation;
        public Quaternion referenceRotation;

        public bool valid;
        public bool calibrated;
    }

    private readonly Foot left = new Foot();
    private readonly Foot right = new Foot();

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
            Debug.LogError("Feet: Humanoid Avatar 오류");
            return;
        }

        left.bone = animator.GetBoneTransform(
            HumanBodyBones.LeftFoot);

        right.bone = animator.GetBoneTransform(
            HumanBodyBones.RightFoot);

        if (left.bone == null || right.bone == null)
        {
            Debug.LogError("양발 뼈를 찾지 못했습니다.");
            return;
        }

        left.restLocal = left.bone.localRotation;
        right.restLocal = right.bone.localRotation;

        initialized = true;
        Debug.Log("양발 리타게팅 준비 완료");
    }

    private Vector3 Convert(PoseLandmark p)
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

        SetFoot(left, landmarks, 27, 29, 31);
        SetFoot(right, landmarks, 28, 30, 32);
    }

    private void SetFoot(
        Foot foot,
        PoseLandmark[] landmarks,
        int ankleId,
        int heelId,
        int toeId)
    {
        foot.valid = false;

        if (landmarks[ankleId].visibility < minVisibility ||
            landmarks[heelId].visibility < minVisibility ||
            landmarks[toeId].visibility < minVisibility)
            return;

        Vector3 ankle = Convert(landmarks[ankleId]);
        Vector3 heel = Convert(landmarks[heelId]);
        Vector3 toe = Convert(landmarks[toeId]);

        Vector3 forward = toe - heel;
        Vector3 upHint = ankle - heel;

        if (forward.sqrMagnitude < 0.000001f ||
            upHint.sqrMagnitude < 0.000001f)
            return;

        forward.Normalize();

        Vector3 up = Vector3.ProjectOnPlane(
            upHint, forward
        );

        if (up.sqrMagnitude < 0.000001f)
            return;

        up.Normalize();

        foot.currentRotation =
            Quaternion.LookRotation(forward, up);

        foot.valid = true;
    }

    public void Calibrate()
    {
        CalibrateFoot(left);
        CalibrateFoot(right);
    }

    private void CalibrateFoot(Foot foot)
    {
        if (!foot.valid)
            return;

        foot.referenceRotation = foot.currentRotation;
        foot.calibrated = true;
    }

    public void ApplyPose()
    {
        if (!initialized)
            return;

        ApplyFoot(left);
        ApplyFoot(right);
    }

    private void ApplyFoot(Foot foot)
    {
        // 이전 프레임 회전 누적 방지
        foot.bone.localRotation = foot.restLocal;

        if (!foot.valid || !foot.calibrated)
            return;

        Quaternion delta =
            foot.currentRotation *
            Quaternion.Inverse(foot.referenceRotation);

        Quaternion weightedDelta =
            Quaternion.Slerp(
                Quaternion.identity,
                delta,
                rotationWeight
            );

        foot.bone.rotation =
            weightedDelta * foot.bone.rotation;
    }
}


using UnityEngine;

public class HumanoidBoneDebugger : MonoBehaviour
{
    public Animator animator;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null ||
            animator.avatar == null ||
            !animator.avatar.isValid ||
            !animator.avatar.isHuman)
        {
            Debug.LogError("유효한 Humanoid Avatar가 없습니다.");
            return;
        }

        PrintBone(HumanBodyBones.LeftUpperArm);
        PrintBone(HumanBodyBones.LeftLowerArm);
        PrintBone(HumanBodyBones.LeftHand);
    }

    private void PrintBone(HumanBodyBones bone)
    {
        Transform t = animator.GetBoneTransform(bone);

        Debug.Log(
            $"{bone}: {(t != null ? t.name : "Missing")}"
        );
    }
}


using UnityEngine;

public class HumanoidRestPoseDebugger : MonoBehaviour
{
    public Animator animator;

    [ContextMenu("Print Humanoid Bone Directions")]
    public void PrintBoneDirections()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("Humanoid Animator가 필요합니다.");
            return;
        }

        PrintDirection(
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm
        );

        PrintDirection(
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand
        );

        PrintDirection(
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg
        );

        PrintDirection(
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot
        );
    }

    private void PrintDirection(
        HumanBodyBones from,
        HumanBodyBones to)
    {
        Transform a = animator.GetBoneTransform(from);
        Transform b = animator.GetBoneTransform(to);

        if (a == null || b == null)
        {
            Debug.LogWarning($"{from} 또는 {to} 없음");
            return;
        }

        Vector3 direction =
            (b.position - a.position).normalized;

        Debug.Log(
            $"{from} → {to} : {direction}"
        );
    }
}

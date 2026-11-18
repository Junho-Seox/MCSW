
using UnityEngine;

public class FootIKTargetController : MonoBehaviour
{
    [Header("Foot Bones")]
    public Transform leftFoot;
    public Transform rightFoot;

    [Header("IK Targets")]
    public Transform leftTarget;
    public Transform rightTarget;

    [Header("Knee Hints")]
    public Transform leftKnee;
    public Transform rightKnee;

    public Transform leftHint;
    public Transform rightHint;

    [Header("Settings")]
    public float hintDistance = 0.3f;

    private void Start()
    {
        InitializeTargets();
    }

    [ContextMenu("Initialize IK Targets")]
    public void InitializeTargets()
    {
        if (leftFoot != null && leftTarget != null)
        {
            leftTarget.position = leftFoot.position;
            leftTarget.rotation = leftFoot.rotation;
        }

        if (rightFoot != null && rightTarget != null)
        {
            rightTarget.position = rightFoot.position;
            rightTarget.rotation = rightFoot.rotation;
        }

        if (leftKnee != null && leftHint != null)
        {
            leftHint.position =
                leftKnee.position +
                transform.forward * hintDistance;
        }

        if (rightKnee != null && rightHint != null)
        {
            rightHint.position =
                rightKnee.position +
                transform.forward * hintDistance;
        }
    }
}

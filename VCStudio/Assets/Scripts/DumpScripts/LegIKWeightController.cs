
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LegIKWeightController : MonoBehaviour
{
    public TwoBoneIKConstraint leftLegIK;
    public TwoBoneIKConstraint rightLegIK;

    [Range(0f, 1f)]
    public float ikWeight = 0f;

    private void Update()
    {
        if (leftLegIK != null)
            leftLegIK.weight = ikWeight;

        if (rightLegIK != null)
            rightLegIK.weight = ikWeight;
    }
}


using UnityEngine;

public class PoseAxisDebugger : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Display")]
    public float axisLength = 0.2f;
    public bool showAxes = true;

    private void OnDrawGizmos()
    {
        if (!showAxes || target == null)
            return;

        Vector3 origin = target.position;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            origin,
            origin + target.right * axisLength
        );

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin,
            origin + target.up * axisLength
        );

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(
            origin,
            origin + target.forward * axisLength
        );
    }
}

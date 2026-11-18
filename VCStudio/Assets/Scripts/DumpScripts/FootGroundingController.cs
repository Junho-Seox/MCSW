
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class FootGroundingController : MonoBehaviour
{
    [System.Serializable]
    public class FootState
    {
        public Transform foot;
        public Transform target;
        public TwoBoneIKConstraint ik;

        [HideInInspector] public bool locked;
        [HideInInspector] public bool initialized;
        [HideInInspector] public Vector3 previousPosition;
        [HideInInspector] public Vector3 lockedPosition;
        [HideInInspector] public float currentWeight;
    }

    [Header("Feet")]
    public FootState left = new FootState();
    public FootState right = new FootState();

    [Header("Ground")]
    public LayerMask groundMask;
    public float rayStartHeight = 0.4f;
    public float rayDistance = 1.0f;
    public float footGroundOffset = 0.03f;

    [Header("Contact Detection")]
    public float contactHeight = 0.08f;
    public float releaseHeight = 0.15f;
    public float maxContactSpeed = 0.15f;

    [Header("IK")]
    [Range(0f, 1f)]
    public float maxIKWeight = 0.8f;

    public float weightChangeSpeed = 5f;

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        UpdateFoot(left, dt);
        UpdateFoot(right, dt);
    }

    private void UpdateFoot(FootState state, float dt)
    {
        if (state.foot == null ||
            state.target == null ||
            state.ik == null)
            return;

        Vector3 footPosition = state.foot.position;

        if (!state.initialized)
        {
            state.initialized = true;
            state.previousPosition = footPosition;
            state.target.position = footPosition;
        }

        float speed = Vector3.Distance(
            footPosition,
            state.previousPosition
        ) / dt;

        state.previousPosition = footPosition;

        Vector3 rayOrigin =
            footPosition + Vector3.up * rayStartHeight;

        bool hasGround = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (hasGround)
        {
            Vector3 groundTarget =
                hit.point +
                Vector3.up * footGroundOffset;

            float height =
                footPosition.y - groundTarget.y;

            if (!state.locked)
            {
                if (Mathf.Abs(height) <= contactHeight &&
                    speed <= maxContactSpeed)
                {
                    state.locked = true;
                    state.lockedPosition = groundTarget;
                }
            }
            else
            {
                if (height > releaseHeight)
                {
                    state.locked = false;
                }
            }
        }
        else
        {
            state.locked = false;
        }

        float targetWeight =
            state.locked ? maxIKWeight : 0f;

        state.currentWeight = Mathf.MoveTowards(
            state.currentWeight,
            targetWeight,
            weightChangeSpeed * dt
        );

        if (state.locked)
        {
            state.target.position =
                state.lockedPosition;
        }
        else
        {
            state.target.position = footPosition;
        }

        state.ik.weight = state.currentWeight;
    }

    private void OnDrawGizmosSelected()
    {
        DrawFootGizmo(left, Color.green);
        DrawFootGizmo(right, Color.cyan);
    }

    private void DrawFootGizmo(
        FootState state,
        Color color)
    {
        if (state.target == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(
            state.target.position,
            0.04f
        );
    }
}

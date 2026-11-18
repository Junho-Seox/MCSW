using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ChairGroundFitter : MonoBehaviour
{
    [Header("Floor Reference")]
    [Tooltip("Assign the Renderer on the actual floor mesh. Its top surface is used as ground height.")]
    [SerializeField] private Renderer floorSurface;

    [Tooltip("Used only when Floor Surface is not assigned. World-space Y of the floor surface.")]
    [SerializeField] private float groundHeight = 0f;

    [ContextMenu("Fit Chair Wheels To Ground")]
    public void FitChairWheelsToGround()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ChairGroundFitter] Stop Play mode before fitting the chair.", this);
            return;
        }
#endif
        Transform visuals = transform.Find("ChairVisuals_ScaleHere");
        if (visuals == null)
        {
            Debug.LogError("[ChairGroundFitter] Could not find ChairVisuals_ScaleHere under this object.", this);
            return;
        }

        float floorY = floorSurface != null ? floorSurface.bounds.max.y : groundHeight;
        float currentBottomY = float.PositiveInfinity;
        int casterCount = 0;

        foreach (Transform child in visuals)
        {
            if (!child.name.StartsWith("Caster_")) continue;
            Renderer casterRenderer = child.GetComponent<Renderer>();
            if (casterRenderer == null) continue;
            currentBottomY = Mathf.Min(currentBottomY, casterRenderer.bounds.min.y);
            casterCount++;
        }

        if (casterCount == 0)
        {
            Debug.LogError("[ChairGroundFitter] No Caster_1~5 objects with Renderers were found.", this);
            return;
        }

        float deltaWorldY = floorY - currentBottomY;
        if (Mathf.Abs(deltaWorldY) < 0.001f)
        {
            Debug.Log("[ChairGroundFitter] The wheels are already aligned with the floor.", this);
            return;
        }

        Vector3 localDelta = visuals.InverseTransformVector(Vector3.up * deltaWorldY);
        Transform column = visuals.Find("GasLift_Column");
        if (column == null)
        {
            Debug.LogError("[ChairGroundFitter] GasLift_Column is missing.", this);
            return;
        }

        float newColumnHalfHeight = column.localScale.y - localDelta.y * 0.5f;
        if (newColumnHalfHeight < 0.015f)
        {
            Debug.LogWarning("[ChairGroundFitter] The floor is too high to shorten the existing lift column safely.", this);
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(column, "Fit chair lift column to floor");
#endif
        column.localPosition += localDelta * 0.5f;
        Vector3 columnScale = column.localScale;
        columnScale.y = newColumnHalfHeight;
        column.localScale = columnScale;

        foreach (Transform child in visuals)
        {
            bool isLowerBase = child.name == "Base_Hub" ||
                               child.name.StartsWith("Base_Spoke_") ||
                               child.name.StartsWith("Caster_");
            if (!isLowerBase) continue;
#if UNITY_EDITOR
            Undo.RecordObject(child, "Fit chair base to floor");
#endif
            child.localPosition += localDelta;
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
        Debug.Log($"[ChairGroundFitter] Moved lower base by {deltaWorldY:F3} world units to floor Y={floorY:F3}. Seat and backrest unchanged.", this);
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VirtualCharacterChairGenerator
{
    private const string ParentName = "SeatingProps";
    private const string ChairName = "StreamingChair";
    private const string MaterialFolder = "Assets/VirtualCharacterStudio/Generated/ChairMaterials";

    [MenuItem("Tools/VirtualCharacterStudio/Generate Chair For Selected Avatar")]
    private static void GenerateChair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Generate Chair", "Stop Play mode first. Changes made in Play mode are not saved to the scene.", "OK");
            return;
        }

        GameObject selected = Selection.activeGameObject;
        Animator animator = FindHumanoidAnimator(selected);
        if (animator == null)
        {
            EditorUtility.DisplayDialog("Generate Chair", "Select the VRM avatar root (or its Humanoid Animator) in the Hierarchy, then try again.", "OK");
            return;
        }

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform shin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (hips == null)
        {
            EditorUtility.DisplayDialog("Generate Chair", "The Humanoid rig has no Hips bone.", "OK");
            return;
        }

        float torso = head != null ? Vector3.Distance(hips.position, head.position) : 0.65f;
        float scale = Mathf.Clamp(torso / 0.65f, 0.65f, 1.7f);
        float shinLength = (shin != null && foot != null)
            ? Vector3.Distance(shin.position, foot.position) / scale
            : 0.45f;
        float baseDepth = Mathf.Clamp(shinLength + 0.08f, 0.43f, 0.72f);

        Vector3 forward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        // Seat cushion top is approximately 0.11m below the Hips bone at default avatar scale.
        // The chair's root is independent of the avatar and all background slots.
        Vector3 seatSurfacePosition = hips.position - Vector3.up * (0.11f * scale);
        Quaternion chairRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            parent = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create SeatingProps");
        }

        Transform existing = parent.transform.Find(ChairName);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Chair already exists",
                "Replace the existing StreamingChair? Any manual adjustments to that chair will be lost.",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Material shell = GetOrCreateMaterial("Chair_Shell", new Color(0.095f, 0.12f, 0.17f), 0.24f, 0.08f);
        Material cushion = GetOrCreateMaterial("Chair_Cushion", new Color(0.18f, 0.22f, 0.30f), 0.16f, 0f);
        Material trim = GetOrCreateMaterial("Chair_Accent", new Color(0.10f, 0.65f, 0.83f), 0.38f, 0.05f);
        Material metal = GetOrCreateMaterial("Chair_Metal", new Color(0.31f, 0.35f, 0.42f), 0.58f, 0.55f);
        Material wheel = GetOrCreateMaterial("Chair_Wheel", new Color(0.065f, 0.073f, 0.085f), 0.12f, 0f);

        GameObject chair = new GameObject(ChairName);
        chair.transform.SetParent(parent.transform, true);
        chair.transform.SetPositionAndRotation(seatSurfacePosition, chairRotation);
        chair.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(chair, "Generate Streaming Chair");

        GameObject visuals = new GameObject("ChairVisuals_ScaleHere");
        visuals.transform.SetParent(chair.transform, false);
        visuals.transform.localScale = Vector3.one * scale;
        Transform v = visuals.transform;

        // Seat: shell + upholstered cushion + side bolsters.
        Box(v, "Seat_Base", new Vector3(0, -0.090f, 0.012f), new Vector3(0.59f, 0.11f, 0.57f), shell);
        Box(v, "Seat_Cushion", new Vector3(0, -0.029f, 0.038f), new Vector3(0.53f, 0.060f, 0.51f), cushion);
        Box(v, "Seat_LeftBolster", new Vector3(-0.264f, -0.014f, 0.012f), new Vector3(0.054f, 0.084f, 0.49f), shell);
        Box(v, "Seat_RightBolster", new Vector3(0.264f, -0.014f, 0.012f), new Vector3(0.054f, 0.084f, 0.49f), shell);
        Box(v, "Seat_FrontAccent", new Vector3(0, -0.083f, 0.299f), new Vector3(0.49f, 0.014f, 0.012f), trim);

        // Backrest: reclines away from the avatar, leaving the torso unobstructed.
        Quaternion recline = Quaternion.Euler(-9f, 0, 0);
        Box(v, "Backrest_Frame", new Vector3(0, 0.358f, -0.305f), new Vector3(0.60f, 0.78f, 0.11f), shell, recline);
        Box(v, "Backrest_Cushion", new Vector3(0, 0.36f, -0.244f), new Vector3(0.52f, 0.65f, 0.058f), cushion, recline);
        Box(v, "Backrest_CenterAccent", new Vector3(0, 0.39f, -0.206f), new Vector3(0.018f, 0.44f, 0.010f), trim, recline);
        Box(v, "Backrest_LeftTrim", new Vector3(-0.27f, 0.36f, -0.236f), new Vector3(0.024f, 0.64f, 0.042f), trim, recline);
        Box(v, "Backrest_RightTrim", new Vector3(0.27f, 0.36f, -0.236f), new Vector3(0.024f, 0.64f, 0.042f), trim, recline);
        Box(v, "Headrest", new Vector3(0, 0.695f, -0.265f), new Vector3(0.40f, 0.13f, 0.09f), shell, recline);
        Box(v, "Headrest_Pad", new Vector3(0, 0.695f, -0.206f), new Vector3(0.32f, 0.085f, 0.036f), cushion, recline);
        Box(v, "Lumbar_Pad", new Vector3(0, 0.19f, -0.188f), new Vector3(0.35f, 0.13f, 0.068f), cushion, recline);

        // Armrests: independent of avatar bones; can be disabled individually in Hierarchy.
        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            float x = side * 0.36f;
            Box(v, prefix + "_ArmrestSupport", new Vector3(x, 0.080f, -0.075f), new Vector3(0.055f, 0.29f, 0.055f), metal);
            Box(v, prefix + "_ArmrestPad", new Vector3(x, 0.235f, 0.035f), new Vector3(0.105f, 0.065f, 0.39f), cushion);
            Box(v, prefix + "_ArmrestTrim", new Vector3(x, 0.198f, 0.035f), new Vector3(0.105f, 0.010f, 0.36f), trim);
        }

        // Central lift column and five-spoke base, sized to the avatar's shin length.
        float postTop = -0.13f;
        float postBottom = -baseDepth + 0.14f;
        float postHeight = Mathf.Max(0.08f, postTop - postBottom);
        Cylinder(v, "GasLift_Column", new Vector3(0, (postTop + postBottom) * 0.5f, 0),
            new Vector3(0.060f, postHeight * 0.5f, 0.060f), metal);
        Cylinder(v, "GasLift_Collar", new Vector3(0, -0.17f, 0), new Vector3(0.085f, 0.055f, 0.085f), shell);
        Cylinder(v, "Base_Hub", new Vector3(0, -baseDepth + 0.11f, 0),
            new Vector3(0.12f, 0.055f, 0.12f), shell);

        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f;
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 direction = rotation * Vector3.forward;
            Box(v, "Base_Spoke_" + (i + 1),
                direction * 0.17f + Vector3.up * (-baseDepth + 0.09f),
                new Vector3(0.083f, 0.048f, 0.34f), metal, rotation);
            Sphere(v, "Caster_" + (i + 1),
                direction * 0.335f + Vector3.up * (-baseDepth + 0.043f),
                new Vector3(0.095f, 0.075f, 0.095f), wheel);
        }

        Selection.activeGameObject = chair;
        EditorGUIUtility.PingObject(chair);
        EditorSceneManager.MarkSceneDirty(chair.scene);
        Debug.Log("[VirtualCharacterStudio] StreamingChair generated. Adjust the StreamingChair root position to align the seat with the avatar's hips. The chair is under SeatingProps, outside background slots.", chair);
    }

    private static Animator FindHumanoidAnimator(GameObject selected)
    {
        if (selected == null) return null;
        Animator[] children = selected.GetComponentsInChildren<Animator>(true);
        foreach (Animator a in children)
            if (a != null && a.isHuman) return a;
        Animator[] parents = selected.GetComponentsInParent<Animator>(true);
        foreach (Animator a in parents)
            if (a != null && a.isHuman) return a;
        return null;
    }

    private static GameObject Primitive(Transform parent, string name, PrimitiveType type,
        Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static void Box(Transform parent, string name, Vector3 pos, Vector3 size,
        Material material, Quaternion? rotation = null)
    {
        Primitive(parent, name, PrimitiveType.Cube, pos, size, material, rotation ?? Quaternion.identity);
    }

    private static void Cylinder(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
    {
        Primitive(parent, name, PrimitiveType.Cylinder, pos, size, material, Quaternion.identity);
    }

    private static void Sphere(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
    {
        Primitive(parent, name, PrimitiveType.Sphere, pos, size, material, Quaternion.identity);
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic)
    {
        EnsureFolder("Assets/VirtualCharacterStudio");
        EnsureFolder("Assets/VirtualCharacterStudio/Generated");
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("[VirtualCharacterStudio] No compatible shader found for chair materials.");
            return null;
        }

        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif

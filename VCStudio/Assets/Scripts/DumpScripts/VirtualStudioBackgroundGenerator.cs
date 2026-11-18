#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VirtualStudioBackgroundGenerator
{
    private const string RootName = "VCS_Backgrounds";
    private static Transform parent;
    private static Material wall, floor, trim, dark, accent, accent2, wood, fabric, metal, white, green, glow;
    private static bool shadows;

    [MenuItem("Tools/VirtualCharacterStudio/Generate All Backgrounds")]
    public static void GenerateAll()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null && !EditorUtility.DisplayDialog("Replace backgrounds?", "Existing VCS_Backgrounds will be replaced. Other scene objects are untouched.", "Replace", "Cancel")) return;
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Generate VCS Backgrounds");
        root.transform.position = Vector3.zero;
        MakeScene("Studio", root.transform, BuildStudio);
        MakeScene("Stage", root.transform, BuildStage);
        MakeScene("Room", root.transform, BuildRoom);
        root.transform.Find("Stage").gameObject.SetActive(false);
        root.transform.Find("Room").gameObject.SetActive(false);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("VCS: Studio, Stage, Room generated. Assign VCS_Backgrounds to Background Root, or its children to your controller's slots.");
    }

    [MenuItem("Tools/VirtualCharacterStudio/Generate Studio Only")]
    public static void StudioOnly() => GenerateSingle("Studio", BuildStudio);
    [MenuItem("Tools/VirtualCharacterStudio/Generate Stage Only")]
    public static void StageOnly() => GenerateSingle("Stage", BuildStage);
    [MenuItem("Tools/VirtualCharacterStudio/Generate Room Only")]
    public static void RoomOnly() => GenerateSingle("Room", BuildRoom);

    private static void GenerateSingle(string name, Action builder)
    {
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Create Background Root"); }
        var old = root.transform.Find(name);
        if (old != null && !EditorUtility.DisplayDialog("Replace background?", "Replace existing " + name + "?", "Replace", "Cancel")) return;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        MakeScene(name, root.transform, builder);
        Selection.activeGameObject = root;
    }

    private static void MakeScene(string name, Transform root, Action builder)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(root, false);
        parent = go.transform;
        builder();
    }

    private static Material Mat(string name, Color color, float metallic = 0, float smooth = .3f, bool emissive = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = "VCS_" + name };
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2.5f);
        }
        return m;
    }

    private static void Palette(Color wallColor, Color floorColor, Color primary, Color secondary, Color woodColor)
    {
        wall = Mat("Wall", wallColor);
        floor = Mat("Floor", floorColor, 0, .24f);
        trim = Mat("Trim", new Color(.16f, .18f, .23f), .4f, .6f);
        dark = Mat("Dark", new Color(.045f, .055f, .085f), .25f, .45f);
        accent = Mat("Accent", primary, .15f, .55f);
        accent2 = Mat("Accent2", secondary, .1f, .55f);
        wood = Mat("Wood", woodColor, 0, .27f);
        fabric = Mat("Fabric", new Color(.32f, .36f, .44f));
        metal = Mat("Metal", new Color(.32f, .36f, .41f), .75f, .68f);
        white = Mat("White", new Color(.88f, .88f, .83f));
        green = Mat("Plant", new Color(.13f, .33f, .22f));
        glow = Mat("Emissive", primary, 0, .5f, true);
    }

    private static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material material, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;
        if (!collider)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
        }
        return go;
    }

    private static GameObject Box(string name, Vector3 p, Vector3 s, Material m) => Primitive(PrimitiveType.Cube, name, p, s, m);
    private static GameObject Ball(string name, Vector3 p, Vector3 s, Material m) => Primitive(PrimitiveType.Sphere, name, p, s, m);
    private static GameObject Cylinder(string name, Vector3 p, Vector3 s, Material m) => Primitive(PrimitiveType.Cylinder, name, p, s, m);

    private static void Light(string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    private static void Shell(bool ceiling = true)
    {
        Box("Floor", new Vector3(0, -.14f, 1.5f), new Vector3(12, .28f, 9), floor);
        Box("Back wall", new Vector3(0, 2.65f, 5.95f), new Vector3(12, 5.3f, .25f), wall);
        Box("Left wall", new Vector3(-5.95f, 2.65f, 1.5f), new Vector3(.25f, 5.3f, 9), wall);
        Box("Right wall", new Vector3(5.95f, 2.65f, 1.5f), new Vector3(.25f, 5.3f, 9), wall);
        if (ceiling) Box("Ceiling", new Vector3(0, 5.32f, 1.5f), new Vector3(12, .22f, 9), wall);
        Box("Rear baseboard", new Vector3(0, .16f, 5.77f), new Vector3(11.7f, .3f, .08f), trim);
        Box("Rear crown", new Vector3(0, 5.05f, 5.77f), new Vector3(11.7f, .18f, .08f), trim);
    }

    private static void Panel(string name, float x, float y, float z, float width, float height, Material material)
    {
        Box(name, new Vector3(x, y, z), new Vector3(width, height, .12f), material);
    }

    private static void Shelf(float x, float y, float z, float width)
    {
        Box("Floating shelf", new Vector3(x, y, z), new Vector3(width, .12f, .55f), wood);
        for (int i = 0; i < 4; i++)
        {
            var m = i % 2 == 0 ? accent2 : white;
            Box("Book " + i, new Vector3(x - width * .32f + i * .18f, y + .23f, z), new Vector3(.13f, .34f + .08f * (i % 2), .25f), m);
        }
    }

    private static void Plant(float x, float z)
    {
        Cylinder("Plant pot", new Vector3(x, .34f, z), new Vector3(.38f, .34f, .38f), wood);
        Cylinder("Plant stem", new Vector3(x, .95f, z), new Vector3(.05f, .43f, .05f), green);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            Ball("Plant foliage", new Vector3(x + Mathf.Cos(a) * .25f, 1.3f + (i % 2) * .2f, z + Mathf.Sin(a) * .25f), new Vector3(.55f, .65f, .42f), green);
        }
    }

    private static void BuildStudio()
    {
        Palette(new Color(.075f, .105f, .16f), new Color(.10f, .13f, .17f), new Color(.03f, .78f, .88f), new Color(.28f, .48f, .73f), new Color(.22f, .17f, .15f));
        Shell();
        Box("Studio center backdrop", new Vector3(0, 2.75f, 5.68f), new Vector3(5.2f, 4.1f, .16f), dark);
        for (int i = 0; i < 9; i++)
        {
            float x = -4.8f + i * 1.2f;
            if (Mathf.Abs(x) < 2.8f) continue;
            Panel("Acoustic panel " + i, x, 2.8f, 5.65f, .85f, 2.5f, i % 2 == 0 ? trim : dark);
        }
        for (int i = 0; i < 4; i++)
        {
            float x = -2.1f + i * 1.4f;
            Box("Backdrop LED bar", new Vector3(x, 2.65f, 5.52f), new Vector3(.035f, 3.35f, .08f), glow);
        }
        Box("Studio top LED", new Vector3(0, 4.65f, 5.5f), new Vector3(5.4f, .07f, .09f), glow);
        Box("Left equipment desk", new Vector3(-3.65f, 1.05f, 3.85f), new Vector3(2.6f, .14f, 1.1f), wood);
        Box("Desk leg L", new Vector3(-4.75f, .5f, 3.85f), new Vector3(.12f, 1, .8f), metal);
        Box("Desk leg R", new Vector3(-2.55f, .5f, 3.85f), new Vector3(.12f, 1, .8f), metal);
        Box("Monitor body", new Vector3(-3.65f, 1.76f, 4.15f), new Vector3(1.45f, .85f, .1f), dark);
        Box("Monitor display", new Vector3(-3.65f, 1.76f, 4.08f), new Vector3(1.3f, .7f, .02f), accent2);
        Cylinder("Monitor stand", new Vector3(-3.65f, 1.22f, 4.15f), new Vector3(.07f, .17f, .07f), metal);
        Shelf(3.8f, 2.2f, 5.25f, 2.8f);
        Shelf(3.8f, 3.3f, 5.25f, 2.8f);
        Plant(4.7f, 4.15f);
        Box("Floor light strip", new Vector3(0, .035f, 5.3f), new Vector3(9.8f, .05f, .08f), glow);
        Light("Studio cyan fill", new Vector3(-3, 3.7f, 3.6f), new Color(.16f, .75f, 1), 2.0f, 7);
        Light("Studio warm fill", new Vector3(3.2f, 3.6f, 3.5f), new Color(1, .78f, .58f), 1.2f, 7);
    }

    private static void BuildStage()
    {
        Palette(new Color(.055f, .035f, .105f), new Color(.06f, .045f, .095f), new Color(.95f, .12f, .65f), new Color(.24f, .3f, .9f), new Color(.18f, .12f, .2f));
        Shell(false);
        Box("Main stage", new Vector3(0, .3f, 2.5f), new Vector3(8.5f, .6f, 5.5f), dark);
        Box("Stage fascia", new Vector3(0, .38f, -.28f), new Vector3(8.55f, .65f, .14f), accent2);
        Box("Stage front neon", new Vector3(0, .75f, -.37f), new Vector3(8.5f, .06f, .08f), glow);
        Box("Back LED screen", new Vector3(0, 3f, 5.68f), new Vector3(7.6f, 3.8f, .12f), dark);
        for (int i = 0; i < 6; i++)
        {
            float x = -3.35f + i * 1.34f;
            Box("LED screen vertical", new Vector3(x, 3f, 5.52f), new Vector3(.065f, 3.4f, .08f), i % 2 == 0 ? glow : accent2);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 4.65f;
            for (int j = 0; j < 5; j++)
            {
                Box("Truss segment", new Vector3(x, .65f + j * .95f, 3.7f), new Vector3(.17f, .8f, .17f), metal);
                Box("Truss crossbrace", new Vector3(x, .6f + j * .95f, 3.7f), new Vector3(.48f, .07f, .48f), metal);
            }
            for (int j = 0; j < 3; j++)
            {
                Cylinder("Stage lamp", new Vector3(x, 1.5f + j * 1.2f, 3.25f), new Vector3(.23f, .22f, .23f), dark);
                Ball("Lamp lens", new Vector3(x, 1.5f + j * 1.2f, 3.02f), new Vector3(.26f, .26f, .08f), j % 2 == 0 ? glow : accent2);
            }
        }
        for (int i = 0; i < 5; i++)
            Box("Stage runway inset", new Vector3(-3.2f + i * 1.6f, .62f, 1.1f), new Vector3(.05f, .025f, 2.4f), glow);
        Light("Stage pink light", new Vector3(-3.8f, 4.2f, 2.3f), new Color(1, .18f, .65f), 2.5f, 8);
        Light("Stage blue light", new Vector3(3.8f, 4.2f, 2.3f), new Color(.24f, .36f, 1), 2.5f, 8);
        Light("Stage backlight", new Vector3(0, 4.2f, 4.8f), new Color(.8f, .5f, 1), 1.6f, 7);
    }

    private static void BuildRoom()
    {
        Palette(new Color(.72f, .67f, .59f), new Color(.34f, .25f, .19f), new Color(1, .67f, .35f), new Color(.45f, .58f, .62f), new Color(.44f, .28f, .18f));
        Shell();
        for (int i = 0; i < 18; i++)
            Box("Wood floor plank", new Vector3(-5.55f + i * .65f, .008f, 1.5f), new Vector3(.61f, .025f, 8.8f), i % 3 == 0 ? wood : floor);
        Box("Wall picture frame", new Vector3(0, 3.25f, 5.68f), new Vector3(2.7f, 1.7f, .15f), wood);
        Box("Wall picture", new Vector3(0, 3.25f, 5.55f), new Vector3(2.42f, 1.42f, .04f), accent2);
        Ball("Picture sun", new Vector3(.45f, 3.55f, 5.5f), new Vector3(.55f, .55f, .06f), accent);
        Box("Sofa seat", new Vector3(3.65f, .65f, 4.1f), new Vector3(2.9f, .45f, 1.3f), fabric);
        Box("Sofa back", new Vector3(3.65f, 1.2f, 4.65f), new Vector3(2.9f, 1.1f, .3f), fabric);
        Box("Sofa left arm", new Vector3(2.2f, .94f, 4.1f), new Vector3(.25f, .8f, 1.35f), fabric);
        Box("Sofa right arm", new Vector3(5.1f, .94f, 4.1f), new Vector3(.25f, .8f, 1.35f), fabric);
        Box("Cushion", new Vector3(3.1f, 1.05f, 4.1f), new Vector3(.65f, .4f, .6f), accent);
        for (int i = 0; i < 3; i++) Shelf(-3.65f, 1.4f + i * 1.1f, 5.22f, 2.9f);
        Box("Coffee table top", new Vector3(3.65f, .62f, 2.45f), new Vector3(2.0f, .12f, .9f), wood);
        for (int s = -1; s <= 1; s += 2)
            Box("Coffee table leg", new Vector3(3.65f + s * .78f, .3f, 2.45f), new Vector3(.12f, .55f, .65f), wood);
        Cylinder("Lamp pole", new Vector3(-4.95f, 1.15f, 3.35f), new Vector3(.06f, 1.12f, .06f), metal);
        Cylinder("Lamp shade", new Vector3(-4.95f, 2.25f, 3.35f), new Vector3(.47f, .36f, .47f), white);
        Plant(-4.75f, 4.3f);
        Light("Room warm lamp", new Vector3(-4.95f, 2.1f, 3.3f), new Color(1, .7f, .4f), 2.1f, 6);
        Light("Room soft fill", new Vector3(2.8f, 3.8f, 2.7f), new Color(1, .91f, .8f), 1.35f, 8);
    }
}
#endif

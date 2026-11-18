using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StudioGestureProceduralVisual : MonoBehaviour
{
    public enum VisualType { Shield, Headset }
    private VisualType visualType;
    private Material lineMaterial;
    private Material surfaceMaterial;
    private Texture2D patternTexture;
    private Mesh patternMesh;
    private Transform[] notes;
    private Vector3[] noteStarts;
    private float timeOrigin;
    private float visualScale = 1f;
    private Renderer shieldFace;

    public static GameObject CreateShield(float scale)
    {
        GameObject root = new GameObject("Gesture_Shield_Procedural");
        StudioGestureProceduralVisual effect = root.AddComponent<StudioGestureProceduralVisual>();
        effect.visualType = VisualType.Shield;
        effect.visualScale = Mathf.Max(.25f, scale);
        effect.BuildShield();
        return root;
    }

    public static GameObject CreateHeadset(float scale)
    {
        GameObject root = new GameObject("Gesture_Headset_Procedural");
        StudioGestureProceduralVisual effect = root.AddComponent<StudioGestureProceduralVisual>();
        effect.visualType = VisualType.Headset;
        effect.visualScale = Mathf.Max(.25f, scale);
        effect.BuildHeadset();
        return root;
    }

    private void Awake() { timeOrigin = Time.realtimeSinceStartup; }

    private Material NewMaterial(Color color, bool transparent)
    {
        Shader shader = transparent ? Shader.Find("Sprites/Default") : null;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        Material result = new Material(shader);
        result.color = color;
        return result;
    }

    private LineRenderer MakeLine(string name, Transform parent, Color color, float width, int count, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = count;
        line.loop = loop;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.startColor = line.endColor = color;
        if (lineMaterial != null) line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void Circle(string name, Transform parent, float radius, int count, float width, Color color, int sides = 64)
    {
        LineRenderer line = MakeLine(name, parent, color, width, sides, true);
        for (int i = 0; i < sides; i++)
        {
            float t = (i * Mathf.PI * 2f) / sides;
            line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    private void BuildShield()
    {
        Color cyan = new Color(.20f, .90f, 1f, .92f);
        lineMaterial = NewMaterial(cyan, true);
        surfaceMaterial = NewMaterial(new Color(.12f, .72f, 1f, .60f), true);
        patternTexture = BuildShieldTexture();
        if (surfaceMaterial != null)
        {
            if (surfaceMaterial.HasProperty("_MainTex")) surfaceMaterial.SetTexture("_MainTex", patternTexture);
            if (surfaceMaterial.HasProperty("_BaseMap")) surfaceMaterial.SetTexture("_BaseMap", patternTexture);
            GameObject quad = new GameObject("Animated_Shield_Pattern");
            quad.transform.SetParent(transform, false);
            MeshFilter mf = quad.AddComponent<MeshFilter>();
            patternMesh = new Mesh { name = "Shield_DoubleSided_Quad" };
            patternMesh.vertices = new[] { new Vector3(-.54f,-.54f,0), new Vector3(.54f,-.54f,0), new Vector3(.54f,.54f,0), new Vector3(-.54f,.54f,0) };
            patternMesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            patternMesh.triangles = new[] { 0,1,2,0,2,3, 2,1,0,3,2,0 };
            patternMesh.RecalculateNormals();
            mf.sharedMesh = patternMesh;
            MeshRenderer mr = quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = surfaceMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            shieldFace = mr;
        }
        Circle("Outer_Pulse", transform, .59f, 64, .012f, cyan);
        Circle("Hexagonal_Boundary", transform, .53f, 6, .020f, cyan, 6);
        Circle("Inner_Ring", transform, .37f, 64, .012f, new Color(.38f, 1f, 1f, .9f));
        Circle("Core_Ring", transform, .12f, 32, .010f, cyan);
        for (int i = 0; i < 6; i++)
        {
            float t = 2f * Mathf.PI * i / 6f;
            LineRenderer ray = MakeLine("Energy_Ray_" + i, transform, cyan, .008f, 2, false);
            Vector3 v = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
            ray.SetPosition(0, v * .38f);
            ray.SetPosition(1, v * .52f);
        }
    }

    private static Texture2D BuildShieldTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Runtime_Shield_Hex_Pattern";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(u*u + v*v);
            float radial = Mathf.Clamp01((1f - r) * 2.1f);
            float a = Mathf.Abs(Mathf.Sin(u * 21.5f + v * 12.4f));
            float b = Mathf.Abs(Mathf.Sin(u * 21.5f - v * 12.4f));
            float c = Mathf.Abs(Mathf.Sin(v * 24.8f));
            float lattice = Mathf.Pow(1f - Mathf.Min(a, Mathf.Min(b,c)), 12f);
            float rim = Mathf.Exp(-Mathf.Pow((r - .78f) * 25f, 2f));
            float ring = Mathf.Exp(-Mathf.Pow((r - .43f) * 29f, 2f));
            float alpha = Mathf.Clamp01((.13f + .23f*lattice + .32f*rim + .15f*ring) * radial);
            pixels[y*size+x] = new Color(.25f, .89f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private GameObject Cube(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null && surfaceMaterial != null) r.sharedMaterial = surfaceMaterial;
        return go;
    }

    private void BuildHeadset()
    {
        Color deep = new Color(.16f,.18f,.25f,1f);
        lineMaterial = NewMaterial(new Color(.32f,.86f,1f,1f), true);
        surfaceMaterial = NewMaterial(deep, false);
        Cube("Left_Ear_Cup", new Vector3(-.142f, .095f, .008f), new Vector3(.050f,.125f,.095f), transform);
        Cube("Right_Ear_Cup", new Vector3(.142f, .095f, .008f), new Vector3(.050f,.125f,.095f), transform);
        LineRenderer band = MakeLine("Headband", transform, Color.cyan, .024f, 29, false);
        for (int i = 0; i < 29; i++)
        {
            float t = Mathf.PI - Mathf.PI*i / 28f;
            band.SetPosition(i, new Vector3(Mathf.Cos(t)*.15f, .11f + Mathf.Sin(t)*.165f, .002f));
        }
        notes = new Transform[6];
        noteStarts = new Vector3[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            GameObject note = new GameObject("Musical_Note_" + i);
            note.transform.SetParent(transform, false);
            float t = 2f*Mathf.PI*i / notes.Length;
            noteStarts[i] = new Vector3(Mathf.Cos(t)*.27f, .26f + (i % 3)*.08f, Mathf.Sin(t)*.17f);
            note.transform.localPosition = noteStarts[i];
            notes[i] = note.transform;
            LineRenderer stem = MakeLine("Note_Stem", note.transform, Color.cyan, .009f, 3, false);
            stem.SetPosition(0, new Vector3(0,0,0));
            stem.SetPosition(1, new Vector3(.02f,.085f,0));
            stem.SetPosition(2, new Vector3(.065f,.062f,0));
            Circle("Note_Head", note.transform, .025f, 24, .011f, Color.cyan, 20);
        }
    }

    private void Update()
    {
        float t = Time.realtimeSinceStartup - timeOrigin;
        if (visualType == VisualType.Shield)
        {
            transform.localScale = Vector3.one * visualScale * (1f + .025f*Mathf.Sin(t*7f));
            if (shieldFace != null) shieldFace.transform.localRotation = Quaternion.Euler(0f,0f, t*9f);
        }
        else
        {
            transform.localScale = Vector3.one * visualScale;
            if (notes == null) return;
            for (int i = 0; i < notes.Length; i++)
            {
                float phase = i*1.18f;
                notes[i].localPosition = noteStarts[i] + new Vector3(.035f*Mathf.Cos(t*1.6f+phase),
                    .038f*Mathf.Sin(t*2.2f+phase), .014f*Mathf.Sin(t*1.1f+phase));
                notes[i].localRotation = Quaternion.Euler(0f, -t*14f, 0f);
            }
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
        if (surfaceMaterial != null) Destroy(surfaceMaterial);
        if (patternTexture != null) Destroy(patternTexture);
        if (patternMesh != null) Destroy(patternMesh);
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;


public static class VCSGesturePrefabGenerator
{
    private const string Root = "Assets/VirtualCharacterStudio/GeneratedGesturePrefabs";
    private static readonly Color Cyan = new Color(.22f, .91f, 1f, 1f);
    private static Material rim, dark, pad, bright, hologram;

    [MenuItem("Tools/VirtualCharacterStudio/Step 6/Generate Shield + Headset Prefabs")]
    public static void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Gesture/Prefabs] Exit Play mode before generating assets.");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/VCS_Shield.prefab") != null &&
            !EditorUtility.DisplayDialog("Regenerate VCS Gesture Prefabs?",
                "Generated prefab assets already exist. Regenerating will replace those assets. Back up any edits you made in the generated folder.",
                "Regenerate", "Cancel")) return;

        EnsureFolder("Assets", "VirtualCharacterStudio");
        EnsureFolder("Assets/VirtualCharacterStudio", "GeneratedGesturePrefabs");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Meshes");
        EnsureFolder(Root, "Textures");

        rim = SaveMaterial("Holo_Cyan", Cyan, true);
        bright = SaveMaterial("Accent_Light", new Color(.66f, 1f, 1f, 1f), false);
        dark = SaveMaterial("Headset_Case", new Color(.09f, .13f, .20f, 1f), false);
        pad = SaveMaterial("Headset_Pads", new Color(.028f, .04f, .08f, 1f), false);
        hologram = SaveShieldMaterial();

        GameObject shield = BuildShield();
        SavePrefab(shield, "VCS_Shield");
        GameObject headset = BuildHeadset();
        SavePrefab(headset, "VCS_Headset");
        GameObject notes = BuildNotes();
        SavePrefab(notes, "VCS_MusicNotes");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssignSceneActions();
        Debug.Log("[Gesture/Prefabs] Generated 3 persistent prefabs at " + Root +
            ". Check Prefab Mode for size and materials, then press Play to test.");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/VCS_Shield.prefab");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Tools/VirtualCharacterStudio/Step 6/Connect Generated Prefabs to GestureSystem")]
    public static void AssignSceneActions()
    {
        StudioGestureActions[] all = UnityEngine.Object.FindObjectsOfType<StudioGestureActions>();
        if (all.Length == 0)
        {
            Debug.LogWarning("[Gesture/Prefabs] No active StudioGestureActions found. Drag the generated 3 prefabs into its Shield / Headset / Music Note slots manually.");
            return;
        }
        GameObject sh = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/VCS_Shield.prefab");
        GameObject hs = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/VCS_Headset.prefab");
        GameObject nt = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/VCS_MusicNotes.prefab");
        if (sh == null || hs == null || nt == null)
        {
            Debug.LogError("[Gesture/Prefabs] Generate prefabs first, then connect.");
            return;
        }
        foreach (StudioGestureActions action in all)
        {
            Undo.RecordObject(action, "Wire Gesture Prefabs and Events");
            action.shieldPrefab = sh;
            action.headsetPrefab = hs;
            action.musicNotePrefab = nt;
            action.executeActions = true;
            action.useProceduralFallback = true;
            if (action.recognizer == null) action.recognizer = UnityEngine.Object.FindObjectOfType<StudioGestureRecognizer>();
            if (action.studioEnvironment == null) action.studioEnvironment = UnityEngine.Object.FindObjectOfType<StudioEnvironmentController>();
            if (action.studioCamera == null) action.studioCamera = Camera.main;
            EditorUtility.SetDirty(action);
            if (action.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(action.gameObject.scene);
            Debug.Log("[Gesture/Prefabs] Connected " + action.gameObject.name + ": shield/headset/notes, Execute Actions=ON, studioEnvironment=" + (action.studioEnvironment != null) +
                ". Verify the avatarAnimator reference and background/lighting presets in Inspector.", action);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    private static Shader FindShader(bool transparent)
    {
        // Shader.Find can locate URP shaders even in projects currently using Built-in.
        // Choose the active pipeline first to avoid creating pink materials in that case.
        RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;
        bool isURP = activePipeline != null && activePipeline.GetType().Name.Contains("Universal");
        string[] names = transparent
            ? new[] { "Sprites/Default", "Unlit/Transparent", "Universal Render Pipeline/Unlit", "Standard" }
            : isURP
                ? new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Standard", "Unlit/Color" }
                : new[] { "Standard", "Unlit/Color", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" };
        foreach (string name in names)
        {
            Shader shader = Shader.Find(name);
            if (shader != null && shader.isSupported) return shader;
        }
        throw new InvalidOperationException("No supported Unity shader found for generated gesture prefab material.");
    }

    private static Material SaveMaterial(string name, Color color, bool transparent)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material previous = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (previous != null) AssetDatabase.DeleteAsset(path);
        Material m = new Material(FindShader(transparent));
        m.name = name;
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (transparent && m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Material SaveShieldMaterial()
    {
        const string path = Root + "/Textures/VCS_ShieldGrid.png";
        Texture2D t = new Texture2D(384, 384, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[384*384];
        for (int y=0;y<384;y++) for (int x=0;x<384;x++)
        {
            float u = ((x+.5f)/384f)*2f-1f, v=((y+.5f)/384f)*2f-1f;
            float r = Mathf.Sqrt(u*u+v*v);
            // Tessellated hexagonal-style interference grid, transparent at edges.
            float h1=Mathf.Abs(Mathf.Sin(u*24f+v*13.9f));
            float h2=Mathf.Abs(Mathf.Sin(u*24f-v*13.9f));
            float h3=Mathf.Abs(Mathf.Sin(v*27.8f));
            float cells=Mathf.Pow(1f-Mathf.Min(h1,Mathf.Min(h2,h3)),14f);
            float ring=Mathf.Exp(-Mathf.Pow((r-.75f)*33f,2f));
            float mask=Mathf.Clamp01((.10f+.31f*cells+.32f*ring)*Mathf.Clamp01((.98f-r)*6f));
            pixels[y*384+x]=new Color(.24f,.90f,1f,mask);
        }
        t.SetPixels(pixels);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Material m = SaveMaterial("ShieldGrid_Transparent", new Color(.6f,.98f,1f,.72f), true);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", saved);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", saved);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path=Root+"/Meshes/"+name+".asset";
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        mesh.name=name;
        AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }

    private static GameObject BuildShield()
    {
        GameObject root=new GameObject("VCS_Shield");
        root.transform.localScale=Vector3.one;
        GameObject plate=new GameObject("Hologram_DoubleSided");
        plate.transform.SetParent(root.transform,false);
        MeshFilter mf=plate.AddComponent<MeshFilter>();
        mf.sharedMesh=SaveMesh("ShieldDisc",MakeDiscMesh(6,.54f));
        MeshRenderer mr=plate.AddComponent<MeshRenderer>();
        mr.sharedMaterial=hologram;
        DisableShadows(mr);
        Line(root.transform,"Outer_Rim",.61f,72,.013f,rim);
        Line(root.transform,"Hex_Frame",.54f,6,.020f,rim);
        Transform rotating= new GameObject("Rotating_InnerRing").transform;
        rotating.SetParent(root.transform,false);
        Line(rotating,"Inner_Ring",.36f,64,.011f,rim);
        Line(rotating,"Core",.12f,32,.018f,rim);
        for (int i=0;i<6;i++)
        {
            float ang=2f*Mathf.PI*i/6f;
            Vector3 d=new Vector3(Mathf.Cos(ang),Mathf.Sin(ang),0f);
            Segment(root.transform,"Radiant_"+i,d*.38f,d*.52f,.009f,bright);
        }
        StudioGesturePrefabAnimator anim=root.AddComponent<StudioGesturePrefabAnimator>();
        anim.effectKind=StudioGesturePrefabAnimator.EffectKind.Shield;
        anim.shieldInnerRing=rotating;
        return root;
    }

    private static GameObject BuildHeadset()
    {
        GameObject root=new GameObject("VCS_Headset");
        Mesh headband=SaveMesh("HeadsetHeadbandTube",MakeHeadbandMesh());
        GameObject band=new GameObject("Headband_Arc");
        band.transform.SetParent(root.transform,false);
        band.AddComponent<MeshFilter>().sharedMesh=headband;
        MeshRenderer bandRenderer=band.AddComponent<MeshRenderer>();
        bandRenderer.sharedMaterial=dark;
        DisableShadows(bandRenderer);
        for (int s=-1;s<=1;s+=2)
        {
            string side=s<0?"Left":"Right";
            Sphere(root.transform,side+"_Cup",new Vector3(s*.155f,.055f,0f),new Vector3(.059f,.147f,.120f),dark);
            Sphere(root.transform,side+"_Pad",new Vector3(s*.130f,.055f,0f),new Vector3(.028f,.123f,.103f),pad);
            Sphere(root.transform,side+"_Glow",new Vector3(s*.193f,.055f,0f),new Vector3(.009f,.091f,.086f),rim);
        }
        Segment(root.transform,"Microphone_Boom",new Vector3(.16f,-.010f,.015f),new Vector3(.115f,-.12f,.128f),.011f,dark);
        Sphere(root.transform,"Microphone_End",new Vector3(.115f,-.12f,.128f),new Vector3(.027f,.027f,.027f),bright);
        return root;
    }

    private static GameObject BuildNotes()
    {
        GameObject root=new GameObject("VCS_MusicNotes");
        List<Transform> noteTransforms=new List<Transform>();
        for(int i=0;i<7;i++)
        {
            float angle=i*2f*Mathf.PI/7f;
            GameObject note=new GameObject("Floating_Note_"+i);
            note.transform.SetParent(root.transform,false);
            note.transform.localPosition=new Vector3(Mathf.Cos(angle)*.29f,.05f+(i%3)*.082f,Mathf.Sin(angle)*.18f);
            note.transform.localScale=Vector3.one*(.72f+(i%3)*.14f);
            // Music note as an ellipse and a slightly curved beam/stem (original geometry).
            Line(note.transform,"Note_Head",.024f,18,.008f,rim);
            Segment(note.transform,"Note_Stem",new Vector3(.022f,0f,0f),new Vector3(.022f,.116f,0f),.012f,rim);
            Segment(note.transform,"Note_Flag",new Vector3(.022f,.115f,0f),new Vector3(.076f,.078f,0f),.012f,rim);
            noteTransforms.Add(note.transform);
        }
        StudioGesturePrefabAnimator anim=root.AddComponent<StudioGesturePrefabAnimator>();
        anim.effectKind=StudioGesturePrefabAnimator.EffectKind.FloatingNotes;
        anim.orbitNotes=noteTransforms.ToArray();
        return root;
    }

    private static Mesh MakeDiscMesh(int sides,float radius)
    {
        Mesh mesh=new Mesh();
        List<Vector3> v=new List<Vector3> { Vector3.zero };
        List<Vector2> u=new List<Vector2> { new Vector2(.5f,.5f) };
        List<int> triangles=new List<int>();
        for(int i=0;i<sides;i++)
        {
            float a=2f*Mathf.PI*i/sides;
            float x=Mathf.Cos(a)*radius,y=Mathf.Sin(a)*radius;
            v.Add(new Vector3(x,y,0f));
            u.Add(new Vector2(.5f+x/(radius*2.2f),.5f+y/(radius*2.2f)));
        }
        for(int i=1;i<=sides;i++)
        {
            int n=i==sides?1:i+1;
            triangles.Add(0);triangles.Add(i);triangles.Add(n);
            triangles.Add(0);triangles.Add(n);triangles.Add(i);
        }
        mesh.SetVertices(v);
        mesh.SetUVs(0,u);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh MakeHeadbandMesh()
    {
        const int segments=36, rings=8;
        List<Vector3> vertices=new List<Vector3>();
        List<int> triangles=new List<int>();
        for(int j=0;j<=segments;j++)
        {
            float t=Mathf.PI*(1f-(float)j/segments);
            Vector3 center=new Vector3(.155f*Mathf.Cos(t),.055f+.175f*Mathf.Sin(t),0f);
            Vector3 tangent=new Vector3(-Mathf.Sin(t),Mathf.Cos(t),0f);
            Vector3 perpendicular=new Vector3(tangent.y,-tangent.x,0f);
            for(int k=0;k<rings;k++)
            {
                float a=2f*Mathf.PI*k/rings;
                vertices.Add(center+.018f*(Mathf.Cos(a)*perpendicular+Mathf.Sin(a)*Vector3.forward));
            }
        }
        for(int j=0;j<segments;j++) for(int k=0;k<rings;k++)
        {
            int a=j*rings+k,b=j*rings+(k+1)%rings,c=(j+1)*rings+k,d=(j+1)*rings+(k+1)%rings;
            triangles.Add(a);triangles.Add(c);triangles.Add(b);
            triangles.Add(b);triangles.Add(c);triangles.Add(d);
        }
        Mesh m=new Mesh();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    private static void Sphere(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name=name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition=pos;
        go.transform.localScale=scale;
        Collider col=go.GetComponent<Collider>();if(col!=null)UnityEngine.Object.DestroyImmediate(col);
        MeshRenderer mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=mat;DisableShadows(mr);
    }

    private static void DisableShadows(Renderer mr)
    {
        mr.shadowCastingMode=ShadowCastingMode.Off;
        mr.receiveShadows=false;
    }

    private static LineRenderer Line(Transform parent,string name,float radius,int sides,float width,Material mat)
    {
        GameObject child=new GameObject(name);child.transform.SetParent(parent,false);
        LineRenderer line=child.AddComponent<LineRenderer>();
        line.useWorldSpace=false;line.positionCount=sides;line.loop=true;
        line.widthMultiplier=width;line.numCapVertices=4;line.numCornerVertices=3;
        line.sharedMaterial=mat;line.startColor=Cyan;line.endColor=Cyan;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        for(int i=0;i<sides;i++)
        {
            float a=2f*Mathf.PI*i/sides;
            line.SetPosition(i,new Vector3(radius*Mathf.Cos(a),radius*Mathf.Sin(a),0));
        }
        return line;
    }

    private static void Segment(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat)
    {
        GameObject child=new GameObject(name);child.transform.SetParent(parent,false);
        LineRenderer line=child.AddComponent<LineRenderer>();
        line.useWorldSpace=false;line.positionCount=2;line.loop=false;line.widthMultiplier=width;
        line.sharedMaterial=mat;line.startColor=Cyan;line.endColor=Cyan;
        line.numCapVertices=3;
        line.SetPosition(0,a);line.SetPosition(1,b);
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
    }

    private static void SavePrefab(GameObject instance,string name)
    {
        string path=Root+"/"+name+".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance,path);
        UnityEngine.Object.DestroyImmediate(instance);
    }
}
#endif

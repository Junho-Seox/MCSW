#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VCSAppFlowGenerator
{
    private const string Root = "Assets/VirtualCharacterStudio/App";
    private const string MenuPath = Root + "/Scenes/VCS_MainMenu.unity";
    private const string ConfigPath = Root + "/Resources/VCSStudioAppConfig.asset";
    private static readonly Color Background = C(0x0B1420);
    private static readonly Color Panel = C(0x172537);
    private static readonly Color Blue = C(0x337ECB);
    private static readonly Color TextWhite = C(0xEEF6FF);
    private static readonly Color Muted = C(0xA9BACD);
    private static Font font;

    [MenuItem("Tools/VirtualCharacterStudio/App Flow/1 - Generate Complete Menu Flow")]
    public static void Generate()
    {
        Scene studio = SceneManager.GetActiveScene();
        if (!studio.IsValid() || string.IsNullOrEmpty(studio.path) || studio.path == MenuPath)
        {
            EditorUtility.DisplayDialog("VirtualCharacterStudio", "기존의 정상 동작 중인 Studio 씬을 먼저 열고, Assets 아래에 저장한 후 실행하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string studioPath = studio.path;
        EnsureFolder(Root);
        EnsureFolder(Root + "/Scenes");
        EnsureFolder(Root + "/Resources");
        VCSStudioAppConfig config = AssetDatabase.LoadAssetAtPath<VCSStudioAppConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<VCSStudioAppConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        if (config.avatars == null || config.avatars.Length < 2)
            config.avatars = new VCSAvatarOption[]
            {
                new VCSAvatarOption { displayName = "여성 아바타" },
                new VCSAvatarOption { displayName = "남성 아바타" }
            };
        config.mainMenuScenePath = MenuPath;

        if (string.IsNullOrEmpty(config.avatars[0].studioScenePath))
            config.avatars[0].studioScenePath = studioPath;
        if (config.avatars[0].previewPrefab == null)
        {
            GameObject bot = FindBot1ModelPrefab();
            if (bot != null) config.avatars[0].previewPrefab = bot;
        }
        EditorUtility.SetDirty(config);

        EnsureStudioOverlay(studio);
        EditorSceneManager.SaveScene(studio);
        CreateMenuScene(config);
        RegisterBuildScenes(MenuPath, studioPath, config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("VirtualCharacterStudio",
            "메뉴 씬, 3D 프리뷰, 카메라 준비, 1회 제스처 가이드, 스튜디오 복귀 UI가 생성되었습니다.\n\n다음으로 VCSStudioAppConfig에서 두 아바타의 Preview Prefab/Studio Scene을 확인하세요.", "확인");
    }

    [MenuItem("Tools/VirtualCharacterStudio/App Flow/2 - Open Generated Main Menu")]
    public static void OpenMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null)
        {
            EditorUtility.DisplayDialog("VCS", "먼저 Generate Complete Menu Flow를 실행하세요.", "확인");
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
    }

    private static GameObject FindBot1ModelPrefab()
    {
        string[] ids = AssetDatabase.FindAssets("bot1 t:Prefab");
        foreach (string guid in ids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("VRoid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("bot1", StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        string[] items = folder.Split('/');
        string current = items[0];
        for (int i = 1; i < items.Length; ++i)
        {
            string next = current + "/" + items[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, items[i]);
            current = next;
        }
    }

    private static void RegisterBuildScenes(string menu, string studio, VCSStudioAppConfig config)
    {
        var list = new List<EditorBuildSettingsScene>();
        var used = new HashSet<string>();
        AddScene(list, used, menu);
        AddScene(list, used, studio);
        foreach (VCSAvatarOption a in config.avatars)
            if (a != null && !string.IsNullOrEmpty(a.studioScenePath))
                AddScene(list, used, a.studioScenePath);
        foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes)
            if (used.Add(item.path)) list.Add(item);
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void AddScene(List<EditorBuildSettingsScene> list, HashSet<string> used, string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || !used.Add(path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
    }

    private static void EnsureStudioOverlay(Scene studio)
    {
        foreach (GameObject obj in studio.GetRootGameObjects())
            if (obj.name == "VCS_SessionOverlay") return;

        var root = new GameObject("VCS_SessionOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        SetupScaler(root.GetComponent<CanvasScaler>());

        var ui = root.AddComponent<VCSStudioSessionUI>();
        ui.statusLabel = Label("TrackingStatus", root.transform, "○ WAITING FOR TRACKING", 20, Muted,
            TextAnchor.MiddleRight, new Vector2(1,1), new Vector2(330,42), new Vector2(-238,-34));
        Button menu = NewButton("MenuButton", root.transform, "메뉴 / ESC", new Vector2(1,1), new Vector2(165,50), new Vector2(-103,-34), 21);
        menu.onClick.AddListener(ui.TogglePause);

        Image modalBg = NewPanel("PausePanel", root.transform, new Vector2(.5f,.5f), new Vector2(530,370), Vector2.zero, Panel);
        Label("PauseTitle", modalBg.transform, "STUDIO PAUSED", 36, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,.5f), new Vector2(470,55), new Vector2(0,115));
        Label("PauseDesc", modalBg.transform, "추적은 연결된 상태입니다. 메뉴로 돌아가면 Unity가 시작한 Python만 종료합니다.", 18, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(475,52), new Vector2(0,57));
        NewButton("Resume", modalBg.transform, "계속하기", new Vector2(.5f,.5f), new Vector2(320,56), new Vector2(0,-5), 22)
            .onClick.AddListener(ui.TogglePause);
        NewButton("ReturnMenu", modalBg.transform, "메인으로", new Vector2(.5f,.5f), new Vector2(320,56), new Vector2(0,-75), 22)
            .onClick.AddListener(ui.ClickReturnToMenu);
        ui.pausePanel = modalBg.gameObject;
        ui.pausePanel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(studio);
    }

    private static void CreateMenuScene(VCSStudioAppConfig config)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var service = new GameObject("VCS_AppService");
        service.AddComponent<VCSStudioAppFlow>().config = config;

        var cameraGO = new GameObject("VCS_AvatarPreviewCamera", typeof(Camera));
        Camera cam = cameraGO.GetComponent<Camera>();
        cam.transform.position = new Vector3(1000f, 2f, 3f);
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.depth = -50;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = C(0x121F2F);
        var lightGO = new GameObject("VCS_PreviewKeyLight", typeof(Light));
        Light light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        lightGO.transform.rotation = Quaternion.Euler(42f, -25f, 0f);
        var pivotGO = new GameObject("VCS_PreviewPivot");
        pivotGO.transform.position = new Vector3(1000f, 1.1f, 0f);

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.SetActive(true);
        var canvasGO = new GameObject("VCS_MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        SetupScaler(canvasGO.GetComponent<CanvasScaler>());
        Image bg = Fullscreen("MenuBackground", canvasGO.transform, Background);
        var ui = canvasGO.AddComponent<VCSStudioMenuUI>();
        ui.preflight = canvasGO.AddComponent<VCSStudioPreflight>();

        // MAIN: exactly two actions, by design.
        Image main = Fullscreen("MainPanel", bg.transform, Background);
        ui.mainPanel = main.gameObject;
        Decorative(main.transform);
        Label("MainTitle", main.transform, "VIRTUAL CHARACTER STUDIO", 62, TextWhite,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(1320,110), new Vector2(0,188));
        Label("MainSubtitle", main.transform, "REAL-TIME MOTION  /  INTERACTIVE GESTURES", 24, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(1240,60), new Vector2(0,111));
        NewButton("StartButton", main.transform, "시작하기", new Vector2(.5f,.5f), new Vector2(360,78), new Vector2(0,-31), 28)
            .onClick.AddListener(ui.ClickStart);
        NewButton("ExitButton", main.transform, "종료하기", new Vector2(.5f,.5f), new Vector2(360,78), new Vector2(0,-126), 28)
            .onClick.AddListener(ui.ClickQuit);
        Label("Version", main.transform, "UNITY  •  MEDIAPIPE  •  VRM", 18, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,0), new Vector2(620,38), new Vector2(0,51));

        // AVATAR SELECT
        Image av = Fullscreen("AvatarPanel", bg.transform, Background);
        ui.avatarPanel = av.gameObject;
        Label("AvatarTitle", av.transform, "아바타 선택", 48, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,1f), new Vector2(750,70), new Vector2(0,-105));
        Label("AvatarSub", av.transform, "원하는 캐릭터를 선택하고 드래그해 살펴보세요", 22, Muted, TextAnchor.MiddleCenter,
            new Vector2(.5f,1f), new Vector2(830,50), new Vector2(0,-169));
        Image previewFrame = NewPanel("PreviewFrame", av.transform, new Vector2(.5f,.5f), new Vector2(650,650), new Vector2(170,0), Panel);
        RectTransform rawRect = NewRect("3DPreview", previewFrame.transform, new Vector2(.5f,.5f), new Vector2(624,620), Vector2.zero);
        RawImage raw = rawRect.gameObject.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = true;
        VCSAvatarPreviewOrbit orbit = rawRect.gameObject.AddComponent<VCSAvatarPreviewOrbit>();
        orbit.previewImage = raw;
        orbit.previewCamera = cam;
        orbit.previewPivot = pivotGO.transform;
        ui.preview = orbit;
        ui.avatarButtons = new Button[2];
        ui.avatarButtons[0] = NewButton("FemaleSelect", av.transform, "여성 아바타", new Vector2(.5f,.5f), new Vector2(305,76), new Vector2(-590,145), 26);
        ui.avatarButtons[1] = NewButton("MaleSelect", av.transform, "남성 아바타", new Vector2(.5f,.5f), new Vector2(305,76), new Vector2(-590,43), 26);
        ui.avatarButtons[0].onClick.AddListener(() => ui.ChooseAvatar(0));
        ui.avatarButtons[1].onClick.AddListener(() => ui.ChooseAvatar(1));
        ui.avatarNameText = Label("AvatarName", av.transform, "아바타", 32, TextWhite,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(440,52), new Vector2(-570,-82));
        ui.avatarHintText = Label("AvatarInfo", av.transform, "", 18, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(480,115), new Vector2(-565,-164));
        ui.continueAvatarButton = NewButton("SelectNext", av.transform, "다음", new Vector2(1,0), new Vector2(220,62), new Vector2(-160,75), 23);
        ui.continueAvatarButton.onClick.AddListener(ui.ClickAvatarNext);
        NewButton("AvatarBack", av.transform, "뒤로", new Vector2(0,0), new Vector2(220,62), new Vector2(160,75), 23)
            .onClick.AddListener(ui.ClickAvatarBack);

        // WEBCAM PREP: intentionally short; checks real pose packet, not just port open.
        Image prep = Fullscreen("PreparePanel", bg.transform, Background);
        ui.preparePanel = prep.gameObject;
        Label("ReadyTitle", prep.transform, "카메라 및 트래킹 준비", 48, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,.5f), new Vector2(980,72), new Vector2(0,200));
        Label("ReadyInfo", prep.transform, "정면을 바라보고 상반신과 양손이 보이게 앉아 주세요.\n추적 데이터가 수신되면 다음으로 진행할 수 있습니다.", 24, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(1010,140), new Vector2(0,80));
        Image stateBg = NewPanel("StatusPanel", prep.transform, new Vector2(.5f,.5f), new Vector2(850,140), new Vector2(0,-80), Panel);
        ui.prepareStatusText = Label("ReadyStatus", stateBg.transform, "Python 추적 서버 연결 중...", 23, TextWhite,
            TextAnchor.MiddleCenter, new Vector2(.5f,.5f), new Vector2(800,110), Vector2.zero);
        ui.enterGuideButton = NewButton("PrepareNext", prep.transform, "준비 완료 · 다음", new Vector2(.5f,0), new Vector2(335,70), new Vector2(200,143), 24);
        ui.enterGuideButton.onClick.AddListener(ui.ClickPrepareNext);
        NewButton("PrepareBack", prep.transform, "이전", new Vector2(.5f,0), new Vector2(230,70), new Vector2(-210,143), 24)
            .onClick.AddListener(ui.ClickPrepareBack);

        // GUIDE: only a static UI. It NEVER artificially moves the avatar.
        Image guide = Fullscreen("GuidePanel", bg.transform, Background);
        ui.guidePanel = guide.gameObject;
        Label("GuideTitle", guide.transform, "GESTURE QUICK GUIDE", 46, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,1), new Vector2(1000,78), new Vector2(0,-112));
        Label("GuideInfo", guide.transform, "웹캠 앞에서 아래 동작을 수행하면 스튜디오가 반응합니다.", 22, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,1), new Vector2(1010,45), new Vector2(0,-175));
        GuideCard(guide.transform, "← →", "양손 좌우 스와이프", "배경 전환", new Vector2(-315,95));
        GuideCard(guide.transform, "↓", "양손 아래 스와이프", "조명 전환", new Vector2(315,95));
        GuideCard(guide.transform, "▣", "양손 앞으로 · 1.1초", "실드 생성 · 5초 유지", new Vector2(-315,-155));
        GuideCard(guide.transform, "♫", "양손 귀에 · 1.1초", "헤드셋 · 10초 유지", new Vector2(315,-155));
        NewButton("EnterStudio", guide.transform, "버추얼 스튜디오 입장", new Vector2(.5f,0), new Vector2(420,74), new Vector2(0,95), 26)
            .onClick.AddListener(ui.ClickGuideEnter);
        Label("GuideFooter", guide.transform, "가이드는 프로그램 실행 중 최초 입장 시 한 번만 표시됩니다.", 18, Muted,
            TextAnchor.MiddleCenter, new Vector2(.5f,0), new Vector2(900,32), new Vector2(0,39));

        main.gameObject.SetActive(true);
        av.gameObject.SetActive(false);
        prep.gameObject.SetActive(false);
        guide.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    private static void GuideCard(Transform parent, string icon, string title, string desc, Vector2 pos)
    {
        Image card = NewPanel("Card_" + title, parent, new Vector2(.5f,.5f), new Vector2(580,210), pos, Panel);
        Label("Symbol", card.transform, icon, 54, C(0x72C5EF), TextAnchor.MiddleCenter,
            new Vector2(.5f,1), new Vector2(450,65), new Vector2(0,-52));
        Label("Title", card.transform, title, 28, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,.5f), new Vector2(535,50), new Vector2(0,-5));
        Label("Desc", card.transform, desc, 21, Muted, TextAnchor.MiddleCenter,
            new Vector2(.5f,0), new Vector2(525,54), new Vector2(0,40));
    }

    private static void SetupScaler(CanvasScaler s)
    {
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920,1080);
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight = 0.5f;
    }

    private static void Decorative(Transform parent)
    {
        NewPanel("Accent", parent, new Vector2(.5f,.5f), new Vector2(130,5), new Vector2(0,305), C(0x55B9E9));
        NewPanel("BottomLine", parent, new Vector2(.5f,0), new Vector2(780,2), new Vector2(0,94), C(0x274154));
    }

    private static Image Fullscreen(string name, Transform parent, Color color)
    {
        RectTransform r = NewRect(name, parent, new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        Image image = r.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image NewPanel(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
    {
        RectTransform r = NewRect(name,parent,anchor,size,pos);
        Image image = r.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(.5f,.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Button NewButton(string name, Transform parent, string title, Vector2 anchor, Vector2 size, Vector2 pos, int fontsize)
    {
        Image image = NewPanel(name, parent, anchor,size,pos,Blue);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock block = button.colors;
        block.normalColor = Color.white;
        block.highlightedColor = new Color(.8f,.94f,1f);
        block.pressedColor = new Color(.58f,.78f,.97f);
        block.disabledColor = new Color(.4f,.45f,.5f, .7f);
        button.colors = block;
        Label(name + "_Label", image.transform, title, fontsize, TextWhite, TextAnchor.MiddleCenter,
            new Vector2(.5f,.5f),size,Vector2.zero);
        return button;
    }

    private static Text Label(string name, Transform parent, string value, int fontsize, Color color, TextAnchor alignment,
        Vector2 anchor, Vector2 size, Vector2 pos)
    {
        RectTransform r = NewRect(name,parent,anchor,size,pos);
        Text text = r.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = UI_Font();
        text.fontSize = fontsize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Font UI_Font()
    {
        if (font != null) return font;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch (Exception) { }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch (Exception) { }
        }
        return font;
    }

    private static Color C(int color)
    {
        return new Color(((color >> 16) & 255) / 255f, ((color >> 8) & 255) / 255f,
            (color & 255) / 255f, 1f);
    }
}
#endif

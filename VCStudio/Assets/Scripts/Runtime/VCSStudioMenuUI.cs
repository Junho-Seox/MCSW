using UnityEngine;
using UnityEngine.UI;

public sealed class VCSStudioMenuUI : MonoBehaviour
{
    public GameObject mainPanel;
    public GameObject avatarPanel;
    public GameObject preparePanel;
    public GameObject guidePanel;

    public Text avatarNameText;
    public Text avatarHintText;
    public Text prepareStatusText;
    public Button continueAvatarButton;
    public Button enterGuideButton;
    public Button[] avatarButtons;
    public VCSAvatarPreviewOrbit preview;
    public VCSStudioPreflight preflight;

    private VCSStudioAppFlow app;
    private int chosenIndex;
    private bool pythonStartError;

    private enum Page { Main, Avatar, Prepare, Guide }
    private Page currentPage;

    private void Start()
    {
        VCSUIRuntimeBindings.EnsureInputModule(this);
        BindMenuButtons();
        app = VCSStudioAppFlow.Instance;
        if (preflight == null) preflight = GetComponent<VCSStudioPreflight>();
        ShowPage(Page.Main);
    }

    private void BindMenuButtons()
    {
        VCSUIRuntimeBindings.Bind(this, "StartButton", ClickStart);
        VCSUIRuntimeBindings.Bind(this, "ExitButton", ClickQuit);
        VCSUIRuntimeBindings.BindChoice(this, "FemaleSelect", ChooseAvatar, 0);
        VCSUIRuntimeBindings.BindChoice(this, "MaleSelect", ChooseAvatar, 1);
        VCSUIRuntimeBindings.Bind(this, "SelectNext", ClickAvatarNext);
        VCSUIRuntimeBindings.Bind(this, "AvatarBack", ClickAvatarBack);
        VCSUIRuntimeBindings.Bind(this, "PrepareNext", ClickPrepareNext);
        VCSUIRuntimeBindings.Bind(this, "PrepareBack", ClickPrepareBack);
        VCSUIRuntimeBindings.Bind(this, "EnterStudio", ClickGuideEnter);
        Debug.Log("[VCS/UI] Menu button handlers checked and bound at runtime.", this);
    }

    private void ShowPage(Page p)
    {
        currentPage = p;
        if (mainPanel != null) mainPanel.SetActive(p == Page.Main);
        if (avatarPanel != null) avatarPanel.SetActive(p == Page.Avatar);
        if (preparePanel != null) preparePanel.SetActive(p == Page.Prepare);
        if (guidePanel != null) guidePanel.SetActive(p == Page.Guide);
    }

    public void ClickStart()
    {
        if (app == null || app.config == null)
        {
            Debug.LogError("[VCS/UI] Start clicked, but VCSStudioAppFlow or its config is missing.", this);
            return;
        }
        Debug.Log("[VCS/UI] Start clicked -> Avatar Selection", this);
        ShowPage(Page.Avatar);
        ChooseAvatar(0);
    }

    public void ChooseAvatar(int index)
    {
        chosenIndex = index;
        if (app == null || app.config == null || app.config.avatars == null ||
            index < 0 || index >= app.config.avatars.Length) return;
        VCSAvatarOption option = app.config.avatars[index];
        if (option == null) return;
        if (avatarNameText != null) avatarNameText.text = option.displayName;
        bool ready = app.IsAvatarReady(index);
        if (avatarHintText != null)
            avatarHintText.text = ready
                ? "드래그: 회전    |    마우스 휠: 확대/축소"
                : "Preview Prefab 또는 Studio Scene이 아직 등록되지 않았습니다.";
        if (continueAvatarButton != null) continueAvatarButton.interactable = ready;
        if (preview != null) preview.Display(option.previewPrefab);
        if (avatarButtons != null)
            for (int i = 0; i < avatarButtons.Length; ++i)
                if (avatarButtons[i] != null) avatarButtons[i].interactable = i != index;
    }

    public void ClickAvatarNext()
    {
        if (app == null || !app.TrySelectAvatar(chosenIndex)) return;
        ShowPage(Page.Prepare);
        pythonStartError = !app.EnsureBackendStarted();
        if (preflight != null) preflight.Begin(app.config.websocketUrl);
        UpdatePrepareStatus();
    }

    public void ClickAvatarBack() { ShowPage(Page.Main); }

    public void ClickPrepareBack()
    {
        if (preflight != null) preflight.End();
        if (app != null) app.StopOwnedBackend();
        ShowPage(Page.Avatar);
    }

    private void Update()
    {
        if (currentPage == Page.Prepare) UpdatePrepareStatus();
    }

    private void UpdatePrepareStatus()
    {
        if (app == null || app.config == null) return;
        bool connected = preflight != null && preflight.Connected;
        bool hasPose = preflight != null && preflight.HasFreshPose(app.config.poseFreshSeconds);
        bool ready = !pythonStartError && (app.config.requireLivePoseInPreparation ? hasPose : connected);
        if (enterGuideButton != null) enterGuideButton.interactable = ready;
        if (prepareStatusText == null) return;
        if (pythonStartError)
            prepareStatusText.text = app.LastPythonError + "\n설정 수정 후 다시 시도해 주세요.";
        else if (hasPose)
            prepareStatusText.text = "카메라 · 자세 추적 준비 완료\n이제 스튜디오에 입장할 수 있습니다.";
        else if (connected)
            prepareStatusText.text = "서버 연결 성공 · 사람의 자세를 기다리는 중\n상반신이 화면에 보이도록 앉아 주세요.";
        else
            prepareStatusText.text = "Python 추적 서버 연결 중...\nmain.py가 실행 중인지 확인해 주세요.";
    }

    public void ClickPrepareNext()
    {
        if (app == null || app.config == null) return;
        bool hasPose = preflight != null && preflight.HasFreshPose(app.config.poseFreshSeconds);
        bool connected = preflight != null && preflight.Connected;
        if (app.config.requireLivePoseInPreparation ? !hasPose : !connected) return;
        if (app.config.showGuideOncePerLaunch && !app.GuideShownThisLaunch)
            ShowPage(Page.Guide);
        else EnterStudio();
    }

    public void ClickGuideEnter()
    {
        if (app != null) app.MarkGuideShown();
        EnterStudio();
    }

    private void EnterStudio()
    {
        if (preflight != null) preflight.End();
        app?.LoadSelectedStudio();
    }

    public void ClickQuit()
    {
        Debug.Log("[VCS/UI] Quit clicked.", this);
        if (app != null) app.StopOwnedBackend();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

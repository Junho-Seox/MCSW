using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VCSStudioSessionUI : MonoBehaviour
{
    public GameObject pausePanel;
    public Text statusLabel;
    public PoseWebSocketClient trackingClient;
    public float statusRefreshSeconds = 0.5f;
    private float nextRefresh;

    private void Start()
    {
        VCSUIRuntimeBindings.EnsureInputModule(this);
        VCSUIRuntimeBindings.Bind(this, "MenuButton", TogglePause);
        VCSUIRuntimeBindings.Bind(this, "Resume", TogglePause);
        VCSUIRuntimeBindings.Bind(this, "ReturnMenu", ClickReturnToMenu);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (trackingClient == null) trackingClient = FindObjectOfType<PoseWebSocketClient>();
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
#elif ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
#endif
        if (Time.unscaledTime < nextRefresh || statusLabel == null) return;
        nextRefresh = Time.unscaledTime + statusRefreshSeconds;
        if (trackingClient == null) trackingClient = FindObjectOfType<PoseWebSocketClient>();
        bool active = trackingClient != null && trackingClient.IsReceiving && trackingClient.HasPoseTracking;
        statusLabel.text = active ? "● TRACKING LIVE" : "○ WAITING FOR TRACKING";
        statusLabel.color = active ? new Color(0.44f, 0.92f, 0.75f) : new Color(0.98f, 0.72f, 0.51f);
    }

    public void TogglePause()
    {
        if (pausePanel != null) pausePanel.SetActive(!pausePanel.activeSelf);
    }

    public void ClickReturnToMenu()
    {
        if (VCSStudioAppFlow.Instance != null)
            VCSStudioAppFlow.Instance.ReturnToMainMenu();
        else
        {
            VCSStudioAppConfig cfg = Resources.Load<VCSStudioAppConfig>("VCSStudioAppConfig");
            if (cfg != null && Application.CanStreamedLevelBeLoaded(cfg.mainMenuScenePath))
                SceneManager.LoadScene(cfg.mainMenuScenePath);
            else Debug.LogError("[VCS/Studio] Main menu scene not available.");
        }
    }

    public void ClickQuit()
    {
        if (VCSStudioAppFlow.Instance != null)
            VCSStudioAppFlow.Instance.StopOwnedBackend();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

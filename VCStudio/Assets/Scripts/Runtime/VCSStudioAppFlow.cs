using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public sealed class VCSStudioAppFlow : MonoBehaviour
{
    public static VCSStudioAppFlow Instance { get; private set; }

    public VCSStudioAppConfig config;
    public int SelectedAvatarIndex { get; private set; }
    public bool GuideShownThisLaunch { get; private set; }
    public string LastPythonError { get; private set; }

    private Process ownedPython;
    private readonly ConcurrentQueue<string> pythonMessages = new ConcurrentQueue<string>();
    private bool shuttingDown;

    public VCSAvatarOption SelectedAvatar
    {
        get
        {
            if (config == null || config.avatars == null ||
                SelectedAvatarIndex < 0 || SelectedAvatarIndex >= config.avatars.Length)
                return null;
            return config.avatars[SelectedAvatarIndex];
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (config == null)
            config = Resources.Load<VCSStudioAppConfig>("VCSStudioAppConfig");
        DontDestroyOnLoad(gameObject);
        if (config == null)
            UnityEngine.Debug.LogError("[VCS/App] Missing Resources/VCSStudioAppConfig.asset", this);
    }

    public bool TrySelectAvatar(int index)
    {
        if (config == null || config.avatars == null || index < 0 || index >= config.avatars.Length)
            return false;
        VCSAvatarOption choice = config.avatars[index];
        if (choice == null || choice.previewPrefab == null || string.IsNullOrEmpty(choice.studioScenePath))
            return false;
        if (!Application.CanStreamedLevelBeLoaded(choice.studioScenePath))
            return false;
        SelectedAvatarIndex = index;
        return true;
    }

    public bool IsAvatarReady(int index)
    {
        return config != null && config.avatars != null && index >= 0 && index < config.avatars.Length &&
               config.avatars[index] != null && config.avatars[index].previewPrefab != null &&
               !string.IsNullOrEmpty(config.avatars[index].studioScenePath) &&
               Application.CanStreamedLevelBeLoaded(config.avatars[index].studioScenePath);
    }

    public bool EnsureBackendStarted()
    {
        LastPythonError = null;
        if (config == null)
        {
            LastPythonError = "App Config가 없습니다.";
            return false;
        }
        if (!config.managePythonProcess) return true;
        try
        {
            if (ownedPython != null && !ownedPython.HasExited) return true;
            string executable;
            string arguments;
            string workingDirectory;
#if UNITY_EDITOR
            string serviceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TrackingService"));
            string script = Path.Combine(serviceRoot, "main.py");
            executable = Path.Combine(serviceRoot, ".venv", "Scripts", "python.exe");
            arguments = "\"" + script + "\"";
            workingDirectory = serviceRoot;
            if (!File.Exists(script) || !File.Exists(executable))
            {
                LastPythonError = "개발용 Python 없음: " + executable +
                                  "\nTrackingService/.venv 생성과 main.py 위치를 확인하세요.";
                return false;
            }
#else
            string playerRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            executable = Path.Combine(playerRoot, "Backend", "VCS_Tracker", "VCS_Tracker.exe");
            arguments = "";
            workingDirectory = Path.GetDirectoryName(executable);
            if (!File.Exists(executable))
            {
                LastPythonError = "배포용 추적 실행 파일 없음: " + executable +
                                  "\nScripts/PrepareRelease.ps1로 전체 배포 폴더를 생성하세요.";
                return false;
            }
#endif
            if (ownedPython != null) { ownedPython.Dispose(); ownedPython = null; }
            var start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.EnvironmentVariables["VCS_HEADLESS"] = "1";
            start.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            ownedPython = new Process { StartInfo = start, EnableRaisingEvents = false };
            ownedPython.OutputDataReceived += (sender, args) => { if (args.Data != null) pythonMessages.Enqueue(args.Data); };
            ownedPython.ErrorDataReceived += (sender, args) => { if (args.Data != null) pythonMessages.Enqueue("ERR " + args.Data); };
            if (!ownedPython.Start())
            {
                LastPythonError = "Python 프로세스 시작 실패";
                return false;
            }
            ownedPython.BeginOutputReadLine();
            ownedPython.BeginErrorReadLine();
            UnityEngine.Debug.Log("[VCS/App] Managed Python started. PID=" + ownedPython.Id, this);
            return true;
        }
        catch (Exception e)
        {
            LastPythonError = "Python 실행 실패: " + e.Message;
            UnityEngine.Debug.LogError("[VCS/App] " + LastPythonError, this);
            return false;
        }
    }

    private void Update()
    {
        string msg;
        int count = 0;
        while (count++ < 12 && pythonMessages.TryDequeue(out msg))
        {
            if (msg.StartsWith("ERR ")) UnityEngine.Debug.LogWarning("[VCS/Python] " + msg.Substring(4));
            else if (msg.Contains("WebSocket 서버") || msg.Contains("웹캠") || msg.Contains("FPS"))
                UnityEngine.Debug.Log("[VCS/Python] " + msg);
        }
    }

    public void LoadSelectedStudio()
    {
        VCSAvatarOption option = SelectedAvatar;
        if (option == null || !Application.CanStreamedLevelBeLoaded(option.studioScenePath))
        {
            UnityEngine.Debug.LogError("[VCS/App] Avatar Studio scene is not in Build Settings.");
            return;
        }
        SceneManager.LoadScene(option.studioScenePath, LoadSceneMode.Single);
    }

    public void MarkGuideShown() { GuideShownThisLaunch = true; }

    public void ReturnToMainMenu()
    {
        StopOwnedBackend();
        if (config != null && Application.CanStreamedLevelBeLoaded(config.mainMenuScenePath))
            SceneManager.LoadScene(config.mainMenuScenePath, LoadSceneMode.Single);
        else
            UnityEngine.Debug.LogError("[VCS/App] Main menu scene not registered in Build Settings.");
    }

    public void StopOwnedBackend()
    {
        if (ownedPython == null) return;
        try
        {
            if (!ownedPython.HasExited)
            {
                ownedPython.Kill(); 
                ownedPython.WaitForExit(500);
            }
        }
        catch (Exception e) { UnityEngine.Debug.LogWarning("[VCS/App] Python cleanup: " + e.Message); }
        finally { ownedPython.Dispose(); ownedPython = null; }
    }

    private void OnApplicationQuit() { shuttingDown = true; StopOwnedBackend(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (!shuttingDown) StopOwnedBackend();
    }
}

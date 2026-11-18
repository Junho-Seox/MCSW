using System;
using UnityEngine;

[Serializable]
public class VCSAvatarOption
{
    public string displayName = "아바타";
    [Tooltip("Preview-only prefab: use the imported VRoid/VRM model, NOT your live TrackingManager hierarchy.")]
    public GameObject previewPrefab;
    [Tooltip("Studio scene that has been independently tested with this avatar's Animator/VRM rig.")]
    public string studioScenePath;
}

[CreateAssetMenu(menuName = "VirtualCharacterStudio/App Config", fileName = "VCSStudioAppConfig")]
public class VCSStudioAppConfig : ScriptableObject
{
    [Header("Screen flow")]
    public string mainMenuScenePath = "Assets/VirtualCharacterStudio/App/Scenes/VCS_MainMenu.unity";
    public VCSAvatarOption[] avatars = new VCSAvatarOption[]
    {
        new VCSAvatarOption { displayName = "여성 아바타" },
        new VCSAvatarOption { displayName = "남성 아바타" }
    };

    [Header("WebSocket / Python")]
    public string websocketUrl = "ws://127.0.0.1:8765";
    [Tooltip("OFF: run main.py in your terminal first. ON: Unity launches Python only when opening webcam preparation.")]
    public bool managePythonProcess = false;
    [Tooltip("Absolute Windows path to venv\\Scripts\\python.exe or python.exe in PATH.")]
    public string pythonExecutable = "python";
    [Tooltip("Absolute path to your existing Python main.py; required when Manage Python Process is ON.")]
    public string mainPythonScript = "";
    [Tooltip("Leave ON so only a proven Pose WebSocket stream allows entering the studio.")]
    public bool requireLivePoseInPreparation = true;
    public float poseFreshSeconds = 1f;

    [Header("Demo")]
    [Tooltip("Shows the four-gesture guide once per app launch, before the first Studio entry.")]
    public bool showGuideOncePerLaunch = true;
}

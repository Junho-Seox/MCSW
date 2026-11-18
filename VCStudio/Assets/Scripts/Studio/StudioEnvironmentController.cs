using System;
using UnityEngine;
using UnityEngine.Rendering;

public class StudioEnvironmentController : MonoBehaviour
{
    [Serializable]
    public class BackgroundSlot
    {
        public string displayName = "Background";
        [Tooltip("Only put the background root here; never the avatar or tracking objects.")]
        public GameObject backgroundRoot;
        [Tooltip("Optional: use a solid camera background instead of a 3D background root.")]
        public bool useSolidCameraColor;
        public Color cameraColor = new Color(0.10f, 0.12f, 0.17f, 1f);
    }

    [Serializable]
    public class LightingPreset
    {
        public string displayName = "Lighting";
        public Color keyLightColor = Color.white;
        [Min(0f)] public float keyLightIntensity = 1.2f;
        public Vector3 keyLightEulerAngles = new Vector3(45f, -30f, 0f);
        public bool overrideAmbientColor = true;
        public Color ambientColor = new Color(0.42f, 0.42f, 0.48f, 1f);
    }

    [Header("Scene References")]
    [SerializeField] private Camera studioCamera;
    [SerializeField] private Light keyDirectionalLight;

    [Header("Background Slots")]
    [SerializeField] private BackgroundSlot[] backgrounds = new BackgroundSlot[0];
    [SerializeField, Min(0)] private int initialBackgroundIndex;

    [Header("Lighting Presets")]
    [SerializeField] private LightingPreset[] lightingPresets = new LightingPreset[0];
    [SerializeField, Min(0)] private int initialLightingIndex;

    [Header("Optional Keyboard Test")]
    [SerializeField] private bool enableKeyboardShortcuts = false;
    [Tooltip("1/2/3 selects backgrounds; L cycles lighting presets.")]
    [SerializeField] private bool showDebugLogs = true;

    public int CurrentBackgroundIndex { get; private set; } = -1;
    public int CurrentLightingIndex { get; private set; } = -1;

    private CameraClearFlags originalCameraClearFlags;
    private Color originalCameraColor;
    private bool hasOriginalCameraSettings;

    private void Reset()
    {
        studioCamera = Camera.main;
        keyDirectionalLight = RenderSettings.sun;
    }

    private void Start()
    {
        if (studioCamera == null)
            studioCamera = Camera.main;

        if (keyDirectionalLight == null)
            keyDirectionalLight = RenderSettings.sun;

        if (studioCamera != null)
        {
            originalCameraClearFlags = studioCamera.clearFlags;
            originalCameraColor = studioCamera.backgroundColor;
            hasOriginalCameraSettings = true;
        }

        if (backgrounds != null && backgrounds.Length > 0)
            SetBackground(Mathf.Clamp(initialBackgroundIndex, 0, backgrounds.Length - 1));

        if (lightingPresets != null && lightingPresets.Length > 0)
            SetLighting(Mathf.Clamp(initialLightingIndex, 0, lightingPresets.Length - 1));
    }

    private void Update()
    {
        if (!enableKeyboardShortcuts)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SetBackground(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetBackground(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetBackground(2);
        if (Input.GetKeyDown(KeyCode.L)) NextLighting();
    }

    public void SetBackground(int index)
    {
        if (backgrounds == null || index < 0 || index >= backgrounds.Length)
        {
            Debug.LogWarning($"[Studio] Invalid background index: {index}", this);
            return;
        }

        for (int i = 0; i < backgrounds.Length; i++)
        {
            BackgroundSlot slot = backgrounds[i];
            if (slot != null && slot.backgroundRoot != null)
                slot.backgroundRoot.SetActive(i == index);
        }

        BackgroundSlot selected = backgrounds[index];
        if (studioCamera != null && selected != null)
        {
            if (selected.useSolidCameraColor)
            {
                studioCamera.clearFlags = CameraClearFlags.SolidColor;
                studioCamera.backgroundColor = selected.cameraColor;
            }
            else if (hasOriginalCameraSettings)
            {
                studioCamera.clearFlags = originalCameraClearFlags;
                studioCamera.backgroundColor = originalCameraColor;
            }
        }
        else if (studioCamera == null && selected != null && selected.useSolidCameraColor)
        {
            Debug.LogWarning("[Studio] Camera reference is missing.", this);
        }

        CurrentBackgroundIndex = index;
        if (showDebugLogs)
            Debug.Log($"[Studio] Background {index}: {selected?.displayName}", this);
    }

    public void SetLighting(int index)
    {
        if (lightingPresets == null || index < 0 || index >= lightingPresets.Length)
        {
            Debug.LogWarning($"[Studio] Invalid lighting index: {index}", this);
            return;
        }

        LightingPreset selected = lightingPresets[index];
        if (selected == null)
        {
            Debug.LogWarning($"[Studio] Lighting preset {index} is null.", this);
            return;
        }

        if (keyDirectionalLight != null)
        {
            keyDirectionalLight.color = selected.keyLightColor;
            keyDirectionalLight.intensity = Mathf.Max(0f, selected.keyLightIntensity);
            keyDirectionalLight.transform.rotation = Quaternion.Euler(selected.keyLightEulerAngles);
        }
        else
        {
            Debug.LogWarning("[Studio] Key Directional Light reference is missing.", this);
        }

        if (selected.overrideAmbientColor)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = selected.ambientColor;
        }

        CurrentLightingIndex = index;
        if (showDebugLogs)
            Debug.Log($"[Studio] Lighting {index}: {selected.displayName}", this);
    }

    public void NextBackground()
    {
        if (backgrounds == null || backgrounds.Length == 0) return;
        SetBackground((CurrentBackgroundIndex + 1) % backgrounds.Length);
    }

    public void NextLighting()
    {
        if (lightingPresets == null || lightingPresets.Length == 0) return;
        SetLighting((CurrentLightingIndex + 1) % lightingPresets.Length);
    }
}

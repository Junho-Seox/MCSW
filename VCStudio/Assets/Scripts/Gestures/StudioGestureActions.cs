using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public sealed class StudioGestureActions : MonoBehaviour
{
    [Header("Recognition")]
    public StudioGestureRecognizer recognizer;
    [Tooltip("Must be ON for gestures to control the scene. Existing scenes may retain the old serialized OFF value!")]
    public bool executeActions = true;

    [Header("Existing background & lighting")]
    public StudioEnvironmentController studioEnvironment;

    [Header("Avatar / Camera")]
    public Animator avatarAnimator;
    public Camera studioCamera;

    [Header("Visual prefab slots")]
    public GameObject shieldPrefab;
    public GameObject headsetPrefab;
    public GameObject musicNotePrefab;
    public Vector3 headsetLocalOffset = Vector3.zero;
    public Vector3 headsetLocalEuler = Vector3.zero;
    public Vector3 noteLocalOffset = new Vector3(0f, .25f, 0f);
    [Tooltip("Positive Z = offset the shield TOWARD the camera, not into the avatar.")]
    public Vector3 shieldPositionOffset = new Vector3(0f, 0f, .13f);

    [Header("Procedural fallback")]
    public bool useProceduralFallback = true;
    [Range(0.5f, 3f)] public float proceduralVisualScale = 1f;

    [Header("Audio (optional)")]
    public AudioSource audioSource;
    public AudioClip shieldSound;
    public AudioClip headsetSound;

    [Header("Diagnostics & manual studio tests")]
    public bool logActions = true;
    [Tooltip("F2 cycles backgrounds, F3 cycles lighting in Play mode regardless of Execute Actions. Used to isolate scene configuration from gesture detection.")]
    public bool enableManualStudioTests = true;

    private GameObject shieldInstance;
    private GameObject headsetInstance;
    private GameObject noteInstance;
    private Coroutine visualLifetime;
    private StudioGestureRecognizer.Gesture? currentVisual;
    private Transform leftHand;
    private Transform rightHand;
    private Transform head;
    private StudioGestureRecognizer subscribedRecognizer;

    private void Reset()
    {
        recognizer = FindObjectOfType<StudioGestureRecognizer>();
        studioEnvironment = FindObjectOfType<StudioEnvironmentController>();
        studioCamera = Camera.main;
    }

    private void Awake()
    {
        ResolveReferences();
        if (shieldSound == null) shieldSound = Resources.Load<AudioClip>("GestureSFX/shield_activate");
        if (headsetSound == null) headsetSound = Resources.Load<AudioClip>("GestureSFX/headset_activate");
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void ResolveReferences()
    {
        if (recognizer == null) recognizer = GetComponent<StudioGestureRecognizer>();
        if (recognizer == null) recognizer = FindObjectOfType<StudioGestureRecognizer>();
        if (studioEnvironment == null) studioEnvironment = FindObjectOfType<StudioEnvironmentController>();
        if (studioCamera == null) studioCamera = Camera.main;
        if (avatarAnimator != null && avatarAnimator.isHuman)
        {
            leftHand = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            head = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    private void OnEnable() { Subscribe(); }
    private void Start()
    {
        ResolveReferences();
        Subscribe();
        if (recognizer == null) Debug.LogError("[Gesture/Action] Recognizer missing. Set GestureSystem/StudioGestureRecognizer in Inspector.", this);
        if (studioEnvironment == null) Debug.LogError("[Gesture/Action] StudioEnvironmentController missing. Swipe cannot change background or lighting.", this);
        if (!executeActions) Debug.LogWarning("[Gesture/Action] Execute Actions = OFF. Scores and FIRE logs can appear, but no automatic effects will run. Enable it in Inspector.", this);
        if (studioEnvironment != null && (studioEnvironment.CurrentBackgroundIndex < 0 || studioEnvironment.CurrentLightingIndex < 0))
            Debug.LogWarning("[Gesture/Action] One or both StudioEnvironmentController presets are unconfigured. Check Background Slots / Lighting Presets before testing swipes.", this);
        if (avatarAnimator == null) Debug.LogWarning("[Gesture/Action] Avatar Animator missing; shield/headset attachment may be unavailable.", this);
        if (logActions) Debug.Log("[Gesture/Action] READY execute=" + executeActions + " env=" + (studioEnvironment != null) + " recognizer=" + (recognizer != null) +
            " backgroundIndex=" + (studioEnvironment != null ? studioEnvironment.CurrentBackgroundIndex : -1) +
            " lightingIndex=" + (studioEnvironment != null ? studioEnvironment.CurrentLightingIndex : -1), this);
    }

    private void Subscribe()
    {
        if (recognizer == null) recognizer = GetComponent<StudioGestureRecognizer>();
        if (recognizer == null) recognizer = FindObjectOfType<StudioGestureRecognizer>();
        if (subscribedRecognizer == recognizer) return;
        if (subscribedRecognizer != null) subscribedRecognizer.GestureTriggered -= OnGesture;
        subscribedRecognizer = recognizer;
        if (subscribedRecognizer != null) subscribedRecognizer.GestureTriggered += OnGesture;
    }

    private void OnDisable()
    {
        if (subscribedRecognizer != null) subscribedRecognizer.GestureTriggered -= OnGesture;
        subscribedRecognizer = null;
        if (visualLifetime != null) StopCoroutine(visualLifetime);
        visualLifetime = null;
        CleanupVisuals();
    }

    private void Update()
    {
        if (!enableManualStudioTests || !Application.isPlaying) return;
        if (Input.GetKeyDown(KeyCode.F2)) TestNextBackground();
        if (Input.GetKeyDown(KeyCode.F3)) TestNextLighting();
    }

    [ContextMenu("TEST / Next Background (Play mode)")]
    public void TestNextBackground()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Gesture/Action] Enter Play mode before testing.", this); return; }
        ChangeBackground("MANUAL F2");
    }

    [ContextMenu("TEST / Next Lighting (Play mode)")]
    public void TestNextLighting()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Gesture/Action] Enter Play mode before testing.", this); return; }
        ChangeLighting("MANUAL F3");
    }

    private void OnGesture(StudioGestureRecognizer.Gesture gesture)
    {
        if (logActions) Debug.Log("[Gesture/Action] received=" + gesture + " execute=" + executeActions, this);
        if (!executeActions) return;
        switch (gesture)
        {
            case StudioGestureRecognizer.Gesture.HorizontalSwipe:
                ChangeBackground("GESTURE HorizontalSwipe");
                break;
            case StudioGestureRecognizer.Gesture.DownSwipe:
                ChangeLighting("GESTURE DownSwipe");
                break;
            case StudioGestureRecognizer.Gesture.Shield:
                SpawnVisual(gesture, recognizer != null ? recognizer.shieldDurationSeconds : 5f);
                if (audioSource != null && shieldSound != null) audioSource.PlayOneShot(shieldSound);
                break;
            case StudioGestureRecognizer.Gesture.Headset:
                SpawnVisual(gesture, recognizer != null ? recognizer.headsetDurationSeconds : 10f);
                if (audioSource != null && headsetSound != null) audioSource.PlayOneShot(headsetSound);
                break;
        }
    }

    private void ChangeBackground(string origin)
    {
        if (studioEnvironment == null) ResolveReferences();
        if (studioEnvironment == null) { Debug.LogError("[Gesture/Action] " + origin + " failed: StudioEnvironmentController not assigned/found.", this); return; }
        int oldIndex = studioEnvironment.CurrentBackgroundIndex;
        studioEnvironment.NextBackground();
        int newIndex = studioEnvironment.CurrentBackgroundIndex;
        Debug.Log("[Gesture/Action] " + origin + " background: " + oldIndex + " -> " + newIndex, this);
        if (newIndex < 0) Debug.LogWarning("[Gesture/Action] No Background Slots set in StudioEnvironmentController.", this);
        else if (newIndex == oldIndex) Debug.LogWarning("[Gesture/Action] Background index unchanged. Ensure 2+ different background slots and their Background Roots are assigned.", this);
    }

    private void ChangeLighting(string origin)
    {
        if (studioEnvironment == null) ResolveReferences();
        if (studioEnvironment == null) { Debug.LogError("[Gesture/Action] " + origin + " failed: StudioEnvironmentController not assigned/found.", this); return; }
        int oldIndex = studioEnvironment.CurrentLightingIndex;
        studioEnvironment.NextLighting();
        int newIndex = studioEnvironment.CurrentLightingIndex;
        Debug.Log("[Gesture/Action] " + origin + " lighting: " + oldIndex + " -> " + newIndex, this);
        if (newIndex < 0) Debug.LogWarning("[Gesture/Action] No Lighting Presets set in StudioEnvironmentController.", this);
        else if (newIndex == oldIndex) Debug.LogWarning("[Gesture/Action] Lighting index unchanged. Ensure 2+ lighting presets with visibly different intensity/colors, and assign Key Directional Light.", this);
    }

    private void SpawnVisual(StudioGestureRecognizer.Gesture gesture, float seconds)
    {
        if (visualLifetime != null) StopCoroutine(visualLifetime);
        visualLifetime = null;
        CleanupVisuals();
        currentVisual = gesture;
        if (gesture == StudioGestureRecognizer.Gesture.Shield)
        {
            if (shieldPrefab != null)
            {
                shieldInstance = Instantiate(shieldPrefab);
                UpdateShieldPosition();
            }
            else if (useProceduralFallback)
            {
                shieldInstance = StudioGestureProceduralVisual.CreateShield(proceduralVisualScale);
                UpdateShieldPosition();
            }
            else Debug.LogWarning("[Gesture/Action] Shield prefab and fallback are both unavailable.", this);
        }
        else if (gesture == StudioGestureRecognizer.Gesture.Headset)
        {
            if (head == null) ResolveReferences();
            if (head == null)
                Debug.LogWarning("[Gesture/Action] Head bone unavailable; assign a Humanoid Animator.", this);
            else
            {
                if (headsetPrefab != null)
                {
                    headsetInstance = Instantiate(headsetPrefab, head);
                    headsetInstance.transform.localPosition = headsetLocalOffset;
                    headsetInstance.transform.localRotation = Quaternion.Euler(headsetLocalEuler);
                }
                else if (useProceduralFallback)
                {
                    headsetInstance = StudioGestureProceduralVisual.CreateHeadset(proceduralVisualScale);
                    headsetInstance.transform.SetParent(head, false);
                    headsetInstance.transform.localPosition = headsetLocalOffset;
                    headsetInstance.transform.localRotation = Quaternion.Euler(headsetLocalEuler);
                }
                else Debug.LogWarning("[Gesture/Action] Headset prefab and fallback are both unavailable.", this);
                if (musicNotePrefab != null)
                {
                    noteInstance = Instantiate(musicNotePrefab, head);
                    noteInstance.transform.localPosition = noteLocalOffset;
                    noteInstance.transform.localRotation = Quaternion.identity;
                }
            }
        }
        visualLifetime = StartCoroutine(EndVisualAfter(Mathf.Max(.1f, seconds)));
    }

    private IEnumerator EndVisualAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        CleanupVisuals();
        visualLifetime = null;
    }

    private void LateUpdate()
    {
        if (currentVisual == StudioGestureRecognizer.Gesture.Shield && shieldInstance != null)
            UpdateShieldPosition();
    }

    private void UpdateShieldPosition()
    {
        if (shieldInstance == null) return;
        if (leftHand == null || rightHand == null) ResolveReferences();
        if (leftHand == null || rightHand == null) { Debug.LogWarning("[Gesture/Action] Shield cannot follow hands: Humanoid hand bones missing.", this); return; }
        Vector3 center = (leftHand.position + rightHand.position) * .5f;
        Vector3 towardViewer = studioCamera != null ? -studioCamera.transform.forward :
                               avatarAnimator != null ? avatarAnimator.transform.forward : Vector3.forward;
        Vector3 side = studioCamera != null ? studioCamera.transform.right : Vector3.right;
        shieldInstance.transform.position = center + towardViewer * shieldPositionOffset.z +
                                            Vector3.up * shieldPositionOffset.y + side * shieldPositionOffset.x;
        shieldInstance.transform.rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
    }

    private void CleanupVisuals()
    {
        if (shieldInstance != null) Destroy(shieldInstance);
        if (headsetInstance != null) Destroy(headsetInstance);
        if (noteInstance != null) Destroy(noteInstance);
        shieldInstance = headsetInstance = noteInstance = null;
        currentVisual = null;
    }
}

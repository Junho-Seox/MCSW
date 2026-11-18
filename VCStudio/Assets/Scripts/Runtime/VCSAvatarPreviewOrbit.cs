using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VCSAvatarPreviewOrbit : MonoBehaviour, IDragHandler, IScrollHandler
{
    public RawImage previewImage;
    public Camera previewCamera;
    public Transform previewPivot;
    public float dragDegreesPerPixel = 0.32f;
    public float zoomSensitivity = 0.7f;
    public float minimumDistance = 1.2f;
    public float maximumDistance = 8f;

    private GameObject currentModel;
    private RenderTexture texture;
    private float distance = 3f;
    private float yaw;
    private float pitch = 5f;
    private Vector3 focus;

    private void Awake()
    {
        if (previewImage == null) previewImage = GetComponent<RawImage>();
        if (previewCamera != null)
        {
            texture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            texture.name = "VCS_AvatarPreview_RT";
            texture.Create();
            previewCamera.targetTexture = texture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.075f, 0.105f, 0.16f, 1f);
            previewCamera.fieldOfView = 34f;
        }
        if (previewImage != null) previewImage.texture = texture;
    }

    public void Display(GameObject prefab)
    {
        if (currentModel != null) Destroy(currentModel);
        currentModel = null;
        if (prefab == null || previewPivot == null || previewCamera == null) return;

        currentModel = Instantiate(prefab, previewPivot.position, Quaternion.identity);
        currentModel.name = "VCS_PreviewOnly_" + prefab.name;

        MonoBehaviour[] behaviours = currentModel.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; ++i)
            if (behaviours[i] != null) behaviours[i].enabled = false;
        Animator[] animators = currentModel.GetComponentsInChildren<Animator>(true);
        foreach (Animator a in animators) a.enabled = false;

        Bounds b = new Bounds(currentModel.transform.position + Vector3.up, Vector3.one);
        Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        previewPivot.position = b.center;
        currentModel.transform.SetParent(previewPivot, true);
        float height = Mathf.Max(0.8f, b.size.y);
        float width = Mathf.Max(0.5f, b.size.x);
        distance = Mathf.Clamp(Mathf.Max(height, width) * 1.9f, minimumDistance, maximumDistance);
        focus = previewPivot.position;
        yaw = 0f;
        pitch = 4f;
        UpdateCamera();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (currentModel == null) return;
        yaw -= eventData.delta.x * dragDegreesPerPixel;
        pitch = Mathf.Clamp(pitch + eventData.delta.y * dragDegreesPerPixel, -25f, 35f);
        UpdateCamera();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (currentModel == null) return;
        distance = Mathf.Clamp(distance - eventData.scrollDelta.y * zoomSensitivity,
            minimumDistance, maximumDistance);
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (previewCamera == null || previewPivot == null) return;
        previewPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion q = Quaternion.Euler(pitch, 0f, 0f);
        previewCamera.transform.position = focus + q * (Vector3.forward * distance);
        previewCamera.transform.LookAt(focus);
    }

    private void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (currentModel != null) Destroy(currentModel);
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public sealed class StudioGesturePrefabAnimator : MonoBehaviour
{
    public enum EffectKind { Shield, FloatingNotes }
    public EffectKind effectKind = EffectKind.Shield;
    public Transform shieldInnerRing;
    public Transform[] orbitNotes;
    [Range(0f, 0.1f)] public float shieldPulseStrength = 0.028f;
    public float noteOrbitSpeed = 1.4f;
    public float noteBobAmount = 0.045f;

    private Vector3 originalScale;
    private Vector3[] originalPositions;
    private float createdAt;

    private void Awake()
    {
        originalScale = transform.localScale;
        createdAt = Time.realtimeSinceStartup;
        if (orbitNotes != null)
        {
            originalPositions = new Vector3[orbitNotes.Length];
            for (int i = 0; i < orbitNotes.Length; i++)
                if (orbitNotes[i] != null) originalPositions[i] = orbitNotes[i].localPosition;
        }
    }

    private void Update()
    {
        float t = Time.realtimeSinceStartup - createdAt;
        if (effectKind == EffectKind.Shield)
        {
            float appear = Mathf.Clamp01(t / 0.18f);
            float pulse = 1f + shieldPulseStrength * Mathf.Sin(t * 7f);
            transform.localScale = originalScale * ((0.70f + 0.30f * appear) * pulse);
            if (shieldInnerRing != null)
                shieldInnerRing.localRotation = Quaternion.Euler(0f, 0f, t * 17f);
        }
        else if (orbitNotes != null && originalPositions != null)
        {
            for (int i = 0; i < orbitNotes.Length; i++)
            {
                Transform note = orbitNotes[i];
                if (note == null) continue;
                float phase = i * 1.24f;
                Vector3 home = originalPositions[i];
                note.localPosition = home + new Vector3(
                    0.035f * Mathf.Sin(t * noteOrbitSpeed + phase),
                    noteBobAmount * Mathf.Sin(t * 2.05f + phase),
                    0.019f * Mathf.Cos(t * noteOrbitSpeed + phase));
                note.localRotation = Quaternion.Euler(0f, -t * 28f, 0f);
            }
        }
    }
}

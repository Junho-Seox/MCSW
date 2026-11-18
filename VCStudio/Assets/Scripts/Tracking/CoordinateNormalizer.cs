using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CoordinateNormalizer : MonoBehaviour
{
    [Header("Pose World -> Unity tracking axes")]
    [Tooltip("Defaults preserve existing Torso/Arms/Head coordinate conversion.")]
    public bool invertX = false;
    public bool invertY = true;
    public bool invertZ = true;

    [Header("Pose anatomical left/right")]
    [Tooltip("Defaults preserve BothArmsRetargeter.swapLeftRight=false. Do not change without a motion test.")]
    public bool swapPoseLeftRight = false;

    [Header("Hand World -> Unity tracking axes")]
    [Tooltip("Hand WORLD is hand-centered. These signs affect palm ORIENTATION only.")]
    public bool handInvertX = false;
    public bool handInvertY = true;
    public bool handInvertZ = true;

    [Header("Hand handedness -> avatar side")]
    [Tooltip("True preserves the previous Wrist/Finger swapHands=true. Pose and Hand labels are different input conventions.")]
    public bool swapHandLabels = true;

    [Header("Diagnostics")]
    public bool logConfigurationOnStart = true;

    private static readonly int[] LeftIds =
    {
        1, 2, 3, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31
    };
    private static readonly int[] RightIds =
    {
        4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32
    };

    private void Start()
    {
        if (logConfigurationOnStart)
            PrintConfiguration();
    }

    [ContextMenu("Print Pose Coordinate Configuration")]
    public void PrintConfiguration()
    {
        Debug.Log($"[CoordinateNormalizer/Step1A] Pose signs: " +
            $"X={(invertX ? "-" : "+")}, Y={(invertY ? "-" : "+")}, " +
            $"Z={(invertZ ? "-" : "+")}; swapPoseLeftRight={swapPoseLeftRight}. " +
            $"Hand signs: X={(handInvertX ? "-" : "+")}, " +
            $"Y={(handInvertY ? "-" : "+")}, Z={(handInvertZ ? "-" : "+")}; " +
            $"swapHandLabels={swapHandLabels}.");
    }

    public bool TryGetAvatarHandSide(string rawLabel, out bool avatarIsLeft)
    {
        avatarIsLeft = false;
        if (string.Equals(rawLabel, "Left", StringComparison.OrdinalIgnoreCase))
        {
            avatarIsLeft = !swapHandLabels;
            return true;
        }
        if (string.Equals(rawLabel, "Right", StringComparison.OrdinalIgnoreCase))
        {
            avatarIsLeft = swapHandLabels;
            return true;
        }
        return false;
    }
    public Vector3 NormalizeHandWorldPosition(Vector3 source)
    {
        return new Vector3(
            handInvertX ? -source.x : source.x,
            handInvertY ? -source.y : source.y,
            handInvertZ ? -source.z : source.z);
    }

    public PoseLandmark[] NormalizePose(PoseLandmark[] source)
    {
        if (source == null || source.Length != 33)
            return null;

        PoseLandmark[] result = new PoseLandmark[33];
        for (int i = 0; i < 33; ++i)
        {
            PoseLandmark p = source[i];
            if (p == null)
                return null;

            result[i] = new PoseLandmark
            {
                id = i,
                x = invertX ? -p.x : p.x,
                y = invertY ? -p.y : p.y,
                z = invertZ ? -p.z : p.z,
                visibility = p.visibility
            };
        }

        if (swapPoseLeftRight)
        {
            for (int k = 0; k < LeftIds.Length; ++k)
            {
                int l = LeftIds[k];
                int r = RightIds[k];
                PoseLandmark temp = result[l];
                result[l] = result[r];
                result[r] = temp;
                result[l].id = l;
                result[r].id = r;
            }
        }
        return result;
    }
}

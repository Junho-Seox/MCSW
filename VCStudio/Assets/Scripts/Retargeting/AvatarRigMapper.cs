using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class AvatarRigMapper : MonoBehaviour
{
    [Header("Humanoid Avatar")]
    [Tooltip("Assign the same Animator used by Torso/Head/Arms retargeters.")]
    public Animator animator;

    [Header("Diagnostics Only - No Transform Writes")]
    public bool logReportOnStart = true;
    public bool drawSnapshotGizmos = true;
    [Min(0.001f)] public float gizmoRadius = 0.015f;

    public struct BoneSnapshot
    {
        public Vector3 rootLocalPosition;
        public Quaternion rootLocalRotation;
        public Quaternion localRotation;
    }

    private readonly Dictionary<HumanBodyBones, BoneSnapshot> snapshot =
        new Dictionary<HumanBodyBones, BoneSnapshot>();

    private static readonly HumanBodyBones[] CaptureBones =
    {
        HumanBodyBones.Hips,
        HumanBodyBones.Spine,
        HumanBodyBones.Chest,
        HumanBodyBones.UpperChest,
        HumanBodyBones.Neck,
        HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder,
        HumanBodyBones.RightShoulder,
        HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.RightHand
    };

    public bool IsReady { get; private set; }
    public float ShoulderWidth { get; private set; }
    public float LeftUpperArmLength { get; private set; }
    public float LeftForearmLength { get; private set; }
    public float RightUpperArmLength { get; private set; }
    public float RightForearmLength { get; private set; }
    public float TorsoHeight { get; private set; }
    public Quaternion RootLocalTorsoFrame { get; private set; } = Quaternion.identity;

    private void Awake()
    {

        TryCaptureRig();
    }

    private void Start()
    {

        if (!IsReady)
            TryCaptureRig();

        if (logReportOnStart)
            PrintRigReport();
    }

    [ContextMenu("Capture Current Rig Snapshot (Neutral Pose Only)")]
    public void CaptureCurrentRigSnapshot()
    {
        if (Application.isPlaying)
            Debug.LogWarning("[AvatarRigMapper] Capturing during Play may record a tracked/animated pose. Use only in neutral pose.", this);

        TryCaptureRig();
        PrintRigReport();
    }

    private bool TryCaptureRig()
    {
        IsReady = false;
        snapshot.Clear();

        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.LogWarning("[AvatarRigMapper] Valid Humanoid Animator not ready. Assign the VRM Animator.", this);
            return false;
        }

        Transform root = animator.transform;
        Quaternion invRootRotation = Quaternion.Inverse(root.rotation);

        foreach (HumanBodyBones bone in CaptureBones)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
                continue;

            snapshot[bone] = new BoneSnapshot
            {
                rootLocalPosition = root.InverseTransformPoint(t.position),
                rootLocalRotation = invRootRotation * t.rotation,
                localRotation = t.localRotation
            };
        }

        HumanBodyBones[] required =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
        };

        foreach (HumanBodyBones bone in required)
        {
            if (!snapshot.ContainsKey(bone))
            {
                Debug.LogWarning("[AvatarRigMapper] Missing required humanoid bone: " + bone, this);
                return false;
            }
        }

        Vector3 leftShoulder = Position(HumanBodyBones.LeftUpperArm);
        Vector3 rightShoulder = Position(HumanBodyBones.RightUpperArm);
        Vector3 hips = Position(HumanBodyBones.Hips);
        Vector3 head = Position(HumanBodyBones.Head);

        ShoulderWidth = Vector3.Distance(leftShoulder, rightShoulder);
        LeftUpperArmLength = Vector3.Distance(leftShoulder, Position(HumanBodyBones.LeftLowerArm));
        LeftForearmLength = Vector3.Distance(Position(HumanBodyBones.LeftLowerArm), Position(HumanBodyBones.LeftHand));
        RightUpperArmLength = Vector3.Distance(rightShoulder, Position(HumanBodyBones.RightLowerArm));
        RightForearmLength = Vector3.Distance(Position(HumanBodyBones.RightLowerArm), Position(HumanBodyBones.RightHand));
        TorsoHeight = Vector3.Distance(hips, head);

        const float minBoneLength = 0.0001f;
        if (ShoulderWidth < minBoneLength || LeftUpperArmLength < minBoneLength ||
            LeftForearmLength < minBoneLength || RightUpperArmLength < minBoneLength ||
            RightForearmLength < minBoneLength || TorsoHeight < minBoneLength)
        {
            Debug.LogWarning("[AvatarRigMapper] Invalid or zero-length rig geometry. Check the Animator/bone mapping.", this);
            return false;
        }

        Vector3 right = (rightShoulder - leftShoulder).normalized;
        Vector3 up = Vector3.ProjectOnPlane(head - hips, right);
        if (up.sqrMagnitude < 1e-8f)
        {
            Debug.LogWarning("[AvatarRigMapper] Cannot construct torso frame from current pose.", this);
            return false;
        }
        up.Normalize();
        Vector3 forward = Vector3.Cross(right, up).normalized;
        RootLocalTorsoFrame = Quaternion.LookRotation(forward, up);

        IsReady = true;
        return true;
    }

    private Vector3 Position(HumanBodyBones bone)
    {
        return snapshot[bone].rootLocalPosition;
    }

    public bool TryGetBoneSnapshot(HumanBodyBones bone, out BoneSnapshot boneSnapshot)
    {
        boneSnapshot = default(BoneSnapshot);
        return IsReady && snapshot.TryGetValue(bone, out boneSnapshot);
    }

    [ContextMenu("Print Rig Report")]
    public void PrintRigReport()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[AvatarRigMapper] No valid snapshot. Check Animator and start pose.", this);
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[AvatarRigMapper / Step 2A] Read-only rig report");
        sb.AppendLine("Animator: " + animator.name);
        sb.AppendLine("Lengths are in animator-root local units (Unity units if root scale = 1).");
        sb.AppendLine("Shoulder width: " + ShoulderWidth.ToString("F4"));
        sb.AppendLine("Left arm: upper=" + LeftUpperArmLength.ToString("F4") +
            ", forearm=" + LeftForearmLength.ToString("F4"));
        sb.AppendLine("Right arm: upper=" + RightUpperArmLength.ToString("F4") +
            ", forearm=" + RightForearmLength.ToString("F4"));
        sb.AppendLine("Hip-to-head distance: " + TorsoHeight.ToString("F4"));
        sb.AppendLine("Torso frame forward (root local): " + (RootLocalTorsoFrame * Vector3.forward).ToString("F3"));

        Vector3 leftUpper = Position(HumanBodyBones.LeftUpperArm);
        Vector3 leftElbow = Position(HumanBodyBones.LeftLowerArm);
        Vector3 leftHand = Position(HumanBodyBones.LeftHand);
        Vector3 rightUpper = Position(HumanBodyBones.RightUpperArm);
        Vector3 rightElbow = Position(HumanBodyBones.RightLowerArm);
        Vector3 rightHand = Position(HumanBodyBones.RightHand);
        float leftBend = Vector3.Angle(leftElbow - leftUpper, leftHand - leftElbow);
        float rightBend = Vector3.Angle(rightElbow - rightUpper, rightHand - rightElbow);
        sb.AppendLine("Initial elbow direction-change angles: L=" + leftBend.ToString("F1") +
            "°, R=" + rightBend.ToString("F1") + "° (0° ~ straight)");

        if (leftBend > 35f || rightBend > 35f)
            sb.AppendLine("WARNING: Arms appear bent in captured pose; this may not be a neutral rig reference.");
        if (Vector3.Dot(RootLocalTorsoFrame * Vector3.forward, Vector3.forward) < 0.5f)
            sb.AppendLine("WARNING: Derived torso forward differs from Animator root +Z. Verify rig facing direction.");
        sb.AppendLine("Snapshot is diagnostic, NOT a guaranteed imported bind pose or calibrated human forward.");
        Debug.Log(sb.ToString(), this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSnapshotGizmos || !IsReady || animator == null)
            return;

        Transform root = animator.transform;
        DrawSegment(root, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
        DrawSegment(root, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        DrawSegment(root, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
        DrawSegment(root, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        DrawSegment(root, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
        DrawSegment(root, HumanBodyBones.Hips, HumanBodyBones.Spine);
        DrawSegment(root, HumanBodyBones.Spine, HumanBodyBones.Head);
    }

    private void DrawSegment(Transform root, HumanBodyBones a, HumanBodyBones b)
    {
        BoneSnapshot first, second;
        if (!snapshot.TryGetValue(a, out first) || !snapshot.TryGetValue(b, out second))
            return;

        Vector3 p0 = root.TransformPoint(first.rootLocalPosition);
        Vector3 p1 = root.TransformPoint(second.rootLocalPosition);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawSphere(p0, gizmoRadius);
        Gizmos.DrawSphere(p1, gizmoRadius);
    }
}

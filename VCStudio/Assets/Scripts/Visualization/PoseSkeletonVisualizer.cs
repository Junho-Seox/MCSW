
using UnityEngine;

public class PoseSkeletonVisualizer : MonoBehaviour
{
    public float scale = 2.0f;
    public float jointSize = 0.06f;

    private const int JointCount = 33;

    private GameObject[] joints = new GameObject[JointCount];
    private LineRenderer[] bones;

    private static readonly int[,] Connections =
    {
        {11, 12}, 
        {11, 13}, {13, 15}, 
        {12, 14}, {14, 16}, 

        {11, 23}, {12, 24}, 
        {23, 24}, 
        {23, 25}, {25, 27}, 
        {24, 26}, {26, 28}, 

        {27, 29}, {29, 31}, 
        {28, 30}, {30, 32} 
    };

    private void Start()
    {
        CreateJoints();
        CreateBones();
    }

    private void CreateJoints()
    {
        for (int i = 0; i < JointCount; i++)
        {
            GameObject joint = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            joint.name = $"Joint_{i}";
            joint.transform.SetParent(transform, false);
            joint.transform.localScale =
                Vector3.one * jointSize;

            Destroy(joint.GetComponent<Collider>());

            joints[i] = joint;
        }
    }

    private void CreateBones()
    {
        int count = Connections.GetLength(0);
        bones = new LineRenderer[count];

        Material lineMaterial = new Material(
            Shader.Find("Sprites/Default")
        );

        for (int i = 0; i < count; i++)
        {
            GameObject bone = new GameObject($"Bone_{i}");
            bone.transform.SetParent(transform, false);

            LineRenderer line =
                bone.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.material = lineMaterial;

            bones[i] = line;
        }
    }

    public void UpdatePose(PoseLandmark[] landmarks)
    {
        if (joints[0] == null)
            return;

        if (landmarks == null ||
            landmarks.Length != JointCount)
            return;

        for (int i = 0; i < JointCount; i++)
        {
            PoseLandmark p = landmarks[i];

            Vector3 position = new Vector3(
                p.x,
                -p.y,
                -p.z
            ) * scale;

            joints[i].transform.localPosition = position;
        }

        for (int i = 0; i < bones.Length; i++)
        {
            int start = Connections[i, 0];
            int end = Connections[i, 1];

            bones[i].SetPosition(
                0,
                joints[start].transform.localPosition
            );

            bones[i].SetPosition(
                1,
                joints[end].transform.localPosition
            );
        }
    }
}

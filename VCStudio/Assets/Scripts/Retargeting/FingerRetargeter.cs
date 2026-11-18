
using System;
using System.Text;
using UnityEngine;

public class FingerRetargeter : MonoBehaviour
{
    [Header("Avatar")]
    public Animator animator;
    public PoseWebSocketClient poseClient;

    [Header("Tracking")]
    public bool enableLeftHand = true;
    public bool enableRightHand = true;

    [Header("Current Hand Mapping")]
    public bool swapHands = true;

    [Header("Finger Rotation")]
    [Range(0f, 1.5f)]
    public float bendWeight = 1f;

    [Range(0f, 120f)]
    public float maxBendAngle = 85f;

    public Vector3 leftBendAxis = Vector3.forward;
    public Vector3 rightBendAxis = Vector3.forward;

    public bool invertLeft = true;
    public bool invertRight = true;

    [Header("Thumb")]
    [Range(0f, 1.5f)]
    public float thumbWeight = 0.7f;

    [Header("Step 4B - Thumb Tuck Assist (Opt-in)")]
    [Tooltip("엄지 끝의 손바닥 접근도를 사용해 엄지 뿌리/중간 관절 굽힘을 보완합니다. 기존 동작과 A/B 비교를 위해 기본값은 Off입니다.")]
    public bool enableThumbTuckAssist = false;

    [Tooltip("손바닥 폭 대비 엄지 끝-손바닥 중심 거리. 이 값 이상이면 추가 굽힘이 없습니다.")]
    [Min(0.1f)]
    public float thumbTuckOpenDistance = 1.05f;

    [Tooltip("손바닥 폭 대비 엄지 끝-손바닥 중심 거리. 이 값 이하이면 최대 보조 굽힘을 사용합니다.")]
    [Min(0.05f)]
    public float thumbTuckClosedDistance = 0.40f;

    [Tooltip("엄지가 손바닥에 가까울 때 적용할 엄지 뿌리 굽힘 목표각(가중치/Dead Zone 적용 전).")]
    [Range(0f, 110f)]
    public float thumbTuckRootTargetDegrees = 75f;

    [Tooltip("엄지가 손바닥에 가까울 때 적용할 엄지 중간 관절 굽힘 목표각(가중치/Dead Zone 적용 전).")]
    [Range(0f, 110f)]
    public float thumbTuckMiddleTargetDegrees = 55f;

    [Tooltip("추가 굽힘의 반영 강도. 0이면 기존 각도만 사용합니다.")]
    [Range(0f, 1f)]
    public float thumbTuckAssistStrength = 1f;

    [Header("Smoothing")]
    [Min(0f)]
    public float rotationSpeed = 18f;

    [Header("Tracking Loss")]
    [Min(0f)]
    public float lostTrackingDelay = 0.3f;

    [Tooltip("추적 손실 시 기본 손 자세로 돌아가는 속도. 기존 추적 속도와 별도로 설정합니다.")]
    [Min(0f)]
    public float returnSpeed = 5f;

    [Tooltip("손가락 중간/끝 관절의 입력 각도를 약간 평활화할지 여부")]
    public bool enableAngleSmoothing = false;

    [Range(0f, 40f)]
    public float angleSmoothingSpeed = 24f;

    [System.Serializable]
    public class FingerSettings
    {
        public Vector3 bendAxis = Vector3.forward;

        [Header("MCP Direction")]
        public bool invertMcp = false;

        [Range(0f, 30f)]
        public float deadZone = 8f;
        
        [Range(0f, 1.5f)]
        public float bendMultiplier = 1f;

        [Range(0f, 1.5f)]
        public float mcpWeight = 1f;

        [Header("Thumb Opposition")]
        public Vector3 oppositionAxis = Vector3.up;

        [Range(0f, 90f)]
        public float maxOppositionAngle = 55f;

        [Range(0f, 2f)]
        public float oppositionWeight = 1f;
        public bool invertOpposition = false;
    }

    [Header("Step 4A - Diagnostics and Tracking Freshness")]
    [Tooltip("같은 WebSocket 패킷의 손 데이터를 매 Unity 프레임마다 새 입력으로 처리하지 않습니다. 기본 On 권장.")]
    public bool useFreshHandPackets = true;

    [Tooltip("손가락별 원본 MCP/PIP/DIP 굽힘 각도와 추적 상태를 출력합니다.")]
    public bool logStep4ADiagnostics = true;

    [Range(0.5f, 5f)]
    public float step4ALogIntervalSeconds = 1.5f;

    private float lastProcessedHandPacketTime = -100f;
    private float lastStep4ALogTime = -100f;
    private string leftFingerDiagnostic = "waiting";
    private string rightFingerDiagnostic = "waiting";

    private HandLandmark[] lastLeftPoints;
    private HandLandmark[] lastRightPoints;

    [Header("Left Finger Settings")]
    public FingerSettings[] leftFingerSettings = new FingerSettings[5];

    [Header("Right Finger Settings")]
    public FingerSettings[] rightFingerSettings = new FingerSettings[5];

    private class Finger
    {
        public Transform[] bones = new Transform[3];
        public Quaternion[] rests = new Quaternion[3];
        public int startIndex;
        public bool isThumb;
        public float[] smoothedAngles = new float[3];
        public bool anglesInitialized;
    }

    private Finger[] leftFingers;
    private Finger[] rightFingers;

    private float lastLeftSeen = -100f;
    private float lastRightSeen = -100f;

    private void InitializeFingerSettings()
    {

        if (leftFingerSettings == null || leftFingerSettings.Length != 5)
            Array.Resize(ref leftFingerSettings, 5);
        if (rightFingerSettings == null || rightFingerSettings.Length != 5)
            Array.Resize(ref rightFingerSettings, 5);

        for (int i = 0; i < 5; i++)
        {
            if (leftFingerSettings[i] == null)
                leftFingerSettings[i] = new FingerSettings();

            if (rightFingerSettings[i] == null)
                rightFingerSettings[i] = new FingerSettings();
        }
    }
    private void Start()
    {
        InitializeFingerSettings();
        
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (poseClient == null)
            poseClient = FindObjectOfType<PoseWebSocketClient>();

        if (animator == null || !animator.isHuman)
        {
            Debug.LogError(
                "FingerRetargeter: Humanoid Animator가 필요합니다."
            );
            enabled = false;
            return;
        }

        leftFingers = CreateHand(true);
        rightFingers = CreateHand(false);

        Debug.Log("FingerRetargeter: 양손 초기화 완료");
    }

    private static float ThumbOpposition(HandLandmark[] points)
    {
        Vector3 indexBase = Pos(points[5]);
        Vector3 pinkyBase = Pos(points[17]);
        Vector3 thumbTip = Pos(points[4]);

        Vector3 palmAcross = pinkyBase - indexBase;

        float palmWidth = palmAcross.magnitude;

        if (palmWidth < 1e-6f)
            return 0f;

        Vector3 acrossDirection = palmAcross / palmWidth;

        float position = Vector3.Dot(
            thumbTip - indexBase,
            acrossDirection
        ) / palmWidth;


        const float openPosition = -0.35f;
        const float closedPosition = 0.65f;

        float oppositionRatio = Mathf.InverseLerp(
            openPosition,
            closedPosition,
            position
        );

        return oppositionRatio * 90f;
    }

    private float ThumbTuckRatio(HandLandmark[] points, out float normalizedDistance)
    {
        Vector3 indexBase = Pos(points[5]);
        Vector3 middleBase = Pos(points[9]);
        Vector3 ringBase = Pos(points[13]);
        Vector3 pinkyBase = Pos(points[17]);
        Vector3 palmCenter = (indexBase + middleBase + ringBase + pinkyBase) * 0.25f;
        float palmWidth = Vector3.Distance(indexBase, pinkyBase);
        if (palmWidth < 1e-5f)
        {
            normalizedDistance = -1f;
            return 0f;
        }

        normalizedDistance = Vector3.Distance(Pos(points[4]), palmCenter) / palmWidth;
        float openDistance = Mathf.Max(thumbTuckOpenDistance, thumbTuckClosedDistance + 0.05f);
        float t = Mathf.Clamp01((openDistance - normalizedDistance) / (openDistance - thumbTuckClosedDistance));
        return t * t * (3f - 2f * t); 
    }

    private Finger[] CreateHand(bool left)
    {
        Finger[] fingers = new Finger[5];

        HumanBodyBones[][] boneIds = left
            ? new HumanBodyBones[][]
            {
                new[] { HumanBodyBones.LeftThumbProximal,
                        HumanBodyBones.LeftThumbIntermediate,
                        HumanBodyBones.LeftThumbDistal },
                new[] { HumanBodyBones.LeftIndexProximal,
                        HumanBodyBones.LeftIndexIntermediate,
                        HumanBodyBones.LeftIndexDistal },
                new[] { HumanBodyBones.LeftMiddleProximal,
                        HumanBodyBones.LeftMiddleIntermediate,
                        HumanBodyBones.LeftMiddleDistal },
                new[] { HumanBodyBones.LeftRingProximal,
                        HumanBodyBones.LeftRingIntermediate,
                        HumanBodyBones.LeftRingDistal },
                new[] { HumanBodyBones.LeftLittleProximal,
                        HumanBodyBones.LeftLittleIntermediate,
                        HumanBodyBones.LeftLittleDistal }
            }
            : new HumanBodyBones[][]
            {
                new[] { HumanBodyBones.RightThumbProximal,
                        HumanBodyBones.RightThumbIntermediate,
                        HumanBodyBones.RightThumbDistal },
                new[] { HumanBodyBones.RightIndexProximal,
                        HumanBodyBones.RightIndexIntermediate,
                        HumanBodyBones.RightIndexDistal },
                new[] { HumanBodyBones.RightMiddleProximal,
                        HumanBodyBones.RightMiddleIntermediate,
                        HumanBodyBones.RightMiddleDistal },
                new[] { HumanBodyBones.RightRingProximal,
                        HumanBodyBones.RightRingIntermediate,
                        HumanBodyBones.RightRingDistal },
                new[] { HumanBodyBones.RightLittleProximal,
                        HumanBodyBones.RightLittleIntermediate,
                        HumanBodyBones.RightLittleDistal }
            };

        int[] starts = { 1, 5, 9, 13, 17 };

        for (int f = 0; f < 5; f++)
        {
            Finger finger = new Finger();
            finger.startIndex = starts[f];
            finger.isThumb = f == 0;

            for (int j = 0; j < 3; j++)
            {
                finger.bones[j] =
                    animator.GetBoneTransform(boneIds[f][j]);

                if (finger.bones[j] != null)
                {
                    finger.rests[j] =
                        finger.bones[j].localRotation;
                }
                else
                {
                    Debug.LogWarning(
                        $"Finger bone missing: {boneIds[f][j]}"
                    );
                }
            }

            fingers[f] = finger;
        }

        return fingers;
    }

    private static Vector3 Pos(HandLandmark p)
    {
        return new Vector3(p.x, p.y, p.z);
    }

    private static float JointBend(
        HandLandmark a,
        HandLandmark b,
        HandLandmark c)
    {
        Vector3 ba = Pos(a) - Pos(b);
        Vector3 bc = Pos(c) - Pos(b);

        if (ba.sqrMagnitude < 1e-10f ||
            bc.sqrMagnitude < 1e-10f)
            return 0f;

        return Mathf.Clamp(
            180f - Vector3.Angle(ba, bc),
            0f,
            120f
        );
    }
    private static float ApplyDeadZone(
        float angle,
        float deadZone)
    {
        return Mathf.Max(
            0f,
            angle - deadZone
        );
    }


    private static float BaseBend(
        HandLandmark[] points,
        int baseIndex)
    {
        Vector3 wrist = Pos(points[0]);
        Vector3 indexBase = Pos(points[5]);
        Vector3 middleBase = Pos(points[9]);
        Vector3 pinkyBase = Pos(points[17]);

        Vector3 across = pinkyBase - indexBase;
        Vector3 along = middleBase - wrist;

        Vector3 normal = Vector3.Cross(
            across,
            along
        );

        if (normal.sqrMagnitude < 1e-10f)
            return 0f;

        normal.Normalize();

        Vector3 fingerDirection =
            Pos(points[baseIndex + 1]) -
            Pos(points[baseIndex]);

        if (fingerDirection.sqrMagnitude < 1e-10f)
            return 0f;

        fingerDirection.Normalize();

        float dot = Mathf.Abs(
            Vector3.Dot(fingerDirection, normal)
        );

        float angle = Mathf.Asin(
            Mathf.Clamp01(dot)
        ) * Mathf.Rad2Deg;

        return Mathf.Clamp(angle, 0f, 90f);
    }

    private void LateUpdate()
    {
        if (poseClient == null || leftFingers == null || rightFingers == null)
            return;

        float now = Time.realtimeSinceStartup;
        bool freshPacket = !useFreshHandPackets ||
            (poseClient.LastMessageTime >= 0f &&
             poseClient.LastMessageTime > lastProcessedHandPacketTime);

        if (freshPacket)
            lastProcessedHandPacketTime = poseClient.LastMessageTime;

        HandData[] hands = freshPacket ? poseClient.LatestHands : null;
        if (hands != null)
        {
            foreach (HandData hand in hands)
            {
                if (hand == null || hand.landmarks == null || hand.landmarks.Length != 21)
                    continue;

                bool targetLeft = swapHands
                    ? string.Equals(hand.label, "Right", StringComparison.OrdinalIgnoreCase)
                    : string.Equals(hand.label, "Left", StringComparison.OrdinalIgnoreCase);
                bool targetRight = swapHands
                    ? string.Equals(hand.label, "Left", StringComparison.OrdinalIgnoreCase)
                    : string.Equals(hand.label, "Right", StringComparison.OrdinalIgnoreCase);

                if (targetLeft && enableLeftHand)
                {
                    lastLeftPoints = hand.landmarks;
                    lastLeftSeen = now;
                    if (logStep4ADiagnostics)
                        leftFingerDiagnostic = FormatFingerDiagnostics(hand.landmarks);
                }
                else if (targetRight && enableRightHand)
                {
                    lastRightPoints = hand.landmarks;
                    lastRightSeen = now;
                    if (logStep4ADiagnostics)
                        rightFingerDiagnostic = FormatFingerDiagnostics(hand.landmarks);
                }
            }
        }

        if (enableLeftHand)
        {
            if (lastLeftPoints != null && now - lastLeftSeen <= lostTrackingDelay)
                ApplyHand(lastLeftPoints, leftFingers, leftBendAxis, invertLeft);
            else
            {
                lastLeftPoints = null;
                RestoreHand(leftFingers);
            }
        }

        if (enableRightHand)
        {
            if (lastRightPoints != null && now - lastRightSeen <= lostTrackingDelay)
                ApplyHand(lastRightPoints, rightFingers, rightBendAxis, invertRight);
            else
            {
                lastRightPoints = null;
                RestoreHand(rightFingers);
            }
        }

        if (logStep4ADiagnostics && now - lastStep4ALogTime >= step4ALogIntervalSeconds)
        {
            lastStep4ALogTime = now;
            string leftStatus = !enableLeftHand ? "disabled" :
                (now - lastLeftSeen <= lostTrackingDelay ? leftFingerDiagnostic : "LOST/return-to-rest");
            string rightStatus = !enableRightHand ? "disabled" :
                (now - lastRightSeen <= lostTrackingDelay ? rightFingerDiagnostic : "LOST/return-to-rest");

            string leftTuck = "-";
            string rightTuck = "-";
            if (enableThumbTuckAssist && lastLeftPoints != null && now - lastLeftSeen <= lostTrackingDelay)
            {
                float d;
                leftTuck = ThumbTuckRatio(lastLeftPoints, out d).ToString("F2") + "(d=" + d.ToString("F2") + ")";
            }
            if (enableThumbTuckAssist && lastRightPoints != null && now - lastRightSeen <= lostTrackingDelay)
            {
                float d;
                rightTuck = ThumbTuckRatio(lastRightPoints, out d).ToString("F2") + "(d=" + d.ToString("F2") + ")";
            }
            Debug.Log($"[Finger/Step4B] freshPackets={useFreshHandPackets} tuckAssist={enableThumbTuckAssist} " +
                      $"L: {leftStatus} tuck={leftTuck} | R: {rightStatus} tuck={rightTuck}");
        }
    }

    private static string FormatFingerDiagnostics(HandLandmark[] points)
    {
        int[] starts = { 1, 5, 9, 13, 17 };
        string[] names = { "T", "I", "M", "R", "P" };
        StringBuilder sb = new StringBuilder(160);
        for (int i = 0; i < 5; i++)
        {
            int s = starts[i];
            float mcp = i == 0
                ? JointBend(points[0], points[1], points[2])
                : BaseBend(points, s);
            float pip = i == 0
                ? JointBend(points[1], points[2], points[3])
                : JointBend(points[s], points[s + 1], points[s + 2]);
            float dip = i == 0
                ? JointBend(points[2], points[3], points[4])
                : JointBend(points[s + 1], points[s + 2], points[s + 3]);
            if (i > 0) sb.Append(' ');
            sb.Append(names[i]).Append('(')
              .Append(mcp.ToString("F0")).Append('/')
              .Append(pip.ToString("F0")).Append('/')
              .Append(dip.ToString("F0")).Append(')');
        }
        sb.Append(" opp=").Append(ThumbOpposition(points).ToString("F0"));
        return sb.ToString();
    }

    private void ApplyHand(
        HandLandmark[] points,
        Finger[] fingers,
        Vector3 axis,
        bool invert)
    {
        if (points == null || points.Length != 21)
            return;

        float sign = invert ? -1f : 1f;

        FingerSettings[] handSettings =
            fingers == leftFingers
                ? leftFingerSettings
                : rightFingerSettings;

        for (int f = 0; f < fingers.Length; f++)
        {
            Finger finger = fingers[f];
            FingerSettings settings = handSettings[f];

            if (finger == null || settings == null)
                continue;

            int s = finger.startIndex;

            float baseBend;
            float middleBend;
            float distalBend;

            if (finger.isThumb)
            {
                baseBend = JointBend(
                    points[0], points[1], points[2]
                );

                middleBend = JointBend(
                    points[1], points[2], points[3]
                );

                distalBend = JointBend(
                    points[2], points[3], points[4]
                );
            }
            else
            {
                baseBend = BaseBend(points, s);

                middleBend = JointBend(
                    points[s],
                    points[s + 1],
                    points[s + 2]
                );

                distalBend = JointBend(
                    points[s + 1],
                    points[s + 2],
                    points[s + 3]
                );
            }

            if (finger.isThumb && enableThumbTuckAssist)
            {
                float unusedDistance;
                float tuck = ThumbTuckRatio(points, out unusedDistance) * thumbTuckAssistStrength;
                baseBend = Mathf.Max(baseBend, tuck * thumbTuckRootTargetDegrees);
                middleBend = Mathf.Max(middleBend, tuck * thumbTuckMiddleTargetDegrees);
            }


            baseBend = ApplyDeadZone(
                baseBend,
                settings.deadZone
            );

            middleBend = ApplyDeadZone(
                middleBend,
                settings.deadZone
            );

            distalBend = ApplyDeadZone(
                distalBend,
                settings.deadZone
            );

            float[] angles =
            {
                baseBend * settings.mcpWeight,
                middleBend,
                distalBend
            };

            if (enableAngleSmoothing)
            {
                float angleT = 1f - Mathf.Exp(
                    -Mathf.Max(0f, angleSmoothingSpeed) * Time.deltaTime
                );
                for (int j = 0; j < 3; j++)
                {
                    if (!finger.anglesInitialized)
                        finger.smoothedAngles[j] = angles[j];
                    else
                        finger.smoothedAngles[j] = Mathf.Lerp(
                            finger.smoothedAngles[j], angles[j], angleT
                        );
                    angles[j] = finger.smoothedAngles[j];
                }
                finger.anglesInitialized = true;
            }
            else
            {
                finger.anglesInitialized = false;
            }

            float weight = finger.isThumb
                ? thumbWeight
                : 1f;

            Vector3 fingerAxis = settings.bendAxis;

            if (fingerAxis.sqrMagnitude < 1e-6f)
                continue;

            fingerAxis.Normalize();

            for (int j = 0; j < 3; j++)
            {
                if (finger.bones[j] == null)
                    continue;

                float angle =
                    Mathf.Min(angles[j], maxBendAngle)
                    * sign
                    * (j == 0 && !finger.isThumb && settings.invertMcp ? -1f : 1f)
                    * bendWeight
                    * settings.bendMultiplier
                    * weight;

                if (finger.isThumb && j == 0)
                {
                    float opposition = ThumbOpposition(points);

                    opposition = Mathf.Min(
                        opposition,
                        settings.maxOppositionAngle
                    );
                    opposition *= settings.oppositionWeight;

                    if (settings.invertOpposition)
                        opposition *= -1f;

                    Vector3 oppositionAxis = settings.oppositionAxis;
                    if (oppositionAxis.sqrMagnitude > 1e-6f)
                    {
                        oppositionAxis.Normalize();

                        Quaternion bendRotation =
                            Quaternion.AngleAxis(
                                angle,
                                fingerAxis
                            );
                        Quaternion oppositionRotation =
                            Quaternion.AngleAxis(
                                opposition,
                                oppositionAxis
                            );
                        Quaternion target = 
                            finger.rests[j] * oppositionRotation * bendRotation;

                        float t = 1f - Mathf.Exp(
                            -Mathf.Max(0f, rotationSpeed) * Time.deltaTime
                        );

                        finger.bones[j].localRotation =
                            Quaternion.Slerp(
                                finger.bones[j].localRotation,
                                target,
                                t
                            );
                        continue;
                    }
                }

                ApplyBone(
                    finger.bones[j],
                    finger.rests[j],
                    fingerAxis,
                    angle
                );
            }
        }
    }


    private void ApplyBone(
        Transform bone,
        Quaternion rest,
        Vector3 axis,
        float angle)
    {
        if (bone == null)
            return;

        Quaternion target =
            rest * Quaternion.AngleAxis(angle, axis);

        float t = 1f - Mathf.Exp(
            -Mathf.Max(0f, rotationSpeed) * Time.deltaTime
        );

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            target,
            t
        );
    }

    private void RestoreHand(Finger[] fingers)
    {
        foreach (Finger finger in fingers)
        {

            finger.anglesInitialized = false;
            for (int j = 0; j < 3; j++)
            {
                Transform bone = finger.bones[j];

                if (bone == null)
                    continue;

                float t = 1f - Mathf.Exp(
                    -Mathf.Max(0f, returnSpeed) * Time.deltaTime
                );

                bone.localRotation = Quaternion.Slerp(
                    bone.localRotation,
                    finger.rests[j],
                    t
                );
            }
        }
    }
}

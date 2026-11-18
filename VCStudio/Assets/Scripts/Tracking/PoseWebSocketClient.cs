using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;

public class PoseWebSocketClient : MonoBehaviour
{
    [Header("WebSocket")]
    public string serverUrl = "ws://127.0.0.1:8765";

    [Header("Tracking Timeout")]
    [Min(0.05f)]
    public float trackingTimeout = 0.5f;

    [Header("Pose")]
    public PoseSkeletonVisualizer visualizer;
    public PoseTrackingProcessor trackingProcessor;
    public UpperBodyRetargeter upperBodyRetargeter;

    [Header("Pose Coordinate Normalization (optional)")]
    [Tooltip("Assign CoordinateNormalizer. Empty or disabled = legacy behavior.")]
    public CoordinateNormalizer coordinateNormalizer;


    [Header("Hand Tracking")]
    public bool enableHandTracking = true;
    public bool logHandTracking = false;

    public HandData[] LatestHands { get; private set; }
        = Array.Empty<HandData>();

    public bool HasHandTracking { get; private set; }

    public bool HasLeftHandTracking { get; private set; }
    public bool HasRightHandTracking { get; private set; }

    public float LastHandTime { get; private set; } = -1f;
    public float LastLeftHandTime { get; private set; } = -1f;
    public float LastRightHandTime { get; private set; } = -1f;

    [Header("Face Tracking")]
    public bool enableFaceTracking = true;
    public bool logFaceTracking = false;

    public FaceBlendshape[] LatestFaceBlendshapes
    {
        get;
        private set;
    } = Array.Empty<FaceBlendshape>();

    public bool HasFaceTracking { get; private set; }

    public float LastFaceTime { get; private set; } = -1f;

    [Header("Status Debug")]
    public bool logTrackingStatus = true;

    public bool HasPoseTracking { get; private set; }

    public float LastPoseTime { get; private set; } = -1f;
    public PoseLandmark[] LatestGesturePose { get; private set; }
    public float LastGesturePoseTime { get; private set; } = -1f;

    public bool IsReceiving { get; private set; }
    public bool IsConnected { get; private set; }

    public float LastMessageTime { get; private set; } = -1f;

    private WebSocket websocket;

    private float lastHandLogTime = -1f;
    private float lastFaceLogTime = -1f;

    private bool shuttingDown;

    private async void Start()
    {
        if (coordinateNormalizer == null)
            coordinateNormalizer = GetComponent<CoordinateNormalizer>();

        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () =>
        {
            if (shuttingDown)
                return;

            IsConnected = true;

            Debug.Log(
                "[Tracking] Python WebSocket 연결 성공"
            );
        };

        websocket.OnError += error =>
        {
            if (shuttingDown)
                return;

            Debug.LogError(
                "[Tracking] WebSocket 오류: " + error
            );
        };

        websocket.OnClose += code =>
        {
            if (shuttingDown)
                return;

            IsConnected = false;

            ClearTracking();

            Debug.LogWarning(
                "[Tracking] WebSocket 연결 종료: " + code
            );
        };

        websocket.OnMessage += bytes =>
        {
            if (shuttingDown)
                return;

            string json = Encoding.UTF8.GetString(bytes);

            PoseMessage message;

            try
            {
                message =
                    JsonUtility.FromJson<PoseMessage>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Tracking] JSON 파싱 실패: " + e.Message
                );

                return;
            }

            if (message == null || message.type != "pose")
                return;

            float now = Time.realtimeSinceStartup;

            LastMessageTime = now;
            IsReceiving = true;

            bool poseValid =
                message.landmarks != null &&
                message.landmarks.Length == 33;

            if (poseValid)
            {
                LastPoseTime = now;
                HasPoseTracking = true;

                LatestGesturePose = coordinateNormalizer != null && coordinateNormalizer.isActiveAndEnabled
                    ? coordinateNormalizer.NormalizePose(message.landmarks)
                    : null;
                LastGesturePoseTime = LatestGesturePose != null ? now : -1f;

                if (visualizer != null)
                {
                    visualizer.UpdatePose(
                        message.landmarks
                    );
                }

                PoseLandmark[] processed =
                    trackingProcessor != null
                        ? trackingProcessor.Process(
                            message.landmarks
                        )
                        : message.landmarks;

                if (trackingProcessor != null)
                    processed = PreserveRawVisibility(processed, message.landmarks);

                bool normalized = coordinateNormalizer != null &&
                                  coordinateNormalizer.isActiveAndEnabled;
                PoseLandmark[] poseForRetargeting = normalized
                    ? coordinateNormalizer.NormalizePose(processed)
                    : processed;

                if (upperBodyRetargeter != null && poseForRetargeting != null)
                {
                    upperBodyRetargeter.SetPose(poseForRetargeting, normalized);
                }
            }
            else
            {
                HasPoseTracking = false;

                LatestGesturePose = null;
                LastGesturePoseTime = -1f;
            }

            LatestHands = enableHandTracking
                ? (
                    message.hands ??
                    Array.Empty<HandData>()
                )
                : Array.Empty<HandData>();

            HasLeftHandTracking = false;
            HasRightHandTracking = false;

            foreach (HandData hand in LatestHands)
            {
                if (
                    hand == null ||
                    hand.landmarks == null ||
                    hand.landmarks.Length != 21
                )
                {
                    continue;
                }

                bool avatarIsLeft;
                bool validLabel;
                if (coordinateNormalizer != null &&
                    coordinateNormalizer.isActiveAndEnabled)
                {
                    validLabel = coordinateNormalizer.TryGetAvatarHandSide(
                        hand.label, out avatarIsLeft);
                }
                else
                {

                    validLabel = TryGetRawHandSide(hand.label, out avatarIsLeft);
                }

                if (!validLabel) continue;
                if (avatarIsLeft)
                {
                    HasLeftHandTracking = true;
                    LastLeftHandTime = now;
                }
                else
                {
                    HasRightHandTracking = true;
                    LastRightHandTime = now;
                }
            }

            HasHandTracking =
                HasLeftHandTracking ||
                HasRightHandTracking;

            if (HasHandTracking)
            {
                LastHandTime = now;
            }

            if (
                logHandTracking &&
                now - lastHandLogTime >= 1f
            )
            {
                lastHandLogTime = now;

                Debug.Log(
                    $"[Hand Tracking] " +
                    $"Left: {HasLeftHandTracking} | " +
                    $"Right: {HasRightHandTracking} | " +
                    $"Total: {LatestHands.Length}"
                );
            }

            LatestFaceBlendshapes = enableFaceTracking
                ? (
                    message.faceBlendshapes ??
                    Array.Empty<FaceBlendshape>()
                )
                : Array.Empty<FaceBlendshape>();

            HasFaceTracking =
                LatestFaceBlendshapes.Length > 0;

            if (HasFaceTracking)
            {
                LastFaceTime = now;
            }

            if (
                logFaceTracking &&
                now - lastFaceLogTime >= 1f
            )
            {
                lastFaceLogTime = now;

                Debug.Log(
                    $"[Face Tracking] " +
                    $"Detected: {HasFaceTracking} | " +
                    $"Blendshapes: " +
                    $"{LatestFaceBlendshapes.Length} | " +
                    $"Blink L: " +
                    $"{GetFaceScore("eyeBlinkLeft"):F2} | " +
                    $"Blink R: " +
                    $"{GetFaceScore("eyeBlinkRight"):F2} | " +
                    $"Jaw: " +
                    $"{GetFaceScore("jawOpen"):F2} | " +
                    $"Smile L: " +
                    $"{GetFaceScore("mouthSmileLeft"):F2} | " +
                    $"Smile R: " +
                    $"{GetFaceScore("mouthSmileRight"):F2}"
                );
            }
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception e)
        {
            if (!shuttingDown)
            {
                IsConnected = false;

                ClearTracking();

                Debug.LogError(
                    "[Tracking] 연결 실패: " +
                    e.Message
                );
            }
        }
    }

    private static bool TryGetRawHandSide(string label, out bool isLeft)
    {
        isLeft = false;
        if (string.Equals(label, "Left", StringComparison.OrdinalIgnoreCase))
        {
            isLeft = true;
            return true;
        }
        if (string.Equals(label, "Right", StringComparison.OrdinalIgnoreCase))
        {
            isLeft = false;
            return true;
        }
        return false;
    }

    private static PoseLandmark[] PreserveRawVisibility(PoseLandmark[] processed, PoseLandmark[] raw)
    {
        if (processed == null || processed.Length != 33 ||
            raw == null || raw.Length != 33) return null;

        PoseLandmark[] safe = new PoseLandmark[33];
        for (int i = 0; i < 33; ++i)
        {
            PoseLandmark p = processed[i];
            PoseLandmark r = raw[i];
            if (p == null || r == null) return null;
            float rv = r.visibility;
            float pv = p.visibility;
            if (float.IsNaN(rv) || float.IsInfinity(rv)) rv = 0f;
            if (float.IsNaN(pv) || float.IsInfinity(pv)) pv = 0f;
            safe[i] = new PoseLandmark
            {
                id = i, x = p.x, y = p.y, z = p.z,
                visibility = Mathf.Min(Mathf.Clamp01(rv), Mathf.Clamp01(pv))
            };
        }
        return safe;
    }

    public float GetFaceScore(string blendshapeName)
    {
        if (
            !enableFaceTracking ||
            !HasFaceTracking ||
            string.IsNullOrEmpty(blendshapeName)
        )
        {
            return 0f;
        }

        foreach (
            FaceBlendshape blendshape
            in LatestFaceBlendshapes
        )
        {
            if (
                blendshape != null &&
                blendshape.name == blendshapeName
            )
            {
                return blendshape.score;
            }
        }

        return 0f;
    }


    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif

        if (
            !IsReceiving ||
            LastMessageTime < 0f
        )
        {
            return;
        }

        float elapsed =
            Time.realtimeSinceStartup -
            LastMessageTime;

        if (elapsed > trackingTimeout)
        {
            ClearTracking();

            if (logTrackingStatus)
            {
                Debug.LogWarning(
                    "[Tracking] 데이터 수신 타임아웃"
                );
            }
        }
    }


    private void ClearTracking()
    {
        IsReceiving = false;

        HasPoseTracking = false;
        HasHandTracking = false;

        HasLeftHandTracking = false;
        HasRightHandTracking = false;

        HasFaceTracking = false;

        LatestGesturePose = null;
        LastGesturePoseTime = -1f;

        LatestHands = Array.Empty<HandData>();

        LatestFaceBlendshapes =
            Array.Empty<FaceBlendshape>();
    }

    private async void OnDestroy()
    {
        shuttingDown = true;

        if (websocket != null)
        {
            try
            {
                await websocket.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Tracking] 종료 중 오류: " +
                    e.Message
                );
            }
        }
    }
}
using UnityEngine;
using UniVRM10;


[DefaultExecutionOrder(500)]
public class FaceRetargeter : MonoBehaviour
{
    [Header("References")]
    public PoseWebSocketClient trackingClient;
    public Vrm10Instance vrmInstance;

    [Header("Enable Expressions")]
    public bool enableBlink = true;
    public bool enableMouth = true;
    public bool enableSmile = true;

    [Header("Blink Settings")]
    public bool useSeparateBlink = true;
    [Range(0f, 3f)] public float blinkSensitivity = 1.5f;
    [Range(0f, 1f)] public float blinkThreshold = 0.15f;

    [Header("Mouth Settings")]
    [Range(0f, 3f)] public float mouthSensitivity = 1.5f;
    [Range(0f, 1f)] public float mouthThreshold = 0.08f;

    [Header("Smile Settings")]
    [Range(0f, 3f)] public float smileSensitivity = 1.2f;
    [Range(0f, 1f)] public float smileThreshold = 0.20f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float smoothingSpeed = 18f;
    [Range(0.5f, 3f)] public float blinkSpeedMultiplier = 1.7f;
    [Range(0.5f, 3f)] public float mouthSpeedMultiplier = 1.0f;
    [Range(0.5f, 3f)] public float smileSpeedMultiplier = 0.8f;

    [Header("Independent Eye Tuning (Optional)")]
    [Tooltip("ON uses independent thresholds and gains for each eye.")]
    public bool enablePerEyeBlinkTuning = false;
    [Range(0f, 1f)] public float leftBlinkThreshold = 0.15f;
    [Range(0f, 3f)] public float leftBlinkSensitivity = 1.5f;
    [Range(0f, 1f)] public float rightBlinkThreshold = 0.15f;
    [Range(0f, 3f)] public float rightBlinkSensitivity = 1.5f;

    [Header("Blink Close / Open Response (Optional)")]
    [Tooltip("ON allows faster closing and slower opening independently.")]
    public bool enableAsymmetricBlinkSmoothing = false;
    [Range(0.5f, 4f)] public float blinkCloseSpeedMultiplier = 1.8f;
    [Range(0.5f, 4f)] public float blinkOpenSpeedMultiplier = 1.0f;

    [Header("Expression Response Curves (Optional)")]
    [Tooltip("ON allows customizing the response curves for mouth and smile expressions.")]
    public bool enableFaceResponseCurves = false;
    [Range(0.4f, 2.5f)] public float mouthResponseExponent = 1.0f;
    [Range(0.4f, 2.5f)] public float smileResponseExponent = 1.0f;

    [Header("Diagnostics")]
    public bool logStep5bTargets = false;
    [SerializeField] private float step5bTargetBlinkLeft;
    [SerializeField] private float step5bTargetBlinkRight;
    [SerializeField] private float step5bTargetMouth;
    [SerializeField] private float step5bTargetSmile;

    [Header("Expression Conflict Control")]
    [Tooltip("Reduce Happy while the mouth is wide open. VRM Happy clips may also animate the mouth.")]
    [Range(0f, 1f)] public float smileReductionWhenMouthOpen = 0.55f;
    [Tooltip("Reduce emotion expression weights while the mouth is open.")]
    [Range(0f, 1f)] public float emotionReductionWhenMouthOpen = 0.35f;

    [Header("Tracking Loss")]
    [Min(0f)] public float trackingLossDelay = 0.3f;

    [Header("Fresh Face Data")]
    [Tooltip("Reject cached face data when the last face packet is too old. Expression interpolation still runs every Unity frame.")]
    public bool requireRecentFacePacket = true;
    [Min(0.05f)] public float maxFacePacketAge = 0.35f;

    [Header("Runtime Diagnostics (Read Only)")]
    [SerializeField] private bool step5aFaceDetected;
    [SerializeField] private float step5aFaceAgeSeconds = -1f;
    [SerializeField] private int step5aFacePacketsSeen;

    [Header("Emotion Expressions")]
    [Tooltip("Start with this OFF to tune blink/mouth/smile separately.")]
    public bool enableEmotions = false;
    [Range(0f, 3f)] public float angrySensitivity = 1.2f;
    [Range(0f, 3f)] public float sadSensitivity = 1.0f;
    [Range(0f, 3f)] public float surprisedSensitivity = 1.5f;
    [Range(0f, 1f)] public float emotionThreshold = 0.3f;
    [Range(1f, 40f)] public float emotionSmoothingSpeed = 10f;

    [Header("Debug")]
    public bool debugLog = false;
    public bool debugRawScores = true;
    [Range(0.2f, 5f)] public float debugLogInterval = 1f;

    private float blinkLeft;
    private float blinkRight;
    private float mouthOpen;
    private float smile;
    private float angry;
    private float sad;
    private float surprised;

    private float lastValidFaceTime = -100f;
    private float lastDebugTime = -100f;
    private float lastObservedFacePacketTime = -1f;

    private void Start()
    {
        if (trackingClient == null)
            trackingClient = FindObjectOfType<PoseWebSocketClient>();

        if (vrmInstance == null)
            vrmInstance = GetComponentInChildren<Vrm10Instance>();

        if (trackingClient == null)
            Debug.LogError("PoseWebSocketClient reference is missing.", this);
        if (vrmInstance == null)
            Debug.LogError("Vrm10Instance reference is missing.", this);
    }

    private void Update()
    {
        if (trackingClient == null || vrmInstance == null || vrmInstance.Runtime == null)
            return;

        float facePacketTime = trackingClient.LastFaceTime;
        float faceAge = facePacketTime >= 0f
            ? Mathf.Max(0f, Time.realtimeSinceStartup - facePacketTime)
            : -1f;
        bool recent = !requireRecentFacePacket ||
            (faceAge >= 0f && faceAge <= maxFacePacketAge);
        bool detected = trackingClient.IsReceiving && trackingClient.HasFaceTracking && recent;
        bool freshPacket = detected && facePacketTime > lastObservedFacePacketTime;
        if (freshPacket)
        {
            lastObservedFacePacketTime = facePacketTime;
            step5aFacePacketsSeen++;
        }
        step5aFaceDetected = detected;
        step5aFaceAgeSeconds = faceAge;

        float rawBlinkL = 0f;
        float rawBlinkR = 0f;
        float rawJaw = 0f;
        float rawSmileL = 0f;
        float rawSmileR = 0f;
        float targetBlinkL = 0f;
        float targetBlinkR = 0f;
        float targetMouth = 0f;
        float targetSmile = 0f;
        float targetAngry = 0f;
        float targetSad = 0f;
        float targetSurprised = 0f;

        if (detected)
        {
            lastValidFaceTime = Time.time;

            rawBlinkL = trackingClient.GetFaceScore("eyeBlinkLeft");
            rawBlinkR = trackingClient.GetFaceScore("eyeBlinkRight");
            rawJaw = trackingClient.GetFaceScore("jawOpen");
            rawSmileL = trackingClient.GetFaceScore("mouthSmileLeft");
            rawSmileR = trackingClient.GetFaceScore("mouthSmileRight");

            targetBlinkL = enablePerEyeBlinkTuning
                ? NormalizeScore(rawBlinkL, leftBlinkThreshold, leftBlinkSensitivity)
                : NormalizeScore(rawBlinkL, blinkThreshold, blinkSensitivity);
            targetBlinkR = enablePerEyeBlinkTuning
                ? NormalizeScore(rawBlinkR, rightBlinkThreshold, rightBlinkSensitivity)
                : NormalizeScore(rawBlinkR, blinkThreshold, blinkSensitivity);
            targetMouth = NormalizeScore(rawJaw, mouthThreshold, mouthSensitivity);
            targetSmile = NormalizeScore(
                (rawSmileL + rawSmileR) * 0.5f,
                smileThreshold,
                smileSensitivity
            );

            if (enableFaceResponseCurves)
            {
                targetMouth = Mathf.Pow(targetMouth, mouthResponseExponent);
                targetSmile = Mathf.Pow(targetSmile, smileResponseExponent);
            }

            if (enableEmotions)
            {
                float browDownL = trackingClient.GetFaceScore("browDownLeft");
                float browDownR = trackingClient.GetFaceScore("browDownRight");
                float browInnerUp = trackingClient.GetFaceScore("browInnerUp");
                float eyeWideL = trackingClient.GetFaceScore("eyeWideLeft");
                float eyeWideR = trackingClient.GetFaceScore("eyeWideRight");

                targetAngry = NormalizeScore(
                    (browDownL + browDownR) * 0.5f,
                    emotionThreshold, angrySensitivity
                );
                targetSad = NormalizeScore(browInnerUp, emotionThreshold, sadSensitivity);
                targetSurprised = NormalizeScore(
                    (eyeWideL + eyeWideR) * 0.5f,
                    emotionThreshold, surprisedSensitivity
                );

                if (targetAngry >= targetSad && targetAngry >= targetSurprised)
                {
                    targetSad = 0f;
                    targetSurprised = 0f;
                }
                else if (targetSad >= targetSurprised)
                {
                    targetAngry = 0f;
                    targetSurprised = 0f;
                }
                else
                {
                    targetAngry = 0f;
                    targetSad = 0f;
                }
            }
        }
        else if (Time.time - lastValidFaceTime < trackingLossDelay)
        {

            targetBlinkL = blinkLeft;
            targetBlinkR = blinkRight;
            targetMouth = mouthOpen;
            targetSmile = smile;
            targetAngry = angry;
            targetSad = sad;
            targetSurprised = surprised;
        }

        float blinkT = SmoothFactor(smoothingSpeed * blinkSpeedMultiplier);
        float blinkLeftT = enableAsymmetricBlinkSmoothing
            ? SmoothFactor(smoothingSpeed * blinkSpeedMultiplier *
                (targetBlinkL > blinkLeft ? blinkCloseSpeedMultiplier : blinkOpenSpeedMultiplier))
            : blinkT;
        float blinkRightT = enableAsymmetricBlinkSmoothing
            ? SmoothFactor(smoothingSpeed * blinkSpeedMultiplier *
                (targetBlinkR > blinkRight ? blinkCloseSpeedMultiplier : blinkOpenSpeedMultiplier))
            : blinkT;
        float mouthT = SmoothFactor(smoothingSpeed * mouthSpeedMultiplier);
        float smileT = SmoothFactor(smoothingSpeed * smileSpeedMultiplier);
        float emotionT = SmoothFactor(emotionSmoothingSpeed);

        blinkLeft = Mathf.Lerp(blinkLeft, targetBlinkL, blinkLeftT);
        blinkRight = Mathf.Lerp(blinkRight, targetBlinkR, blinkRightT);
        mouthOpen = Mathf.Lerp(mouthOpen, targetMouth, mouthT);
        smile = Mathf.Lerp(smile, targetSmile, smileT);
        angry = Mathf.Lerp(angry, targetAngry, emotionT);
        sad = Mathf.Lerp(sad, targetSad, emotionT);
        surprised = Mathf.Lerp(surprised, targetSurprised, emotionT);

        step5bTargetBlinkLeft = targetBlinkL;
        step5bTargetBlinkRight = targetBlinkR;
        step5bTargetMouth = targetMouth;
        step5bTargetSmile = targetSmile;

        float appliedSmile = enableSmile
            ? smile * (1f - mouthOpen * smileReductionWhenMouthOpen)
            : 0f;
        float emotionScale = enableEmotions
            ? (1f - mouthOpen * emotionReductionWhenMouthOpen)
            : 0f;

        var expression = vrmInstance.Runtime.Expression;

        if (enableBlink)
        {
            if (useSeparateBlink)
            {
                expression.SetWeight(ExpressionKey.Blink, 0f);
                expression.SetWeight(ExpressionKey.BlinkLeft, blinkLeft);
                expression.SetWeight(ExpressionKey.BlinkRight, blinkRight);
            }
            else
            {
                expression.SetWeight(ExpressionKey.BlinkLeft, 0f);
                expression.SetWeight(ExpressionKey.BlinkRight, 0f);
                expression.SetWeight(ExpressionKey.Blink, (blinkLeft + blinkRight) * 0.5f);
            }
        }
        else
        {
            expression.SetWeight(ExpressionKey.Blink, 0f);
            expression.SetWeight(ExpressionKey.BlinkLeft, 0f);
            expression.SetWeight(ExpressionKey.BlinkRight, 0f);
        }

        expression.SetWeight(ExpressionKey.Aa, enableMouth ? mouthOpen : 0f);
        expression.SetWeight(ExpressionKey.Happy, Mathf.Clamp01(appliedSmile));
        expression.SetWeight(ExpressionKey.Angry, Mathf.Clamp01(angry * emotionScale));
        expression.SetWeight(ExpressionKey.Sad, Mathf.Clamp01(sad * emotionScale));
        expression.SetWeight(ExpressionKey.Surprised, Mathf.Clamp01(surprised * emotionScale));

        if (debugLog && Time.time - lastDebugTime >= debugLogInterval)
        {
            lastDebugTime = Time.time;
            if (debugRawScores)
            {
                Debug.Log(
                    $"[Face/Step5A] detected={detected} fresh={freshPacket} " +
                    $"age={faceAge:F2}s packets={step5aFacePacketsSeen} | " +
                    $"RAW blink L/R={rawBlinkL:F2}/{rawBlinkR:F2}, jaw={rawJaw:F2}, " +
                    $"smile L/R={rawSmileL:F2}/{rawSmileR:F2} | " +
                    $"VRM blink L/R={blinkLeft:F2}/{blinkRight:F2}, " +
                    $"Aa={mouthOpen:F2}, Happy={appliedSmile:F2}", this
                );
            }
            else
            {
                Debug.Log(
                    $"[Face/Step5A] detected={detected} fresh={freshPacket} " +
                    $"age={faceAge:F2}s packets={step5aFacePacketsSeen} | " +
                    $"blink L/R={blinkLeft:F2}/{blinkRight:F2}, " +
                    $"Aa={mouthOpen:F2}, Happy={appliedSmile:F2}, " +
                    $"Angry={angry:F2}, Sad={sad:F2}, Surprised={surprised:F2}", this
                );
            }
            if (logStep5bTargets)
            {
                Debug.Log(
                    $"[Face/Step5B] perEye={enablePerEyeBlinkTuning} asymmetricBlink={enableAsymmetricBlinkSmoothing} " +
                    $"curves={enableFaceResponseCurves} | " +
                    $"targets blink L/R={targetBlinkL:F2}/{targetBlinkR:F2}, " +
                    $"mouth={targetMouth:F2}, smile={targetSmile:F2} | " +
                    $"applied blink L/R={blinkLeft:F2}/{blinkRight:F2}, " +
                    $"Aa={mouthOpen:F2}, Happy={appliedSmile:F2}", this
                );
            }
        }
    }

    private float SmoothFactor(float speed)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * Time.deltaTime);
    }


    private static float NormalizeScore(float raw, float threshold, float sensitivity)
    {
        return Mathf.Clamp01(Mathf.Max(0f, raw - threshold) * sensitivity);
    }

    private void OnDisable()
    {
        ResetExpressions();
        blinkLeft = blinkRight = mouthOpen = smile = 0f;
        angry = sad = surprised = 0f;
        lastValidFaceTime = -100f;
        lastObservedFacePacketTime = -1f;
        step5aFaceDetected = false;
        step5aFaceAgeSeconds = -1f;
        step5aFacePacketsSeen = 0;
        step5bTargetBlinkLeft = step5bTargetBlinkRight = 0f;
        step5bTargetMouth = step5bTargetSmile = 0f;
    }

    private void ResetExpressions()
    {
        if (vrmInstance == null || vrmInstance.Runtime == null)
            return;

        var expression = vrmInstance.Runtime.Expression;
        expression.SetWeight(ExpressionKey.Blink, 0f);
        expression.SetWeight(ExpressionKey.BlinkLeft, 0f);
        expression.SetWeight(ExpressionKey.BlinkRight, 0f);
        expression.SetWeight(ExpressionKey.Aa, 0f);
        expression.SetWeight(ExpressionKey.Happy, 0f);
        expression.SetWeight(ExpressionKey.Angry, 0f);
        expression.SetWeight(ExpressionKey.Sad, 0f);
        expression.SetWeight(ExpressionKey.Surprised, 0f);
    }
}

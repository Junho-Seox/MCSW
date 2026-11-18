
using UnityEngine;

public class PoseTrackingProcessor : MonoBehaviour
{
    [Header("Global Filtering")]
    public bool enableFiltering = true;

    [Header("Tracking Reliability")]
    [Range(0f, 1f)]
    public float minVisibility = 0.5f;

    [Range(0f, 1f)]
    public float recoveryVisibility = 0.65f;

    [Min(0.01f)]
    public float recoveryDuration = 0.25f;

    [Header("Filter Settings")]
    public PoseFilterSettings headSettings =
        new PoseFilterSettings();

    public PoseFilterSettings torsoSettings =
        new PoseFilterSettings();

    public PoseFilterSettings armsSettings =
        new PoseFilterSettings();

    public PoseFilterSettings legsSettings =
        new PoseFilterSettings();

    public PoseFilterSettings feetSettings =
        new PoseFilterSettings();

    private const int LandmarkCount = 33;

    private class LandmarkState
    {
        public bool initialized;
        public bool tracking;

        public Vector3 output;
        public Vector3 recoveryStart;
        public float recoveryElapsed;
    }

    private readonly LandmarkState[] states =
        new LandmarkState[LandmarkCount];

    private readonly OneEuroFilter3D[] filters =
        new OneEuroFilter3D[LandmarkCount];

    private bool previousFilteringState;
    private float previousTime = -1f;

    private void Awake()
    {
        for (int i = 0; i < LandmarkCount; i++)
            states[i] = new LandmarkState();

        RebuildFilters();

        previousFilteringState = enableFiltering;
    }

    private PoseFilterSettings GetSettings(int id)
    {
        if (id <= 10)
            return headSettings;

        if (id == 11 || id == 12 ||
            id == 23 || id == 24)
            return torsoSettings;

        if (id >= 13 && id <= 22)
            return armsSettings;

        if (id >= 25 && id <= 28)
            return legsSettings;

        return feetSettings;
    }

    public void RebuildFilters()
    {
        for (int i = 0; i < LandmarkCount; i++)
        {
            PoseFilterSettings s = GetSettings(i);

            filters[i] = new OneEuroFilter3D(
                s.minCutoff,
                s.beta,
                s.dCutoff
            );
        }

        previousTime = -1f;
    }

    public void ResetTracking()
    {
        for (int i = 0; i < LandmarkCount; i++)
            states[i] = new LandmarkState();

        RebuildFilters();
    }

    private static bool IsFinite(Vector3 v)
    {
        return
            !float.IsNaN(v.x) &&
            !float.IsNaN(v.y) &&
            !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) &&
            !float.IsInfinity(v.y) &&
            !float.IsInfinity(v.z);
    }

    public PoseLandmark[] Process(PoseLandmark[] raw)
    {
        if (raw == null ||
            raw.Length != LandmarkCount)
            return null;

        // 필터 ON/OFF 전환 시 필터 상태 초기화
        if (previousFilteringState != enableFiltering)
        {
            RebuildFilters();
            previousFilteringState = enableFiltering;
        }

        float now = Time.realtimeSinceStartup;

        float dt = previousTime < 0f
            ? 1f / 30f
            : Mathf.Clamp(
                now - previousTime,
                1f / 120f,
                0.2f
            );

        previousTime = now;

        float enterThreshold =
            Mathf.Max(minVisibility, recoveryVisibility);

        PoseLandmark[] processed =
            new PoseLandmark[LandmarkCount];

        for (int i = 0; i < LandmarkCount; i++)
        {
            PoseLandmark p = raw[i];
            LandmarkState state = states[i];

            Vector3 measurement = new Vector3(
                p.x, p.y, p.z
            );

            float threshold = state.tracking
                ? minVisibility
                : enterThreshold;

            bool valid =
                IsFinite(measurement) &&
                !float.IsNaN(p.visibility) &&
                !float.IsInfinity(p.visibility) &&
                p.visibility >= threshold;

            Vector3 output = state.output;

            if (!state.initialized)
            {
                if (valid)
                {
                    state.initialized = true;
                    state.tracking = true;
                    state.output = measurement;

                    // 최초 입력으로 필터 초기화
                    if (enableFiltering)
                    {
                        output = filters[i].Filter(
                            measurement, dt
                        );
                    }
                    else
                    {
                        output = measurement;
                    }

                    state.output = output;
                    state.recoveryStart = output;
                    state.recoveryElapsed = recoveryDuration;
                }
            }
            else if (!valid)
            {
                // 잘못된 측정값을 필터에 전달하지 않음
                state.tracking = false;
                state.recoveryElapsed = 0f;

                // 마지막 처리 좌표 유지
                output = state.output;

                // 재추적 시 필터 내부의 오래된 상태 제거
                filters[i].Reset();
            }
            else
            {
                if (!state.tracking)
                {
                    // 추적 복구 시작
                    state.tracking = true;
                    state.recoveryStart = state.output;
                    state.recoveryElapsed = 0f;
                    filters[i].Reset();
                }

                state.recoveryElapsed =
                    Mathf.Min(
                        state.recoveryElapsed + dt,
                        recoveryDuration
                    );

                float t = Mathf.Clamp01(
                    state.recoveryElapsed /
                    recoveryDuration
                );

                // 복구 구간에서 점진적으로 목표 좌표 반영
                Vector3 recovered =
                    Vector3.Lerp(
                        state.recoveryStart,
                        measurement,
                        t
                    );

                // 복구 완료 후에는 원본 측정값 사용
                if (t >= 1f)
                    recovered = measurement;

                output = enableFiltering
                    ? filters[i].Filter(recovered, dt)
                    : recovered;

                state.output = output;
            }

            // IMPORTANT: The retained position is only a fallback for smooth recovery.
            // It must NOT be reported as a currently tracked landmark.
            // Preserve the source confidence for valid measurements; reject stale ones.
            float outputVisibility = valid
                ? Mathf.Clamp01(p.visibility)
                : 0f;

            processed[i] = new PoseLandmark
            {
                id = p.id,
                x = output.x,
                y = output.y,
                z = output.z,
                visibility = outputVisibility
            };
        }

        return processed;
    }
}

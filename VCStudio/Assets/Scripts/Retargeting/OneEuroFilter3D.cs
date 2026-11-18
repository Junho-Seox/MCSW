
using UnityEngine;

public class OneEuroFilter3D
{
    private float minCutoff;
    private float beta;
    private float dCutoff;

    private Vector3 previousValue;
    private Vector3 previousDerivative;

    private bool initialized;

    public OneEuroFilter3D(
        float minCutoff = 1.0f,
        float beta = 0.007f,
        float dCutoff = 1.0f)
    {
        this.minCutoff = Mathf.Max(0.001f, minCutoff);
        this.beta = Mathf.Max(0f, beta);
        this.dCutoff = Mathf.Max(0.001f, dCutoff);
    }

    private float Alpha(float cutoff, float dt)
    {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }

    public Vector3 Filter(Vector3 value, float dt)
    {
        dt = Mathf.Max(dt, 0.0001f);

        if (!initialized)
        {
            previousValue = value;
            previousDerivative = Vector3.zero;
            initialized = true;
            return value;
        }

        Vector3 derivative =
            (value - previousValue) / dt;

        float derivativeAlpha =
            Alpha(dCutoff, dt);

        Vector3 filteredDerivative =
            Vector3.Lerp(
                previousDerivative,
                derivative,
                derivativeAlpha
            );

        float cutoff =
            minCutoff +
            beta * filteredDerivative.magnitude;

        float alpha = Alpha(cutoff, dt);

        Vector3 filteredValue =
            Vector3.Lerp(
                previousValue,
                value,
                alpha
            );

        previousValue = filteredValue;
        previousDerivative = filteredDerivative;

        return filteredValue;
    }

    public void Reset()
    {
        initialized = false;
        previousValue = Vector3.zero;
        previousDerivative = Vector3.zero;
    }
}

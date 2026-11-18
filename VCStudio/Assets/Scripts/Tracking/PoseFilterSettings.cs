
using System;
using UnityEngine;

[Serializable]
public class PoseFilterSettings
{
    [Min(0.001f)]
    public float minCutoff = 1.0f;

    [Min(0f)]
    public float beta = 0.007f;

    [Min(0.001f)]
    public float dCutoff = 1.0f;
}

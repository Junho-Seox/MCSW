
using System;
using UnityEngine;

[Serializable]
public class PoseLandmark
{
    public int id;
    public float x;
    public float y;
    public float z;
    public float visibility;
}

[Serializable]
public class HandLandmark
{
    public int id;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class HandData
{
    public string label;
    public float score;
    public HandLandmark[] landmarks;
}

[System.Serializable]
public class FaceBlendshape
{
    public string name;
    public float score;
}

[Serializable]
public class PoseMessage
{
    public string type;
    public double timestamp;
    public PoseLandmark[] landmarks;
    public HandData[] hands;
    public FaceBlendshape[] faceBlendshapes;
}



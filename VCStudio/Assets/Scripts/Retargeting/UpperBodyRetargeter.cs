using UnityEngine;

[DefaultExecutionOrder(500)]
public class UpperBodyRetargeter : MonoBehaviour
{
    [Header("Upper Body Modules")]
    public TorsoRetargeter torso;
    public BothArmsRetargeter bothArms;
    public HeadNeckRetargeter headNeck;

    [Header("Calibration Keys")]
    public KeyCode headCalibrationKey = KeyCode.Z;
    public KeyCode torsoCalibrationKey = KeyCode.X;

    [Header("Debug")]
    public bool enableRetargeting = true;

    [Header("Input Frame Freshness")]
    [Tooltip("ON: dispatch only newly received Pose frames. OFF: legacy behavior, re-dispatch the cached frame every Unity LateUpdate.")]
    public bool onlyDispatchNewPose = true;

    private PoseLandmark[] currentPose;
    private bool currentPoseIsNormalized;
    private bool hasNewPose;

    public void SetPose(PoseLandmark[] pose)
    {
        SetPose(pose, false);
    }

    public void SetPose(PoseLandmark[] pose, bool isNormalized)
    {
        if (pose == null || pose.Length != 33)
            return;

        currentPose = pose;
        currentPoseIsNormalized = isNormalized;
        hasNewPose = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(headCalibrationKey))
        {
            if (headNeck != null)
                headNeck.Calibrate();
        }

        if (Input.GetKeyDown(torsoCalibrationKey))
        {
            if (torso != null)
                torso.Calibrate();
        }
    }

    private void LateUpdate()
    {
        if (!enableRetargeting || currentPose == null)
            return;

        if (!onlyDispatchNewPose || hasNewPose)
        {
            hasNewPose = false;

            if (torso != null)
                torso.SetPose(currentPose, currentPoseIsNormalized);

            if (bothArms != null)
                bothArms.SetPose(currentPose, currentPoseIsNormalized);

            if (headNeck != null)
                headNeck.SetPose(currentPose, currentPoseIsNormalized);
        }

        if (torso != null)
            torso.ApplyPose();

        if (bothArms != null)
            bothArms.ApplyPose();

        if (headNeck != null)
            headNeck.ApplyPose();
    }
}

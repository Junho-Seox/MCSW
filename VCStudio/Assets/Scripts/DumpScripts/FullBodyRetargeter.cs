
using UnityEngine;

[DefaultExecutionOrder(100)]
public class FullBodyRetargeter : MonoBehaviour
{
    [Header("Retargeting Modules")]
    public TorsoRetargeter torso;
    public BothArmsRetargeter bothArms;
    public BothLegsRetargeter bothLegs;
    public HeadNeckRetargeter headNeck;
    public FeetRetargeter feet;
    private PoseLandmark[] currentPose;

    public void SetPose(PoseLandmark[] landmarks)
    {
        if (landmarks == null || landmarks.Length != 33)
        {
            currentPose = null;
            return;
        }

        currentPose = landmarks;
    }

    private void LateUpdate()
    {
        if (currentPose == null)
            return;

        if (torso != null)
            torso.SetPose(currentPose);

        if (bothArms != null)
            bothArms.SetPose(currentPose);

        if (bothLegs != null)
        bothLegs.SetPose(currentPose);

        if (headNeck != null)
        headNeck.SetPose(currentPose);

        if (feet != null)
        feet.SetPose(currentPose);

        if (torso != null)
            torso.ApplyPose();

        if (bothArms != null)
            bothArms.ApplyPose();

        if (bothLegs != null)
            bothLegs.ApplyPose();

        if (headNeck != null)
            headNeck.ApplyPose();

        if (feet != null)
            feet.ApplyPose();
    }
    private void Update()
{
    if (Input.GetKeyDown(KeyCode.Z))
    {
        if (headNeck != null)
            headNeck.Calibrate();
    }
    if (Input.GetKeyDown(KeyCode.X))
    {
        if (torso != null)
            torso.Calibrate();
    }
    if (Input.GetKeyDown(KeyCode.C))
    {
        if (feet != null)
            feet.Calibrate();
    }
}
    public void CalibrateAll()
    {
        if (torso != null)
            torso.Calibrate();

        if (headNeck != null)
            headNeck.Calibrate();

        if (feet != null)
            feet.Calibrate();

        Debug.Log("전신 기준 자세 캘리브레이션 요청 완료");
    }
}

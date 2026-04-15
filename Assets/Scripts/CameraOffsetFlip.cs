using UnityEngine;
using Unity.Cinemachine;

public class CameraOffsetFlip : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCam;
  
private Vector3 defaultOffset;
    CinemachineCamera cam;
    CinemachinePositionComposer composer;

    void Start()
    {
        cam = GetComponent<CinemachineCamera>();
        composer = cam.GetComponent<CinemachinePositionComposer>();
        Debug.Log("Offset: " + composer.TargetOffset);
        defaultOffset = composer.TargetOffset;
        

    }

    public void FlipCameraOffset(float facingDirection)
    {
        Debug.Log("Offset: " + composer.TargetOffset);
        if (facingDirection > 0)
        {
            composer.TargetOffset = defaultOffset;
            
        }
        else if (facingDirection < 0 )
        {
            composer.TargetOffset = -defaultOffset;
            
        }
    }

}


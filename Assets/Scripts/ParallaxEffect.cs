using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{

    //NOTE: The order of the backgrounds in the array should be from furthest to closest (i.e. the first element should be the furthest background, and the last element should be the closest background)
    //Use the Z position of the backgrounds to determine the parallax scale (the further back the background, the smaller the parallax scale should be)

    public Transform[] backgrounds; // Array of all the backgrounds to be parallaxed

    private float[] parallaxScales; // The proportion of the camera's movement to move the backgrounds by
    public float smoothing = 1f; // How smooth the parallax effect should be. Make sure to set this above 0
    private Transform cam; // reference to the main cameras transform
    private Vector3 previousCamPos; // the position of the camera in the previous frame
                                    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void Awake()
    {
        // set up the reference to the main camera transform
        cam = Camera.main.transform;
    }
    void Start()
    {
        //cam = Camera.main.transform;
        previousCamPos = cam.position;
        parallaxScales = new float[backgrounds.Length];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            parallaxScales[i] = backgrounds[i].position.z * -1;
        }


    }

    // Update is called once per frame
    void Update()
    {
        //float parallax = (previousCamPos.x - cam.position.x) * parallaxScale;
        for (int i = 0; i < backgrounds.Length; i++)
        {
            float parallax = (previousCamPos.x - cam.position.x) * parallaxScales[i];
            float backgroundTargetPosX = backgrounds[i].position.x + parallax;
            Vector3 backgroundTargetPos = new Vector3(backgroundTargetPosX, backgrounds[i].position.y, backgrounds[i].position.z);
            backgrounds[i].position = Vector3.Lerp(backgrounds[i].position, backgroundTargetPos, smoothing * Time.deltaTime);
            //backgrounds[i].position += Vector3.right * parallax * (i + 1);
        }
        previousCamPos = cam.position;
    }
}

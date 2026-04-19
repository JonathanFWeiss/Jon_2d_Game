using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomTransition : MonoBehaviour
{
    public string sceneToLoad;
    public void changeSceneNow()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Enering new room");
       changeSceneNow();
    }
}
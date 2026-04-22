using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;


public class RoomTransition : MonoBehaviour
{
    public Animator transitionAnim;
    public string sceneToLoad;
    private float transitionTime = 1f; // Duration of the transition animation
    public void ChangeSceneNow()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Enering new room");
        //changeSceneNow();
        StartCoroutine(TransitionCoroutine());
    }

    IEnumerator TransitionCoroutine()
    {
        transitionAnim.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime); // Wait for the animation to finish
        ChangeSceneNow();
    }
}
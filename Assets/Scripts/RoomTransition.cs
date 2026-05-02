using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class RoomTransition : MonoBehaviour
{
    public Animator transitionAnim;
    public string sceneToLoad;

    [Tooltip("Entry point ID to use in the scene being loaded. Leave blank to use that scene's default player position.")]
    public string targetEntryId;

    [SerializeField] private float transitionTime = 1f; // Duration of the transition animation
    private bool isTransitioning;

    public void ChangeSceneNow()
    {
        RoomTransitionData.SetTarget(sceneToLoad, targetEntryId);
        SceneManager.LoadScene(sceneToLoad);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning || !other.CompareTag("Player"))
            return;

        if (string.IsNullOrWhiteSpace(sceneToLoad))
        {
            Debug.LogWarning($"{nameof(RoomTransition)} on {name} has no scene to load.");
            return;
        }

        Debug.Log("Entering new room");
        StartCoroutine(TransitionCoroutine());
    }

    IEnumerator TransitionCoroutine()
    {
        isTransitioning = true;

        if (transitionAnim != null)
        {
            transitionAnim.SetTrigger("Start");
        }

        yield return new WaitForSeconds(transitionTime); // Wait for the animation to finish
        ChangeSceneNow();
    }
}

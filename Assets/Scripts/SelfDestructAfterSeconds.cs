using UnityEngine;

[DisallowMultipleComponent]
public class SelfDestructAfterSeconds : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How many seconds this object stays alive after it starts.")]
    public float secondsUntilDestruction = 5f;

    void Start()
    {
        Destroy(gameObject, Mathf.Max(0f, secondsUntilDestruction));
    }
}

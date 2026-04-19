using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float minimumSpawnUpdateDistance = 20f;

    private GameMaster gameMaster;
    private bool warnedMissingGameMaster;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveGameMaster();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform playerTransform = GetPlayerTransform(other);
        if (playerTransform == null)
            return;

        if (!ResolveGameMaster())
            return;

        gameMaster.TrySetCheckpointRespawnPosition(
            playerTransform.position,
            minimumSpawnUpdateDistance
        );
    }

    private bool ResolveGameMaster()
    {
        if (gameMaster != null)
            return true;

        gameMaster = FindObjectOfType<GameMaster>();
        if (gameMaster != null)
            return true;

        if (!warnedMissingGameMaster)
        {
            warnedMissingGameMaster = true;
            Debug.LogWarning("Checkpoint could not find a GameMaster to update the respawn point.");
        }

        return false;
    }

    private static Transform GetPlayerTransform(Collider2D other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController.transform;

            Hero rigidbodyHero = other.attachedRigidbody.GetComponent<Hero>();
            if (rigidbodyHero != null)
                return rigidbodyHero.transform;
        }

        JonCharacterController playerController = other.GetComponentInParent<JonCharacterController>();
        if (playerController != null)
            return playerController.transform;

        Hero hero = other.GetComponentInParent<Hero>();
        if (hero != null)
            return hero.transform;

        return null;
    }

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}

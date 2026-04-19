using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterHazard : MonoBehaviour
{
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private Vector2 respawnOffset = Vector2.zero;
    [SerializeField] private float retriggerCooldown = 0.1f;

    private readonly Dictionary<JonCharacterController, float> nextAllowedTriggerTimes =
        new Dictionary<JonCharacterController, float>();

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        JonCharacterController playerController = GetPlayerController(other);
        if (playerController == null)
            return;

        if (nextAllowedTriggerTimes.TryGetValue(playerController, out float nextAllowedTime) &&
            Time.time < nextAllowedTime)
        {
            return;
        }

        nextAllowedTriggerTimes[playerController] = Time.time + retriggerCooldown;
        playerController.TeleportToLastGroundedPosition(respawnOffset);

        if (damageAmount > 0)
        {
            PlayerData.RemoveHP(damageAmount);
        }
    }

    private static JonCharacterController GetPlayerController(Collider2D other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController;
        }

        return other.GetComponentInParent<JonCharacterController>();
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

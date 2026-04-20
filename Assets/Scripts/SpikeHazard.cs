using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : MonoBehaviour
{
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private Vector2 respawnOffset = Vector2.zero;
    [SerializeField] private float retriggerCooldown = 0.1f;

    private readonly Dictionary<JonCharacterController, float> nextAllowedDamageTimes =
        new Dictionary<JonCharacterController, float>();

    private void Reset()
    {
        EnsureSolidCollider();
    }

    private void Awake()
    {
        EnsureSolidCollider();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void HandleCollision(Collision2D collision)
    {
        JonCharacterController playerController = GetPlayerController(collision);
        if (playerController == null)
            return;

        if (nextAllowedDamageTimes.TryGetValue(playerController, out float nextAllowedTime) &&
            Time.time < nextAllowedTime)
        {
            return;
        }

        nextAllowedDamageTimes[playerController] = Time.time + retriggerCooldown;
        playerController.TeleportToLastGroundedPosition(respawnOffset);

        if (damageAmount > 0)
        {
            PlayerData.RemoveHP(damageAmount);
        }
    }

    private static JonCharacterController GetPlayerController(Collision2D collision)
    {
        if (collision == null)
            return null;

        if (TryGetPlayerController(collision.rigidbody, out JonCharacterController rigidbodyController))
            return rigidbodyController;

        if (TryGetPlayerController(collision.otherRigidbody, out JonCharacterController otherRigidbodyController))
            return otherRigidbodyController;

        if (TryGetPlayerController(collision.collider, out JonCharacterController colliderController))
            return colliderController;

        if (TryGetPlayerController(collision.otherCollider, out JonCharacterController otherColliderController))
            return otherColliderController;

        return null;
    }

    private static bool TryGetPlayerController(Rigidbody2D body, out JonCharacterController playerController)
    {
        playerController = body != null ? body.GetComponent<JonCharacterController>() : null;
        return playerController != null;
    }

    private static bool TryGetPlayerController(Collider2D collider, out JonCharacterController playerController)
    {
        playerController = collider != null ? collider.GetComponentInParent<JonCharacterController>() : null;
        return playerController != null;
    }

    private void EnsureSolidCollider()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();
        if (hazardCollider != null)
        {
            hazardCollider.isTrigger = false;
        }
    }
}

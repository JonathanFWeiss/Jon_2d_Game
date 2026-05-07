using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : FixedPositionEnemy
{
  
    [SerializeField] private Vector2 respawnOffset = Vector2.zero;
    [SerializeField] private float teleportDelay = 0.5f;
    

    private readonly Dictionary<JonCharacterController, float> nextAllowedDamageTimes =
        new Dictionary<JonCharacterController, float>();

    private void Reset()
    {
        EnsureSolidCollider();
    }

    protected override void Die()
    {
        hp = Mathf.Max(hp, 1);
    }

    protected override void Awake()
    {
        base.Awake();
        contactDamage = 1;
        EnsureSolidCollider();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (ShouldIgnoreCollision(collision))
            return;

        HandleCollision(collision);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        if (ShouldIgnoreCollision(collision))
            return;

        HandleCollision(collision);
    }

    protected virtual bool ShouldIgnoreCollision(Collision2D collision)
    {
        return false;
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

        nextAllowedDamageTimes[playerController] =
            Time.time + Mathf.Max(contactDamageCooldown, teleportDelay);

        PlayerData.RemoveHP(contactDamage);
        StartCoroutine(TeleportAfterDelay(playerController));
    }

    private IEnumerator TeleportAfterDelay(JonCharacterController playerController)
    {
        if (teleportDelay > 0f)
        {
            yield return new WaitForSeconds(teleportDelay);
        }

        if (playerController != null)
        {
            playerController.TeleportToLastGroundedPosition(respawnOffset);
        }
    }

    protected static JonCharacterController GetPlayerController(Collision2D collision)
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

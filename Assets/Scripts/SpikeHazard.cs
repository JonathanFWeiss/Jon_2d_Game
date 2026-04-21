using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : FixedPositionEnemy
{
  
    [SerializeField] private Vector2 respawnOffset = Vector2.zero;
    

    private readonly Dictionary<JonCharacterController, float> nextAllowedDamageTimes =
        new Dictionary<JonCharacterController, float>();

    private void Reset()
    {
        EnsureSolidCollider();
    }

    protected override void Awake()
    {
        base.Awake();
        contactDamage = 1;
        EnsureSolidCollider();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
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

        playerController.TeleportToLastGroundedPosition(respawnOffset);

       
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

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SyrupBallProjectile : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private LayerMask playerMask;
    private int damageAmount;
    private Vector2 pushbackImpulse;
    private bool hasHit;

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    public void Initialize(
        Collider2D[] ownerColliders,
        LayerMask playerLayerMask,
        int contactDamage,
        Vector2 contactPushback,
        float lifetime,
        Vector2 launchVelocity
    )
    {
        rb2d = rb2d != null ? rb2d : GetComponent<Rigidbody2D>();
        playerMask = playerLayerMask;
        damageAmount = contactDamage;
        pushbackImpulse = contactPushback;

        Collider2D projectileCollider = GetComponent<Collider2D>();

        if (projectileCollider != null && ownerColliders != null)
        {
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
                }
            }
        }

        if (rb2d != null)
        {
            rb2d.linearVelocity = launchVelocity;
        }

        Destroy(gameObject, Mathf.Max(0.25f, lifetime));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        HandleHit(collision.gameObject, collision.rigidbody, collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        HandleHit(other.gameObject, other.attachedRigidbody, other);
    }

    private void HandleHit(GameObject hitObject, Rigidbody2D hitRigidbody, Collider2D hitCollider)
    {
        if (hasHit)
            return;

        if (hitObject == null)
        {
            Destroy(gameObject);
            return;
        }

        GameObject rootObject = hitObject.transform.root.gameObject;

        if (IsPlayerObject(rootObject))
        {
            hasHit = true;

            if (damageAmount > 0)
            {
                PlayerData.RemoveHP(damageAmount);
            }

            ApplyPushback(hitRigidbody, hitCollider);
            Destroy(gameObject);
            return;
        }

        if (!hitObject.transform.IsChildOf(transform))
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }

    private bool IsPlayerObject(GameObject obj)
    {
        if (obj == null)
            return false;

        if (obj.CompareTag("Player"))
            return true;

        return ((1 << obj.layer) & playerMask.value) != 0;
    }

    private void ApplyPushback(Rigidbody2D hitRigidbody, Collider2D hitCollider)
    {
        Rigidbody2D playerRb = hitRigidbody;

        if (playerRb == null && hitCollider != null)
        {
            playerRb = hitCollider.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb == null || rb2d == null)
            return;

        Vector2 direction = playerRb.worldCenterOfMass - rb2d.worldCenterOfMass;
        float horizontalDirection = Mathf.Sign(direction.x);

        if (Mathf.Approximately(horizontalDirection, 0f))
        {
            horizontalDirection = Mathf.Sign(rb2d.linearVelocity.x);

            if (Mathf.Approximately(horizontalDirection, 0f))
            {
                horizontalDirection = 1f;
            }
        }

        Vector2 impulse = new Vector2(
            horizontalDirection * pushbackImpulse.x,
            pushbackImpulse.y
        );
        playerRb.linearVelocity = Vector2.zero;
        playerRb.AddForce(impulse, ForceMode2D.Impulse);

        JonCharacterController playerController = playerRb.GetComponent<JonCharacterController>();

        if (playerController == null)
        {
            playerController = playerRb.GetComponentInParent<JonCharacterController>();
        }

        if (playerController != null)
        {
            playerController.StartGettingHit();
        }
    }
}

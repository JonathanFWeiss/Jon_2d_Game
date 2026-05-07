using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SyrupBallProjectile : EnemyBase
{
    private GameObject ownerRoot;
    private bool hasHit;

    protected override void Awake()
    {
        base.Awake();
        moveSpeed = 0f;
    }

    public void Initialize(
        Collider2D[] ownerColliders,
        LayerMask playerLayerMask,
        int contactDamageAmount,
        Vector2 contactPushback,
        float lifetime,
        Vector2 launchVelocity,
        GameObject owner = null
    )
    {
        rb2d = rb2d != null ? rb2d : GetComponent<Rigidbody2D>();
        this.contactDamage = contactDamageAmount;
        contactPushbackImpulse = contactPushback;
        ownerRoot = owner;

        if (playerLayerMask.value != 0)
        {
            this.playerLayerMask = playerLayerMask;
        }

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

        Debug.Log(
            $"{gameObject.name} projectile initialized. Owner: {GetDebugObjectName(ownerRoot)}, " +
            $"Velocity: {launchVelocity}, Lifetime: {Mathf.Max(0.25f, lifetime)}"
        );
        Destroy(gameObject, Mathf.Max(0.25f, lifetime));
    }

    protected override void FixedUpdate()
    {
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
            return;

        HandleHit(collision.gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (hasHit)
            return;

        if (hitObject == null)
        {
            Destroy(gameObject);
            return;
        }

        if (IsOwnerObject(hitObject))
        {
            Debug.Log($"{gameObject.name} ignored owner hit with {GetDebugObjectName(hitObject)}.");
            return;
        }

        TryDamagePlayer(hitObject);
        if (hasHit)
            return;

        if (!hitObject.transform.IsChildOf(transform))
        {
            hasHit = true;
            Debug.Log($"{gameObject.name} hit non-player object {GetDebugObjectName(hitObject)} and will be destroyed.");
            Destroy(gameObject);
        }
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
        if (isDead || hasHit || hitObject == null || IsOwnerObject(hitObject))
            return;

        GameObject rootObject = hitObject.transform.root.gameObject;

        if (!IsPlayerObject(rootObject))
            return;

        hasHit = true;
        lastContactDamageTime = Time.time;
        Debug.Log($"{gameObject.name} hit player object {GetDebugObjectName(hitObject)}.");

        if (contactDamage > 0)
        {
            PlayerData.RemoveHP(contactDamage);
        }

        ApplyContactPushback(hitObject, rootObject);
        Destroy(gameObject);
    }

    private bool IsOwnerObject(GameObject hitObject)
    {
        return ownerRoot != null &&
            hitObject != null &&
            (hitObject == ownerRoot || hitObject.transform.IsChildOf(ownerRoot.transform));
    }

    private static string GetDebugObjectName(GameObject obj)
    {
        return obj != null ? obj.name : "null";
    }
}

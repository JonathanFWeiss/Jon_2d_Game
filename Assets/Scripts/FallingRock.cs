using UnityEngine;

public class FallingRock : GroundStationaryEnemy
{
    [Header("Falling Rock")]
    [Tooltip("How close the player must be before the rock starts falling.")]
    public float activationRadius = 3f;

    [Tooltip("Optional point to measure player distance from. If empty, the rock's transform is used.")]
    public Transform activationCheck;

    [Tooltip("Gravity scale applied after the rock is triggered.")]
    public float fallGravityScale = 5f;

    private const float DamageWindowAfterActivation = 2f;

    protected bool hasActivated = false;
    protected float activationTime = -Mathf.Infinity;

    protected override void Awake()
    {
        base.Awake();
        hp = Mathf.Max(hp, 1);
        coinDropCount = 0;

        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
        }

        Debug.Log("FallingRock Awake");
    }

    protected override void FixedUpdate()
    {
        if (!hasActivated && IsPlayerNearby())
        {
            ActivateFalling();
        }

        base.FixedUpdate();
    }

    public override void TakeDamage(int amount)
    {
        // Falling rocks are a permanent hazard and ignore incoming damage.
    }

    protected override void Die()
    {
        hp = Mathf.Max(hp, 1);
    }

    protected virtual bool IsPlayerNearby()
    {
        Vector2 checkPosition = activationCheck != null
            ? activationCheck.position
            : transform.position;

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(
            checkPosition,
            activationRadius
        );

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (nearbyCollider == null)
                continue;

            GameObject rootObject = nearbyCollider.transform.root.gameObject;

            if (IsPlayerObject(rootObject))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void ActivateFalling()
    {
        hasActivated = true;
        activationTime = Time.time;

        if (rb2d != null)
        {
            rb2d.gravityScale = fallGravityScale;
        }
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
        if (!hasActivated || Time.time > activationTime + DamageWindowAfterActivation)
            return;

        if (rb2d == null)
            return;

        Vector2 currentVelocity = rb2d.linearVelocity;

        if (Mathf.Abs(currentVelocity.y) <= 5f)
            return;

        Debug.Log($"{gameObject.name} is moving with velocity {currentVelocity}, attempting to damage player.");
        base.TryDamagePlayer(hitObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = hasActivated ? Color.red : Color.yellow;

        Vector3 checkPosition = activationCheck != null
            ? activationCheck.position
            : transform.position;

        Gizmos.DrawWireSphere(checkPosition, activationRadius);
    }
}

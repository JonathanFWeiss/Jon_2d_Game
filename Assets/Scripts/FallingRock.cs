using System.Collections;
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

    [Tooltip("How long the rock vibrates before it starts falling.")]
    public float preFallVibrateDuration = 1f;

    [Tooltip("How far the rock moves from its resting position while vibrating.")]
    public float preFallVibrateDistance = 0.05f;

    [Tooltip("How quickly the pre-fall vibration oscillates.")]
    public float preFallVibrateSpeed = 35f;

    private const float DamageWindowAfterActivation = 2f;

    protected bool hasActivated = false;
    protected float activationTime = -Mathf.Infinity;
    protected bool isVibrating = false;
    protected Coroutine preFallVibrateCoroutine;
    protected Vector3 vibrationRestLocalPosition;

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

        if (preFallVibrateDuration <= 0f || preFallVibrateDistance <= 0f)
        {
            BeginFalling();
            return;
        }

        preFallVibrateCoroutine = StartCoroutine(VibrateBeforeFalling());
    }

    protected virtual IEnumerator VibrateBeforeFalling()
    {
        isVibrating = true;
        vibrationRestLocalPosition = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < preFallVibrateDuration)
        {
            elapsed += Time.deltaTime;

            float xOffset = Mathf.Sin(elapsed * preFallVibrateSpeed) * preFallVibrateDistance;
            float yOffset = Mathf.Sin(elapsed * preFallVibrateSpeed * 1.37f) * preFallVibrateDistance * 0.5f;
            transform.localPosition = vibrationRestLocalPosition + new Vector3(xOffset, yOffset, 0f);

            if (rb2d != null)
            {
                rb2d.linearVelocity = Vector2.zero;
            }

            yield return null;
        }

        transform.localPosition = vibrationRestLocalPosition;
        isVibrating = false;
        preFallVibrateCoroutine = null;

        BeginFalling();
    }

    protected virtual void BeginFalling()
    {
        activationTime = Time.time;

        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.gravityScale = fallGravityScale;
        }
    }

    protected override void OnDisable()
    {
        if (preFallVibrateCoroutine != null)
        {
            StopCoroutine(preFallVibrateCoroutine);
            preFallVibrateCoroutine = null;
        }

        if (isVibrating)
        {
            transform.localPosition = vibrationRestLocalPosition;
            isVibrating = false;
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

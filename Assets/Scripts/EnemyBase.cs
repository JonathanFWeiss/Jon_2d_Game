using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public int hp = 3;

    [Header("Movement")]
    public float moveSpeed = 2f;
    [Tooltip("How quickly the enemy accelerates toward its target move speed.")]
    public float moveAcceleration = 20f;
    public bool startFacingRight = true;

    [Header("Contact Damage")]
    [Tooltip("How much HP the player loses when touching this enemy.")]
    public int contactDamage = 1;

    [Tooltip("Cooldown between repeated contact-damage hits.")]
    public float contactDamageCooldown = 1f;

    [Tooltip("Which layers count as the player for contact damage. Defaults to the Player layer.")]
    public LayerMask playerLayerMask = 0;

    [Tooltip("Impulse applied to the player when this enemy deals contact damage.")]
    public Vector2 contactPushbackImpulse = new Vector2(6f, 6f);

    [Header("Drops")]
    [Tooltip("Prefab to spawn on death.")]
    public GameObject coinPrefab;

    [Tooltip("How many coins this enemy drops.")]
    public int coinDropCount = 0;

    [Header("Hit Flash")]
    [Tooltip("How long enemies flash white after taking damage.")]
    public float hitFlashDuration = 0.25f;

    [Tooltip("Tint applied while the enemy is flashing from damage.")]
    public Color hitFlashColor = Color.red;

    protected Rigidbody2D rb2d;
    protected float facingDirection = 1f;
    protected bool isDead = false;
    protected float lastContactDamageTime = -Mathf.Infinity;
    protected SpriteRenderer[] hitFlashRenderers;
    protected Color[] hitFlashOriginalColors;
    protected Coroutine hitFlashCoroutine;
    protected float hitFlashEndTime = -Mathf.Infinity;

    protected virtual void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.freezeRotation = true;

        if (playerLayerMask == 0)
        {
            playerLayerMask = LayerMask.GetMask("Player");
        }

        facingDirection = startFacingRight ? 1f : -1f;
        UpdateSpriteDirection();
        CacheHitFlashRenderers();

//        Debug.Log($"{gameObject.name} remaining {hp}");
        gameObject.layer = LayerMask.NameToLayer("NPCs");
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;
        //Debug.Log($"{gameObject.name} is moving with speed {moveSpeed} in direction {facingDirection}");
        Move();
    }

    protected virtual void Update()
    {
        
        if (!isDead && hp <= 0)
        {
            Die();
        }
    }

    protected virtual void Move()
    {
        float targetVelocityX = facingDirection * moveSpeed;
        float currentVelocityX = rb2d.linearVelocity.x;
        float maxVelocityChange = moveAcceleration * Time.fixedDeltaTime;
        float velocityChange = Mathf.Clamp(
            targetVelocityX - currentVelocityX,
            -maxVelocityChange,
            maxVelocityChange
        );

        rb2d.AddForce(Vector2.right * (velocityChange * rb2d.mass), ForceMode2D.Impulse);
    }

    public virtual void TakeDamage(int amount)
    {
        if (isDead) return;

        hp -= amount;

        if (amount > 0)
        {
            TriggerHitFlash();
        }

        Debug.Log($"{gameObject.name} lost {amount} hp, remaining {hp}");
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other.gameObject);
    }

    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        DropCoins();
        Destroy(gameObject);
    }

    protected virtual void DropCoins()
    {
        if (coinPrefab == null || coinDropCount <= 0)
            return;

        for (int i = 0; i < coinDropCount; i++)
        {
            Instantiate(coinPrefab, transform.position, Quaternion.identity);
        }
    }

    protected virtual void TurnAround()
    {
        facingDirection *= -1f;
        UpdateSpriteDirection();
    }

    protected virtual void TryDamagePlayer(GameObject hitObject)
    {
        if (isDead || contactDamage <= 0 || hitObject == null)
            return;

        if (Time.time < lastContactDamageTime + contactDamageCooldown)
            return;

        GameObject rootObject = hitObject.transform.root.gameObject;

        if (!IsPlayerObject(rootObject))
            return;

        lastContactDamageTime = Time.time;
        PlayerData.RemoveHP(contactDamage);
        Debug.Log($"{gameObject.name} dealt {contactDamage} contact damage to player. Player HP: {PlayerData.HP}");
        ApplyContactPushback(hitObject, rootObject);
    }

    protected virtual bool IsPlayerObject(GameObject obj)
    {
        if (obj == null)
            return false;

        if (obj.CompareTag("Player"))
            return true;

        return ((1 << obj.layer) & playerLayerMask.value) != 0;
    }

    protected virtual void ApplyContactPushback(GameObject hitObject, GameObject playerObject)
    {
        if (hitObject == null && playerObject == null)
            return;

        Rigidbody2D playerRb = null;

        if (hitObject != null)
        {
            playerRb = hitObject.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRb == null)
        {
            playerRb = playerObject.GetComponent<Rigidbody2D>();
        }

        if (playerRb == null)
            return;

        Vector2 direction = playerRb.worldCenterOfMass - rb2d.worldCenterOfMass;
        float horizontalDirection = Mathf.Sign(direction.x);

        if (horizontalDirection == 0f)
        {
            horizontalDirection = facingDirection == 0f ? 1f : facingDirection;
        }

        Vector2 impulse = new Vector2(
            horizontalDirection * contactPushbackImpulse.x,
            contactPushbackImpulse.y
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

        Debug.Log($"Applying pushback impulse {impulse} to player from {gameObject.name}");
    }

    protected virtual void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }

    protected virtual void CacheHitFlashRenderers()
    {
        hitFlashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    protected virtual void TriggerHitFlash()
    {
        if (hitFlashDuration <= 0f)
            return;

        if (hitFlashRenderers == null || hitFlashRenderers.Length == 0)
        {
            CacheHitFlashRenderers();
        }

        if (hitFlashRenderers == null || hitFlashRenderers.Length == 0)
            return;

        hitFlashEndTime = Time.time + hitFlashDuration;

        if (hitFlashCoroutine == null)
        {
            CacheHitFlashOriginalColors();
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
            return;
        }

        SetHitFlashColor();
    }

    protected virtual void CacheHitFlashOriginalColors()
    {
        hitFlashOriginalColors = new Color[hitFlashRenderers.Length];

        for (int i = 0; i < hitFlashRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = hitFlashRenderers[i];
            hitFlashOriginalColors[i] = spriteRenderer != null ? spriteRenderer.color : Color.white;
        }
    }

    protected virtual IEnumerator HitFlashRoutine()
    {
        SetHitFlashColor();

        while (Time.time < hitFlashEndTime)
        {
            yield return null;
        }

        RestoreHitFlashColors();
        hitFlashCoroutine = null;
    }

    protected virtual void SetHitFlashColor()
    {
        foreach (SpriteRenderer spriteRenderer in hitFlashRenderers)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = hitFlashColor;
                Debug.Log($"Setting hit flash color {hitFlashColor} on {spriteRenderer.gameObject.name} for {gameObject.name}");
            }
        }
    }

    protected virtual void RestoreHitFlashColors()
    {
        if (hitFlashRenderers == null || hitFlashOriginalColors == null)
            return;

        for (int i = 0; i < hitFlashRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = hitFlashRenderers[i];
            if (spriteRenderer != null)
            {
                spriteRenderer.color = hitFlashOriginalColors[i];
            }
        }

        hitFlashOriginalColors = null;
    }
}

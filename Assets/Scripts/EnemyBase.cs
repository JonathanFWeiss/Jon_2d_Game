using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public int hp = 3;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public bool startFacingRight = true;

    [Header("Drops")]
    [Tooltip("Prefab to spawn on death.")]
    public GameObject coinPrefab;

    [Tooltip("How many coins this enemy drops.")]
    public int coinDropCount = 0;

    protected Rigidbody2D rb2d;
    protected float facingDirection = 1f;
    protected bool isDead = false;

    protected virtual void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.freezeRotation = true;

        facingDirection = startFacingRight ? 1f : -1f;
        UpdateSpriteDirection();

        Debug.Log($"{gameObject.name} remaining {hp}");
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;
        Debug.Log($"{gameObject.name} is moving with speed {moveSpeed} in direction {facingDirection}");
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
        Vector2 velocity = rb2d.linearVelocity;
        velocity.x = facingDirection * moveSpeed;
        rb2d.linearVelocity = velocity;
    }

    public virtual void TakeDamage(int amount)
    {
        if (isDead) return;

        hp -= amount;
        Debug.Log($"{gameObject.name} lost {amount} hp, remaining {hp}");
    }

    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;
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

    protected virtual void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }
}
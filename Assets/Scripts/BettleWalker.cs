using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BettleWalker : MonoBehaviour
{
    [Tooltip("Movement speed while walking.")]
    public float walkSpeed = 2f;

    [Tooltip("Which layers count as obstacles that make the beetle turn.")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("Start walking to the right when the scene begins.")]
    public bool startFacingRight = true;

    [Tooltip("Starting hit points for the beetle.")]
    public int hp = 3;

    [Tooltip("Prefab to spawn when the beetle is destroyed.")]
    public GameObject coinPrefab;

    Rigidbody2D rb2d;
    float facingDirection = 1f;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.freezeRotation = true;
        rb2d.gravityScale = rb2d.gravityScale; // preserve configured gravity
        facingDirection = startFacingRight ? 1f : -1f;
        UpdateSpriteDirection();
        Debug.Log($"{gameObject.name}  remaining {hp}");
    }

    void FixedUpdate()
    {
        Vector2 velocity = rb2d.linearVelocity;
        velocity.x = facingDirection * walkSpeed;
        rb2d.linearVelocity = velocity;

    }

    void Update()
    {
        if (hp <= 0)
        {{Destroy(gameObject);
            if (coinPrefab != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Instantiate(coinPrefab, transform.position, Quaternion.identity);
                }
            }
        }}
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsObstacle(collision.gameObject) && HasHorizontalContact(collision))
        {
            TurnAround();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsObstacle(other.gameObject))
        {
            TurnAround();
        }
    }

    bool IsObstacle(GameObject obj)
    {
        return (obstacleMask.value & (1 << obj.layer)) != 0;
    }

    bool HasHorizontalContact(Collision2D collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
                return true;
        }
        return false;
    }

    void TurnAround()
    {
        facingDirection *= -1f;
        UpdateSpriteDirection();
    }

    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log($"{gameObject.name} lost {amount} hp, remaining {hp}");

    }

    void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CrowController : MonoBehaviour
{
    [Tooltip("Base horizontal speed during hops.")]
    public float hopSpeed = 2f;

    [Tooltip("Upward impulse applied on each hop.")]
    public float hopForce = 3.5f;

    [Tooltip("Minimum and maximum seconds between hops.")]
    public Vector2 hopIntervalRange = new Vector2(0.5f, 1.2f);

    [Tooltip("Chance for the crow to dash instead of a normal hop.")]
    [Range(0f, 1f)]
    public float dashChance = 0.2f;

    [Tooltip("Horizontal dash speed.")]
    public float dashSpeed = 6f;

    [Tooltip("Duration of the dash in seconds.")]
    public float dashDuration = 0.25f;

    [Tooltip("Which direction the crow initially moves in.")]
    public bool startFacingRight = true;

    [Tooltip("Starting hit points for the crow.")]
    public int hp = 3;

    [Tooltip("Which layers count as obstacles that make the crow turn.")]
    public LayerMask obstacleMask = ~0;

    [Tooltip("Which layers count as ground for hop resets and edge detection.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Position used to check if the crow is on the ground.")]
    public Transform groundCheck;

    [Tooltip("Position used to check if there's ground ahead of the crow.")]
    public Transform edgeCheck;

    [Tooltip("Horizontal distance ahead of the crow to probe for ground.")]
    public float edgeCheckForward = 0.2f;

    [Tooltip("Distance to look downward for ground on the edge check.")]
    public float edgeCheckDistance = 0.25f;

    [Tooltip("Radius for the ground check.")]
    public float groundCheckRadius = 0.1f;
    [Tooltip("Prefab to spawn when the beetle is destroyed.")]
    public GameObject coinPrefab;


    Rigidbody2D rb2d;
    float hopTimer;
    bool isDashing;
    float dashTimer;
    float facingDirection = 1f;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        rb2d.freezeRotation = true;
        facingDirection = startFacingRight ? 1f : -1f;
        UpdateSpriteDirection();
        ResetHopTimer();
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            Vector2 velocity = rb2d.linearVelocity;
            velocity.x = facingDirection * dashSpeed;
            rb2d.linearVelocity = velocity;

            if (dashTimer <= 0f)
                EndDash();

            return;
        }

        if (IsGrounded() && IsNearEdge())
        {
            TurnAround();
        }

        hopTimer -= Time.fixedDeltaTime;
        if (hopTimer <= 0f && IsGrounded())
        {
            PerformHop();
            ResetHopTimer();
        }
        if (hp <= 0)
        {
            Destroy(gameObject);
            if (coinPrefab != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Instantiate(coinPrefab, transform.position, Quaternion.identity);
                }
            }

        }
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

    bool IsNearEdge()
    {
        if (edgeCheck == null)
            return false;

        Vector2 origin = (Vector2)edgeCheck.position + Vector2.right * facingDirection * edgeCheckForward;
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundMask).collider == null;
    }

    void TurnAround()
    {
        facingDirection *= -1f;
        UpdateSpriteDirection();
    }

    void PerformHop()
    {
        Vector2 velocity = rb2d.linearVelocity;
        velocity.x = facingDirection * hopSpeed;
        velocity.y = hopForce;
        rb2d.linearVelocity = velocity;

        if (Random.value < dashChance)
        {
            StartDash();
        }
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
    }

    void EndDash()
    {
        isDashing = false;
    }

    void ResetHopTimer()
    {
        hopTimer = Random.Range(hopIntervalRange.x, hopIntervalRange.y);
    }

    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log($"{gameObject.name} lost {amount} hp, remaining {hp}");

    }

    bool IsGrounded()
    {
        if (groundCheck == null)
            return false;

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask) != null;
    }

    void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }
}

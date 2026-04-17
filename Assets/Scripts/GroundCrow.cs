using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class GroundCrow : GroundWalkerEnemy
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

    [FormerlySerializedAs("groundMask")]
    [Tooltip("Which layers count as ground for hop resets and edge detection.")]
    public LayerMask crowGroundMask = ~0;

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

    [Tooltip("Legacy crow coin drop count preserved for existing prefabs.")]
    public int CoinsToDrop = 3;

    private float hopTimer;
    private bool isDashing;
    private float dashTimer;

    protected override void Awake()
    {
        if (ledgeCheck == null && edgeCheck != null)
        {
            ledgeCheck = edgeCheck;
        }

        if (turnAtLedges && ledgeCheck == null)
        {
            turnAtLedges = false;
        }

        if (obstacleMask == 0)
        {
            obstacleMask = ~0;
        }

        if (coinDropCount <= 0)
        {
            coinDropCount = CoinsToDrop;
        }

        moveSpeed = 0f;
        moveAcceleration = 0f;

        base.Awake();

        if (edgeCheck == null)
        {
            edgeCheck = ledgeCheck;
        }

        ResetHopTimer();
    }

    protected override void InitializeGroundMask()
    {
        if (crowGroundMask == 0)
        {
            base.InitializeGroundMask();
            return;
        }

        base.groundMask = crowGroundMask;

        if (obstacleMask == 0)
        {
            obstacleMask = crowGroundMask;
        }
    }

    protected override void UpdateLedgeCheckPosition()
    {
        if (edgeCheck != null && ledgeCheck == edgeCheck)
        {
            return;
        }

        base.UpdateLedgeCheckPosition();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;

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

        if (turnAtLedges && IsGrounded() && IsNearEdge())
        {
            TurnAround();
        }

        hopTimer -= Time.fixedDeltaTime;
        if (hopTimer <= 0f && IsGrounded())
        {
            PerformHop();
            ResetHopTimer();
        }
    }

    protected override void Move()
    {
        // GroundCrow uses its own hop/dash movement instead of EnemyBase's walk acceleration.
    }

    protected virtual bool IsNearEdge()
    {
        if (edgeCheck == null)
            return false;

        Vector2 origin = (Vector2)edgeCheck.position + Vector2.right * facingDirection * edgeCheckForward;
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, crowGroundMask).collider == null;
    }

    protected virtual void PerformHop()
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

    protected virtual void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
    }

    protected virtual void EndDash()
    {
        isDashing = false;
    }

    protected virtual void ResetHopTimer()
    {
        hopTimer = Random.Range(hopIntervalRange.x, hopIntervalRange.y);
    }

    protected virtual bool IsGrounded()
    {
        if (groundCheck == null)
            return false;

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, crowGroundMask) != null;
    }
}

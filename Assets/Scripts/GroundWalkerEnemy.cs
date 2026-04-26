using UnityEngine;

public class GroundWalkerEnemy : EnemyBase
{
    [Header("Ground Walker")]
    [Tooltip("Optional override for what counts as a wall/obstacle. If left at 0, Ground layer is used.")]
    public LayerMask obstacleMask = 0;

    

    [Tooltip("Vertical offset below the enemy for the ledge check.")]
    public float ledgeCheckY = -0.5f;
    public float ledgeCheckX = 1;

    [Tooltip("Extra horizontal padding past the collider edge for the wall check.")]
    public float wallCheckX = 0.05f;

    [Tooltip("Vertical offset from the enemy center for the wall check.")]
    public float wallCheckY = 0f;

    

    [Tooltip("Turn around when there is no ground ahead.")]
    public bool turnAtLedges = true;

    [Tooltip("Turn around when colliding with a wall.")]
    public bool turnAtWalls = true;

    [Tooltip("Minimum time in seconds between turn-arounds.")]
    public float turnAroundCooldown = 1f;

    protected int groundLayerIndex = -1;
    public LayerMask groundMask;
    protected float nextTurnAroundTime = float.NegativeInfinity;

    [SerializeField] private float ledgeCheckDistance = 1f;
    [SerializeField] private float wallCheckDistance = 0.25f;
    private Collider2D wallCheckCollider;
    


    protected override void Awake()
    {
        base.Awake();

        wallCheckCollider = GetComponent<Collider2D>();
        InitializeGroundMask();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;

        bool shouldTurnAround = turnAtWalls && IsWallAhead();
        string turnAroundReason = "wall";

        if (!shouldTurnAround && turnAtLedges && IsLedgeAhead())
        {
            shouldTurnAround = true;
            turnAroundReason = "ledge";
        }

        if (shouldTurnAround)
        {
            float previousFacingDirection = facingDirection;
            TurnAround();

            if (!Mathf.Approximately(previousFacingDirection, facingDirection))
            {
                Debug.Log($"{gameObject.name} turned around at {turnAroundReason}");
            }
        }

        base.FixedUpdate();
    }

   

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (!turnAtWalls || isDead) return;

        if (IsObstacle(collision.gameObject) && HasHorizontalContact(collision))
        {
            TurnAround();
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);

        if (!turnAtWalls || isDead) return;

        if (IsObstacle(other.gameObject))
        {
            TurnAround();
        }
    }

    protected virtual void InitializeGroundMask()
    {
        groundLayerIndex = LayerMask.NameToLayer("Ground");

        if (groundMask == 0)
        {
            if (groundLayerIndex == -1)
            {
                Debug.LogWarning(
                    $"{gameObject.name}: No layer named 'Ground' exists. " +
                    "Create a Ground layer in Unity for ledge checks."
                );

                return;
            }

            groundMask = 1 << groundLayerIndex;
        }

        if (obstacleMask == 0)
        {
            obstacleMask = groundMask;
        }
    }

   



   

    protected bool IsLedgeAhead()
    {
        Vector2 origin = GetLedgeCheckOrigin(facingDirection);
        RaycastHit2D groundHit = Physics2D.Raycast(
            origin,
            Vector2.down,
            ledgeCheckDistance,
            groundMask
        );

        
        return groundHit.collider == null;
    }

    protected bool IsWallAhead()
    {
        Vector2 origin = GetWallCheckOrigin(facingDirection);
        Vector2 direction = Vector2.right * facingDirection;
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction,
            wallCheckDistance,
            obstacleMask
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    protected Vector2 GetLedgeCheckOrigin(float direction)
    {
        return (Vector2)transform.position + new Vector2(ledgeCheckX * direction, ledgeCheckY);
    }

    protected Vector2 GetWallCheckOrigin(float direction)
    {
        Collider2D checkCollider = wallCheckCollider != null
            ? wallCheckCollider
            : GetComponent<Collider2D>();

        if (checkCollider == null)
        {
            return (Vector2)transform.position + new Vector2(wallCheckX * direction, wallCheckY);
        }

        Bounds bounds = checkCollider.bounds;
        float edgeX = direction >= 0f ? bounds.max.x : bounds.min.x;
        return new Vector2(edgeX + wallCheckX * direction, bounds.center.y + wallCheckY);
    }

    protected bool IsObstacle(GameObject obj)
    {
        return ((1 << obj.layer) & obstacleMask.value) != 0;
    }

    protected bool HasHorizontalContact(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > 0.5f)
                return true;
        }

        return false;
    }

    protected override void TurnAround()
    {
        if (Time.time < nextTurnAroundTime)
            return;

        nextTurnAroundTime = Time.time + Mathf.Max(0f, turnAroundCooldown);
        base.TurnAround();
        
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        DrawLedgeCheckGizmo();
        Gizmos.color = Color.red;
        DrawWallCheckGizmo();
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        DrawLedgeCheckGizmo();
        Gizmos.color = Color.red;
        DrawWallCheckGizmo();
    }

    private void DrawLedgeCheckGizmo()
    {
        float previewFacingDirection = Application.isPlaying
            ? facingDirection
            : startFacingRight ? 1f : -1f;

        Vector3 start = GetLedgeCheckOrigin(previewFacingDirection);
        Gizmos.DrawLine(start, start + Vector3.down * ledgeCheckDistance);
    }

    private void DrawWallCheckGizmo()
    {
        float previewFacingDirection = Application.isPlaying
            ? facingDirection
            : startFacingRight ? 1f : -1f;

        Vector3 start = GetWallCheckOrigin(previewFacingDirection);
        Gizmos.DrawLine(start, start + Vector3.right * previewFacingDirection * wallCheckDistance);
    }
}

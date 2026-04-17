using UnityEngine;

public class GroundWalkerEnemy : EnemyBase
{
    [Header("Ground Walker")]
    [Tooltip("Optional override for what counts as a wall/obstacle. If left at 0, Ground layer is used.")]
    public LayerMask obstacleMask = 0;

    [Tooltip("Optional manually assigned ledge check. If empty, one is created automatically.")]
    public Transform ledgeCheck;

    [Tooltip("Horizontal distance in front of the enemy for the ledge check.")]
    public float ledgeCheckX = 1f;

    [Tooltip("Vertical offset below the enemy for the ledge check.")]
    public float ledgeCheckY = -0.5f;

    [Tooltip("Radius of the ledge ground check.")]
    public float ledgeCheckRadius = 0.1f;

    [Tooltip("Turn around when there is no ground ahead.")]
    public bool turnAtLedges = true;

    [Tooltip("Turn around when colliding with a wall.")]
    public bool turnAtWalls = true;

    protected int groundLayerIndex = -1;
    protected LayerMask groundMask;

    protected override void Awake()
    {
        base.Awake();

        InitializeGroundMask();
        EnsureLedgeCheckExists();
        UpdateLedgeCheckPosition();
    }

    protected override void FixedUpdate()
    {
        if (isDead) return;

        if (turnAtLedges && IsLedgeAhead())
        {
            TurnAround();
            Debug.Log($"{gameObject.name} turned around at ledge");
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

        if (groundLayerIndex == -1)
        {
            Debug.LogWarning(
                $"{gameObject.name}: No layer named 'Ground' exists. " +
                "Create a Ground layer in Unity for ledge and wall checks."
            );

            groundMask = 0;
            return;
        }

        groundMask = 1 << groundLayerIndex;

        if (obstacleMask == 0)
        {
            obstacleMask = groundMask;
        }
    }

    protected virtual void EnsureLedgeCheckExists()
    {
        if (ledgeCheck != null)
        {
            
            return;
        }

        Transform existing = transform.Find("LedgeCheck");
        if (existing != null)
        {
            ledgeCheck = existing;
            return;
        }

        GameObject ledgeCheckObject = new GameObject("LedgeCheck");
        ledgeCheckObject.transform.SetParent(transform, true);
        ledgeCheckObject.transform.localScale = Vector3.one;
        ledgeCheck = ledgeCheckObject.transform;

        
    }



    protected virtual void UpdateLedgeCheckPosition()
    {
        if (ledgeCheck == null)
            return;

        Vector3 localOffset = new Vector3(
            Mathf.Abs(ledgeCheckX),
            ledgeCheckY,
            0f
        );

        if (ledgeCheck.parent == transform)
        {
            // The enemy already flips by changing its own X scale, so the child
            // should keep a positive local X and let the parent mirror it.
            ledgeCheck.localPosition = localOffset;
            return;
        }

        ledgeCheck.position = transform.position + new Vector3(
            Mathf.Abs(ledgeCheckX) * facingDirection,
            ledgeCheckY,
            0f
        );
    }

    protected bool IsLedgeAhead()
    {
        if (ledgeCheck == null || groundMask == 0)
            return false;

        bool groundAhead = Physics2D.OverlapCircle(
            ledgeCheck.position,
            ledgeCheckRadius,
            groundMask
        );
        //Debug.Log($"{gameObject.name} ledge check at {ledgeCheck.position} found ground: {groundAhead}");

        return !groundAhead;
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
        base.TurnAround();
        UpdateLedgeCheckPosition();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (ledgeCheck != null)
        {
            Gizmos.DrawWireSphere(ledgeCheck.position, ledgeCheckRadius);
        }
        else
        {
            float previewFacing = startFacingRight ? 1f : -1f;
            Vector3 previewPos = transform.position + new Vector3(
                Mathf.Abs(ledgeCheckX) * previewFacing,
                ledgeCheckY,
                0f
            );

            Gizmos.DrawWireSphere(previewPos, ledgeCheckRadius);
        }
    }
}

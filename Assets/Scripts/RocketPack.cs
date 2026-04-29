using UnityEngine;

public class RocketPack : FlyingEnemy
{
    private enum RocketPackState
    {
        Idle,
        MoveAwayFromGround,
        AimAtPlayer,
        Charge,
        RandomMove,
        RetreatUp
    }

    [Header("Rocket Pack Timing")]
    [Tooltip("How long the enemy idles before checking for the player.")]
    public float idleDuration = 2f;

    [Tooltip("How long the enemy spends turning toward the player before charging.")]
    public float aimDuration = 1f;

    [Tooltip("How long the enemy moves randomly before checking line of sight again.")]
    public float randomMoveDuration = 2f;

    [Tooltip("How long the enemy flies upward after a charge impact.")]
    public float upwardRetreatDuration = 1f;

    [Header("Rocket Pack Movement")]
    [Tooltip("How far the enemy moves away from the closest ground structure before aiming at the player.")]
    public float groundAvoidanceDistance = 1f;

    [Tooltip("How long the enemy takes to move away from the closest ground structure before aiming.")]
    public float groundAvoidanceDuration = 0.5f;

    [Tooltip("Speed used while wandering in a random direction.")]
    public float randomMoveSpeed = 3f;

    [Tooltip("How quickly the enemy accelerates during its charge.")]
    public float chargeAcceleration = 40f;

    [Tooltip("Maximum speed during the charge.")]
    public float maxChargeSpeed = 14f;

    [Tooltip("Fallback time before leaving charge if no player or ground collision happens. Set to 0 to charge indefinitely.")]
    public float maxChargeDuration = 5f;

    [Tooltip("Fraction of the ceiling ray distance to travel upward after charging.")]
    [Range(0f, 1f)]
    public float ceilingRetreatFraction = 0.75f;

    [Tooltip("Distance to fly upward when no ceiling is found.")]
    public float fallbackCeilingDistance = 4f;

    [Header("Rocket Pack Raycasts")]
    [Tooltip("Optional transform to use as the line-of-sight ray origin. If empty, the rigidbody center is used.")]
    public Transform raycastOrigin;

    [Tooltip("Layers that count as ground or ceiling. Defaults to the Ground layer.")]
    public LayerMask groundMask = 0;

    [Tooltip("Layers that block line of sight to the player. Defaults to the ground mask.")]
    public LayerMask lineOfSightBlockerMask = 0;

    [Tooltip("Maximum distance to check for a ceiling above the enemy.")]
    public float ceilingRaycastDistance = 12f;

    [Tooltip("Degrees added after aligning the enemy's local up vector to its flight direction.")]
    public float spriteForwardAngleOffset = 0f;

    private RocketPackState currentState;
    private Transform playerTransform;
    private Collider2D bodyCollider;
    private float stateStartTime;
    private float stateEndTime;
    private float nextPlayerSearchTime = float.NegativeInfinity;
    private Vector2 chargeDirection = Vector2.right;
    private Vector2 randomMoveDirection = Vector2.right;
    private Vector2 groundAvoidanceStartPosition;
    private Vector2 groundAvoidanceTargetPosition;
    private Vector2 retreatStartPosition;
    private Vector2 retreatTargetPosition;
    private Quaternion aimStartRotation;
    private Quaternion aimTargetRotation;

    private void Reset()
    {
        moveSpeed = 0f;
    }

    protected override void Awake()
    {
        base.Awake();

        bodyCollider = GetComponent<Collider2D>();
        InitializeMasks();
        EnterIdle();
    }

    protected override void Move()
    {
        switch (currentState)
        {
            case RocketPackState.Idle:
                UpdateIdle();
                break;
            case RocketPackState.MoveAwayFromGround:
                UpdateMoveAwayFromGround();
                break;
            case RocketPackState.AimAtPlayer:
                UpdateAimAtPlayer();
                break;
            case RocketPackState.Charge:
                UpdateCharge();
                break;
            case RocketPackState.RandomMove:
                UpdateRandomMove();
                break;
            case RocketPackState.RetreatUp:
                UpdateRetreatUp();
                break;
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        TryFinishChargeFromHit(collision.gameObject);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        base.OnCollisionStay2D(collision);
        TryFinishChargeFromHit(collision.gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);
        TryFinishChargeFromHit(other.gameObject);
    }

    protected override void OnTriggerStay2D(Collider2D other)
    {
        base.OnTriggerStay2D(other);
        TryFinishChargeFromHit(other.gameObject);
    }

    protected override void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void InitializeMasks()
    {
        if (groundMask == 0)
        {
            int groundLayerIndex = LayerMask.NameToLayer("Ground");

            if (groundLayerIndex == -1)
            {
                Debug.LogWarning($"{gameObject.name}: No layer named 'Ground' exists for RocketPack raycasts.");
            }
            else
            {
                groundMask = 1 << groundLayerIndex;
            }
        }

        if (lineOfSightBlockerMask == 0)
        {
            lineOfSightBlockerMask = groundMask;
        }
    }

    private void EnterIdle()
    {
        currentState = RocketPackState.Idle;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0f, idleDuration);
        rb2d.linearVelocity = Vector2.zero;
    }

    private void UpdateIdle()
    {
        ApplyIdleBob();

        if (Time.time < stateEndTime)
            return;

        CheckPlayerLineOfSight();
    }

    private void CheckPlayerLineOfSight()
    {
        if (CanRaycastToPlayer(out Vector2 directionToPlayer))
        {
            if (TryEnterMoveAwayFromGround())
                return;

            EnterAimAtPlayer(directionToPlayer);
            return;
        }

        EnterRandomMove();
    }

    private bool TryEnterMoveAwayFromGround()
    {
        if (!TryGetDirectionAwayFromClosestGround(out Vector2 awayDirection))
            return false;

        currentState = RocketPackState.MoveAwayFromGround;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, groundAvoidanceDuration);
        groundAvoidanceStartPosition = rb2d.position;
        groundAvoidanceTargetPosition = groundAvoidanceStartPosition
            + awayDirection * Mathf.Max(0f, groundAvoidanceDistance);
        rb2d.linearVelocity = Vector2.zero;
        return true;
    }

    private void UpdateMoveAwayFromGround()
    {
        float moveTime = Mathf.Max(0.01f, groundAvoidanceDuration);
        float t = Mathf.Clamp01((Time.time - stateStartTime) / moveTime);
        Vector2 nextPosition = Vector2.Lerp(
            groundAvoidanceStartPosition,
            groundAvoidanceTargetPosition,
            Mathf.SmoothStep(0f, 1f, t)
        );

        rb2d.MovePosition(nextPosition);
        rb2d.linearVelocity = Vector2.zero;

        if (t >= 1f)
        {
            AimAfterGroundAvoidance();
        }
    }

    private void AimAfterGroundAvoidance()
    {
        if (CanRaycastToPlayer(out Vector2 directionToPlayer))
        {
            EnterAimAtPlayer(directionToPlayer);
            return;
        }

        EnterRandomMove();
    }

    private void EnterAimAtPlayer(Vector2 directionToPlayer)
    {
        currentState = RocketPackState.AimAtPlayer;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, aimDuration);
        chargeDirection = directionToPlayer.normalized;
        aimStartRotation = transform.rotation;
        aimTargetRotation = GetRotationForDirection(chargeDirection);
        rb2d.linearVelocity = Vector2.zero;
    }

    private void UpdateAimAtPlayer()
    {
        rb2d.linearVelocity = Vector2.zero;

        float aimTime = Mathf.Max(0.01f, aimDuration);
        float t = Mathf.Clamp01((Time.time - stateStartTime) / aimTime);
        transform.rotation = Quaternion.Slerp(aimStartRotation, aimTargetRotation, Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f)
        {
            EnterCharge();
        }
    }

    private void EnterCharge()
    {
        currentState = RocketPackState.Charge;
        stateStartTime = Time.time;
        stateEndTime = maxChargeDuration > 0f
            ? Time.time + maxChargeDuration
            : float.PositiveInfinity;
        chargeDirection = transform.up.normalized;
        rb2d.linearVelocity = Vector2.zero;
    }

    private void UpdateCharge()
    {
        rb2d.linearVelocity += chargeDirection * chargeAcceleration * Time.fixedDeltaTime;

        if (rb2d.linearVelocity.magnitude > maxChargeSpeed)
        {
            rb2d.linearVelocity = rb2d.linearVelocity.normalized * maxChargeSpeed;
        }

        if (Time.time >= stateEndTime)
        {
            EnterRetreatUp();
        }
    }

    private void EnterRandomMove()
    {
        currentState = RocketPackState.RandomMove;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0f, randomMoveDuration);
        randomMoveDirection = Random.insideUnitCircle.normalized;

        if (randomMoveDirection == Vector2.zero)
        {
            randomMoveDirection = Vector2.right;
        }

        rb2d.linearVelocity = Vector2.zero;
    }

    private void UpdateRandomMove()
    {
        Vector2 targetVelocity = randomMoveDirection * randomMoveSpeed;
        rb2d.linearVelocity = Vector2.MoveTowards(
            rb2d.linearVelocity,
            targetVelocity,
            moveAcceleration * Time.fixedDeltaTime
        );

        if (Time.time >= stateEndTime)
        {
            rb2d.linearVelocity = Vector2.zero;
            CheckPlayerLineOfSight();
        }
    }

    private void EnterRetreatUp()
    {
        currentState = RocketPackState.RetreatUp;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, upwardRetreatDuration);
        retreatStartPosition = rb2d.position;
        retreatTargetPosition = retreatStartPosition + Vector2.up * GetUpwardRetreatDistance();
        transform.rotation = GetRotationForDirection(Vector2.up);
        rb2d.linearVelocity = Vector2.zero;
    }

    private void UpdateRetreatUp()
    {
        float retreatTime = Mathf.Max(0.01f, upwardRetreatDuration);
        float t = Mathf.Clamp01((Time.time - stateStartTime) / retreatTime);
        Vector2 nextPosition = Vector2.Lerp(retreatStartPosition, retreatTargetPosition, Mathf.SmoothStep(0f, 1f, t));

        rb2d.MovePosition(nextPosition);
        rb2d.linearVelocity = Vector2.zero;

        if (t >= 1f)
        {
            EnterIdle();
        }
    }

    private bool CanRaycastToPlayer(out Vector2 directionToPlayer)
    {
        directionToPlayer = Vector2.zero;
        ResolvePlayerTransform();

        if (playerTransform == null)
            return false;

        Vector2 origin = GetRaycastOrigin();
        Vector2 target = GetPlayerTargetPosition();
        Vector2 offset = target - origin;
        float distanceToPlayer = offset.magnitude;

        if (distanceToPlayer <= 0.01f)
        {
            directionToPlayer = Vector2.right;
            return true;
        }

        directionToPlayer = offset / distanceToPlayer;

        if (lineOfSightBlockerMask == 0)
            return true;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            directionToPlayer,
            distanceToPlayer,
            lineOfSightBlockerMask
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null)
            return;

        if (Time.time < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.time + 0.5f;

        JonCharacterController jonCharacter = FindAnyObjectByType<JonCharacterController>();

        if (jonCharacter != null)
        {
            playerTransform = jonCharacter.transform;
            return;
        }

        Hero hero = FindAnyObjectByType<Hero>();

        if (hero != null)
        {
            playerTransform = hero.transform;
        }
    }

    private float GetUpwardRetreatDistance()
    {
        float ceilingDistance = fallbackCeilingDistance;

        if (groundMask != 0)
        {
            Vector2 origin = GetCeilingRaycastOrigin();
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.up,
                Mathf.Max(0f, ceilingRaycastDistance),
                groundMask
            );

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    continue;

                ceilingDistance = hit.distance;
                break;
            }
        }

        return Mathf.Max(0f, ceilingDistance * ceilingRetreatFraction);
    }

    private void TryFinishChargeFromHit(GameObject hitObject)
    {
        if (currentState != RocketPackState.Charge || hitObject == null)
            return;

        GameObject rootObject = hitObject.transform.root.gameObject;

        if (IsPlayerObject(rootObject) || IsGroundObject(hitObject))
        {
            EnterRetreatUp();
        }
    }

    private bool IsGroundObject(GameObject obj)
    {
        return obj != null && ((1 << obj.layer) & groundMask.value) != 0;
    }

    private bool TryGetDirectionAwayFromClosestGround(out Vector2 awayDirection)
    {
        awayDirection = Vector2.zero;

        if (groundMask == 0)
            return false;

        Collider2D[] groundColliders = FindObjectsByType<Collider2D>();
        Vector2 origin = GetRaycastOrigin();
        Vector2 closestPoint = Vector2.zero;
        Bounds closestBounds = default;
        bool hasClosestGround = false;
        float closestDistanceSqr = float.PositiveInfinity;

        foreach (Collider2D groundCollider in groundColliders)
        {
            if (groundCollider == null || !groundCollider.enabled)
                continue;

            if (groundCollider.transform.IsChildOf(transform))
                continue;

            if (((1 << groundCollider.gameObject.layer) & groundMask.value) == 0)
                continue;

            Vector2 point = groundCollider.ClosestPoint(origin);
            float distanceSqr = (origin - point).sqrMagnitude;

            if (distanceSqr >= closestDistanceSqr)
                continue;

            hasClosestGround = true;
            closestDistanceSqr = distanceSqr;
            closestPoint = point;
            closestBounds = groundCollider.bounds;
        }

        if (!hasClosestGround)
            return false;

        awayDirection = origin - closestPoint;

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = origin - (Vector2)closestBounds.center;
        }

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = Vector2.up;
        }

        awayDirection.Normalize();
        return true;
    }

    private Vector2 GetRaycastOrigin()
    {
        if (raycastOrigin != null)
        {
            return raycastOrigin.position;
        }

        if (rb2d != null)
        {
            return rb2d.worldCenterOfMass;
        }

        return transform.position;
    }

    private Vector2 GetPlayerTargetPosition()
    {
        Rigidbody2D playerRb = playerTransform.GetComponentInParent<Rigidbody2D>();

        if (playerRb != null)
        {
            return playerRb.worldCenterOfMass;
        }

        Collider2D playerCollider = playerTransform.GetComponentInChildren<Collider2D>();

        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        return playerTransform.position;
    }

    private Vector2 GetCeilingRaycastOrigin()
    {
        Vector2 origin = GetRaycastOrigin();

        if (bodyCollider != null)
        {
            origin.y = bodyCollider.bounds.max.y;
        }

        return origin;
    }

    private Quaternion GetRotationForDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
        {
            direction = Vector2.right;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f + spriteForwardAngleOffset;
        return Quaternion.Euler(0f, 0f, angle);
    }
}

using UnityEngine;

public class RocketPack : FlyingEnemy
{
    private enum RocketPackState
    {
        Idle,
        MoveAwayFromGround,
        AimAtPlayer,
        Charge,
        GroundCollisionRecovery,
        HitStun,
        RandomMove,
        TurnUpright,
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

    [Tooltip("How long the enemy takes to turn upright before flying upward after a charge.")]
    public float uprightTurnDuration = 0.5f;

    [Tooltip("How long movement is interrupted after taking damage so attack knockback can move the enemy.")]
    public float hitStunDuration = 0.35f;

    [Tooltip("How long the enemy moves away after touching ground geometry.")]
    public float groundCollisionRecoveryDuration = 1f;

    [Tooltip("How fast the enemy moves away from a ground collision point.")]
    public float groundCollisionRecoverySpeed = 3f;

    [Tooltip("How far to probe for ground while sanity-checking escape directions.")]
    public float groundDirectionProbeDistance = 2f;

    [Header("Rocket Pack Movement")]
    [Tooltip("How far the enemy moves away from the closest ground structure before aiming at the player.")]
    public float groundAvoidanceDistance = 1f;

    [Tooltip("How long the enemy takes to move away from the closest ground structure before aiming.")]
    public float groundAvoidanceDuration = 0.5f;

    [Tooltip("Speed used while wandering in a random direction.")]
    public float randomMoveSpeed = 1f;

    [Tooltip("How quickly the enemy accelerates during its charge.")]
    public float chargeAcceleration = 20f;

    [Tooltip("Maximum speed during the charge.")]
    public float maxChargeSpeed = 10f;

    [Tooltip("Fallback time before leaving charge if no player or ground collision happens. Set to 0 to charge indefinitely.")]
    public float maxChargeDuration = 5f;

    [Tooltip("Fraction of the ceiling ray distance to travel upward after charging.")]
    [Range(0f, 1f)]
    public float ceilingRetreatFraction = 0.5f;

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

    [Header("Rocket Pack Particles")]
    [Tooltip("Prefab template to spawn for the rocket trail at runtime.")]
    public ParticleSystem rocketTrailParticlePrefab;

    [Tooltip("Optional live scene instance. Leave empty when using a prefab template.")]
    public ParticleSystem rocketTrailParticles;

    [Tooltip("Optional transform used as the rocket trail emission origin. If empty, this enemy's transform is used.")]
    public Transform particleOrigin;

    [Tooltip("Minimum Rigidbody2D speed required before the rocket trail emits.")]
    public float rocketTrailMinSpeed = 0.1f;

    [Tooltip("How many rocket trail particles are emitted per second while moving.")]
    public float rocketTrailParticlesPerSecond = 36f;

    [Tooltip("How long each rocket trail particle lives.")]
    public float rocketTrailStartLifetime = 0.32f;

    [Tooltip("Base size for each rocket trail particle.")]
    public float rocketTrailStartSize = 0.18f;

    [Tooltip("Tint applied to emitted rocket trail particles.")]
    public Color rocketTrailColor = new Color(1f, 0.65f, 0.22f, 0.65f);

    private RocketPackState currentState;
    private Transform playerTransform;
    private Collider2D bodyCollider;
    private float stateStartTime;
    private float stateEndTime;
    private float nextPlayerSearchTime = float.NegativeInfinity;
    private Vector2 chargeDirection = Vector2.right;
    private Vector2 randomMoveDirection = Vector2.right;
    private Vector2 groundCollisionRecoveryDirection = Vector2.up;
    private Vector2 groundAvoidanceTargetPosition;
    private Vector2 retreatTargetPosition;
    private Quaternion aimStartRotation;
    private Quaternion aimTargetRotation;
    private Quaternion uprightStartRotation;
    private Quaternion uprightTargetRotation;
    private float rocketTrailEmissionAccumulator;

    private void Reset()
    {
        moveSpeed = 0f;
        particleOrigin = transform;
    }

    protected override void Awake()
    {
        base.Awake();

        bodyCollider = GetComponent<Collider2D>();
        SetupRocketTrailParticles();
        InitializeMasks();
        EnterIdle();
    }

    protected override void FixedUpdate()
    {
        if (isDead)
        {
            StopRocketTrailEffect();
            return;
        }

        base.FixedUpdate();
        UpdateRocketTrailEffect();
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
            case RocketPackState.GroundCollisionRecovery:
                UpdateGroundCollisionRecovery();
                break;
            case RocketPackState.HitStun:
                UpdateHitStun();
                break;
            case RocketPackState.RandomMove:
                UpdateRandomMove();
                break;
            case RocketPackState.TurnUpright:
                UpdateTurnUpright();
                break;
            case RocketPackState.RetreatUp:
                UpdateRetreatUp();
                break;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopRocketTrailEffect();
    }

    public override void TakeDamage(int amount)
    {
        bool shouldInterruptMovement = amount > 0 && !isDead;

        base.TakeDamage(amount);

        if (!shouldInterruptMovement || isDead)
            return;

        EnterHitStun();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        if (TryStartGroundCollisionRecovery(collision))
            return;

        TryFinishChargeFromHit(collision.gameObject);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        base.OnCollisionStay2D(collision);

        if (TryStartGroundCollisionRecovery(collision))
            return;

        TryFinishChargeFromHit(collision.gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);

        if (TryStartGroundCollisionRecovery(other))
            return;

        TryFinishChargeFromHit(other.gameObject);
    }

    protected override void OnTriggerStay2D(Collider2D other)
    {
        base.OnTriggerStay2D(other);

        if (TryStartGroundCollisionRecovery(other))
            return;

        TryFinishChargeFromHit(other.gameObject);
    }

    protected override void UpdateSpriteDirection()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    protected override string GetMovementDebugStateName()
    {
        return currentState.ToString();
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

    private void SetupRocketTrailParticles()
    {
        ParticleSystem rocketTrailTemplate = null;
        if (rocketTrailParticles != null && !rocketTrailParticles.gameObject.scene.IsValid())
        {
            rocketTrailTemplate = rocketTrailParticles;
            rocketTrailParticles = null;
        }

        Transform emitterParent = GetRocketTrailParent();

        if (rocketTrailParticles == null)
        {
            rocketTrailTemplate = rocketTrailTemplate != null ? rocketTrailTemplate : rocketTrailParticlePrefab;
            if (rocketTrailTemplate != null)
            {
                rocketTrailParticles = Instantiate(rocketTrailTemplate, emitterParent);
                rocketTrailParticles.name = rocketTrailTemplate.name;
                rocketTrailParticles.transform.localPosition = Vector3.zero;
            }
            else
            {
                GameObject trailObject = new GameObject("Rocket Trail Particles");
                trailObject.transform.SetParent(emitterParent, false);
                trailObject.transform.localPosition = Vector3.zero;
                rocketTrailParticles = trailObject.AddComponent<ParticleSystem>();
            }
        }
        else
        {
            if (rocketTrailParticles.transform.parent != emitterParent)
            {
                rocketTrailParticles.transform.SetParent(emitterParent, false);
            }

            rocketTrailParticles.transform.localPosition = Vector3.zero;
        }

        ParticleSystem.MainModule main = rocketTrailParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = rocketTrailStartLifetime;
        main.startSize = rocketTrailStartSize;
        main.startColor = rocketTrailColor;
        main.gravityModifier = 0.05f;

        ParticleSystem.EmissionModule emission = rocketTrailParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = rocketTrailParticles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = rocketTrailParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(rocketTrailColor, 0f),
                new GradientColorKey(rocketTrailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(rocketTrailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        ParticleSystemRenderer renderer = rocketTrailParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "Foreground";
            renderer.sortingOrder = 2;
        }

        rocketTrailParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateRocketTrailEffect()
    {
        if (!ShouldPlayRocketTrailEffect(out Vector2 velocity))
        {
            StopRocketTrailEffect();
            return;
        }

        if (!rocketTrailParticles.isPlaying)
        {
            rocketTrailParticles.Play();
        }

        rocketTrailEmissionAccumulator += rocketTrailParticlesPerSecond * Time.fixedDeltaTime;

        int particleCount = Mathf.Min(6, Mathf.FloorToInt(rocketTrailEmissionAccumulator));
        if (particleCount <= 0)
        {
            return;
        }

        rocketTrailEmissionAccumulator -= particleCount;

        for (int i = 0; i < particleCount; i++)
        {
            EmitRocketTrailParticle(velocity);
        }
    }

    private bool ShouldPlayRocketTrailEffect(out Vector2 velocity)
    {
        velocity = rb2d != null ? rb2d.linearVelocity : Vector2.zero;

        return rocketTrailParticles != null &&
            velocity.sqrMagnitude >= rocketTrailMinSpeed * rocketTrailMinSpeed;
    }

    private void EmitRocketTrailParticle(Vector2 velocity)
    {
        Vector2 movementDirection = velocity.normalized;
        Vector2 sideDirection = new Vector2(-movementDirection.y, movementDirection.x);
        Vector3 emitPosition = GetRocketTrailOriginPosition();
        emitPosition += (Vector3)(sideDirection * Random.Range(-0.05f, 0.05f));
        emitPosition += (Vector3)(-movementDirection * Random.Range(0f, 0.08f));

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = emitPosition,
            velocity = new Vector3(
                -movementDirection.x * Random.Range(0.8f, 2.2f) + sideDirection.x * Random.Range(-0.35f, 0.35f),
                -movementDirection.y * Random.Range(0.8f, 2.2f) + sideDirection.y * Random.Range(-0.35f, 0.35f),
                0f
            ),
            startLifetime = Random.Range(rocketTrailStartLifetime * 0.75f, rocketTrailStartLifetime * 1.25f),
            startSize = Random.Range(rocketTrailStartSize * 0.75f, rocketTrailStartSize * 1.35f),
            startColor = rocketTrailColor,
            rotation = Random.Range(0f, 360f)
        };

        rocketTrailParticles.Emit(emitParams, 1);
    }

    private void StopRocketTrailEffect()
    {
        rocketTrailEmissionAccumulator = 0f;

        if (rocketTrailParticles != null && rocketTrailParticles.isPlaying)
        {
            rocketTrailParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private Transform GetRocketTrailParent()
    {
        return particleOrigin != null ? particleOrigin : transform;
    }

    private Vector3 GetRocketTrailOriginPosition()
    {
        return particleOrigin != null ? particleOrigin.position : transform.position;
    }

    private void EnterIdle()
    {
        currentState = RocketPackState.Idle;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0f, idleDuration);
        SetHoverCenterToCurrentPosition();
        ApplyStopImpulse();
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
        groundAvoidanceTargetPosition = rb2d.position
            + awayDirection * Mathf.Max(0f, groundAvoidanceDistance);
        ApplyStopImpulse();
        return true;
    }

    private void UpdateMoveAwayFromGround()
    {
        if (Time.time >= stateEndTime)
        {
            AimAfterGroundAvoidance();
            return;
        }

        ApplyForceTowardTimedPosition(
            groundAvoidanceTargetPosition,
            stateEndTime - Time.time,
            moveAcceleration
        );
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
        ApplyStopImpulse();
    }

    private void UpdateAimAtPlayer()
    {
        ApplyBrakingForce();

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
        ApplyStopImpulse();
    }

    private void UpdateCharge()
    {
        ApplyVelocitySteering(chargeDirection * Mathf.Max(0f, maxChargeSpeed), chargeAcceleration);

        if (Time.time >= stateEndTime)
        {
            EnterTurnUpright();
        }
    }

    private void EnterGroundCollisionRecovery(Vector2 collisionPoint)
    {
        currentState = RocketPackState.GroundCollisionRecovery;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, groundCollisionRecoveryDuration);
        groundCollisionRecoveryDirection = GetDirectionAwayFromPoint(collisionPoint);
        ApplyStopImpulse();
        ApplyVelocitySteering(
            groundCollisionRecoveryDirection * Mathf.Max(0f, groundCollisionRecoverySpeed),
            moveAcceleration
        );
    }

    private void UpdateGroundCollisionRecovery()
    {
        if (Time.time >= stateEndTime)
        {
            EnterIdle();
            return;
        }

        ApplyVelocitySteering(
            groundCollisionRecoveryDirection * Mathf.Max(0f, groundCollisionRecoverySpeed),
            moveAcceleration
        );
    }

    private void EnterHitStun()
    {
        currentState = RocketPackState.HitStun;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, hitStunDuration);
        ApplyStopImpulse();
    }

    private void UpdateHitStun()
    {
        if (Time.time >= stateEndTime)
        {
            EnterIdle();
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

        ApplyStopImpulse();
    }

    private void UpdateRandomMove()
    {
        if (Time.time >= stateEndTime)
        {
            CheckPlayerLineOfSight();
            return;
        }

        ApplyVelocitySteering(randomMoveDirection * Mathf.Max(0f, randomMoveSpeed), moveAcceleration);
    }

    private void EnterRetreatUp()
    {
        currentState = RocketPackState.RetreatUp;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, upwardRetreatDuration);
        retreatTargetPosition = rb2d.position + Vector2.up * GetUpwardRetreatDistance();
        ApplyStopImpulse();
    }

    private void EnterTurnUpright()
    {
        currentState = RocketPackState.TurnUpright;
        stateStartTime = Time.time;
        stateEndTime = Time.time + Mathf.Max(0.01f, uprightTurnDuration);
        uprightStartRotation = transform.rotation;
        uprightTargetRotation = GetRotationForDirection(Vector2.up);
        ApplyStopImpulse();
    }

    private void UpdateTurnUpright()
    {
        ApplyBrakingForce();

        float turnTime = Mathf.Max(0.01f, uprightTurnDuration);
        float t = Mathf.Clamp01((Time.time - stateStartTime) / turnTime);
        transform.rotation = Quaternion.Slerp(
            uprightStartRotation,
            uprightTargetRotation,
            Mathf.SmoothStep(0f, 1f, t)
        );

        if (t >= 1f)
        {
            transform.rotation = uprightTargetRotation;
            EnterRetreatUp();
        }
    }

    private void UpdateRetreatUp()
    {
        if (Time.time >= stateEndTime)
        {
            EnterIdle();
            return;
        }

        ApplyForceTowardTimedPosition(
            retreatTargetPosition,
            stateEndTime - Time.time,
            moveAcceleration
        );
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

        if (IsPlayerObject(rootObject))
        {
            EnterTurnUpright();
        }
    }

    private bool IsGroundObject(GameObject obj)
    {
        return obj != null && ((1 << obj.layer) & groundMask.value) != 0;
    }

    private bool TryStartGroundCollisionRecovery(Collision2D collision)
    {
        if (collision == null || currentState == RocketPackState.GroundCollisionRecovery)
            return false;

        if (!IsGroundObject(collision.gameObject))
            return false;

        EnterGroundCollisionRecovery(GetCollisionPoint(collision));
        return true;
    }

    private bool TryStartGroundCollisionRecovery(Collider2D other)
    {
        if (other == null || currentState == RocketPackState.GroundCollisionRecovery)
            return false;

        if (!IsGroundObject(other.gameObject))
            return false;

        EnterGroundCollisionRecovery(GetColliderContactPoint(other));
        return true;
    }

    private bool TryGetDirectionAwayFromClosestGround(out Vector2 awayDirection)
    {
        awayDirection = Vector2.zero;

        if (groundMask == 0)
            return false;

        Collider2D[] groundColliders = FindObjectsByType<Collider2D>();
        Collider2D closestGroundCollider = null;
        bool hasClosestGround = false;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider2D groundCollider in groundColliders)
        {
            if (groundCollider == null || !groundCollider.enabled)
                continue;

            if (groundCollider.transform.IsChildOf(transform))
                continue;

            if (((1 << groundCollider.gameObject.layer) & groundMask.value) == 0)
                continue;

            if (!TryGetDirectionAwayFromGroundCollider(
                groundCollider,
                out Vector2 candidateAwayDirection,
                out float candidateDistance
            ))
            {
                continue;
            }

            if (candidateDistance >= closestDistance)
                continue;

            hasClosestGround = true;
            closestDistance = candidateDistance;
            awayDirection = candidateAwayDirection;
            closestGroundCollider = groundCollider;
        }

        if (!hasClosestGround)
            return false;

        // Debug.Log(
        //     $"{gameObject.name} RocketPack ground avoidance: closest ground is " +
        //     $"{closestGroundCollider.gameObject.name} ({closestGroundCollider.GetType().Name}), " +
        //     $"distance {closestDistance}, away direction {awayDirection}, body center {GetBodyCenter()}"
        // );

        return true;
    }

    private bool TryGetDirectionAwayFromGroundCollider(
        Collider2D groundCollider,
        out Vector2 awayDirection,
        out float signedDistance
    )
    {
        awayDirection = Vector2.zero;
        signedDistance = float.PositiveInfinity;

        if (groundCollider == null)
            return false;

        if (bodyCollider != null && bodyCollider.enabled)
        {
            ColliderDistance2D colliderDistance = bodyCollider.Distance(groundCollider);

            if (colliderDistance.isValid)
            {
                signedDistance = colliderDistance.distance;
                Vector2 bodyCenter = GetBodyCenter();
                float pointADistanceSqr = ((Vector2)colliderDistance.pointA - bodyCenter).sqrMagnitude;
                float pointBDistanceSqr = ((Vector2)colliderDistance.pointB - bodyCenter).sqrMagnitude;
                bool pointAIsBodyPoint = pointADistanceSqr <= pointBDistanceSqr;
                Vector2 bodyPoint = pointAIsBodyPoint ? colliderDistance.pointA : colliderDistance.pointB;
                Vector2 groundPoint = pointAIsBodyPoint ? colliderDistance.pointB : colliderDistance.pointA;

                awayDirection = bodyPoint - groundPoint;

                if (awayDirection.sqrMagnitude <= 0.0001f)
                {
                    awayDirection = bodyCenter - groundPoint;
                }

                if (awayDirection.sqrMagnitude <= 0.0001f && colliderDistance.normal.sqrMagnitude > 0.0001f)
                {
                    awayDirection = colliderDistance.normal;

                    if (Vector2.Dot(awayDirection, bodyCenter - groundPoint) < 0f)
                    {
                        awayDirection *= -1f;
                    }
                }

                if (awayDirection.sqrMagnitude > 0.0001f)
                {
                    awayDirection.Normalize();
                    awayDirection = CorrectGroundEscapeDirection(awayDirection);
                    // Debug.Log(
                    //     $"{gameObject.name} RocketPack ground avoidance distance points: " +
                    //     $"pointA {colliderDistance.pointA}, pointB {colliderDistance.pointB}, " +
                    //     $"bodyPoint {bodyPoint}, groundPoint {groundPoint}, " +
                    //     $"normal {colliderDistance.normal}, away {awayDirection}"
                    // );
                    return true;
                }
            }
        }

        Vector2 origin = GetRaycastOrigin();
        Vector2 closestPoint = groundCollider.ClosestPoint(origin);
        awayDirection = origin - closestPoint;
        signedDistance = awayDirection.magnitude;

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = origin - (Vector2)groundCollider.bounds.center;
        }

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = Vector2.up;
        }

        awayDirection.Normalize();
        awayDirection = CorrectGroundEscapeDirection(awayDirection);
        return true;
    }

    private Vector2 CorrectGroundEscapeDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.up;

        direction.Normalize();

        float forwardDistance = GetNearestGroundDistanceInDirection(direction);
        float backwardDistance = GetNearestGroundDistanceInDirection(-direction);
        bool forwardBlocked = !float.IsPositiveInfinity(forwardDistance);
        bool backwardBlocked = !float.IsPositiveInfinity(backwardDistance);

        if (forwardBlocked && (!backwardBlocked || forwardDistance < backwardDistance))
        {
            Debug.Log(
                $"{gameObject.name} RocketPack ground avoidance: flipping away direction. " +
                $"Forward {direction} hits ground at {forwardDistance}, " +
                $"backward {-direction} hits ground at {backwardDistance}"
            );
            direction *= -1f;
        }

        return direction;
    }

    private float GetNearestGroundDistanceInDirection(Vector2 direction)
    {
        if (groundMask == 0 || direction.sqrMagnitude <= 0.0001f)
            return float.PositiveInfinity;

        float maxDistance = Mathf.Max(0.01f, groundDirectionProbeDistance);
        RaycastHit2D[] hits = Physics2D.RaycastAll(GetBodyCenter(), direction.normalized, maxDistance, groundMask);
        float nearestDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
            }
        }

        return nearestDistance;
    }

    private Vector2 GetCollisionPoint(Collision2D collision)
    {
        if (collision.contactCount <= 0)
            return collision.collider != null
                ? GetColliderContactPoint(collision.collider)
                : GetRaycastOrigin();

        Vector2 collisionPoint = Vector2.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            collisionPoint += collision.GetContact(i).point;
        }

        return collisionPoint / collision.contactCount;
    }

    private Vector2 GetColliderContactPoint(Collider2D other)
    {
        Vector2 origin = GetRaycastOrigin();
        Vector2 point = other.ClosestPoint(origin);

        if ((origin - point).sqrMagnitude > 0.0001f)
            return point;

        return other.bounds.center;
    }

    private Vector2 GetDirectionAwayFromPoint(Vector2 point)
    {
        Vector2 direction = GetRaycastOrigin() - point;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        return direction.normalized;
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

    private Vector2 GetBodyCenter()
    {
        if (bodyCollider != null)
        {
            return bodyCollider.bounds.center;
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

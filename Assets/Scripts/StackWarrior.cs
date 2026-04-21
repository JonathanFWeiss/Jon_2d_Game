using UnityEngine;

public class StackWarrior : GroundWalkerEnemy
{
    private enum BehaviorMode
    {
        HopMove,
        Charge,
        SyrupVolley
    }

    [Header("Activation")]
    [Tooltip("How close the player must be before this enemy starts acting.")]
    public float activationRadius = 8f;

    [Tooltip("Optional point to measure activation distance from. If empty, the enemy's transform is used.")]
    public Transform activationCheck;

    [Header("Behavior Cycle")]
    [Tooltip("How often Stack Warrior picks a new behavior.")]
    public float behaviorSwapInterval = 10f;

    [Tooltip("Charge speed multiplier applied during the charge behavior.")]
    public float chargeSpeedMultiplier = 3f;

    [Header("Hop Movement")]
    [Tooltip("Time in seconds between each hop.")]
    public float hopInterval = 5f;

    [Tooltip("Horizontal impulse applied when hopping forward.")]
    public float hopForwardImpulse = 10f;

    [Tooltip("Vertical impulse applied when hopping.")]
    public float hopUpwardImpulse = 10f;

    [Header("Syrup Volley")]
    [Tooltip("Projectile prefab launched during the syrup volley behavior.")]
    public GameObject syrupBallPrefab;

    [Tooltip("How many syrup balls are spawned per volley.")]
    public int syrupBallCount = 6;

    [Tooltip("How far above the body the syrup balls spawn.")]
    public float syrupBallSpawnHeight = 1.5f;

    [Tooltip("Horizontal spacing between syrup balls when spawned.")]
    public float syrupBallSpacing = 0.35f;

    [Tooltip("Lowest launch angle used for the syrup-ball arc spread.")]
    public float syrupBallMinLaunchAngle = 30f;

    [Tooltip("Highest launch angle used for the syrup-ball arc spread.")]
    public float syrupBallMaxLaunchAngle = 80f;

    [Tooltip("Launch speed applied to each syrup ball.")]
    public float syrupBallLaunchSpeed = 12f;

    [Tooltip("How long syrup balls stay alive before despawning.")]
    public float syrupBallLifetime = 6f;

    private Collider2D bodyCollider;
    private AudioSource localAudioSource;
    private Transform playerTransform;
    private float nextBehaviorSwapTime;
    private float nextHopTime;
    private float nextPlayerSearchTime = float.NegativeInfinity;
    private BehaviorMode currentBehavior = BehaviorMode.HopMove;
    private BehaviorMode lastBehavior;
    private bool hasActivated;
    private bool hasFiredSyrupVolley;
    private bool isWalking, isAttacking;
    public Animator animator;

    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();
        localAudioSource = GetComponent<AudioSource>();
        coinDropCount = 10;
        Debug.Log("Stack Warrior Awake");
        animator = GetComponent<Animator>();

        if (localAudioSource != null)
        {
            localAudioSource.Stop();
        }
    }


    protected  override void Update()
    {
        if (animator == null)
            return;

        animator.SetBool("isWalking", isWalking);
        animator.SetBool("isAttacking", isAttacking);
        base.Update();
    }
    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        if (!hasActivated)
        {
            HoldStill();

            if (IsPlayerNearby())
            {
                Activate();
            }

            return;
        }

        if (Time.time >= nextBehaviorSwapTime)
        {
            PickRandomBehavior();
        }

        base.FixedUpdate();
    }

    protected override void Move()
    {
        switch (currentBehavior)
        {
            case BehaviorMode.Charge:
                ChargeAtPlayer();
                break;
            case BehaviorMode.SyrupVolley:
                LaunchSyrupVolley();
                break;
            default:
                HopMove();
                break;
        }
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
        if (!hasActivated)
            return;

        base.TryDamagePlayer(hitObject);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = hasActivated ? Color.red : Color.cyan;

        Vector3 checkPosition = activationCheck != null
            ? activationCheck.position
            : transform.position;

        Gizmos.DrawWireSphere(checkPosition, activationRadius);
    }

    private void Activate()
    {
        hasActivated = true;
        SwitchCameraSong();
        PickRandomBehavior();
    }

    private void PickRandomBehavior()
    {
        currentBehavior = (BehaviorMode)Random.Range(0, 3);

        if (currentBehavior == lastBehavior)
        {
            currentBehavior = (BehaviorMode)Random.Range(0, 3);
        }
        if (currentBehavior == lastBehavior)
        {
            currentBehavior = (BehaviorMode)Random.Range(0, 3);
        }
        lastBehavior = currentBehavior;

        Debug.Log($"{gameObject.name} picked new behavior: {currentBehavior}");
        nextBehaviorSwapTime = Time.time + Mathf.Max(0.01f, behaviorSwapInterval);
        hasFiredSyrupVolley = false;

        if (currentBehavior == BehaviorMode.HopMove)
        {
            nextHopTime = Time.fixedTime;
            return;
        }

        FacePlayer();

        if (currentBehavior == BehaviorMode.SyrupVolley)
        {
            HoldStill();
        }
    }

    private void HoldStill()
    {
        if (rb2d == null)
            return;

        rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
    }

    private void HopMove()
    {
        isWalking = true;
        isAttacking = false;
        ApplyHorizontalVelocity(moveSpeed);

        if (Time.fixedTime < nextHopTime || !IsGroundedForHop())
        {
            return;
        }

        rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
        rb2d.AddForce(
            new Vector2(facingDirection * hopForwardImpulse, hopUpwardImpulse),
            ForceMode2D.Impulse
        );
        nextHopTime = Time.fixedTime + Mathf.Max(0.01f, hopInterval);
        nextTurnAroundTime = Time.fixedTime + 3f;
    }

    private void ChargeAtPlayer()
    {
        ApplyHorizontalVelocity(moveSpeed * Mathf.Max(1f, chargeSpeedMultiplier));
        isAttacking = true;
        isWalking = false;
    }

    private void LaunchSyrupVolley()
    {
        HoldStill();
        isWalking = false;
        isAttacking = false;

        if (hasFiredSyrupVolley)
            return;

        hasFiredSyrupVolley = true;

        if (syrupBallPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name} is missing a Syrup Ball prefab reference.");
            return;
        }

        float launchDirection = GetPlayerDirection();
        Vector3 spawnBasePosition = GetSyrupSpawnPosition();
        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>(true);
        int projectileCount = Mathf.Max(1, syrupBallCount);

        for (int i = 0; i < projectileCount; i++)
        {
            float spreadT = projectileCount == 1
                ? 0.5f
                : (float)i / (projectileCount - 1);
            float launchAngle = Mathf.Lerp(
                syrupBallMinLaunchAngle,
                syrupBallMaxLaunchAngle,
                spreadT
            );
            float angleRadians = launchAngle * Mathf.Deg2Rad;
            Vector2 launchVelocity = new Vector2(
                Mathf.Cos(angleRadians) * launchDirection,
                Mathf.Sin(angleRadians)
            ) * syrupBallLaunchSpeed;
            float horizontalOffset = (i - (projectileCount - 1) * 0.5f) * syrupBallSpacing;
            Vector3 spawnPosition = spawnBasePosition + new Vector3(horizontalOffset, 0f, 0f);
            GameObject syrupBall = Instantiate(syrupBallPrefab, spawnPosition, Quaternion.identity);
            EnemyBase projectileEnemy = syrupBall.GetComponent<EnemyBase>();

            if (projectileEnemy != null)
            {
                projectileEnemy.enabled = false;
            }

            SyrupBallProjectile projectile = syrupBall.GetComponent<SyrupBallProjectile>();

            if (projectile == null)
            {
                projectile = syrupBall.AddComponent<SyrupBallProjectile>();
            }

            projectile.Initialize(
                ownerColliders,
                playerLayerMask,
                contactDamage,
                contactPushbackImpulse,
                syrupBallLifetime,
                launchVelocity
            );
        }
    }

    private void ApplyHorizontalVelocity(float targetSpeed)
    {
        float targetVelocityX = facingDirection * targetSpeed;
        float currentVelocityX = rb2d.linearVelocity.x;
        float maxVelocityChange = moveAcceleration * Time.fixedDeltaTime;
        float velocityChange = Mathf.Clamp(
            targetVelocityX - currentVelocityX,
            -maxVelocityChange,
            maxVelocityChange
        );

        rb2d.linearVelocity = new Vector2(
            rb2d.linearVelocity.x + velocityChange,
            rb2d.linearVelocity.y
        );
    }

    private bool IsGroundedForHop()
    {
        if (bodyCollider != null && groundMask != 0)
        {
            return bodyCollider.IsTouchingLayers(groundMask);
        }

        return Mathf.Abs(rb2d.linearVelocity.y) <= 0.05f;
    }

    private bool IsPlayerNearby()
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

    private void FacePlayer()
    {
        ResolvePlayerTransform();
        FaceDirection(GetPlayerDirection());
    }

    private void FaceDirection(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        float newFacingDirection = Mathf.Sign(direction);

        if (Mathf.Approximately(newFacingDirection, facingDirection))
            return;

        facingDirection = newFacingDirection;
        UpdateSpriteDirection();
        UpdateLedgeCheckPosition();
    }

    private float GetPlayerDirection()
    {
        ResolvePlayerTransform();

        if (playerTransform == null)
            return facingDirection;

        float directionToPlayer = playerTransform.position.x - transform.position.x;

        if (Mathf.Approximately(directionToPlayer, 0f))
            return facingDirection;

        return Mathf.Sign(directionToPlayer);
    }

    private Vector3 GetSyrupSpawnPosition()
    {
        float spawnY = transform.position.y + syrupBallSpawnHeight;

        if (bodyCollider != null)
        {
            spawnY = bodyCollider.bounds.max.y + syrupBallSpawnHeight;
        }

        return new Vector3(transform.position.x, spawnY, transform.position.z);
    }

    private void SwitchCameraSong()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            AudioSource cameraAudioSource = mainCamera.GetComponent<AudioSource>();

            if (cameraAudioSource != null && cameraAudioSource != localAudioSource)
            {
                cameraAudioSource.Stop();
            }
        }

        if (localAudioSource == null)
        {
            Debug.LogWarning($"{gameObject.name} needs an AudioSource attached for activation music.");
            return;
        }

        localAudioSource.Stop();
        localAudioSource.Play();
    }

    protected override void Die()
    {Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            AudioSource cameraAudioSource = mainCamera.GetComponent<AudioSource>();

            if (cameraAudioSource != null && cameraAudioSource != localAudioSource)
            {
                cameraAudioSource.Play();
            }
        }

        if (localAudioSource == null)
        {
            Debug.LogWarning($"{gameObject.name} needs an AudioSource attached for activation music.");
            return;
        }

        localAudioSource.Stop();
        
        base.Die();
        isWalking = false;
        isAttacking = false;
    }
}

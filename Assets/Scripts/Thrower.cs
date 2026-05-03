using UnityEngine;

public class Thrower : GroundStationaryEnemy
{
    private enum ThrowState
    {
        Ready,
        MovingSpikeBall,
        Cooldown
    }

    [Header("Thrower")]
    [Tooltip("Child object used as the held spike-ball visual. If empty, a child named SpikeBall is used.")]
    [SerializeField] private Transform spikeBall;

    [Tooltip("Local position the held spike ball moves to before it is thrown.")]
    [SerializeField] private Vector3 secondarySpikeBallLocalPosition = new Vector3(1.2f, 1f, 0f);

    [Tooltip("Optional point to measure player range from. If empty, the enemy position is used.")]
    [SerializeField] private Transform rangeOrigin;

    [Tooltip("How close the player must be before the thrower attacks.")]
    [SerializeField] private float attackRange = 8f;

    [Tooltip("Minimum delay after Start before this enemy can begin its first throw.")]
    [SerializeField] private float initialAttackDelay = 1.5f;

    [Tooltip("How long the held spike ball takes to move to the throw position.")]
    [SerializeField] private float windupDuration = 0.45f;

    [Tooltip("Speed applied to the thrown spike ball.")]
    [SerializeField] private float throwSpeed = 12f;

    [Tooltip("Small distance to spawn the thrown spike ball away from the held ball in the throw direction.")]
    [SerializeField] private float projectileSpawnOffset = 0.25f;

    [Tooltip("Delay before another spike ball is readied after a throw.")]
    [SerializeField] private float throwCooldown = 2f;

    [Tooltip("How long a thrown spike ball can exist before despawning.")]
    [SerializeField] private float projectileLifetime = 6f;

    [Tooltip("Gravity scale used by the thrown spike ball.")]
    [SerializeField] private float projectileGravityScale = 0f;

    [Header("Animation")]
    [Tooltip("Optional Animator state played while winding up.")]
    [SerializeField] private string throwAnimationState = "ThrowerThrow";

    [Tooltip("Optional Animator state played while idle or reloading.")]
    [SerializeField] private string idleAnimationState = "ThrowerIdle";

    private Rigidbody2D spikeBallRigidbody;
    private EnemyBase[] spikeBallEnemies;
    private Animator animator;
    private Transform playerTransform;
    private Vector3 startingSpikeBallLocalPosition;
    private float windupElapsed;
    private float nextThrowTime;
    private float nextPlayerSearchTime = float.NegativeInfinity;
    private ThrowState throwState = ThrowState.Ready;

    private void Reset()
    {
        moveSpeed = 0f;
        spikeBall = transform.Find("SpikeBall");
    }

    protected override void Awake()
    {
        base.Awake();

        animator = GetComponent<Animator>();
        ResolveSpikeBall();
        CacheSpikeBallParts();
        startingSpikeBallLocalPosition = spikeBall != null ? spikeBall.localPosition : Vector3.zero;
        ConfigureHeldSpikeBall(true);
        PlayAnimationState(idleAnimationState);
    }

    private void Start()
    {
        nextThrowTime = Mathf.Max(nextThrowTime, Time.time + Mathf.Max(0f, initialAttackDelay));
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        base.FixedUpdate();
        UpdateThrowState();
    }

    private void UpdateThrowState()
    {
        switch (throwState)
        {
            case ThrowState.MovingSpikeBall:
                MoveSpikeBallToThrowPosition();
                break;
            case ThrowState.Cooldown:
                if (Time.time >= nextThrowTime)
                {
                    ResetHeldSpikeBall();
                }
                break;
            default:
                HoldSpikeBallAtStart();

                if (Time.time >= nextThrowTime && IsPlayerWithinRange())
                {
                    StartThrowWindup();
                }
                break;
        }
    }

    private void ResolveSpikeBall()
    {
        if (spikeBall != null)
            return;

        spikeBall = transform.Find("SpikeBall");

        if (spikeBall == null)
        {
            Debug.LogWarning($"{gameObject.name} needs a child object named SpikeBall or an assigned spikeBall reference.");
        }
    }

    private void CacheSpikeBallParts()
    {
        if (spikeBall == null)
            return;

        spikeBallRigidbody = spikeBall.GetComponent<Rigidbody2D>();
        spikeBallEnemies = spikeBall.GetComponentsInChildren<EnemyBase>(true);
    }

    private void ConfigureHeldSpikeBall(bool visible)
    {
        if (spikeBall == null)
            return;

        spikeBall.gameObject.SetActive(visible);

        if (spikeBallRigidbody != null)
        {
            spikeBallRigidbody.simulated = false;
            spikeBallRigidbody.linearVelocity = Vector2.zero;
            spikeBallRigidbody.angularVelocity = 0f;
        }

        if (spikeBallEnemies == null)
            return;

        foreach (EnemyBase spikeBallEnemy in spikeBallEnemies)
        {
            if (spikeBallEnemy != null)
            {
                spikeBallEnemy.enabled = false;
            }
        }
    }

    private void HoldSpikeBallAtStart()
    {
        if (spikeBall == null)
            return;

        ConfigureHeldSpikeBall(true);
        spikeBall.localPosition = startingSpikeBallLocalPosition;
    }

    private void StartThrowWindup()
    {
        FacePlayer();
        ConfigureHeldSpikeBall(true);
        windupElapsed = 0f;
        throwState = ThrowState.MovingSpikeBall;
        PlayAnimationState(throwAnimationState);
        Debug.Log($"{gameObject.name} started throw windup. SpikeBall: {GetSpikeBallDebugName()}, Player: {GetPlayerDebugName()}");
    }

    private void MoveSpikeBallToThrowPosition()
    {
        if (spikeBall == null)
        {
            BeginCooldown();
            return;
        }

        float duration = Mathf.Max(0.01f, windupDuration);
        windupElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(windupElapsed / duration);
        spikeBall.localPosition = Vector3.Lerp(
            startingSpikeBallLocalPosition,
            secondarySpikeBallLocalPosition,
            progress
        );

        if (progress < 1f)
            return;

        Debug.Log(
            $"{gameObject.name} windup complete. Throwing spike ball from {spikeBall.position}. " +
            $"Player: {GetPlayerDebugName()}, ThrowSpeed: {throwSpeed}, SpikeBall active: {spikeBall.gameObject.activeInHierarchy}"
        );
        ThrowSpikeBallAtPlayer();
        BeginCooldown();
    }

    private void ThrowSpikeBallAtPlayer()
    {
        Vector2 launchDirection = GetLaunchDirection();
        Vector2 launchVelocity = launchDirection * Mathf.Max(0f, throwSpeed);
        Vector3 heldSpikeBallScale = GetPositiveWorldScale(spikeBall.lossyScale);
        Vector3 projectilePosition = spikeBall.position + (Vector3)(launchDirection * Mathf.Max(0f, projectileSpawnOffset));
        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>(true);
        Debug.Log(
            $"{gameObject.name} preparing spike-ball projectile. " +
            $"Direction: {launchDirection}, Velocity: {launchVelocity}, Spawn: {projectilePosition}, Scale: {heldSpikeBallScale}"
        );

        GameObject projectile = Instantiate(spikeBall.gameObject, projectilePosition, spikeBall.rotation, null);
        projectile.name = "SpikeBall Projectile";
        projectile.transform.localScale = heldSpikeBallScale;
        projectile.SetActive(true);
        Debug.Log($"{gameObject.name} instantiated {projectile.name} at {projectile.transform.position}.");

        foreach (EnemyBase projectileEnemy in projectile.GetComponentsInChildren<EnemyBase>(true))
        {
            if (projectileEnemy != null)
            {
                projectileEnemy.enabled = false;
            }
        }

        Rigidbody2D projectileRigidbody = projectile.GetComponent<Rigidbody2D>();

        if (projectileRigidbody != null)
        {
            projectileRigidbody.simulated = true;
            projectileRigidbody.bodyType = RigidbodyType2D.Dynamic;
            projectileRigidbody.gravityScale = Mathf.Max(0f, projectileGravityScale);
            projectileRigidbody.linearVelocity = Vector2.zero;
            projectileRigidbody.angularVelocity = 0f;
            Debug.Log(
                $"{projectile.name} Rigidbody2D ready. Simulated: {projectileRigidbody.simulated}, " +
                $"BodyType: {projectileRigidbody.bodyType}, GravityScale: {projectileRigidbody.gravityScale}"
            );
        }
        else
        {
            Debug.LogWarning($"{projectile.name} has no Rigidbody2D, so it cannot be launched.");
        }

        foreach (Collider2D projectileCollider in projectile.GetComponentsInChildren<Collider2D>(true))
        {
            if (projectileCollider != null)
            {
                projectileCollider.enabled = true;
            }
        }
        Debug.Log(
            $"{projectile.name} visuals/colliders after spawn. " +
            $"SpriteRenderers: {CountEnabledSpriteRenderers(projectile)}, Colliders: {CountEnabledColliders(projectile)}, Layer: {projectile.layer}"
        );

        SyrupBallProjectile projectileDamage = projectile.GetComponent<SyrupBallProjectile>();

        if (projectileDamage == null)
        {
            projectileDamage = projectile.AddComponent<SyrupBallProjectile>();
        }

        projectileDamage.Initialize(
            ownerColliders,
            playerLayerMask,
            contactDamage,
            contactPushbackImpulse,
            projectileLifetime,
            launchVelocity,
            gameObject
        );
        Debug.Log($"{projectile.name} initialized with lifetime {projectileLifetime} and velocity {launchVelocity}.");

        ConfigureHeldSpikeBall(false);
        Debug.Log($"{gameObject.name} hid held spike ball after spawning projectile.");
    }

    private void BeginCooldown()
    {
        nextThrowTime = Time.time + Mathf.Max(0f, throwCooldown);
        throwState = ThrowState.Cooldown;
        PlayAnimationState(idleAnimationState);
    }

    private void ResetHeldSpikeBall()
    {
        throwState = ThrowState.Ready;
        HoldSpikeBallAtStart();
    }

    private bool IsPlayerWithinRange()
    {
        Vector2 checkPosition = GetRangeOrigin();
        ResolvePlayerTransform();

        if (playerTransform != null)
        {
            GameObject playerObject = playerTransform.root.gameObject;

            if (IsPlayerObject(playerObject))
            {
                return Vector2.Distance(checkPosition, playerTransform.position) <= Mathf.Max(0f, attackRange);
            }
        }

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(checkPosition, Mathf.Max(0f, attackRange));

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (nearbyCollider == null)
                continue;

            GameObject rootObject = nearbyCollider.transform.root.gameObject;

            if (!IsPlayerObject(rootObject))
                continue;

            playerTransform = rootObject.transform;
            return true;
        }

        return false;
    }

    private void ResolvePlayerTransform(bool forceSearch = false)
    {
        if (playerTransform != null)
            return;

        if (!forceSearch && Time.time < nextPlayerSearchTime)
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

        if (playerTransform == null)
            return;

        float directionToPlayer = playerTransform.position.x - transform.position.x;

        if (Mathf.Approximately(directionToPlayer, 0f))
            return;

        float newFacingDirection = Mathf.Sign(directionToPlayer);

        if (Mathf.Approximately(newFacingDirection, facingDirection))
            return;

        facingDirection = newFacingDirection;
        UpdateSpriteDirection();
    }

    private Vector2 GetLaunchDirection()
    {
        ResolvePlayerTransform(true);

        if (playerTransform != null && spikeBall != null)
        {
            Vector2 directionToPlayer = playerTransform.position - spikeBall.position;

            if (directionToPlayer.sqrMagnitude > 0.0001f)
            {
                return directionToPlayer.normalized;
            }
        }

        return new Vector2(facingDirection == 0f ? 1f : facingDirection, 0f);
    }

    private Vector2 GetRangeOrigin()
    {
        if (rangeOrigin != null)
        {
            return rangeOrigin.position;
        }

        return transform.position;
    }

    private string GetSpikeBallDebugName()
    {
        return spikeBall != null ? spikeBall.gameObject.name : "null";
    }

    private string GetPlayerDebugName()
    {
        return playerTransform != null ? playerTransform.gameObject.name : "null";
    }

    private Vector3 GetPositiveWorldScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Approximately(scale.z, 0f) ? 1f : Mathf.Abs(scale.z)
        );
    }

    private int CountEnabledSpriteRenderers(GameObject target)
    {
        if (target == null)
            return 0;

        int count = 0;
        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    private int CountEnabledColliders(GameObject target)
    {
        if (target == null)
            return 0;

        int count = 0;
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    private void PlayAnimationState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.Play(stateName);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 checkPosition = rangeOrigin != null
            ? rangeOrigin.position
            : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(checkPosition, Mathf.Max(0f, attackRange));

        Transform previewSpikeBall = spikeBall != null
            ? spikeBall
            : transform.Find("SpikeBall");

        if (previewSpikeBall == null)
            return;

        Vector3 startPosition = Application.isPlaying
            ? transform.TransformPoint(startingSpikeBallLocalPosition)
            : previewSpikeBall.position;
        Vector3 throwPosition = transform.TransformPoint(secondarySpikeBallLocalPosition);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition, 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(throwPosition, 0.1f);
        Gizmos.DrawLine(startPosition, throwPosition);
    }
}

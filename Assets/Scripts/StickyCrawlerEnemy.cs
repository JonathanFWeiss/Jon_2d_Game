using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class StickyCrawlerEnemy : EnemyBase
{
    private struct SurfaceSample
    {
        public Vector2 point;
        public Vector2 normal;
    }

    [Header("Sticky Crawler")]
    [Tooltip("Layers this enemy can crawl on. Defaults to the Ground layer.")]
    public LayerMask groundMask = 0;

    [Tooltip("Surface normal used before the crawler finds ground. Use up for the top of a platform.")]
    public Vector2 initialSurfaceNormal = Vector2.up;

    [Tooltip("Distance to keep between the crawler body center and the platform surface. Set to 0 to auto-detect from the collider.")]
    public float surfaceOffset = 0f;

    [Tooltip("Small extra gap added to the surface offset so the crawler does not grind into the platform.")]
    public float surfaceSkin = 0.02f;

    [Tooltip("How far from the body center to look for a platform surface.")]
    public float surfaceSearchRadius = 1.5f;

    [Tooltip("How fast the crawler pulls itself back toward its last surface when briefly detached.")]
    public float detachedAdhesionSpeed = 6f;

    [Tooltip("Rotate the crawler so its local up points away from the platform surface.")]
    public bool alignToSurface = true;

    [Tooltip("How quickly the crawler rotates to match a new surface normal.")]
    public float rotationSharpness = 20f;

    [Tooltip("Move the crawler onto the closest surface when play starts.")]
    public bool snapToSurfaceOnStart = true;

    private const float MinNormalSqrMagnitude = 0.0001f;

    private Collider2D bodyCollider;
    private Vector2 surfaceNormal = Vector2.up;
    private float crawlDirection = 1f;
    private float resolvedSurfaceOffset;
    private bool hasSurface;

    private void Reset()
    {
        moveSpeed = 2f;
        moveAcceleration = 30f;
        initialSurfaceNormal = Vector2.up;
        surfaceSearchRadius = 1.5f;
        detachedAdhesionSpeed = 6f;
        alignToSurface = true;
        rotationSharpness = 20f;
        snapToSurfaceOnStart = true;
    }

    protected override void Awake()
    {
        base.Awake();

        bodyCollider = GetComponent<Collider2D>();
        surfaceNormal = NormalizeOrDefault(initialSurfaceNormal, Vector2.up);
        crawlDirection = facingDirection >= 0f ? 1f : -1f;
        InitializeGroundMask();
        ConfigureRigidbody();
        ResolveSurfaceOffset();

        if (snapToSurfaceOnStart)
        {
            SnapToClosestSurface();
        }

        ApplySurfaceRotation(true);
    }

    private void OnValidate()
    {
        if (initialSurfaceNormal.sqrMagnitude < MinNormalSqrMagnitude)
        {
            initialSurfaceNormal = Vector2.up;
        }

        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        surfaceSkin = Mathf.Max(0f, surfaceSkin);
        surfaceSearchRadius = Mathf.Max(0.01f, surfaceSearchRadius);
        detachedAdhesionSpeed = Mathf.Max(0f, detachedAdhesionSpeed);
        rotationSharpness = Mathf.Max(0f, rotationSharpness);
    }

    protected override void FixedUpdate()
    {
        if (isDead)
            return;

        CrawlAlongSurface();
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        TryAdoptCollisionSurface(collision);
    }

    protected override void OnCollisionStay2D(Collision2D collision)
    {
        base.OnCollisionStay2D(collision);
        TryAdoptCollisionSurface(collision);
    }

    private void InitializeGroundMask()
    {
        if (groundMask != 0)
            return;

        int groundLayerIndex = LayerMask.NameToLayer("Ground");

        if (groundLayerIndex == -1)
        {
            Debug.LogWarning(
                $"{gameObject.name}: No layer named 'Ground' exists. " +
                "Create a Ground layer or assign StickyCrawlerEnemy.groundMask."
            );
            return;
        }

        groundMask = 1 << groundLayerIndex;
    }

    private void ConfigureRigidbody()
    {
        if (rb2d == null)
            return;

        rb2d.gravityScale = 0f;
        rb2d.freezeRotation = true;
    }

    private void ResolveSurfaceOffset()
    {
        if (surfaceOffset > 0f)
        {
            resolvedSurfaceOffset = surfaceOffset + surfaceSkin;
            return;
        }

        resolvedSurfaceOffset = GetColliderSupportDistance(surfaceNormal) + surfaceSkin;
    }

    private float GetColliderSupportDistance(Vector2 normal)
    {
        if (bodyCollider == null)
            return 0.5f;

        Bounds bounds = bodyCollider.bounds;
        Vector2 extents = bounds.extents;
        Vector2 resolvedNormal = NormalizeOrDefault(normal, Vector2.up);
        float supportDistance =
            Mathf.Abs(resolvedNormal.x) * extents.x +
            Mathf.Abs(resolvedNormal.y) * extents.y;

        return Mathf.Max(0.01f, supportDistance);
    }

    private void SnapToClosestSurface()
    {
        SurfaceSample surface;

        if (!TryFindBestSurface(rb2d.position, GetSearchRadius(), out surface))
            return;

        surfaceNormal = surface.normal;
        hasSurface = true;

        Vector2 targetPosition = surface.point + surface.normal * resolvedSurfaceOffset;
        rb2d.position = targetPosition;
        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
    }

    private void CrawlAlongSurface()
    {
        SurfaceSample currentSurface;
        Vector2 currentPosition = rb2d.position;

        if (!TryFindBestSurface(currentPosition, GetSearchRadius(), out currentSurface))
        {
            hasSurface = false;
            MoveWhileDetached();
            ApplySurfaceRotation(false);
            return;
        }

        hasSurface = true;
        surfaceNormal = currentSurface.normal;

        Vector2 tangent = GetMovementTangent(surfaceNormal);
        float targetSpeed = Mathf.Max(0f, moveSpeed);
        float currentTangentSpeed = Vector2.Dot(rb2d.linearVelocity, tangent);
        float nextTangentSpeed = Mathf.MoveTowards(
            currentTangentSpeed,
            targetSpeed,
            moveAcceleration * Time.fixedDeltaTime
        );
        Vector2 predictedPosition = currentPosition + tangent * nextTangentSpeed * Time.fixedDeltaTime;

        SurfaceSample predictedSurface;

        if (!TryFindBestSurface(predictedPosition, GetSearchRadius(), out predictedSurface))
        {
            MoveWhileDetached();
            ApplySurfaceRotation(false);
            return;
        }

        surfaceNormal = predictedSurface.normal;
        Vector2 targetPosition = predictedSurface.point + predictedSurface.normal * resolvedSurfaceOffset;
        Vector2 movementDelta = targetPosition - currentPosition;

        rb2d.linearVelocity = Time.fixedDeltaTime > 0f
            ? movementDelta / Time.fixedDeltaTime
            : Vector2.zero;
        rb2d.MovePosition(targetPosition);

        ApplySurfaceRotation(false);
    }

    private void MoveWhileDetached()
    {
        Vector2 tangent = GetMovementTangent(surfaceNormal);
        Vector2 targetVelocity =
            tangent * Mathf.Max(0f, moveSpeed) -
            surfaceNormal * detachedAdhesionSpeed;
        float maxVelocityChange = moveAcceleration * Time.fixedDeltaTime;

        rb2d.linearVelocity = Vector2.MoveTowards(
            rb2d.linearVelocity,
            targetVelocity,
            maxVelocityChange
        );
    }

    private bool TryFindBestSurface(Vector2 position, float searchRadius, out SurfaceSample bestSurface)
    {
        bestSurface = new SurfaceSample();

        if (groundMask == 0)
            return false;

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(
            position,
            searchRadius,
            groundMask
        );
        bool foundSurface = false;
        float bestScore = float.PositiveInfinity;

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (!CanUseSurfaceCollider(nearbyCollider))
                continue;

            Vector2 closestPoint = nearbyCollider.ClosestPoint(position);
            Vector2 candidateNormal = GetSurfaceNormal(nearbyCollider, position, closestPoint);
            float distance = Vector2.Distance(position, closestPoint);
            float offsetError = Mathf.Abs(distance - resolvedSurfaceOffset);
            float normalContinuityPenalty = 0.25f *
                (1f - Mathf.Clamp(Vector2.Dot(candidateNormal, surfaceNormal), -1f, 1f));
            float score = offsetError + normalContinuityPenalty;

            if (score >= bestScore)
                continue;

            bestScore = score;
            foundSurface = true;
            bestSurface = new SurfaceSample
            {
                point = closestPoint,
                normal = candidateNormal
            };
        }

        return foundSurface;
    }

    private bool CanUseSurfaceCollider(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled || candidate.isTrigger)
            return false;

        if (candidate.transform.IsChildOf(transform))
            return false;

        return true;
    }

    private Vector2 GetSurfaceNormal(Collider2D surfaceCollider, Vector2 position, Vector2 closestPoint)
    {
        Vector2 normal = position - closestPoint;

        if (normal.sqrMagnitude >= MinNormalSqrMagnitude)
        {
            return normal.normalized;
        }

        Vector2 fromColliderCenter = position - (Vector2)surfaceCollider.bounds.center;

        if (fromColliderCenter.sqrMagnitude >= MinNormalSqrMagnitude)
        {
            return fromColliderCenter.normalized;
        }

        return NormalizeOrDefault(surfaceNormal, Vector2.up);
    }

    private void TryAdoptCollisionSurface(Collision2D collision)
    {
        if (collision == null || !IsGroundObject(collision.gameObject))
            return;

        Vector2 bestNormal = surfaceNormal;
        float bestDot = -1f;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 contactNormal = contact.normal;

            if (contactNormal.sqrMagnitude < MinNormalSqrMagnitude)
                continue;

            contactNormal.Normalize();
            float dot = Vector2.Dot(contactNormal, surfaceNormal);

            if (dot <= bestDot)
                continue;

            bestDot = dot;
            bestNormal = contactNormal;
        }

        if (bestDot < -0.5f)
            return;

        surfaceNormal = bestNormal;
        hasSurface = true;
    }

    private bool IsGroundObject(GameObject obj)
    {
        return obj != null && ((1 << obj.layer) & groundMask.value) != 0;
    }

    private float GetSearchRadius()
    {
        return Mathf.Max(surfaceSearchRadius, resolvedSurfaceOffset + 0.05f);
    }

    private Vector2 GetMovementTangent(Vector2 normal)
    {
        return GetClockwiseTangent(normal) * crawlDirection;
    }

    private static Vector2 GetClockwiseTangent(Vector2 normal)
    {
        Vector2 resolvedNormal = NormalizeOrDefault(normal, Vector2.up);
        return new Vector2(resolvedNormal.y, -resolvedNormal.x);
    }

    private static Vector2 NormalizeOrDefault(Vector2 value, Vector2 defaultValue)
    {
        return value.sqrMagnitude >= MinNormalSqrMagnitude
            ? value.normalized
            : defaultValue.normalized;
    }

    private void ApplySurfaceRotation(bool instant)
    {
        if (!alignToSurface)
            return;

        Vector2 clockwiseTangent = GetClockwiseTangent(surfaceNormal);
        float targetAngle = Mathf.Atan2(clockwiseTangent.y, clockwiseTangent.x) * Mathf.Rad2Deg;

        if (instant || rotationSharpness <= 0f || rb2d == null)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            return;
        }

        float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);
        float nextAngle = Mathf.LerpAngle(rb2d.rotation, targetAngle, rotationBlend);
        rb2d.SetRotation(nextAngle);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 normal = Application.isPlaying
            ? surfaceNormal
            : NormalizeOrDefault(initialSurfaceNormal, Vector2.up);
        Vector2 tangent = GetClockwiseTangent(normal) * (startFacingRight ? 1f : -1f);
        Vector3 position = transform.position;
        float searchRadius = Application.isPlaying
            ? GetSearchRadius()
            : Mathf.Max(surfaceSearchRadius, 0.05f);

        Gizmos.color = hasSurface ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(position, searchRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(position, position + (Vector3)normal);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(position, position + (Vector3)tangent);
    }
}

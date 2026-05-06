using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class CircularMovingPlatform2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How fast the platform moves around the circle, in world units per second.")]
    public float speed = 2f;

    [Tooltip("Move clockwise instead of counter-clockwise.")]
    public bool clockwise = false;

    [Tooltip("Move the platform onto the circular path immediately when play starts.")]
    public bool snapToCircleOnStart = true;

    [Header("Circle")]
    [Tooltip("Optional transform used as the center of the circular path. Its starting world position is cached when play starts.")]
    public Transform centerPoint;

    [Tooltip("World-space center of the circular path when Center Point is unassigned.")]
    public Vector2 centerPosition;

    [Tooltip("Radius of the circular path, in world units.")]
    public float radius = 2f;

    [Tooltip("Use the platform's scene position to determine its starting angle around the center.")]
    public bool startFromCurrentPosition = true;

    [Tooltip("Starting angle in degrees when Start From Current Position is off, or when the platform is exactly at the center.")]
    public float startAngleDegrees = 0f;

    private const int CircleGizmoSegments = 64;

    private readonly HashSet<Rigidbody2D> passengerRigidbodies = new HashSet<Rigidbody2D>();
    private readonly HashSet<Rigidbody2D> fullDeltaPassengerRigidbodies = new HashSet<Rigidbody2D>();
    private readonly List<Rigidbody2D> passengerRemovalBuffer = new List<Rigidbody2D>();
    private Rigidbody2D rb2d;
    private Vector2 cachedCenterPosition;
    private bool hasCachedCenterPosition;
    private float currentAngleRadians;

    public Vector2 CurrentPosition
    {
        get
        {
            Rigidbody2D platformRigidbody = rb2d != null ? rb2d : GetComponent<Rigidbody2D>();
            return platformRigidbody != null ? platformRigidbody.position : (Vector2)transform.position;
        }
    }

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        CacheCenterPosition();

        currentAngleRadians = GetInitialAngleRadians();

        if (snapToCircleOnStart)
        {
            Vector2 circlePosition = GetPointOnCircle(currentAngleRadians);
            rb2d.position = circlePosition;
            transform.position = new Vector3(circlePosition.x, circlePosition.y, transform.position.z);
        }
    }

    private void Reset()
    {
        ConfigureRigidbody();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        radius = Mathf.Max(0.001f, radius);
    }

    private void FixedUpdate()
    {
        if (rb2d == null || speed <= 0f || radius <= 0f)
            return;

        Vector2 previousPosition = rb2d.position;
        float direction = clockwise ? -1f : 1f;
        float angularSpeed = speed / radius;

        currentAngleRadians += direction * angularSpeed * Time.fixedDeltaTime;
        currentAngleRadians = Mathf.Repeat(currentAngleRadians, Mathf.PI * 2f);

        Vector2 nextPosition = GetPointOnCircle(currentAngleRadians);
        Vector2 platformDelta = nextPosition - previousPosition;

        rb2d.MovePosition(nextPosition);
        MovePassengers(platformDelta);
    }

    private void ConfigureRigidbody()
    {
        Rigidbody2D platformRigidbody = rb2d != null ? rb2d : GetComponent<Rigidbody2D>();

        if (platformRigidbody == null)
            return;

        platformRigidbody.bodyType = RigidbodyType2D.Kinematic;
        platformRigidbody.gravityScale = 0f;
        platformRigidbody.freezeRotation = true;
    }

    private float GetInitialAngleRadians()
    {
        if (startFromCurrentPosition)
        {
            Vector2 offsetFromCenter = (Vector2)transform.position - GetCenterPosition();

            if (offsetFromCenter.sqrMagnitude > 0.0001f)
            {
                return Mathf.Atan2(offsetFromCenter.y, offsetFromCenter.x);
            }
        }

        return startAngleDegrees * Mathf.Deg2Rad;
    }

    private void CacheCenterPosition()
    {
        cachedCenterPosition = GetConfiguredCenterPosition();
        hasCachedCenterPosition = true;
    }

    private Vector2 GetCenterPosition()
    {
        if (Application.isPlaying && hasCachedCenterPosition)
            return cachedCenterPosition;

        return GetConfiguredCenterPosition();
    }

    private Vector2 GetConfiguredCenterPosition()
    {
        if (centerPoint == null)
            return centerPosition;

        Vector3 centerPointPosition = centerPoint.position;
        return new Vector2(centerPointPosition.x, centerPointPosition.y);
    }

    private Vector2 GetPointOnCircle(float angleRadians)
    {
        Vector2 circleCenter = GetCenterPosition();
        Vector2 offset = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * radius;
        return circleCenter + offset;
    }

    private void MovePassengers(Vector2 platformDelta)
    {
        if (platformDelta == Vector2.zero || passengerRigidbodies.Count == 0)
            return;

        passengerRemovalBuffer.Clear();

        foreach (Rigidbody2D passengerRigidbody in passengerRigidbodies)
        {
            if (passengerRigidbody == null)
            {
                passengerRemovalBuffer.Add(passengerRigidbody);
                continue;
            }

            Vector2 passengerDelta = fullDeltaPassengerRigidbodies.Contains(passengerRigidbody)
                ? platformDelta
                : new Vector2(platformDelta.x, 0f);

            passengerRigidbody.position += passengerDelta;
        }

        foreach (Rigidbody2D passengerRigidbody in passengerRemovalBuffer)
        {
            passengerRigidbodies.Remove(passengerRigidbody);
            fullDeltaPassengerRigidbodies.Remove(passengerRigidbody);
        }
    }

    public bool AddPassenger(Rigidbody2D passengerRigidbody, bool carryFullDelta = false)
    {
        if (passengerRigidbody == null || passengerRigidbody == rb2d)
            return false;

        if (carryFullDelta)
        {
            fullDeltaPassengerRigidbodies.Add(passengerRigidbody);
        }

        return passengerRigidbodies.Add(passengerRigidbody);
    }

    public void RemovePassenger(Rigidbody2D passengerRigidbody)
    {
        if (passengerRigidbody == null)
            return;

        passengerRigidbodies.Remove(passengerRigidbody);
        fullDeltaPassengerRigidbodies.Remove(passengerRigidbody);
    }

    private void TrackPassenger(Collision2D collision)
    {
        AddPassenger(collision.rigidbody);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 circleCenter = GetCenterPosition();
        Vector3 center = new Vector3(circleCenter.x, circleCenter.y, transform.position.z);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, 0.15f);

        Vector3 previousPoint = GetCircleGizmoPoint(circleCenter, 0f);

        for (int i = 1; i <= CircleGizmoSegments; i++)
        {
            float angleRadians = (i / (float)CircleGizmoSegments) * Mathf.PI * 2f;
            Vector3 nextPoint = GetCircleGizmoPoint(circleCenter, angleRadians);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }

        Gizmos.DrawLine(center, GetCircleGizmoPoint(circleCenter, GetInitialAngleRadians()));
    }

    private Vector3 GetCircleGizmoPoint(Vector2 circleCenter, float angleRadians)
    {
        return new Vector3(
            circleCenter.x + Mathf.Cos(angleRadians) * radius,
            circleCenter.y + Mathf.Sin(angleRadians) * radius,
            transform.position.z
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrackPassenger(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TrackPassenger(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody == null)
            return;

        if (!fullDeltaPassengerRigidbodies.Contains(collision.rigidbody))
        {
            passengerRigidbodies.Remove(collision.rigidbody);
        }
    }
}

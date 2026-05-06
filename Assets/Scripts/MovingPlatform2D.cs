using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("How fast the platform moves, in world units per second.")]
    public float speed = 2f;

    [Tooltip("How close the platform must get to a waypoint before moving to the next one.")]
    public float arrivalDistance = 0.02f;

    [Tooltip("Move the platform to Waypoint 1 immediately when play starts.")]
    public bool snapToFirstWaypointOnStart = false;

    [Header("Waypoints")]
    [Tooltip("First waypoint in the travel cycle.")]
    public Transform waypoint1;

    [Tooltip("Second waypoint in the travel cycle.")]
    public Transform waypoint2;

    [Tooltip("Third waypoint in the travel cycle.")]
    public Transform waypoint3;

    [Tooltip("Fourth waypoint in the travel cycle.")]
    public Transform waypoint4;

    [Tooltip("Fifth waypoint in the travel cycle.")]
    public Transform waypoint5;

    private const int MaxWaypointCount = 5;

    private readonly Vector2[] cachedWaypointPositions = new Vector2[MaxWaypointCount];
    private readonly bool[] cachedWaypointIsAssigned = new bool[MaxWaypointCount];
    private readonly HashSet<Rigidbody2D> passengerRigidbodies = new HashSet<Rigidbody2D>();
    private readonly HashSet<Rigidbody2D> fullDeltaPassengerRigidbodies = new HashSet<Rigidbody2D>();
    private readonly List<Rigidbody2D> passengerRemovalBuffer = new List<Rigidbody2D>();
    private Rigidbody2D rb2d;
    private int currentWaypointIndex;

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
        CacheWaypointPositions();

        currentWaypointIndex = GetFirstValidWaypointIndex();

        if (snapToFirstWaypointOnStart && currentWaypointIndex >= 0)
        {
            Vector2 waypointPosition = GetWaypointPosition(currentWaypointIndex);
            rb2d.position = waypointPosition;
            transform.position = new Vector3(waypointPosition.x, waypointPosition.y, transform.position.z);
            currentWaypointIndex = GetNextValidWaypointIndex(currentWaypointIndex);
        }
    }

    private void Reset()
    {
        ConfigureRigidbody();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        arrivalDistance = Mathf.Max(0.001f, arrivalDistance);
    }

    private void FixedUpdate()
    {
        if (rb2d == null || speed <= 0f)
            return;

        currentWaypointIndex = GetValidWaypointIndex(currentWaypointIndex);

        if (currentWaypointIndex < 0)
            return;

        Vector2 targetPosition = GetWaypointPosition(currentWaypointIndex);
        Vector2 previousPosition = rb2d.position;
        Vector2 nextPosition = Vector2.MoveTowards(
            previousPosition,
            targetPosition,
            speed * Time.fixedDeltaTime
        );
        Vector2 platformDelta = nextPosition - previousPosition;

        rb2d.MovePosition(nextPosition);
        MovePassengers(platformDelta);

        if (Vector2.Distance(nextPosition, targetPosition) <= arrivalDistance)
        {
            currentWaypointIndex = GetNextValidWaypointIndex(currentWaypointIndex);
        }
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

    private int GetValidWaypointIndex(int index)
    {
        if (IsWaypointAssigned(index))
            return index;

        return GetFirstValidWaypointIndex();
    }

    private int GetFirstValidWaypointIndex()
    {
        for (int i = 0; i < MaxWaypointCount; i++)
        {
            if (IsWaypointAssigned(i))
                return i;
        }

        return -1;
    }

    private int GetNextValidWaypointIndex(int currentIndex)
    {
        for (int offset = 1; offset <= MaxWaypointCount; offset++)
        {
            int nextIndex = (currentIndex + offset) % MaxWaypointCount;

            if (IsWaypointAssigned(nextIndex))
                return nextIndex;
        }

        return -1;
    }

    private void CacheWaypointPositions()
    {
        for (int i = 0; i < MaxWaypointCount; i++)
        {
            Transform waypoint = GetWaypoint(i);
            cachedWaypointIsAssigned[i] = waypoint != null;

            if (waypoint != null)
            {
                cachedWaypointPositions[i] = waypoint.position;
            }
        }
    }

    private bool IsWaypointAssigned(int index)
    {
        return index >= 0 &&
            index < MaxWaypointCount &&
            cachedWaypointIsAssigned[index];
    }

    private Transform GetWaypoint(int index)
    {
        switch (index)
        {
            case 0:
                return waypoint1;
            case 1:
                return waypoint2;
            case 2:
                return waypoint3;
            case 3:
                return waypoint4;
            case 4:
                return waypoint5;
            default:
                return null;
        }
    }

    private Vector2 GetWaypointPosition(int index)
    {
        return IsWaypointAssigned(index)
            ? cachedWaypointPositions[index]
            : transform.position;
    }

    private void MovePassengers(Vector2 platformDelta)
    {
        if (platformDelta == Vector2.zero || passengerRigidbodies.Count == 0)
            return;

//        Debug.Log("Moving " + passengerRigidbodies.Count + " passengers with platform delta: " + platformDelta);
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
        if (AddPassenger(collision.rigidbody))
        {
            Debug.Log("Added passenger: " + collision.rigidbody.name);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 previousPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;
        bool hasPreviousPoint = false;
        bool hasFirstPoint = false;

        for (int i = 0; i < MaxWaypointCount; i++)
        {
            Transform waypoint = GetWaypoint(i);

            if (waypoint == null)
                continue;

            Vector3 waypointPosition = waypoint.position;
            Gizmos.DrawWireSphere(waypointPosition, 0.15f);

            if (!hasFirstPoint)
            {
                firstPoint = waypointPosition;
                hasFirstPoint = true;
            }

            if (hasPreviousPoint)
            {
                Gizmos.DrawLine(previousPoint, waypointPosition);
            }

            previousPoint = waypointPosition;
            hasPreviousPoint = true;
        }

        if (hasFirstPoint && hasPreviousPoint && firstPoint != previousPoint)
        {
            Gizmos.DrawLine(previousPoint, firstPoint);
        }
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

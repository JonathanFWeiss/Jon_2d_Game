using UnityEngine;

public class FlyingEnemy : EnemyBase
{
    [Header("Flying Idle")]
    [Tooltip("How far above and below the hover center the enemy bobs while idle.")]
    public float idleBobAmplitude = 0.25f;

    [Tooltip("How many bob cycles happen per second while idle.")]
    public float idleBobFrequency = 1f;

    [Tooltip("How quickly the enemy corrects toward the bob target height.")]
    public float idleBobResponsiveness = 8f;

    protected float hoverCenterY;
    protected float bobPhaseOffset;

    private void Reset()
    {
        moveSpeed = 0f;
    }

    protected override void Awake()
    {
        base.Awake();

        rb2d.gravityScale = 0f;
        SetHoverCenterToCurrentPosition();
        bobPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override void Move()
    {
        base.Move();

        if (!IsIdle())
            return;

        ApplyIdleBob();
    }

    protected virtual bool IsIdle()
    {
        return Mathf.Approximately(moveSpeed, 0f);
    }

    protected virtual void ApplyIdleBob()
    {
        if (idleBobAmplitude <= 0f || idleBobFrequency <= 0f)
        {
            ApplyBrakingForce();
            return;
        }

        float bobRadians = Time.time * idleBobFrequency * Mathf.PI * 2f + bobPhaseOffset;
        float targetY = hoverCenterY + Mathf.Sin(bobRadians) * idleBobAmplitude;
        float targetVelocityY = (targetY - rb2d.position.y) * idleBobResponsiveness;

        ApplyVelocitySteering(Vector2.up * targetVelocityY, idleBobResponsiveness);
    }

    protected void SetHoverCenterToCurrentPosition()
    {
        if (rb2d != null)
        {
            hoverCenterY = rb2d.position.y;
        }
        else
        {
            hoverCenterY = transform.position.y;
        }
    }

    protected virtual string GetMovementDebugStateName()
    {
        return "Flying";
    }

    protected void ApplyBrakingForce()
    {
        ApplyVelocitySteering(Vector2.zero, moveAcceleration);
    }

    protected void ApplyStopImpulse()
    {
        if (rb2d == null)
            return;

        //        Debug.Log($"{gameObject.name} {GetType().Name} move: stop impulse in {GetMovementDebugStateName()}. Current velocity: {rb2d.linearVelocity}");
        rb2d.AddForce(-rb2d.linearVelocity * rb2d.mass, ForceMode2D.Impulse);
    }

    protected void ApplyForceTowardTimedPosition(Vector2 targetPosition, float remainingTime, float acceleration)
    {
        float safeRemainingTime = Mathf.Max(Time.fixedDeltaTime, remainingTime);
        Vector2 targetVelocity = (targetPosition - rb2d.position) / safeRemainingTime;
        ApplyVelocitySteering(targetVelocity, acceleration);
    }

    protected void ApplyVelocitySteering(Vector2 targetVelocity, float acceleration)
    {
        if (rb2d == null)
            return;

        float safeFixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maxVelocityChange = Mathf.Max(0f, acceleration) * safeFixedDeltaTime;
        Vector2 velocityChange = Vector2.ClampMagnitude(
            targetVelocity - rb2d.linearVelocity,
            maxVelocityChange
        );
        Vector2 force = velocityChange * rb2d.mass / safeFixedDeltaTime;

        // Debug.Log(
        //     $"{gameObject.name} {GetType().Name} move: force in {GetMovementDebugStateName()}. " +
        //     $"Target velocity: {targetVelocity}, current velocity: {rb2d.linearVelocity}, force: {force}"
        // );
        rb2d.AddForce(force, ForceMode2D.Force);
    }
}

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

    private float hoverCenterY;
    private float bobPhaseOffset;

    private void Reset()
    {
        moveSpeed = 0f;
    }

    protected override void Awake()
    {
        base.Awake();

        rb2d.gravityScale = 0f;
        hoverCenterY = rb2d.position.y;
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
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
            return;
        }

        float bobRadians = Time.time * idleBobFrequency * Mathf.PI * 2f + bobPhaseOffset;
        float targetY = hoverCenterY + Mathf.Sin(bobRadians) * idleBobAmplitude;
        float targetVelocityY = (targetY - rb2d.position.y) * idleBobResponsiveness;

        rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, targetVelocityY);
    }
}

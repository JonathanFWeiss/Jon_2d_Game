using UnityEngine;

public class GroundCrow : GroundPersuerEnemy
{
    [Header("Hop Movement")]
    [Tooltip("Time in seconds between each hop.")]
    public float hopInterval = 2f;

    [Tooltip("Horizontal impulse applied when hopping forward.")]
    public float hopForwardImpulse = 1.5f;

    [Tooltip("Vertical impulse applied when hopping.")]
    public float hopUpwardImpulse = 2f;

    private Collider2D bodyCollider;
    private float nextHopTime;

    protected override void Awake()
    {
        base.Awake();
        bodyCollider = GetComponent<Collider2D>();
        coinDropCount = 3;
        nextHopTime = Time.fixedTime + Mathf.Max(0.01f, hopInterval);
        Debug.Log("Crow Awake");
    }

    protected override void Move()
    {
        float targetVelocityX = facingDirection * moveSpeed;
        float currentVelocityX = rb2d.linearVelocity.x;
        float maxVelocityChange = moveAcceleration * Time.fixedDeltaTime;
        float velocityChange = Mathf.Clamp(
            targetVelocityX - currentVelocityX,
            -maxVelocityChange,
            maxVelocityChange
        );
        rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x + velocityChange, rb2d.linearVelocity.y);


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
    }

    private bool IsGroundedForHop()
    {
        if (bodyCollider != null && groundMask != 0)
        {
            return bodyCollider.IsTouchingLayers(groundMask);
        }

        return Mathf.Abs(rb2d.linearVelocity.y) <= 0.05f;
    }
}

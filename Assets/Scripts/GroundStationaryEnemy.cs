using UnityEngine;

public class GroundStationaryEnemy : EnemyBase
{
    protected override void Awake()
    {
        base.Awake();
        moveSpeed = 0f;
    }

    protected override void Move()
    {
        if (rb2d == null)
            return;

        // Keep vertical motion from gravity or other forces, but never patrol horizontally.
        Vector2 velocity = rb2d.linearVelocity;
        velocity.x = 0f;
        rb2d.linearVelocity = velocity;
    }
}

using UnityEngine;

public class FixedPositionEnemy : EnemyBase
{
    protected override void Awake()
    {
        base.Awake();
        moveSpeed = 0f;
    }

    protected override void Move()
    {
        return; // Do nothing, this enemy does not move
    }
}

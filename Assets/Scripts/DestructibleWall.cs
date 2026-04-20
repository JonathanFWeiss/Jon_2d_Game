using UnityEngine;

public class DestructibleWall : GroundStationaryEnemy
{
    protected override void Awake()
    {
        base.Awake();
        contactDamage = 0;
        coinDropCount = 0;
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
    }

    protected override void DropCoins()
    {
    }
}

using UnityEngine;

public class DestructibleWall : FixedPositionEnemy
{
    protected override void Awake()
    {
        base.Awake();
        contactDamage = 0;
        hp = Mathf.Max(hp, 1);
        coinDropCount = 0;
       
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
       return; // Destructible walls do not damage the player on contact
    }

}

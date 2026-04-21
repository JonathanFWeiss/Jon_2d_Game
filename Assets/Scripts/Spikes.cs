using UnityEngine;

public class Spikes : FixedPositionEnemy
{
    protected override void Awake()
    {
        base.Awake();
        hp = Mathf.Max(hp, 1);
        coinDropCount = 0;
//        Debug.Log("Spikes Awake");
    }

    public override void TakeDamage(int amount)
    {
        // Spikes are a permanent hazard and ignore incoming damage.
    }

    protected override void Die()
    {
        hp = Mathf.Max(hp, 1);
    }
}

using UnityEngine;

public class GroundCrow : GroundPersuerEnemy
{
    protected override void Awake()
    {
        base.Awake();
        coinDropCount = 3;
        Debug.Log("Crow Awake");
    }
}
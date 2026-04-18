using UnityEngine;

public class GroundCrow : GroundWalkerEnemy
{
    protected override void Awake()
    {
        base.Awake();
        coinDropCount = 3;
        Debug.Log("Crow Awake");
    }
}
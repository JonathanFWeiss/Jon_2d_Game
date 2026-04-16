using UnityEngine;

public class Beetle : GroundWalkerEnemy
{
    protected override void Awake()
    {
        base.Awake();
        coinDropCount = 3;
        Debug.Log("Beetle Awake");
    }
}
using UnityEngine;

public class StackWarrior : GroundWalkerEnemy
{
    protected override void Awake()
    {
        base.Awake();
        coinDropCount = 10;
        Debug.Log("Stack Warrior Awake");
    }
}

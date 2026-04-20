using UnityEngine;

public static class PlayerData
{
    public const int DefaultHP = 3;

    public static int Coins { get; private set; }
    public static int HP { get; private set; } = DefaultHP;

    public static void AddCoins(int amount = 1)
    {
        Coins += amount;
    }

    public static void RemoveHP(int amount = 1)
    {
        HP -= amount;
        HP = Mathf.Max(HP, 0);
        Debug.Log("Player HP: " + HP);
        
    }

    public static void RestoreFullHP()
    {
        HP = DefaultHP;
        Debug.Log("Player HP restored to: " + HP);
    }

    public static void Reset()
    {
        Coins = 0;
        HP = DefaultHP;
    }
}

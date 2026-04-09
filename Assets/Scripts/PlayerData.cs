using UnityEngine;

public static class PlayerData
{
    public static int Coins { get; private set; }

    public static void AddCoins(int amount = 1)
    {
        Coins += amount;
    }

    public static void Reset()
    {
        Coins = 0;
    }
}

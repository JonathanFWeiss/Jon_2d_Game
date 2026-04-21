using UnityEngine;

public static class PlayerData
{
    public const int DefaultHP = 3;
    private const float RemoveHpCooldownSeconds = 1f;

    public static int Coins { get; private set; }
    public static int HP { get; private set; } = DefaultHP;

    private static float nextAllowedHpRemovalTime = 0f;

    public static void AddCoins(int amount = 1)
    {
        Coins += amount;
    }

    public static void RemoveHP(int amount = 1)
    {// Prevent HP removal if the amount is not positive or if we're still in the cooldown period

        Debug.Log("Current time: " + Time.time + ", Next allowed HP removal time: " + nextAllowedHpRemovalTime);
        if (amount <= 0 || Time.time < nextAllowedHpRemovalTime)
            return;

        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
        HP -= amount;
        HP = Mathf.Max(HP, 0);
        Debug.Log("Player HP: " + HP);

    }

    public static void RestoreFullHP()
    {
        HP = DefaultHP;
        Debug.Log("Player HP restored to: " + HP);
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void Reset()
    {
        Coins = 0;
        HP = DefaultHP;
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void Awake()
    {
        HP = DefaultHP;
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void Start()
    {
        HP = DefaultHP;
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
        
        Debug.Log("PlayerData Start - Next allowed HP removal time set to: " + nextAllowedHpRemovalTime);
    }
}

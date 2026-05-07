using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerData
{
    public const int DefaultHP = 3;
    public const string DashPickupItemName = "DashPickup";
    public const string DoubleJumpPickupItemName = "DoubleJumpPickup";
    public const string WallJumpPickupItemName = "WallJumpPickup";
    public const string MaxHealthPickupItemName = "MaxHealthPickup";
    public const int MaxEnergy = 20;
    private const float RemoveHpCooldownSeconds = 1f;
    private const SimulationMode2D DefaultPhysics2DMode = SimulationMode2D.FixedUpdate;
    private const SimulationMode DefaultPhysicsMode = SimulationMode.FixedUpdate;

    public static int Coins { get; private set; }
    public static int Energy { get; private set; }
    public static int HP { get; private set; } = DefaultHP;
    public static int MaxHP => DefaultHP + GetInventoryItemCount(MaxHealthPickupItemName);
    public static IReadOnlyDictionary<string, int> InventoryItems => inventoryItems;
    public static IReadOnlyCollection<string> CompletedGauntletKeys => completedGauntletKeys;
    public static event Action PlayerDied;

    private static readonly Dictionary<string, int> inventoryItems = new Dictionary<string, int>();
    private static readonly HashSet<string> completedGauntletKeys = new HashSet<string>();
    private static float nextAllowedHpRemovalTime = 0f;

    public static void AddCoins(int amount = 1)
    {
        if (amount <= 0)
            return;

        Coins += amount;
    }

    public static void AddEnergy(int amount = 1)
    {
        if (amount <= 0)
            return;

        Energy = Mathf.Min(Energy + amount, MaxEnergy);
    }

    public static void RemoveEnergy(int amount = 1)
    {
        if (amount <= 0)
            return;

        Energy = Mathf.Max(Energy - amount, 0);
    }

    public static void AddInventoryItem(string itemName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0)
            return;

        if (!inventoryItems.ContainsKey(itemName))
        {
            inventoryItems[itemName] = 0;
        }

        inventoryItems[itemName] += amount;
    }

    public static bool HasInventoryItem(string itemName)
    {
        return GetInventoryItemCount(itemName) > 0;
    }

    public static int GetInventoryItemCount(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return 0;

        return inventoryItems.TryGetValue(itemName, out int count) ? count : 0;
    }

    public static void AddMaxHealthPickup(int amount = 1)
    {
        if (amount <= 0)
            return;

        AddInventoryItem(MaxHealthPickupItemName, amount);
        HP = Mathf.Min(HP + amount, MaxHP);
    }

    public static bool HasCompletedGauntlet(IEnumerable<string> gauntletKeys)
    {
        if (gauntletKeys == null)
            return false;

        foreach (string gauntletKey in gauntletKeys)
        {
            string normalizedGauntletKey = NormalizeGauntletKey(gauntletKey);
            if (normalizedGauntletKey != null &&
                completedGauntletKeys.Contains(normalizedGauntletKey))
            {
                return true;
            }
        }

        return false;
    }

    public static int MarkGauntletCompleted(IEnumerable<string> gauntletKeys)
    {
        if (gauntletKeys == null)
            return 0;

        int addedCount = 0;
        foreach (string gauntletKey in gauntletKeys)
        {
            string normalizedGauntletKey = NormalizeGauntletKey(gauntletKey);
            if (normalizedGauntletKey != null &&
                completedGauntletKeys.Add(normalizedGauntletKey))
            {
                addedCount++;
            }
        }

        return addedCount;
    }

    public static void SetCompletedGauntlets(IEnumerable<string> gauntletKeys)
    {
        completedGauntletKeys.Clear();
        MarkGauntletCompleted(gauntletKeys);
    }

    private static string NormalizeGauntletKey(string gauntletKey)
    {
        return string.IsNullOrWhiteSpace(gauntletKey)
            ? null
            : gauntletKey.Trim();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeOnPlayModeStart()
    {
        RestoreDefaultPauseState();

        Coins = 0;
        Energy = 0;
        HP = DefaultHP;
        inventoryItems.Clear();
        completedGauntletKeys.Clear();
        nextAllowedHpRemovalTime = 0f;
        PlayerDied = null;
    }

    private static void RestoreDefaultPauseState()
    {
        Time.timeScale = 1f;
        Physics2D.simulationMode = DefaultPhysics2DMode;
        Physics.simulationMode = DefaultPhysicsMode;
    }

    public static void RemoveHP(int amount = 1)
    {// Prevent HP removal if the amount is not positive or if we're still in the cooldown period

//        Debug.Log("Current time: " + Time.time + ", Next allowed HP removal time: " + nextAllowedHpRemovalTime);
        if (amount <= 0 || Time.time < nextAllowedHpRemovalTime)
            return;

        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
        int previousHp = HP;
        HP -= amount;
        HP = Mathf.Max(HP, 0);
        if (previousHp > 0 && HP <= 0)
        {
            PlayerDied?.Invoke();
        }
//        Debug.Log("Player HP: " + HP);

    }

    public static void RestoreFullHP()
    {
        HP = MaxHP;
        Debug.Log("Player HP restored to: " + HP);
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void Reset()
    {
        Coins = 0;
        Energy = 0;
        HP = DefaultHP;
        inventoryItems.Clear();
        completedGauntletKeys.Clear();
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void ResetEnergy()
    {
        Energy = 0;
    }
}

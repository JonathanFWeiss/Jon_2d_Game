using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PlayerSaveSystem
{
    public const int SaveSlotCount = 4;
    public const int FirstSlotNumber = 1;
    public const int LastSlotNumber = FirstSlotNumber + SaveSlotCount - 1;
    public const int CurrentSaveVersion = 2;
    public static int ActiveSlotNumber { get; private set; }
    public static bool HasActiveSlot => IsValidSlotNumber(ActiveSlotNumber);

    private const string SaveDirectoryName = "PlayerSaveSlots";
    private const string SaveFileNameFormat = "player-slot-{0}.json";

    public static string SaveDirectoryPath =>
        Path.Combine(Application.persistentDataPath, SaveDirectoryName);

    public static bool TrySaveToSlot(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber))
        {
            LogInvalidSlot(slotNumber);
            return false;
        }

        try
        {
            Directory.CreateDirectory(SaveDirectoryPath);

            PlayerSaveData saveData = PlayerData.CreateSaveData();
            saveData.version = CurrentSaveVersion;
            saveData.slotNumber = slotNumber;
            saveData.savedAtUtc = DateTime.UtcNow.ToString("O");
            GameMaster.WriteCheckpointSaveData(saveData);

            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSlotPath(slotNumber);
            string temporaryPath = savePath + ".tmp";

            File.WriteAllText(temporaryPath, json);
            File.Copy(temporaryPath, savePath, true);
            File.Delete(temporaryPath);
            ActiveSlotNumber = slotNumber;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save PlayerData to slot {slotNumber}: {exception}");
            return false;
        }
    }

    public static bool TryLoadFromSlot(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber))
        {
            LogInvalidSlot(slotNumber);
            return false;
        }

        string savePath = GetSlotPath(slotNumber);
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"No PlayerData save exists in slot {slotNumber}.");
            return false;
        }

        try
        {
            return TryLoadSaveData(slotNumber, null, out _);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to load PlayerData from slot {slotNumber}: {exception}");
            return false;
        }
    }

    public static bool TryLoadFromSlot(int slotNumber, string fallbackSceneName, out string sceneNameToLoad)
    {
        sceneNameToLoad = null;

        if (!IsValidSlotNumber(slotNumber))
        {
            LogInvalidSlot(slotNumber);
            return false;
        }

        string savePath = GetSlotPath(slotNumber);
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"No PlayerData save exists in slot {slotNumber}.");
            return false;
        }

        try
        {
            return TryLoadSaveData(slotNumber, fallbackSceneName, out sceneNameToLoad);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to load PlayerData from slot {slotNumber}: {exception}");
            return false;
        }
    }

    public static bool SlotExists(int slotNumber)
    {
        return IsValidSlotNumber(slotNumber) && File.Exists(GetSlotPath(slotNumber));
    }

    public static bool TryDeleteSlot(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber))
        {
            LogInvalidSlot(slotNumber);
            return false;
        }

        string savePath = GetSlotPath(slotNumber);
        if (!File.Exists(savePath))
            return true;

        try
        {
            File.Delete(savePath);
            if (ActiveSlotNumber == slotNumber)
            {
                ClearActiveSlot();
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to delete PlayerData save slot {slotNumber}: {exception}");
            return false;
        }
    }

    public static bool TryGetSlotInfo(int slotNumber, out PlayerSaveSlotInfo slotInfo)
    {
        slotInfo = null;

        if (!IsValidSlotNumber(slotNumber))
        {
            LogInvalidSlot(slotNumber);
            return false;
        }

        string savePath = GetSlotPath(slotNumber);
        if (!File.Exists(savePath))
            return false;

        try
        {
            PlayerSaveData saveData = ReadSaveData(savePath);
            if (!IsSupportedSaveData(saveData, slotNumber))
                return false;

            slotInfo = PlayerSaveSlotInfo.FromSaveData(saveData);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to read PlayerData save slot {slotNumber}: {exception}");
            return false;
        }
    }

    public static string GetSlotPath(int slotNumber)
    {
        if (!IsValidSlotNumber(slotNumber))
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotNumber),
                slotNumber,
                $"Save slot must be between {FirstSlotNumber} and {LastSlotNumber}."
            );
        }

        return Path.Combine(SaveDirectoryPath, string.Format(SaveFileNameFormat, slotNumber));
    }

    public static bool TrySaveActiveSlot()
    {
        if (!HasActiveSlot)
        {
            Debug.LogWarning("Cannot save PlayerData because no save slot is active.");
            return false;
        }

        return TrySaveToSlot(ActiveSlotNumber);
    }

    public static void ClearActiveSlot()
    {
        ActiveSlotNumber = 0;
    }

    public static bool IsValidSlotNumber(int slotNumber)
    {
        return slotNumber >= FirstSlotNumber && slotNumber <= LastSlotNumber;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveSlotOnPlayModeStart()
    {
        ClearActiveSlot();
    }

    private static PlayerSaveData ReadSaveData(string savePath)
    {
        string json = File.ReadAllText(savePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Save file is empty.");
        }

        PlayerSaveData saveData = JsonUtility.FromJson<PlayerSaveData>(json);
        if (saveData == null)
        {
            throw new InvalidDataException("Save file did not contain PlayerData.");
        }

        return saveData;
    }

    private static bool TryLoadSaveData(int slotNumber, string fallbackSceneName, out string sceneNameToLoad)
    {
        sceneNameToLoad = null;

        PlayerSaveData saveData = ReadSaveData(GetSlotPath(slotNumber));
        if (!IsSupportedSaveData(saveData, slotNumber))
            return false;

        PlayerData.ApplySaveData(saveData);
        PlayerData.RestoreFullHP();
        GameMaster.ApplyCheckpointSaveData(saveData);
        ActiveSlotNumber = slotNumber;
        sceneNameToLoad = GetSceneNameToLoad(saveData, fallbackSceneName);
        return true;
    }

    private static string GetSceneNameToLoad(PlayerSaveData saveData, string fallbackSceneName)
    {
        if (saveData != null &&
            saveData.hasCheckpointRespawnPosition &&
            !string.IsNullOrWhiteSpace(saveData.checkpointRespawnSceneName))
        {
            return saveData.checkpointRespawnSceneName.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackSceneName)
            ? null
            : fallbackSceneName.Trim();
    }

    private static bool IsSupportedSaveData(PlayerSaveData saveData, int requestedSlotNumber)
    {
        if (saveData.version <= 0 || saveData.version > CurrentSaveVersion)
        {
            Debug.LogWarning(
                $"PlayerData save slot {requestedSlotNumber} uses unsupported version {saveData.version}."
            );
            return false;
        }

        if (saveData.slotNumber != 0 && saveData.slotNumber != requestedSlotNumber)
        {
            Debug.LogWarning(
                $"PlayerData save slot {requestedSlotNumber} contains data for slot {saveData.slotNumber}."
            );
            return false;
        }

        return true;
    }

    private static void LogInvalidSlot(int slotNumber)
    {
        Debug.LogWarning(
            $"Invalid PlayerData save slot {slotNumber}. Use slots {FirstSlotNumber} through {LastSlotNumber}."
        );
    }
}

[Serializable]
public sealed class PlayerSaveData
{
    public int version = PlayerSaveSystem.CurrentSaveVersion;
    public int slotNumber;
    public string savedAtUtc;
    public int coins;
    public int energy;
    public int hp;
    public bool hasCheckpointRespawnPosition;
    public string checkpointRespawnSceneName;
    public Vector3 checkpointRespawnPosition;
    public List<PlayerInventorySaveItem> inventoryItems = new List<PlayerInventorySaveItem>();
    public List<string> completedGauntletKeys = new List<string>();
    public List<string> triggeredDiscoverableVisibilityKeys = new List<string>();
}

[Serializable]
public sealed class PlayerInventorySaveItem
{
    public string itemName;
    public int count;
}

public sealed class PlayerSaveSlotInfo
{
    public int slotNumber;
    public int version;
    public string savedAtUtc;
    public int coins;
    public int energy;
    public int hp;
    public bool hasCheckpointRespawnPosition;
    public string checkpointRespawnSceneName;
    public int inventoryItemCount;
    public int completedGauntletCount;
    public int triggeredDiscoverableVisibilityCount;

    public static PlayerSaveSlotInfo FromSaveData(PlayerSaveData saveData)
    {
        return new PlayerSaveSlotInfo
        {
            slotNumber = saveData.slotNumber,
            version = saveData.version,
            savedAtUtc = saveData.savedAtUtc,
            coins = saveData.coins,
            energy = saveData.energy,
            hp = saveData.hp,
            hasCheckpointRespawnPosition = saveData.hasCheckpointRespawnPosition,
            checkpointRespawnSceneName = saveData.checkpointRespawnSceneName,
            inventoryItemCount = CountList(saveData.inventoryItems),
            completedGauntletCount = CountList(saveData.completedGauntletKeys),
            triggeredDiscoverableVisibilityCount = CountList(saveData.triggeredDiscoverableVisibilityKeys)
        };
    }

    private static int CountList<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }
}

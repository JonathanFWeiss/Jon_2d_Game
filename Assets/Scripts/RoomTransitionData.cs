using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomTransitionData
{
    public static string TargetEntryId { get; private set; }
    public static string TargetSceneName { get; private set; }

    public static bool HasTargetEntryId => !string.IsNullOrWhiteSpace(TargetEntryId);

    public static void SetTarget(string sceneName, string entryId)
    {
        TargetSceneName = string.IsNullOrWhiteSpace(entryId) ? null : sceneName;
        TargetEntryId = string.IsNullOrWhiteSpace(entryId) ? null : entryId;
    }

    public static void ClearTargetEntryId()
    {
        TargetSceneName = null;
        TargetEntryId = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeOnPlayModeStart()
    {
        ClearTargetEntryId();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RoomSpawnResolver.ResolveLoadedScene(scene);
    }
}

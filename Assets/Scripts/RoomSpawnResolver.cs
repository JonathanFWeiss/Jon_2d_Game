using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomSpawnResolver
{
    public static void ResolveLoadedScene(Scene scene)
    {
        if (!RoomTransitionData.HasTargetEntryId)
            return;

        if (!IsTargetScene(scene))
            return;

        string targetEntryId = RoomTransitionData.TargetEntryId;
        RoomEntryPoint targetEntryPoint = FindEntryPoint(targetEntryId, scene);

        if (targetEntryPoint == null)
        {
            Debug.LogWarning($"Could not find a {nameof(RoomEntryPoint)} with entryId '{targetEntryId}' in this scene.");
            RoomTransitionData.ClearTargetEntryId();
            return;
        }

        Transform playerTransform = FindPlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogWarning($"Could not place player at entryId '{targetEntryId}' because no player object was found.");
            RoomTransitionData.ClearTargetEntryId();
            return;
        }

        playerTransform.position = targetEntryPoint.transform.position;
        ResetPlayerVelocity(playerTransform);
        UpdateFallbackRespawnPosition(playerTransform.position);
        RoomTransitionData.ClearTargetEntryId();
    }

    private static bool IsTargetScene(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(RoomTransitionData.TargetSceneName))
            return true;

        string activeSceneName = scene.name;
        string targetSceneName = Path.GetFileNameWithoutExtension(RoomTransitionData.TargetSceneName);
        return activeSceneName == targetSceneName;
    }

    private static RoomEntryPoint FindEntryPoint(string targetEntryId, Scene scene)
    {
        RoomEntryPoint[] entryPoints = Object.FindObjectsByType<RoomEntryPoint>();

        foreach (RoomEntryPoint entryPoint in entryPoints)
        {
            if (entryPoint != null &&
                entryPoint.gameObject.scene == scene &&
                entryPoint.entryId == targetEntryId)
            {
                return entryPoint;
            }
        }

        return null;
    }

    private static Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            JonCharacterController jonCharacter = player.GetComponentInParent<JonCharacterController>();
            if (jonCharacter != null)
            {
                return jonCharacter.transform;
            }

            Hero hero = player.GetComponentInParent<Hero>();
            if (hero != null)
            {
                return hero.transform;
            }

            return player.transform;
        }

        JonCharacterController sceneJonCharacter = Object.FindAnyObjectByType<JonCharacterController>();
        if (sceneJonCharacter != null)
        {
            return sceneJonCharacter.transform;
        }

        Hero sceneHero = Object.FindAnyObjectByType<Hero>();
        return sceneHero != null ? sceneHero.transform : null;
    }

    private static void UpdateFallbackRespawnPosition(Vector3 respawnPosition)
    {
        GameMaster gameMaster = Object.FindAnyObjectByType<GameMaster>();
        if (gameMaster != null)
        {
            gameMaster.SetFallbackRespawnPosition(respawnPosition);
        }
    }

    private static void ResetPlayerVelocity(Transform playerTransform)
    {
        Rigidbody2D playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            playerRigidbody = playerTransform.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRigidbody == null)
            return;

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
    }
}

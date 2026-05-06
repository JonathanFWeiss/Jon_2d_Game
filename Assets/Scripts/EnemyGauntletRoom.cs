using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class EnemyGauntletRoom : MonoBehaviour
{
    private const string DefaultCounterLabelName = "GauntletDefeatedCounter";
    private const string BannerLabelName = "GauntletBannerMessage";
    private const float ActiveEnemySpawnSeparationScoreWeight = 2f;
    private const int ActiveEnemyBoundsCheckUpdateInterval = 10;

    [Serializable]
    public class EnemyWave
    {
        [SerializeField] private List<EnemySpawn> enemies = new List<EnemySpawn>();

        public List<EnemySpawn> Enemies => enemies;
    }

    [Serializable]
    public class EnemySpawn
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnPoint;
        [Min(1)]
        [SerializeField] private int count = 1;

        public GameObject EnemyPrefab => enemyPrefab;
        public Transform SpawnPoint => spawnPoint;
        public int Count => Mathf.Max(0, count);
    }

    private struct DoorStartState
    {
        public readonly GameObject Door;
        public readonly bool WasActive;

        public DoorStartState(GameObject door)
        {
            Door = door;
            WasActive = door != null && door.activeSelf;
        }
    }

    [Header("Doors")]
    [Tooltip("Assign the two doors that close when the gauntlet starts.")]
    [SerializeField] private GameObject[] doors = new GameObject[2];

    [Header("Waves")]
    [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();
    [Tooltip("Optional fallback spawn points used if a random room position cannot be found.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Area used for random enemy spawns. If left empty, this object's trigger collider is used.")]
    [SerializeField] private Collider2D spawnArea;
    [Tooltip("Enemies will be spawned at least this far from the player.")]
    [Min(0f)]
    [SerializeField] private float minimumSpawnDistanceFromPlayer = 5f;
    [Tooltip("Extra spacing between enemies spawned in the same wave.")]
    [Min(0f)]
    [SerializeField] private float minimumSpawnDistanceFromOtherEnemies = 1.5f;
    [Tooltip("How many random room positions to try before using fallback spawn points.")]
    [Min(1)]
    [SerializeField] private int randomSpawnAttempts = 30;
    [SerializeField] private Transform spawnedEnemiesParent;

    [Header("Spawn Collision")]
    [Tooltip("Layers that block enemy spawn positions. If empty, the Ground layer is used.")]
    [SerializeField] private LayerMask spawnBlockerMask = 0;
    [Tooltip("Fallback probe size used when an enemy prefab has no usable 2D colliders.")]
    [SerializeField] private Vector2 fallbackSpawnBlockerProbeSize = Vector2.one;
    [Tooltip("How many positions to test while moving a blocked spawn toward the room center.")]
    [Min(1)]
    [SerializeField] private int spawnBlockerAdjustmentAttempts = 8;
    [Tooltip("Extra clearance added around enemy colliders when checking spawn blockers.")]
    [Min(0f)]
    [SerializeField] private float spawnBlockerPadding = 0.05f;

    [Header("Persistence")]
    [Tooltip("Optional stable completion key. If left empty, scene, bounds, and object hierarchy keys are used.")]
    [SerializeField] private string completionLogKey;

    [Header("Audio")]
    [Tooltip("Optional music clip that temporarily replaces the Main Camera AudioSource while the gauntlet is active.")]
    [SerializeField] private AudioClip gauntletMusicClip;

    [Header("UI")]
    [Tooltip("If left empty, the first UIDocument in the scene is used.")]
    [SerializeField] private UIDocument uiDocument;
    [Tooltip("Optional banner text shown briefly when the gauntlet starts. Leave empty to disable.")]
    [SerializeField] private string bannerText;
    [SerializeField] private float bannerDuration = 1.5f;
    [SerializeField] private float bannerTopOffset = 86f;
    [SerializeField] private string counterLabelName = DefaultCounterLabelName;
    [SerializeField] private float counterTopOffset = 86f;

    private readonly List<GameObject> activeWaveEnemies = new List<GameObject>();
    private Label bannerLabel;
    private Label counterLabel;
    private Transform playerTransform;
    private Collider2D triggerCollider;
    private int currentWaveIndex = -1;
    private int defeatedEnemyCount;
    private int totalEnemyCount;
    private int nextSpawnPointIndex;
    private bool isRunning;
    private bool isComplete;
    private bool warnedMissingUiDocument;
    private DoorStartState[] doorStatesBeforeGauntlet;
    private AudioSource gauntletMusicAudioSource;
    private AudioClip previousMusicClip;
    private float previousMusicTime;
    private bool previousMusicLoop;
    private bool previousMusicWasPlaying;
    private bool gauntletMusicActive;
    private bool warnedMissingGauntletMusicAudioSource;
    private bool warnedMissingGroundSpawnBlockerLayer;
    private bool warnedUnclearableGroundSpawn;
    private int activeEnemyBoundsCheckUpdateCounter;

    private void OnValidate()
    {
        LogLiveSceneEnemyPrefabReferenceError();
        LogGauntletBoundsGroundOverlapError();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveSpawnArea();
        ThrowIfLiveSceneEnemyPrefabReferencesExist();
        ThrowIfGauntletBoundsIntersectGroundColliders();
        ApplyLoggedCompletionIfNeeded();
    }

    private void OnEnable()
    {
        PlayerData.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerData.PlayerDied -= HandlePlayerDied;
        StopGauntletMusic();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        if (RemoveDefeatedEnemies() > 0)
        {
            RefreshCounterLabel();
        }

        if (activeWaveEnemies.Count == 0)
        {
            StartNextWaveOrComplete();
        }

        CheckActiveWaveEnemyBoundsPeriodically();
    }

    private void OnDestroy()
    {
        StopGauntletMusic();
        RemoveBannerLabel();
        RemoveCounterLabel();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isRunning || isComplete)
            return;

        Transform enteringPlayer = GetPlayerTransform(other);
        if (enteringPlayer == null)
            return;

        playerTransform = enteringPlayer;
        StartGauntlet();
    }

    private void StartGauntlet()
    {
        ThrowIfLiveSceneEnemyPrefabReferencesExist();
        ThrowIfGauntletBoundsIntersectGroundColliders();

        CacheDoorStatesBeforeGauntlet();
        isRunning = true;
        currentWaveIndex = -1;
        defeatedEnemyCount = 0;
        totalEnemyCount = CountSpawnableEnemies();
        activeEnemyBoundsCheckUpdateCounter = 0;

        ActivateDoors();
        PlayGauntletMusic();
        ShowCounterLabel();
        RefreshCounterLabel();
        ShowBannerMessage();
        StartNextWaveOrComplete();
    }

    private void HandlePlayerDied()
    {
        if (!isRunning)
            return;

        ResetGauntletAfterPlayerDeath();
    }

    private void ResetGauntletAfterPlayerDeath()
    {
        StopGauntletMusic();
        DestroyActiveWaveEnemies();
        RestoreDoorsToPreGauntletState();
        RemoveBannerLabel();
        RemoveCounterLabel();

        isRunning = false;
        isComplete = false;
        currentWaveIndex = -1;
        defeatedEnemyCount = 0;
        totalEnemyCount = 0;
        nextSpawnPointIndex = 0;
        activeEnemyBoundsCheckUpdateCounter = 0;
        playerTransform = null;
    }

    private void CacheDoorStatesBeforeGauntlet()
    {
        if (doors == null)
        {
            doorStatesBeforeGauntlet = null;
            return;
        }

        doorStatesBeforeGauntlet = new DoorStartState[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            doorStatesBeforeGauntlet[i] = new DoorStartState(doors[i]);
        }
    }

    private void ActivateDoors()
    {
        if (doors == null)
            return;

        foreach (GameObject door in doors)
        {
            if (door != null)
            {
                door.SetActive(true);
            }
        }
    }

    private void StartNextWaveOrComplete()
    {
        while (activeWaveEnemies.Count == 0)
        {
            currentWaveIndex++;

            if (waves == null || currentWaveIndex >= waves.Count)
            {
                CompleteGauntlet();
                return;
            }

            SpawnWave(waves[currentWaveIndex]);
        }
    }

    private void SpawnWave(EnemyWave wave)
    {
        if (wave == null || wave.Enemies == null)
            return;

        foreach (EnemySpawn enemySpawn in wave.Enemies)
        {
            if (enemySpawn == null || enemySpawn.EnemyPrefab == null)
                continue;

            for (int i = 0; i < enemySpawn.Count; i++)
            {
                SpawnEnemy(enemySpawn);
            }
        }
    }

    private void SpawnEnemy(EnemySpawn enemySpawn)
    {
        ResolveSpawnPose(enemySpawn, out Vector3 spawnPosition, out Quaternion spawnRotation);

        GameObject spawnedEnemy = Instantiate(
            enemySpawn.EnemyPrefab,
            spawnPosition,
            spawnRotation,
            spawnedEnemiesParent
        );

        if (spawnedEnemy != null)
        {
            activeWaveEnemies.Add(spawnedEnemy);
        }
    }

    private int RemoveDefeatedEnemies()
    {
        int removedCount = 0;

        for (int i = activeWaveEnemies.Count - 1; i >= 0; i--)
        {
            if (activeWaveEnemies[i] != null)
                continue;

            activeWaveEnemies.RemoveAt(i);
            defeatedEnemyCount++;
            removedCount++;
        }

        return removedCount;
    }

    private void CompleteGauntlet()
    {
        isRunning = false;
        isComplete = true;
        activeEnemyBoundsCheckUpdateCounter = 0;

        LogGauntletCompletion();
        DestroyDoors();
        StopGauntletMusic();
        RemoveCounterLabel();
    }

    private void DestroyDoors()
    {
        if (doors == null)
            return;

        foreach (GameObject door in doors)
        {
            if (door != null)
            {
                door.SetActive(false);
                Destroy(door);
            }
        }
    }

    private void ApplyLoggedCompletionIfNeeded()
    {
        if (!IsGauntletCompletionLogged())
            return;

        isRunning = false;
        isComplete = true;
        currentWaveIndex = -1;
        defeatedEnemyCount = 0;
        totalEnemyCount = 0;
        nextSpawnPointIndex = 0;
        activeEnemyBoundsCheckUpdateCounter = 0;
        playerTransform = null;
        DestroyActiveWaveEnemies();

        Debug.Log(
            $"{nameof(EnemyGauntletRoom)} on '{name}' was already completed; restoring completed state.",
            this
        );
        DestroyDoors();
        RemoveCounterLabel();
    }

    private void LogGauntletCompletion()
    {
        List<string> keys = GetCompletionLogKeys();
        int addedCount = PlayerData.MarkGauntletCompleted(keys);
        if (addedCount > 0)
        {
            Debug.Log(
                $"{nameof(EnemyGauntletRoom)} on '{name}' successfully completed and logged as " +
                $"'{GetPrimaryCompletionLogKey(keys)}'.",
                this
            );
        }
    }

    private bool IsGauntletCompletionLogged()
    {
        return PlayerData.HasCompletedGauntlet(GetCompletionLogKeys());
    }

    private List<string> GetCompletionLogKeys()
    {
        List<string> keys = new List<string>();

        if (!string.IsNullOrWhiteSpace(completionLogKey))
        {
            AddCompletionLogKey(keys, completionLogKey.Trim());
        }

        UnityEngine.SceneManagement.Scene scene = gameObject.scene;
        string sceneName = !string.IsNullOrWhiteSpace(scene.name)
            ? scene.name
            : "UnloadedScene";
        string scenePath = !string.IsNullOrWhiteSpace(scene.path)
            ? scene.path
            : sceneName;
        string hierarchyKey = GetTransformPath(transform);
        string positionKey = GetPositionCompletionKey();

        AddCompletionLogKey(keys, $"{sceneName}/{positionKey}");
        AddCompletionLogKey(keys, $"{scenePath}/{positionKey}");
        AddCompletionLogKey(keys, $"{sceneName}/{hierarchyKey}");
        AddCompletionLogKey(keys, $"{scenePath}/{hierarchyKey}");

        return keys;
    }

    private static void AddCompletionLogKey(List<string> keys, string key)
    {
        if (keys == null || string.IsNullOrWhiteSpace(key) || keys.Contains(key))
            return;

        keys.Add(key);
    }

    private static string GetPrimaryCompletionLogKey(List<string> keys)
    {
        return keys != null && keys.Count > 0
            ? keys[0]
            : "UnknownGauntlet";
    }

    private string GetPositionCompletionKey()
    {
        Collider2D boundsSource = GetResolvedSpawnArea();
        Bounds bounds = boundsSource != null
            ? boundsSource.bounds
            : new Bounds(transform.position, Vector3.zero);

        return
            $"{name}@" +
            $"center({FormatCompletionCoordinate(bounds.center.x)}," +
            $"{FormatCompletionCoordinate(bounds.center.y)})/" +
            $"size({FormatCompletionCoordinate(bounds.size.x)}," +
            $"{FormatCompletionCoordinate(bounds.size.y)})";
    }

    private static string FormatCompletionCoordinate(float value)
    {
        return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetTransformPath(Transform targetTransform)
    {
        if (targetTransform == null)
            return string.Empty;

        string path = $"{targetTransform.name}[{targetTransform.GetSiblingIndex()}]";
        for (Transform parent = targetTransform.parent; parent != null; parent = parent.parent)
        {
            path = $"{parent.name}[{parent.GetSiblingIndex()}]/{path}";
        }

        return path;
    }

    private void RestoreDoorsToPreGauntletState()
    {
        if (doorStatesBeforeGauntlet == null)
            return;

        foreach (DoorStartState doorState in doorStatesBeforeGauntlet)
        {
            if (doorState.Door != null)
            {
                doorState.Door.SetActive(doorState.WasActive);
            }
        }
    }

    private void DestroyActiveWaveEnemies()
    {
        foreach (GameObject activeWaveEnemy in activeWaveEnemies)
        {
            if (activeWaveEnemy != null)
            {
                DestroyBossHelpers(activeWaveEnemy);
                Destroy(activeWaveEnemy);
            }
        }

        activeWaveEnemies.Clear();
    }

    private void DestroyBossHelpers(GameObject activeWaveEnemy)
    {
        if (activeWaveEnemy == null)
            return;

        BossBase[] bosses = activeWaveEnemy.GetComponentsInChildren<BossBase>(true);

        foreach (BossBase boss in bosses)
        {
            if (boss != null)
            {
                boss.DestroyActiveHelpers();
            }
        }
    }

    private void CheckActiveWaveEnemyBoundsPeriodically()
    {
        if (!isRunning || activeWaveEnemies.Count == 0)
            return;

        activeEnemyBoundsCheckUpdateCounter++;
        if (activeEnemyBoundsCheckUpdateCounter < ActiveEnemyBoundsCheckUpdateInterval)
            return;

        activeEnemyBoundsCheckUpdateCounter = 0;
        TeleportActiveEnemiesOutsideRoomBoundsToCenter();
    }

    private void TeleportActiveEnemiesOutsideRoomBoundsToCenter()
    {
        ResolveSpawnArea();
        if (spawnArea == null)
            return;

        for (int i = 0; i < activeWaveEnemies.Count; i++)
        {
            GameObject activeWaveEnemy = activeWaveEnemies[i];
            if (activeWaveEnemy == null || IsInsideGauntletBounds(activeWaveEnemy.transform.position))
                continue;

            TeleportEnemyToRoomCenter(activeWaveEnemy);
        }
    }

    private bool IsInsideGauntletBounds(Vector3 position)
    {
        ResolveSpawnArea();
        if (spawnArea == null)
            return true;

        Bounds bounds = spawnArea.bounds;
        return
            position.x >= bounds.min.x &&
            position.x <= bounds.max.x &&
            position.y >= bounds.min.y &&
            position.y <= bounds.max.y;
    }

    private void TeleportEnemyToRoomCenter(GameObject enemy)
    {
        if (enemy == null)
            return;

        Vector3 roomCenter = GetSpawnAreaCenter(enemy.transform.position.z);
        Rigidbody2D enemyRigidbody = enemy.GetComponent<Rigidbody2D>();
        if (enemyRigidbody != null)
        {
            enemyRigidbody.position = new Vector2(roomCenter.x, roomCenter.y);
            enemyRigidbody.linearVelocity = Vector2.zero;
            enemyRigidbody.angularVelocity = 0f;
        }

        enemy.transform.position = roomCenter;
    }

    private int CountSpawnableEnemies()
    {
        int enemyCount = 0;

        if (waves == null)
            return enemyCount;

        foreach (EnemyWave wave in waves)
        {
            if (wave == null || wave.Enemies == null)
                continue;

            foreach (EnemySpawn enemySpawn in wave.Enemies)
            {
                if (enemySpawn == null || enemySpawn.EnemyPrefab == null)
                    continue;

                enemyCount += enemySpawn.Count;
            }
        }

        return enemyCount;
    }

    private void LogLiveSceneEnemyPrefabReferenceError()
    {
        string errorMessage = BuildLiveSceneEnemyPrefabReferenceErrorMessage();
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogError(errorMessage, this);
        }
    }

    private void ThrowIfLiveSceneEnemyPrefabReferencesExist()
    {
        string errorMessage = BuildLiveSceneEnemyPrefabReferenceErrorMessage();
        if (!string.IsNullOrEmpty(errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private string BuildLiveSceneEnemyPrefabReferenceErrorMessage()
    {
        if (waves == null)
            return null;

        List<string> invalidReferences = null;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            EnemyWave wave = waves[waveIndex];
            if (wave == null || wave.Enemies == null)
                continue;

            for (int enemySpawnIndex = 0; enemySpawnIndex < wave.Enemies.Count; enemySpawnIndex++)
            {
                EnemySpawn enemySpawn = wave.Enemies[enemySpawnIndex];
                GameObject enemyPrefab = enemySpawn?.EnemyPrefab;

                if (!IsLiveSceneObject(enemyPrefab))
                    continue;

                if (invalidReferences == null)
                {
                    invalidReferences = new List<string>();
                }

                invalidReferences.Add(
                    $"waves[{waveIndex}].enemies[{enemySpawnIndex}] references " +
                    $"scene object '{enemyPrefab.name}' from scene '{enemyPrefab.scene.name}'"
                );
            }
        }

        if (invalidReferences == null || invalidReferences.Count == 0)
            return null;

        return
            $"{nameof(EnemyGauntletRoom)} on '{name}' has live scene objects assigned " +
            "to enemyPrefab slots. Assign prefab assets instead so killed scene enemies " +
            "cannot remove enemies from the gauntlet spawn list.\n" +
            string.Join("\n", invalidReferences);
    }

    private static bool IsLiveSceneObject(GameObject gameObject)
    {
        return gameObject != null && gameObject.scene.IsValid();
    }

    private void LogGauntletBoundsGroundOverlapError()
    {
        string errorMessage = BuildGauntletBoundsGroundOverlapErrorMessage();
        if (!string.IsNullOrEmpty(errorMessage))
        {
            Debug.LogError(errorMessage, this);
        }
    }

    private void ThrowIfGauntletBoundsIntersectGroundColliders()
    {
        string errorMessage = BuildGauntletBoundsGroundOverlapErrorMessage();
        if (!string.IsNullOrEmpty(errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private string BuildGauntletBoundsGroundOverlapErrorMessage()
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return null;

        Collider2D boundsSource = GetResolvedSpawnArea();
        int blockerMask = GetSpawnBlockerMaskValue();

        if (boundsSource == null || blockerMask == 0)
            return null;

        Bounds bounds = boundsSource.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return null;

        Collider2D[] overlappingColliders = Physics2D.OverlapBoxAll(
            new Vector2(bounds.center.x, bounds.center.y),
            new Vector2(bounds.size.x, bounds.size.y),
            0f,
            blockerMask
        );

        if (overlappingColliders == null || overlappingColliders.Length == 0)
            return null;

        List<string> overlapNames = new List<string>();
        foreach (Collider2D overlappingCollider in overlappingColliders)
        {
            if (overlappingCollider == null || overlappingCollider == boundsSource)
                continue;

            overlapNames.Add(
                $"'{overlappingCollider.name}' on layer " +
                $"'{LayerMask.LayerToName(overlappingCollider.gameObject.layer)}'"
            );
        }

        if (overlapNames.Count == 0)
            return null;

        return
            $"{nameof(EnemyGauntletRoom)} on '{name}' has gauntlet/spawn bounds " +
            "intersecting ground spawn blocker colliders. Move or resize the gauntlet " +
            "trigger/spawn area so it only covers valid enemy spawn space.\n" +
            string.Join("\n", overlapNames);
    }

    private void ResolveSpawnPose(
        EnemySpawn enemySpawn,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation
    )
    {
        bool hasBackupRandomPose = TryGetRandomRoomPose(
            enemySpawn,
            out spawnPosition,
            out spawnRotation,
            out bool randomPoseMeetsPreferredDistances
        );

        if (hasBackupRandomPose && randomPoseMeetsPreferredDistances)
        {
            return;
        }

        Vector3 backupRandomSpawnPosition = spawnPosition;
        Quaternion backupRandomSpawnRotation = spawnRotation;
        Transform assignedSpawnPoint = enemySpawn.SpawnPoint;

        if (TryResolveFallbackSpawnPose(enemySpawn, assignedSpawnPoint, out spawnPosition, out spawnRotation))
            return;

        if (TryGetNextFallbackSpawnPose(enemySpawn, out spawnPosition, out spawnRotation))
            return;

        if (hasBackupRandomPose)
        {
            spawnPosition = backupRandomSpawnPosition;
            spawnRotation = backupRandomSpawnRotation;
            return;
        }

        Vector3 preferredPosition = assignedSpawnPoint != null
            ? assignedSpawnPoint.position
            : transform.position;

        spawnPosition = PushPositionAwayFromPlayerAndActiveEnemies(preferredPosition);
        spawnRotation = assignedSpawnPoint != null
            ? assignedSpawnPoint.rotation
            : enemySpawn.EnemyPrefab.transform.rotation;

        if (TryFindGroundClearSpawnPosition(
            enemySpawn.EnemyPrefab,
            spawnPosition,
            spawnRotation,
            IsInsideSpawnArea,
            out Vector3 adjustedSpawnPosition
        ))
        {
            spawnPosition = adjustedSpawnPosition;
        }
        else
        {
            WarnUnclearableGroundSpawn(enemySpawn.EnemyPrefab, spawnPosition);
        }
    }

    private bool TryGetRandomRoomPose(
        EnemySpawn enemySpawn,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation,
        out bool spawnMeetsPreferredDistances
    )
    {
        ResolveSpawnArea();

        if (spawnArea == null)
        {
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;
            spawnMeetsPreferredDistances = false;
            return false;
        }

        Bounds bounds = spawnArea.bounds;
        int attempts = Mathf.Max(1, randomSpawnAttempts);
        spawnRotation = GetEnemySpawnRotation(enemySpawn);
        bool hasBestCandidate = false;
        bool bestCandidateMeetsPreferredDistances = false;
        float bestCandidateScore = float.NegativeInfinity;
        Vector3 bestSpawnPosition = Vector3.zero;

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidatePosition = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z
            );

            if (!IsInsideSpawnArea(candidatePosition))
                continue;

            if (!TryFindGroundClearSpawnPosition(
                enemySpawn.EnemyPrefab,
                candidatePosition,
                spawnRotation,
                IsInsideSpawnArea,
                out Vector3 adjustedSpawnPosition
            ))
            {
                continue;
            }

            RecordBestSpawnCandidate(
                adjustedSpawnPosition,
                ref hasBestCandidate,
                ref bestCandidateMeetsPreferredDistances,
                ref bestCandidateScore,
                ref bestSpawnPosition
            );
        }

        if (hasBestCandidate)
        {
            spawnPosition = bestSpawnPosition;
            spawnMeetsPreferredDistances = bestCandidateMeetsPreferredDistances;
            return true;
        }

        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;
        spawnMeetsPreferredDistances = false;
        return false;
    }

    private bool IsInsideSpawnArea(Vector3 position)
    {
        return spawnArea == null || spawnArea.OverlapPoint(position);
    }

    private bool IsFarEnoughFromOtherEnemies(Vector3 position)
    {
        if (minimumSpawnDistanceFromOtherEnemies <= 0f)
            return true;

        float minimumDistanceSqr =
            minimumSpawnDistanceFromOtherEnemies * minimumSpawnDistanceFromOtherEnemies;

        foreach (GameObject activeWaveEnemy in activeWaveEnemies)
        {
            if (activeWaveEnemy == null)
                continue;

            Vector2 activeEnemyPosition = activeWaveEnemy.transform.position;
            Vector2 candidatePosition = position;

            if ((candidatePosition - activeEnemyPosition).sqrMagnitude < minimumDistanceSqr)
                return false;
        }

        return true;
    }

    private bool TryGetNextFallbackSpawnPose(
        EnemySpawn enemySpawn,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation
    )
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        if (spawnPoints == null || spawnPoints.Length == 0)
            return false;

        bool hasBestCandidate = false;
        bool bestCandidateMeetsPreferredDistances = false;
        float bestCandidateScore = float.NegativeInfinity;
        Vector3 bestSpawnPosition = Vector3.zero;
        Quaternion bestSpawnRotation = Quaternion.identity;
        int bestSpawnPointIndex = -1;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int spawnPointIndex = (nextSpawnPointIndex + i) % spawnPoints.Length;
            Transform spawnPoint = spawnPoints[spawnPointIndex];

            if (!TryResolveFallbackSpawnPose(
                enemySpawn,
                spawnPoint,
                out Vector3 candidateSpawnPosition,
                out Quaternion candidateSpawnRotation
            ))
            {
                continue;
            }

            bool updatedBestCandidate = RecordBestSpawnCandidate(
                candidateSpawnPosition,
                ref hasBestCandidate,
                ref bestCandidateMeetsPreferredDistances,
                ref bestCandidateScore,
                ref bestSpawnPosition
            );

            if (updatedBestCandidate)
            {
                bestSpawnRotation = candidateSpawnRotation;
                bestSpawnPointIndex = spawnPointIndex;
            }
        }

        if (hasBestCandidate)
        {
            spawnPosition = bestSpawnPosition;
            spawnRotation = bestSpawnRotation;
            nextSpawnPointIndex = (bestSpawnPointIndex + 1) % spawnPoints.Length;
            return true;
        }

        return false;
    }

    private bool RecordBestSpawnCandidate(
        Vector3 candidatePosition,
        ref bool hasBestCandidate,
        ref bool bestCandidateMeetsPreferredDistances,
        ref float bestCandidateScore,
        ref Vector3 bestSpawnPosition
    )
    {
        bool candidateMeetsPreferredDistances = IsPreferredSpawnPosition(candidatePosition);
        float candidateScore = GetSpawnPreferenceScore(candidatePosition);

        if (hasBestCandidate &&
            !IsBetterSpawnCandidate(
                candidateMeetsPreferredDistances,
                candidateScore,
                bestCandidateMeetsPreferredDistances,
                bestCandidateScore
            ))
        {
            return false;
        }

        hasBestCandidate = true;
        bestCandidateMeetsPreferredDistances = candidateMeetsPreferredDistances;
        bestCandidateScore = candidateScore;
        bestSpawnPosition = candidatePosition;
        return true;
    }

    private static bool IsBetterSpawnCandidate(
        bool candidateMeetsPreferredDistances,
        float candidateScore,
        bool bestCandidateMeetsPreferredDistances,
        float bestCandidateScore
    )
    {
        if (candidateMeetsPreferredDistances != bestCandidateMeetsPreferredDistances)
            return candidateMeetsPreferredDistances;

        return candidateScore > bestCandidateScore;
    }

    private bool IsPreferredSpawnPosition(Vector3 position)
    {
        return IsFarEnoughFromPlayer(position) && IsFarEnoughFromOtherEnemies(position);
    }

    private float GetSpawnPreferenceScore(Vector3 position)
    {
        float score = 0f;

        if (playerTransform != null)
        {
            Vector2 playerPosition = playerTransform.position;
            Vector2 candidatePosition = position;
            score += Vector2.Distance(candidatePosition, playerPosition);
        }

        if (activeWaveEnemies.Count > 0)
        {
            score += GetNearestActiveEnemyDistance(position) * ActiveEnemySpawnSeparationScoreWeight;
        }

        return score;
    }

    private float GetNearestActiveEnemyDistance(Vector3 position)
    {
        float nearestDistance = float.PositiveInfinity;
        Vector2 candidatePosition = position;

        foreach (GameObject activeWaveEnemy in activeWaveEnemies)
        {
            if (activeWaveEnemy == null)
                continue;

            Vector2 activeEnemyPosition = activeWaveEnemy.transform.position;
            float distance = Vector2.Distance(candidatePosition, activeEnemyPosition);
            nearestDistance = Mathf.Min(nearestDistance, distance);
        }

        return float.IsPositiveInfinity(nearestDistance) ? 0f : nearestDistance;
    }

    private bool TryResolveFallbackSpawnPose(
        EnemySpawn enemySpawn,
        Transform spawnPoint,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation
    )
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        if (!IsValidFallbackSpawnPoint(spawnPoint))
            return false;

        spawnRotation = spawnPoint.rotation;
        return TryFindGroundClearSpawnPosition(
            enemySpawn.EnemyPrefab,
            spawnPoint.position,
            spawnRotation,
            IsValidFallbackSpawnPosition,
            out spawnPosition
        );
    }

    private bool IsValidFallbackSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null)
            return false;

        return IsValidFallbackSpawnPosition(spawnPoint.position);
    }

    private bool IsValidFallbackSpawnPosition(Vector3 position)
    {
        return IsPreferredSpawnPosition(position);
    }

    private Quaternion GetEnemySpawnRotation(EnemySpawn enemySpawn)
    {
        if (enemySpawn.SpawnPoint != null)
            return enemySpawn.SpawnPoint.rotation;

        return enemySpawn.EnemyPrefab.transform.rotation;
    }

    private bool TryFindGroundClearSpawnPosition(
        GameObject enemyPrefab,
        Vector3 startPosition,
        Quaternion spawnRotation,
        Predicate<Vector3> isCandidateAllowed,
        out Vector3 clearPosition
    )
    {
        clearPosition = startPosition;

        if (IsCandidateAllowed(isCandidateAllowed, startPosition) &&
            IsEnemyClearOfSpawnBlockers(enemyPrefab, startPosition, spawnRotation))
        {
            return true;
        }

        Vector3 roomCenter = GetSpawnAreaCenter(startPosition.z);
        int attempts = Mathf.Max(1, spawnBlockerAdjustmentAttempts);

        for (int i = 1; i <= attempts; i++)
        {
            float centerT = (float)i / attempts;
            Vector3 candidatePosition = Vector3.Lerp(startPosition, roomCenter, centerT);
            candidatePosition.z = startPosition.z;

            if (!IsCandidateAllowed(isCandidateAllowed, candidatePosition))
                continue;

            if (!IsEnemyClearOfSpawnBlockers(enemyPrefab, candidatePosition, spawnRotation))
                continue;

            clearPosition = candidatePosition;
            return true;
        }

        return false;
    }

    private static bool IsCandidateAllowed(Predicate<Vector3> isCandidateAllowed, Vector3 candidatePosition)
    {
        return isCandidateAllowed == null || isCandidateAllowed(candidatePosition);
    }

    private Vector3 GetSpawnAreaCenter(float z)
    {
        ResolveSpawnArea();

        Vector3 center = spawnArea != null
            ? spawnArea.bounds.center
            : transform.position;
        center.z = z;
        return center;
    }

    private bool IsEnemyClearOfSpawnBlockers(
        GameObject enemyPrefab,
        Vector3 spawnPosition,
        Quaternion spawnRotation
    )
    {
        int blockerMask = GetSpawnBlockerMaskValue();
        if (blockerMask == 0 || enemyPrefab == null)
            return true;

        Collider2D[] enemyColliders = enemyPrefab.GetComponentsInChildren<Collider2D>(true);
        bool checkedAnyCollider = false;

        foreach (Collider2D enemyCollider in enemyColliders)
        {
            if (!ShouldUsePrefabColliderForSpawnCheck(enemyCollider, enemyPrefab.transform))
                continue;

            checkedAnyCollider = true;
            if (DoesColliderOverlapSpawnBlockers(enemyPrefab, enemyCollider, spawnPosition, spawnRotation, blockerMask))
                return false;
        }

        if (!checkedAnyCollider)
        {
            Vector2 probeSize = GetPaddedSize(fallbackSpawnBlockerProbeSize);
            return Physics2D.OverlapBox(spawnPosition, probeSize, spawnRotation.eulerAngles.z, blockerMask) == null;
        }

        return true;
    }

    private bool DoesColliderOverlapSpawnBlockers(
        GameObject enemyPrefab,
        Collider2D enemyCollider,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        int blockerMask
    )
    {
        Transform prefabRoot = enemyPrefab.transform;
        Vector2 center = GetSpawnedWorldPoint(
            prefabRoot,
            enemyCollider.transform,
            enemyCollider.offset,
            spawnPosition,
            spawnRotation
        );
        float angle = GetSpawnedWorldAngle(prefabRoot, enemyCollider.transform, spawnRotation);

        if (enemyCollider is CircleCollider2D circleCollider)
        {
            Vector2 scale = GetAbsoluteLossyScale(circleCollider.transform);
            float radius = circleCollider.radius * Mathf.Max(scale.x, scale.y) + spawnBlockerPadding;
            return Physics2D.OverlapCircle(center, Mathf.Max(0.01f, radius), blockerMask) != null;
        }

        if (enemyCollider is CapsuleCollider2D capsuleCollider)
        {
            Vector2 size = GetPaddedSize(Vector2.Scale(capsuleCollider.size, GetAbsoluteLossyScale(capsuleCollider.transform)));
            return Physics2D.OverlapCapsule(
                center,
                size,
                capsuleCollider.direction,
                angle,
                blockerMask
            ) != null;
        }

        if (enemyCollider is BoxCollider2D boxCollider)
        {
            Vector2 size = GetPaddedSize(Vector2.Scale(boxCollider.size, GetAbsoluteLossyScale(boxCollider.transform)));
            return Physics2D.OverlapBox(center, size, angle, blockerMask) != null;
        }

        return Physics2D.OverlapBox(
            center,
            GetPaddedSize(fallbackSpawnBlockerProbeSize),
            angle,
            blockerMask
        ) != null;
    }

    private static bool ShouldUsePrefabColliderForSpawnCheck(Collider2D enemyCollider, Transform prefabRoot)
    {
        if (enemyCollider == null || !enemyCollider.enabled)
            return false;

        for (Transform current = enemyCollider.transform; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
                return false;

            if (current == prefabRoot)
                return true;
        }

        return false;
    }

    private int GetSpawnBlockerMaskValue()
    {
        if (spawnBlockerMask != 0)
            return spawnBlockerMask.value;

        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        if (groundLayerIndex >= 0)
            return 1 << groundLayerIndex;

        if (!warnedMissingGroundSpawnBlockerLayer)
        {
            warnedMissingGroundSpawnBlockerLayer = true;
            Debug.LogWarning(
                "EnemyGauntletRoom could not find a Ground layer for spawn collision checks.",
                this
            );
        }

        return 0;
    }

    private void WarnUnclearableGroundSpawn(GameObject enemyPrefab, Vector3 spawnPosition)
    {
        if (warnedUnclearableGroundSpawn)
            return;

        warnedUnclearableGroundSpawn = true;
        string prefabName = enemyPrefab != null ? enemyPrefab.name : "missing enemy prefab";
        Debug.LogWarning(
            $"{nameof(EnemyGauntletRoom)} on '{name}' could not find a ground-clear spawn " +
            $"position for '{prefabName}' while moving toward the room center. " +
            $"Using {spawnPosition}.",
            this
        );
    }

    private Vector2 GetPaddedSize(Vector2 size)
    {
        float padding = spawnBlockerPadding * 2f;
        return new Vector2(
            Mathf.Max(0.01f, Mathf.Abs(size.x) + padding),
            Mathf.Max(0.01f, Mathf.Abs(size.y) + padding)
        );
    }

    private static Vector2 GetAbsoluteLossyScale(Transform targetTransform)
    {
        Vector3 scale = targetTransform.lossyScale;
        return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }

    private static Vector2 GetSpawnedWorldPoint(
        Transform prefabRoot,
        Transform pointTransform,
        Vector2 localPoint,
        Vector3 spawnPosition,
        Quaternion spawnRotation
    )
    {
        Vector3 prefabWorldPoint = pointTransform.TransformPoint(localPoint);
        Vector3 rootSpacePoint = prefabRoot.InverseTransformPoint(prefabWorldPoint);
        Vector3 scaledRootSpacePoint = Vector3.Scale(rootSpacePoint, prefabRoot.localScale);
        Vector3 spawnedWorldPoint = spawnPosition + spawnRotation * scaledRootSpacePoint;
        return spawnedWorldPoint;
    }

    private static float GetSpawnedWorldAngle(
        Transform prefabRoot,
        Transform targetTransform,
        Quaternion spawnRotation
    )
    {
        Quaternion rootRelativeRotation = Quaternion.Inverse(prefabRoot.rotation) * targetTransform.rotation;
        Quaternion spawnedWorldRotation = spawnRotation * rootRelativeRotation;
        return spawnedWorldRotation.eulerAngles.z;
    }

    private bool IsFarEnoughFromPlayer(Vector3 position)
    {
        if (playerTransform == null || minimumSpawnDistanceFromPlayer <= 0f)
            return true;

        Vector2 playerPosition = playerTransform.position;
        Vector2 spawnPosition = position;
        float minimumDistanceSqr = minimumSpawnDistanceFromPlayer * minimumSpawnDistanceFromPlayer;

        return (spawnPosition - playerPosition).sqrMagnitude >= minimumDistanceSqr;
    }

    private Vector3 PushPositionAwayFromPlayer(Vector3 preferredPosition)
    {
        if (playerTransform == null || minimumSpawnDistanceFromPlayer <= 0f)
            return preferredPosition;

        Vector2 playerPosition = playerTransform.position;
        Vector2 preferredPosition2D = preferredPosition;
        Vector2 offsetFromPlayer = preferredPosition2D - playerPosition;

        if (offsetFromPlayer.sqrMagnitude <= Mathf.Epsilon)
        {
            offsetFromPlayer = Vector2.right;
        }

        Vector2 adjustedPosition =
            playerPosition + offsetFromPlayer.normalized * minimumSpawnDistanceFromPlayer;

        return new Vector3(adjustedPosition.x, adjustedPosition.y, preferredPosition.z);
    }

    private Vector3 PushPositionAwayFromPlayerAndActiveEnemies(Vector3 preferredPosition)
    {
        Vector3 adjustedPosition = PushPositionAwayFromPlayer(preferredPosition);
        return PushPositionAwayFromActiveEnemies(adjustedPosition);
    }

    private Vector3 PushPositionAwayFromActiveEnemies(Vector3 preferredPosition)
    {
        if (minimumSpawnDistanceFromOtherEnemies <= 0f || activeWaveEnemies.Count == 0)
            return preferredPosition;

        Vector2 adjustedPosition = preferredPosition;
        float minimumDistanceSqr =
            minimumSpawnDistanceFromOtherEnemies * minimumSpawnDistanceFromOtherEnemies;
        int maxPasses = Mathf.Max(1, activeWaveEnemies.Count);

        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool moved = false;

            for (int enemyIndex = 0; enemyIndex < activeWaveEnemies.Count; enemyIndex++)
            {
                GameObject activeWaveEnemy = activeWaveEnemies[enemyIndex];
                if (activeWaveEnemy == null)
                    continue;

                Vector2 activeEnemyPosition = activeWaveEnemy.transform.position;
                Vector2 offsetFromEnemy = adjustedPosition - activeEnemyPosition;

                if (offsetFromEnemy.sqrMagnitude >= minimumDistanceSqr)
                    continue;

                if (offsetFromEnemy.sqrMagnitude <= Mathf.Epsilon)
                {
                    offsetFromEnemy = GetFallbackSeparationDirection(
                        preferredPosition,
                        pass + enemyIndex
                    );
                }

                adjustedPosition =
                    activeEnemyPosition +
                    offsetFromEnemy.normalized * minimumSpawnDistanceFromOtherEnemies;
                moved = true;
            }

            if (!moved)
                break;
        }

        return new Vector3(adjustedPosition.x, adjustedPosition.y, preferredPosition.z);
    }

    private Vector2 GetFallbackSeparationDirection(Vector3 preferredPosition, int directionIndex)
    {
        if (playerTransform != null)
        {
            Vector2 awayFromPlayer = (Vector2)preferredPosition - (Vector2)playerTransform.position;
            if (awayFromPlayer.sqrMagnitude > Mathf.Epsilon)
                return awayFromPlayer.normalized;
        }

        float angle = directionIndex * 2.399963f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private void PlayGauntletMusic()
    {
        if (gauntletMusicClip == null || gauntletMusicActive)
            return;

        AudioSource cameraAudioSource = ResolveMainCameraAudioSource();
        if (cameraAudioSource == null)
            return;

        gauntletMusicAudioSource = cameraAudioSource;
        previousMusicClip = cameraAudioSource.clip;
        previousMusicLoop = cameraAudioSource.loop;
        previousMusicWasPlaying = cameraAudioSource.isPlaying;
        previousMusicTime = cameraAudioSource.time;

        cameraAudioSource.Stop();
        cameraAudioSource.clip = gauntletMusicClip;
        cameraAudioSource.loop = true;
        SetAudioSourceTime(cameraAudioSource, 0f);
        cameraAudioSource.Play();

        gauntletMusicActive = true;
    }

    private AudioSource ResolveMainCameraAudioSource()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            AudioSource cameraAudioSource = mainCamera.GetComponent<AudioSource>();
            if (cameraAudioSource != null)
                return cameraAudioSource;
        }

        if (!warnedMissingGauntletMusicAudioSource)
        {
            warnedMissingGauntletMusicAudioSource = true;
            Debug.LogWarning(
                "EnemyGauntletRoom could not find a Main Camera AudioSource for gauntlet music.",
                this
            );
        }

        return null;
    }

    private void StopGauntletMusic()
    {
        if (!gauntletMusicActive)
            return;

        AudioSource audioSource = gauntletMusicAudioSource;
        gauntletMusicActive = false;
        gauntletMusicAudioSource = null;

        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.clip = previousMusicClip;
        audioSource.loop = previousMusicLoop;

        if (previousMusicClip != null)
        {
            SetAudioSourceTime(audioSource, previousMusicTime);
        }

        if (previousMusicWasPlaying && previousMusicClip != null)
        {
            audioSource.Play();
        }
    }

    private static void SetAudioSourceTime(AudioSource audioSource, float time)
    {
        if (audioSource == null || audioSource.clip == null)
            return;

        float clipLength = audioSource.clip.length;
        if (clipLength <= 0f)
            return;

        audioSource.time = Mathf.Clamp(time, 0f, Mathf.Max(0f, clipLength - 0.01f));
    }

    private void ShowCounterLabel()
    {
        if (!ShouldShowDefeatedCounter())
        {
            RemoveCounterLabel();
            return;
        }

        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        RemoveCounterLabel();

        string resolvedLabelName = string.IsNullOrWhiteSpace(counterLabelName)
            ? DefaultCounterLabelName
            : counterLabelName;

        counterLabel = new Label
        {
            name = resolvedLabelName,
            pickingMode = PickingMode.Ignore
        };

        ApplyGauntletLabelStyle(counterLabel, counterTopOffset);

        resolvedUiDocument.rootVisualElement.Add(counterLabel);
    }

    private void ShowBannerMessage()
    {
        if (string.IsNullOrWhiteSpace(bannerText))
            return;

        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        VisualElement root = resolvedUiDocument.rootVisualElement;

        RemoveBannerLabel();

        Label existingMessage = root.Q<Label>(BannerLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        bannerLabel = new Label(bannerText.Trim())
        {
            name = BannerLabelName,
            pickingMode = PickingMode.Ignore
        };

        ApplyGauntletLabelStyle(bannerLabel, bannerTopOffset);

        root.Add(bannerLabel);
        ScheduleBannerRemoval(root, bannerLabel);
    }

    private static void ApplyGauntletLabelStyle(Label label, float topOffset)
    {
        if (label == null)
            return;

        label.style.position = Position.Absolute;
        label.style.top = topOffset;
        label.style.left = 0f;
        label.style.right = 0f;
        label.style.marginLeft = StyleKeyword.Auto;
        label.style.marginRight = StyleKeyword.Auto;
        label.style.paddingLeft = 14f;
        label.style.paddingRight = 14f;
        label.style.paddingTop = 8f;
        label.style.paddingBottom = 8f;
        label.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        label.style.color = Color.white;
        label.style.fontSize = 22f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.borderTopLeftRadius = 8f;
        label.style.borderTopRightRadius = 8f;
        label.style.borderBottomLeftRadius = 8f;
        label.style.borderBottomRightRadius = 8f;
        label.style.alignSelf = Align.Center;
    }

    private UIDocument ResolveUiDocument()
    {
        if (uiDocument != null)
            return uiDocument;

        uiDocument = FindAnyObjectByType<UIDocument>();
        if (uiDocument != null)
            return uiDocument;

        if (!warnedMissingUiDocument)
        {
            warnedMissingUiDocument = true;
            Debug.LogWarning("EnemyGauntletRoom could not find a UIDocument for the gauntlet UI.");
        }

        return null;
    }

    private void RefreshCounterLabel()
    {
        if (!ShouldShowDefeatedCounter())
        {
            RemoveCounterLabel();
            return;
        }

        if (counterLabel != null)
        {
            counterLabel.text = $"{defeatedEnemyCount}/{totalEnemyCount} Defeated";
        }
    }

    private bool ShouldShowDefeatedCounter()
    {
        return totalEnemyCount != 1;
    }

    private void RemoveCounterLabel()
    {
        if (counterLabel != null)
        {
            counterLabel.RemoveFromHierarchy();
            counterLabel = null;
        }
    }

    private void ScheduleBannerRemoval(VisualElement root, Label messageLabel)
    {
        int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, bannerDuration) * 1000f);
        if (delayMilliseconds <= 0)
        {
            RemoveBannerLabel(messageLabel);
            return;
        }

        root.schedule.Execute(() => RemoveBannerLabel(messageLabel)).StartingIn(delayMilliseconds);
    }

    private void RemoveBannerLabel()
    {
        RemoveBannerLabel(bannerLabel);
    }

    private void RemoveBannerLabel(Label messageLabel)
    {
        if (messageLabel != null && messageLabel.parent != null)
        {
            messageLabel.RemoveFromHierarchy();
        }

        if (bannerLabel == messageLabel)
        {
            bannerLabel = null;
        }
    }

    private static Transform GetPlayerTransform(Collider2D other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController.transform;

            Hero rigidbodyHero = other.attachedRigidbody.GetComponent<Hero>();
            if (rigidbodyHero != null)
                return rigidbodyHero.transform;

            if (other.attachedRigidbody.CompareTag("Player"))
                return other.attachedRigidbody.transform;
        }

        JonCharacterController playerController = other.GetComponentInParent<JonCharacterController>();
        if (playerController != null)
            return playerController.transform;

        Hero hero = other.GetComponentInParent<Hero>();
        if (hero != null)
            return hero.transform;

        if (other.CompareTag("Player"))
            return other.transform;

        return null;
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void ResolveSpawnArea()
    {
        if (spawnArea != null)
            return;

        spawnArea = GetResolvedSpawnArea();
    }

    private Collider2D GetResolvedSpawnArea()
    {
        if (spawnArea != null)
            return spawnArea;

        if (triggerCollider != null)
            return triggerCollider;

        return GetComponent<Collider2D>();
    }
}

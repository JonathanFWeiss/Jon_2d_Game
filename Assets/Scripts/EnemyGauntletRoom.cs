using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class EnemyGauntletRoom : MonoBehaviour
{
    private const string DefaultCounterLabelName = "GauntletDefeatedCounter";
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

    [Header("UI")]
    [Tooltip("If left empty, the first UIDocument in the scene is used.")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string counterLabelName = DefaultCounterLabelName;
    [SerializeField] private float counterTopOffset = 86f;

    private readonly List<GameObject> activeWaveEnemies = new List<GameObject>();
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
    }

    private void OnEnable()
    {
        PlayerData.PlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerData.PlayerDied -= HandlePlayerDied;
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
        ShowCounterLabel();
        RefreshCounterLabel();
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
        DestroyActiveWaveEnemies();
        RestoreDoorsToPreGauntletState();
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

        DestroyDoors();
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
                Destroy(door);
            }
        }
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
                Destroy(activeWaveEnemy);
            }
        }

        activeWaveEnemies.Clear();
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
        if (TryGetRandomRoomPose(enemySpawn, out spawnPosition, out spawnRotation))
        {
            return;
        }

        Transform assignedSpawnPoint = enemySpawn.SpawnPoint;

        if (TryResolveFallbackSpawnPose(enemySpawn, assignedSpawnPoint, out spawnPosition, out spawnRotation))
            return;

        if (TryGetNextFallbackSpawnPose(enemySpawn, out spawnPosition, out spawnRotation))
            return;

        Vector3 preferredPosition = assignedSpawnPoint != null
            ? assignedSpawnPoint.position
            : transform.position;

        spawnPosition = PushPositionAwayFromPlayer(preferredPosition);
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
        out Quaternion spawnRotation
    )
    {
        ResolveSpawnArea();

        if (spawnArea == null)
        {
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;
            return false;
        }

        Bounds bounds = spawnArea.bounds;
        int attempts = Mathf.Max(1, randomSpawnAttempts);
        spawnRotation = GetEnemySpawnRotation(enemySpawn);

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidatePosition = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z
            );

            if (!IsValidRandomSpawnPosition(candidatePosition))
                continue;

            if (!TryFindGroundClearSpawnPosition(
                enemySpawn.EnemyPrefab,
                candidatePosition,
                spawnRotation,
                IsValidRandomSpawnPosition,
                out spawnPosition
            ))
            {
                continue;
            }

            return true;
        }

        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;
        return false;
    }

    private bool IsValidRandomSpawnPosition(Vector3 position)
    {
        if (!IsInsideSpawnArea(position))
            return false;

        if (!IsFarEnoughFromPlayer(position))
            return false;

        return IsFarEnoughFromOtherEnemies(position);
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

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int spawnPointIndex = (nextSpawnPointIndex + i) % spawnPoints.Length;
            Transform spawnPoint = spawnPoints[spawnPointIndex];

            if (!TryResolveFallbackSpawnPose(enemySpawn, spawnPoint, out spawnPosition, out spawnRotation))
                continue;

            nextSpawnPointIndex = (spawnPointIndex + 1) % spawnPoints.Length;
            return true;
        }

        return false;
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
        if (!IsFarEnoughFromPlayer(position))
            return false;

        return IsFarEnoughFromOtherEnemies(position);
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

    private void ShowCounterLabel()
    {
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

        counterLabel.style.position = Position.Absolute;
        counterLabel.style.top = counterTopOffset;
        counterLabel.style.left = 0f;
        counterLabel.style.right = 0f;
        counterLabel.style.marginLeft = StyleKeyword.Auto;
        counterLabel.style.marginRight = StyleKeyword.Auto;
        counterLabel.style.paddingLeft = 14f;
        counterLabel.style.paddingRight = 14f;
        counterLabel.style.paddingTop = 8f;
        counterLabel.style.paddingBottom = 8f;
        counterLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        counterLabel.style.color = Color.white;
        counterLabel.style.fontSize = 22f;
        counterLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        counterLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        counterLabel.style.borderTopLeftRadius = 8f;
        counterLabel.style.borderTopRightRadius = 8f;
        counterLabel.style.borderBottomLeftRadius = 8f;
        counterLabel.style.borderBottomRightRadius = 8f;
        counterLabel.style.alignSelf = Align.Center;

        resolvedUiDocument.rootVisualElement.Add(counterLabel);
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
            Debug.LogWarning("EnemyGauntletRoom could not find a UIDocument for the defeated counter.");
        }

        return null;
    }

    private void RefreshCounterLabel()
    {
        if (counterLabel != null)
        {
            counterLabel.text = $"{defeatedEnemyCount}/{totalEnemyCount} Defeated";
        }
    }

    private void RemoveCounterLabel()
    {
        if (counterLabel != null)
        {
            counterLabel.RemoveFromHierarchy();
            counterLabel = null;
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

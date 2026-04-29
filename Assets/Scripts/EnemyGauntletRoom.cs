using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class EnemyGauntletRoom : MonoBehaviour
{
    private const string DefaultCounterLabelName = "GauntletDefeatedCounter";

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

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveSpawnArea();
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
        isRunning = true;
        currentWaveIndex = -1;
        defeatedEnemyCount = 0;
        totalEnemyCount = CountSpawnableEnemies();

        ActivateDoors();
        ShowCounterLabel();
        RefreshCounterLabel();
        StartNextWaveOrComplete();
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

    private void ResolveSpawnPose(
        EnemySpawn enemySpawn,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation
    )
    {
        if (TryGetRandomRoomPosition(out spawnPosition))
        {
            spawnRotation = enemySpawn.SpawnPoint != null
                ? enemySpawn.SpawnPoint.rotation
                : enemySpawn.EnemyPrefab.transform.rotation;
            return;
        }

        Transform assignedSpawnPoint = enemySpawn.SpawnPoint;

        if (IsValidFallbackSpawnPoint(assignedSpawnPoint))
        {
            spawnPosition = assignedSpawnPoint.position;
            spawnRotation = assignedSpawnPoint.rotation;
            return;
        }

        Transform fallbackSpawnPoint = GetNextFarEnoughSpawnPoint();
        if (fallbackSpawnPoint != null)
        {
            spawnPosition = fallbackSpawnPoint.position;
            spawnRotation = fallbackSpawnPoint.rotation;
            return;
        }

        Vector3 preferredPosition = assignedSpawnPoint != null
            ? assignedSpawnPoint.position
            : transform.position;

        spawnPosition = PushPositionAwayFromPlayer(preferredPosition);
        spawnRotation = assignedSpawnPoint != null
            ? assignedSpawnPoint.rotation
            : enemySpawn.EnemyPrefab.transform.rotation;
    }

    private bool TryGetRandomRoomPosition(out Vector3 spawnPosition)
    {
        ResolveSpawnArea();

        if (spawnArea == null)
        {
            spawnPosition = Vector3.zero;
            return false;
        }

        Bounds bounds = spawnArea.bounds;
        int attempts = Mathf.Max(1, randomSpawnAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidatePosition = new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                transform.position.z
            );

            if (!IsValidRandomSpawnPosition(candidatePosition))
                continue;

            spawnPosition = candidatePosition;
            return true;
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool IsValidRandomSpawnPosition(Vector3 position)
    {
        if (spawnArea != null && !spawnArea.OverlapPoint(position))
            return false;

        if (!IsFarEnoughFromPlayer(position))
            return false;

        return IsFarEnoughFromOtherEnemies(position);
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

    private Transform GetNextFarEnoughSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int spawnPointIndex = (nextSpawnPointIndex + i) % spawnPoints.Length;
            Transform spawnPoint = spawnPoints[spawnPointIndex];

            if (!IsValidFallbackSpawnPoint(spawnPoint))
                continue;

            nextSpawnPointIndex = (spawnPointIndex + 1) % spawnPoints.Length;
            return spawnPoint;
        }

        return null;
    }

    private bool IsValidFallbackSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint == null)
            return false;

        if (!IsFarEnoughFromPlayer(spawnPoint.position))
            return false;

        return IsFarEnoughFromOtherEnemies(spawnPoint.position);
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

        spawnArea = triggerCollider != null
            ? triggerCollider
            : GetComponent<Collider2D>();
    }
}

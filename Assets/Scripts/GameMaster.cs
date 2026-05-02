using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class GameMaster : MonoBehaviour
{
    private const string DeathMessageText = "Try Again!";
    private const string DeathMessageLabelName = "DeathMessage";
    private const string PauseMenuText = "paused";
    private const string PauseMenuRootName = "PauseMenu";
    private const string PauseMenuLabelName = "PauseMenuLabel";
    private const string HeartIconsRootName = "HeartIcons";
    private const float HeartIconSpacing = 1f;

    private static readonly BindingFlags PlayerStateBindingFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] PlayerInputActionNames =
    {
        "Move",
        "Jump",
        "Dash",
        "Attack",
        "JumpCut",
        "Pogo",
        "UpSlash",
        "Spell"
    };
    private static readonly string[] JonCharacterInputRequestFieldNames =
    {
        "jumpRequested",
        "dashRequested",
        "attackRequested",
        "jumpcutRequested",
        "pogoRequested",
        "upSlashRequested"
    };

    [Header("Respawn")]
    [Tooltip("Player object to respawn. If left empty, GameMaster will try to find one.")]
    public Transform playerTransform;

    [Tooltip("Optional respawn point. If left empty, the player's starting position is used.")]
    public Transform respawnPoint;

    [Min(0f)]
    [Tooltip("How long to wait after death before respawning the player.")]
    public float deathRespawnDelay = 2f;

    [Header("UI")]
    [Tooltip("UI Toolkit document that contains the Counters label.")]
    public UIDocument uiDocument;

    [Tooltip("Name of the label used to show player counters.")]
    public string countersLabelName = "Counters";

    [Tooltip("Prefab instantiated to show player HP in the upper-left corner of the screen.")]
    public GameObject heartIconPrefab;

    private Label uiCountersText;
    private readonly List<GameObject> heartIcons = new List<GameObject>();
    private int lastDisplayedCoins = int.MinValue;
    private int lastDisplayedEnergy = int.MinValue;
    private int lastDisplayedHp = int.MinValue;
    private bool warnedMissingUiDocument;
    private bool warnedMissingCountersLabel;
    private bool warnedMissingPlayer;
    private bool warnedMissingHeartIconPrefab;
    private bool warnedMissingMainCamera;
    private bool warnedMissingPauseAction;
    private bool isRespawningPlayer;
    private bool isPaused;
    private bool pauseActionEnabledByGameMaster;
    private float timeScaleBeforePause = 1f;
    private Vector3 checkpointRespawnPosition;
    private bool hasCheckpointRespawnPosition;
    private Vector3 fallbackRespawnPosition;
    private bool hasFallbackRespawnPosition;
    private Transform heartIconsRoot;
    private InputAction pauseAction;
    public Animator transitionAnim;

    private void Awake()
    {
        ResolvePlayer();
        CacheFallbackRespawnPosition();
        ResolveUiDocument();
        CacheUiReferences();
        RefreshCounters(forceRefresh: true);
        Application.targetFrameRate = 60;
    }

    private void OnEnable()
    {
        SubscribePauseAction();
    }

    private void OnDisable()
    {
        UnsubscribePauseAction();

        if (isPaused)
        {
            SetPaused(false);
        }

        if (isRespawningPlayer)
        {
            SetPlayerInputEnabled(true);
        }
    }

    private void Update()
    {
        if (pauseAction == null)
        {Debug.LogWarning("Trying to pause.");
            SubscribePauseAction();
        }

        ResolvePlayer();

        if (!isRespawningPlayer && PlayerData.HP <= 0)
        {
            StartCoroutine(RespawnPlayerAfterDelay());
            ResetPlayerIsStateFlags();
            ResetPlayerStatsAfterDeath();
            
        }

        if (uiCountersText == null)
        {
            ResolveUiDocument();
            CacheUiReferences();
        }

        RefreshCounters();
    }

    private void SubscribePauseAction()
    {
        if (pauseAction != null)
            return;

        InputActionAsset inputActions = InputSystem.actions;
        if (inputActions == null)
        {
            WarnMissingPauseAction();
            return;
        }

        pauseAction = inputActions.FindAction("Pause", throwIfNotFound: false);
        if (pauseAction == null)
        {
            WarnMissingPauseAction();
            return;
        }

        pauseAction.performed += OnPausePerformed;

        pauseActionEnabledByGameMaster = !pauseAction.enabled;
        if (pauseActionEnabledByGameMaster)
        {
            pauseAction.Enable();
        }
    }

    private void UnsubscribePauseAction()
    {
        if (pauseAction == null)
            return;

        pauseAction.performed -= OnPausePerformed;

        if (pauseActionEnabledByGameMaster)
        {
            pauseAction.Disable();
        }

        pauseAction = null;
        pauseActionEnabledByGameMaster = false;
    }

    private void WarnMissingPauseAction()
    {
        if (warnedMissingPauseAction)
            return;

        warnedMissingPauseAction = true;
        Debug.LogWarning("GameMaster could not find an Input Action named 'Pause' in InputSystem.actions.");
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (isRespawningPlayer || PlayerData.HP <= 0)
            return;

        SetPaused(!isPaused);
    }

    private void SetPaused(bool shouldPause)
    {
        if (isPaused == shouldPause)
            return;

        isPaused = shouldPause;

        if (isPaused)
        {
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            SetPlayerInputEnabled(false);
            ShowPauseMenu();
            return;
        }

        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        HidePauseMenu();

        if (!isRespawningPlayer)
        {
            SetPlayerInputEnabled(true);
        }
    }

    private void ResolveUiDocument()
    {
        if (uiDocument != null)
            return;

        uiDocument = FindAnyObjectByType<UIDocument>();

        if (uiDocument == null && !warnedMissingUiDocument)
        {
            warnedMissingUiDocument = true;
            Debug.LogWarning("GameMaster could not find a UIDocument. Assign one in the Inspector.");
        }
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null)
            return;

        JonCharacterController jonCharacter = FindAnyObjectByType<JonCharacterController>();
        if (jonCharacter != null)
        {
            playerTransform = jonCharacter.transform;
            CacheFallbackRespawnPosition();
            return;
        }

        Hero hero = FindAnyObjectByType<Hero>();
        if (hero != null)
        {
            playerTransform = hero.transform;
            CacheFallbackRespawnPosition();
            return;
        }

        if (!warnedMissingPlayer)
        {
            warnedMissingPlayer = true;
            Debug.LogWarning("GameMaster could not find a player to respawn. Assign one in the Inspector.");
        }
    }

    private void CacheFallbackRespawnPosition()
    {
        if (playerTransform == null || hasFallbackRespawnPosition)
            return;

        fallbackRespawnPosition = playerTransform.position;
        hasFallbackRespawnPosition = true;
    }

    public bool TryGetCurrentRespawnPosition(out Vector3 respawnPosition)
    {
        if (hasCheckpointRespawnPosition)
        {
            respawnPosition = checkpointRespawnPosition;
            return true;
        }

        if (respawnPoint != null)
        {
            respawnPosition = respawnPoint.position;
            return true;
        }

        if (hasFallbackRespawnPosition)
        {
            respawnPosition = fallbackRespawnPosition;
            return true;
        }

        ResolvePlayer();

        if (playerTransform != null)
        {
            respawnPosition = playerTransform.position;
            return true;
        }

        respawnPosition = Vector3.zero;
        return false;
    }

    public void SetFallbackRespawnPosition(Vector3 newRespawnPosition)
    {
        fallbackRespawnPosition = newRespawnPosition;
        hasFallbackRespawnPosition = true;
    }

    public bool TrySetCheckpointRespawnPosition(Vector3 newRespawnPosition, float minimumDistance = 0f)
    {
        if (minimumDistance > 0f &&
            TryGetCurrentRespawnPosition(out Vector3 currentRespawnPosition) &&
            (currentRespawnPosition - newRespawnPosition).sqrMagnitude <= minimumDistance * minimumDistance)
        {
            return false;
        }

        checkpointRespawnPosition = newRespawnPosition;
        hasCheckpointRespawnPosition = true;
        return true;
    }

    private void CacheUiReferences()
    {
        if (uiDocument == null)
            return;

        uiCountersText = uiDocument.rootVisualElement.Q<Label>(countersLabelName);

        if (uiCountersText == null && !warnedMissingCountersLabel)
        {
            warnedMissingCountersLabel = true;
            Debug.LogWarning(
                $"GameMaster could not find a Label named '{countersLabelName}' in {uiDocument.name}."
            );
        }
    }

    private void RefreshCounters(bool forceRefresh = false)
    {
        RefreshHeartIcons();

        if (uiCountersText == null)
            return;

        if (!forceRefresh &&
            PlayerData.Coins == lastDisplayedCoins &&
            PlayerData.Energy == lastDisplayedEnergy &&
            PlayerData.HP == lastDisplayedHp)
        {
            return;
        }

        lastDisplayedCoins = PlayerData.Coins;
        lastDisplayedEnergy = PlayerData.Energy;
        lastDisplayedHp = PlayerData.HP;

        uiCountersText.text = $"Coins: {PlayerData.Coins} Energy: {PlayerData.Energy} HP: {PlayerData.HP}";
        uiCountersText.style.display = DisplayStyle.Flex;
    }

    private static void ResetPlayerStatsAfterDeath()
    {
        PlayerData.ResetEnergy();
        PlayerData.RestoreFullHP();
    }

    private void RefreshHeartIcons()
    {
        if (heartIconPrefab == null)
        {
            if (!warnedMissingHeartIconPrefab)
            {
                warnedMissingHeartIconPrefab = true;
                Debug.LogWarning("GameMaster could not find the HeartIcon prefab. Assign one in the Inspector.");
            }

            return;
        }

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            if (!warnedMissingMainCamera)
            {
                warnedMissingMainCamera = true;
                Debug.LogWarning("GameMaster could not find a Main Camera to position HeartIcons.");
            }

            return;
        }

        EnsureHeartIconsRoot();
        PruneMissingHeartIcons();
        EnsureHeartIconCount(Mathf.Max(PlayerData.HP, 0));
        LayoutHeartIcons(activeCamera);
    }

    private void EnsureHeartIconsRoot()
    {
        if (heartIconsRoot != null)
            return;

        Transform existingRoot = transform.Find(HeartIconsRootName);
        if (existingRoot != null)
        {
            heartIconsRoot = existingRoot;
            return;
        }

        GameObject heartIconsRootObject = new GameObject(HeartIconsRootName);
        heartIconsRoot = heartIconsRootObject.transform;
        heartIconsRoot.SetParent(transform);
        heartIconsRoot.localPosition = Vector3.zero;
        heartIconsRoot.localRotation = Quaternion.identity;
        heartIconsRoot.localScale = Vector3.one;
    }

    private void PruneMissingHeartIcons()
    {
        for (int i = heartIcons.Count - 1; i >= 0; i--)
        {
            if (heartIcons[i] == null)
            {
                heartIcons.RemoveAt(i);
            }
        }
    }

    private void EnsureHeartIconCount(int targetHeartCount)
    {
        while (heartIcons.Count < targetHeartCount)
        {
            GameObject heartIcon = Instantiate(heartIconPrefab, heartIconsRoot);
            heartIcons.Add(heartIcon);
        }

        while (heartIcons.Count > targetHeartCount)
        {
            int lastHeartIndex = heartIcons.Count - 1;
            GameObject heartIcon = heartIcons[lastHeartIndex];
            heartIcons.RemoveAt(lastHeartIndex);

            if (heartIcon == null)
                continue;

            if (Application.isPlaying)
            {
                Destroy(heartIcon);
            }
            else
            {
                DestroyImmediate(heartIcon);
            }
        }
    }

    private void LayoutHeartIcons(Camera activeCamera)
    {
        float worldDepth = Mathf.Abs(activeCamera.transform.position.z);
        Vector3 upperLeftCorner = activeCamera.ViewportToWorldPoint(new Vector3(0f, 1f, worldDepth));
        Vector3 firstHeartPosition = upperLeftCorner;

        if (heartIcons.Count > 0)
        {
            SpriteRenderer firstHeartRenderer = heartIcons[0].GetComponent<SpriteRenderer>();
            if (firstHeartRenderer != null)
            {
                Vector3 heartExtents = firstHeartRenderer.bounds.extents;
                firstHeartPosition.x += heartExtents.x;
                firstHeartPosition.y -= heartExtents.y;
            }
        }

        firstHeartPosition.z = 0f;

        for (int i = 0; i < heartIcons.Count; i++)
        {
            GameObject heartIcon = heartIcons[i];
            if (heartIcon == null)
                continue;

            heartIcon.transform.position = firstHeartPosition + (Vector3.right * (i * HeartIconSpacing));
        }
    }

    private void ShowDeathMessage()
    {
        ResolveUiDocument();
        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        Label existingMessage = root.Q<Label>(DeathMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label deathMessage = new Label(DeathMessageText)
        {
            name = DeathMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        deathMessage.style.position = Position.Absolute;
        deathMessage.style.top = 48f;
        deathMessage.style.left = 0f;
        deathMessage.style.right = 0f;
        deathMessage.style.marginLeft = StyleKeyword.Auto;
        deathMessage.style.marginRight = StyleKeyword.Auto;
        deathMessage.style.paddingLeft = 14f;
        deathMessage.style.paddingRight = 14f;
        deathMessage.style.paddingTop = 8f;
        deathMessage.style.paddingBottom = 8f;
        deathMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        deathMessage.style.color = Color.white;
        deathMessage.style.fontSize = 22f;
        deathMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        deathMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        deathMessage.style.borderTopLeftRadius = 8f;
        deathMessage.style.borderTopRightRadius = 8f;
        deathMessage.style.borderBottomLeftRadius = 8f;
        deathMessage.style.borderBottomRightRadius = 8f;
        deathMessage.style.alignSelf = Align.Center;

        root.Add(deathMessage);
    }

    private void ShowPauseMenu()
    {
        ResolveUiDocument();
        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        VisualElement existingMenu = root.Q<VisualElement>(PauseMenuRootName);
        if (existingMenu != null)
        {
            existingMenu.RemoveFromHierarchy();
        }

        VisualElement pauseMenu = new VisualElement
        {
            name = PauseMenuRootName,
            pickingMode = PickingMode.Ignore
        };

        pauseMenu.style.position = Position.Absolute;
        pauseMenu.style.top = 0f;
        pauseMenu.style.left = 0f;
        pauseMenu.style.right = 0f;
        pauseMenu.style.bottom = 0f;
        pauseMenu.style.alignItems = Align.Center;
        pauseMenu.style.justifyContent = Justify.Center;

        Label pausedLabel = new Label(PauseMenuText)
        {
            name = PauseMenuLabelName,
            pickingMode = PickingMode.Ignore
        };

        pausedLabel.style.paddingLeft = 18f;
        pausedLabel.style.paddingRight = 18f;
        pausedLabel.style.paddingTop = 10f;
        pausedLabel.style.paddingBottom = 10f;
        pausedLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        pausedLabel.style.color = Color.white;
        pausedLabel.style.fontSize = 22f;
        pausedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pausedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        pausedLabel.style.borderTopLeftRadius = 8f;
        pausedLabel.style.borderTopRightRadius = 8f;
        pausedLabel.style.borderBottomLeftRadius = 8f;
        pausedLabel.style.borderBottomRightRadius = 8f;

        pauseMenu.Add(pausedLabel);
        root.Add(pauseMenu);
    }

    private void HideDeathMessage()
    {
        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        Label existingMessage = root.Q<Label>(DeathMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }
    }

    private void HidePauseMenu()
    {
        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        VisualElement existingMenu = root.Q<VisualElement>(PauseMenuRootName);
        if (existingMenu != null)
        {
            existingMenu.RemoveFromHierarchy();
        }
    }

    private void ResetPlayerIsStateFlags()
    {
        Transform playerRoot = GetPlayerStateRoot();
        if (playerRoot == null)
            return;

        HashSet<MonoBehaviour> processedBehaviours = new HashSet<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in playerRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || !processedBehaviours.Add(behaviour))
                continue;

            ResetBoolMembersWithIsPrefix(behaviour);
        }

        foreach (Animator animator in playerRoot.GetComponentsInChildren<Animator>(true))
        {
            ResetAnimatorBoolParameters(animator);
        }
    }

    private Transform GetPlayerStateRoot()
    {
        if (playerTransform == null)
            return null;

        JonCharacterController jonCharacter = playerTransform.GetComponentInParent<JonCharacterController>();
        if (jonCharacter != null)
            return jonCharacter.transform;

        Hero hero = playerTransform.GetComponentInParent<Hero>();
        if (hero != null)
            return hero.transform;

        return playerTransform;
    }

    private static void ResetBoolMembersWithIsPrefix(MonoBehaviour behaviour)
    {
        Type behaviourType = behaviour.GetType();

        foreach (FieldInfo field in behaviourType.GetFields(PlayerStateBindingFlags))
        {
            if (field.FieldType != typeof(bool) ||
                field.IsInitOnly ||
                field.IsLiteral ||
                !HasIsPrefix(field.Name))
            {
                continue;
            }

            field.SetValue(behaviour, false);
        }

        foreach (PropertyInfo property in behaviourType.GetProperties(PlayerStateBindingFlags))
        {
            if (property.PropertyType != typeof(bool) ||
                property.GetIndexParameters().Length > 0 ||
                !HasIsPrefix(property.Name))
            {
                continue;
            }

            MethodInfo setter = property.GetSetMethod(true);
            if (setter == null)
                continue;

            property.SetValue(behaviour, false);
        }
    }

    private static void ResetAnimatorBoolParameters(Animator animator)
    {
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool || !HasIsPrefix(parameter.name))
                continue;

            animator.SetBool(parameter.name, false);
        }
    }

    private static bool HasIsPrefix(string memberName)
    {
        return memberName.StartsWith("is", StringComparison.Ordinal) ||
               memberName.StartsWith("Is", StringComparison.Ordinal);
    }

    private void SetPlayerInputEnabled(bool isEnabled)
    {
        InputActionAsset inputActions = InputSystem.actions;
        if (inputActions != null)
        {
            foreach (string actionName in PlayerInputActionNames)
            {
                InputAction action = inputActions.FindAction(actionName, throwIfNotFound: false);
                if (action == null)
                    continue;

                if (isEnabled)
                {
                    action.Enable();
                }
                else
                {
                    action.Disable();
                }
            }
        }

        Transform playerRoot = GetPlayerStateRoot();
        if (playerRoot == null)
            return;

        Hero hero = playerRoot.GetComponentInChildren<Hero>(true);
        if (hero != null)
        {
            hero.enabled = isEnabled;
        }

        PlayerInputHandler inputHandler = playerRoot.GetComponentInChildren<PlayerInputHandler>(true);
        if (inputHandler != null)
        {
            inputHandler.enabled = isEnabled;
        }

        if (!isEnabled)
        {
            JonCharacterController jonCharacter = playerRoot.GetComponentInChildren<JonCharacterController>(true);
            if (jonCharacter != null)
            {
                ClearJonCharacterInputState(jonCharacter);
            }
        }
    }

    private static void ClearJonCharacterInputState(JonCharacterController jonCharacter)
    {
        if (jonCharacter == null)
            return;

        Type controllerType = typeof(JonCharacterController);
        foreach (string fieldName in JonCharacterInputRequestFieldNames)
        {
            FieldInfo requestField = controllerType.GetField(fieldName, PlayerStateBindingFlags);
            if (requestField?.FieldType == typeof(bool))
            {
                requestField.SetValue(jonCharacter, false);
            }
        }

        FieldInfo jumpBufferField = controllerType.GetField("jumpBufferExpireTime", PlayerStateBindingFlags);
        if (jumpBufferField?.FieldType == typeof(float))
        {
            jumpBufferField.SetValue(jonCharacter, float.NegativeInfinity);
        }

        FieldInfo movementField = controllerType.GetField("movementVector", PlayerStateBindingFlags);
        if (movementField?.FieldType == typeof(Vector2))
        {
            movementField.SetValue(jonCharacter, Vector2.zero);
        }

        jonCharacter.Move(Vector2.zero);
    }

    private void SetPlayerActive(bool isActive)
    {
        Transform playerRoot = GetPlayerStateRoot();
        if (playerRoot == null)
            return;

        playerRoot.gameObject.SetActive(isActive);
    }

    private IEnumerator RespawnPlayerAfterDelay()
    {
        isRespawningPlayer = true;
        SetPlayerInputEnabled(false);
        try
        {
            ShowDeathMessage();

            if (transitionAnim != null)
            {
                transitionAnim.SetTrigger("RespawnStart");
            }

            yield return new WaitForSeconds(1f);
            SetPlayerActive(false);

            if (deathRespawnDelay > 0f)
            {
                yield return new WaitForSeconds(0f);
            }

            SetPlayerActive(true);
            RespawnPlayer();
            HideDeathMessage();
            ResetPlayerIsStateFlags();
            ResetPlayerStatsAfterDeath();
        }
        finally
        {
            SetPlayerInputEnabled(true);
            isRespawningPlayer = false;
        }
    }

    private void RespawnPlayer()
    {
        if (playerTransform == null)
            return;

        Vector3 respawnPosition = playerTransform.position;
        if (TryGetCurrentRespawnPosition(out Vector3 currentRespawnPosition))
        {
            respawnPosition = currentRespawnPosition;
        }

        playerTransform.position = respawnPosition;

        Rigidbody2D playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            playerRigidbody = playerTransform.GetComponentInParent<Rigidbody2D>();
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        ResetPlayerIsStateFlags();
        ResetPlayerStatsAfterDeath();
        RefreshCounters(forceRefresh: true);
    }
}

using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

public class GameMaster : MonoBehaviour
{
    private const string DeathMessageText = "Try Again!";
    private const string DeathMessageLabelName = "DeathMessage";

    private static readonly BindingFlags PlayerStateBindingFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Header("Respawn")]
    [Tooltip("Player object to respawn. If left empty, GameMaster will try to find one.")]
    public Transform playerTransform;

    [Tooltip("Optional respawn point. If left empty, the player's starting position is used.")]
    public Transform respawnPoint;

    [Min(0f)]
    [Tooltip("How long to wait after death before respawning the player.")]
    public float deathRespawnDelay = 1f;

    [Header("UI")]
    [Tooltip("UI Toolkit document that contains the Counters label.")]
    public UIDocument uiDocument;

    [Tooltip("Name of the label used to show player counters.")]
    public string countersLabelName = "Counters";

    private Label uiCountersText;
    private int lastDisplayedCoins = int.MinValue;
    private int lastDisplayedHp = int.MinValue;
    private bool warnedMissingUiDocument;
    private bool warnedMissingCountersLabel;
    private bool warnedMissingPlayer;
    private bool isRespawningPlayer;
    private Vector3 checkpointRespawnPosition;
    private bool hasCheckpointRespawnPosition;
    private Vector3 fallbackRespawnPosition;
    private bool hasFallbackRespawnPosition;

    private void Awake()
    {
        ResolvePlayer();
        CacheFallbackRespawnPosition();
        ResolveUiDocument();
        CacheUiReferences();
        RefreshCounters(forceRefresh: true);
        Application.targetFrameRate = 60;
    }

    private void Update()
    {
        ResolvePlayer();

        if (!isRespawningPlayer && PlayerData.HP <= 0)
        {
            StartCoroutine(RespawnPlayerAfterDelay());
        }

        if (uiCountersText == null)
        {
            ResolveUiDocument();
            CacheUiReferences();
        }

        RefreshCounters();
    }

    private void ResolveUiDocument()
    {
        if (uiDocument != null)
            return;

        uiDocument = FindObjectOfType<UIDocument>();

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

        JonCharacterController jonCharacter = FindObjectOfType<JonCharacterController>();
        if (jonCharacter != null)
        {
            playerTransform = jonCharacter.transform;
            CacheFallbackRespawnPosition();
            return;
        }

        Hero hero = FindObjectOfType<Hero>();
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
        if (uiCountersText == null)
            return;

        if (!forceRefresh &&
            PlayerData.Coins == lastDisplayedCoins &&
            PlayerData.HP == lastDisplayedHp)
        {
            return;
        }

        lastDisplayedCoins = PlayerData.Coins;
        lastDisplayedHp = PlayerData.HP;

        uiCountersText.text = $"Coins: {PlayerData.Coins} HP: {PlayerData.HP}";
        uiCountersText.style.display = DisplayStyle.Flex;
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
        ShowDeathMessage();
        SetPlayerActive(false);

        if (deathRespawnDelay > 0f)
        {
            yield return new WaitForSeconds(deathRespawnDelay);
        }

        SetPlayerActive(true);
        RespawnPlayer();
        HideDeathMessage();
        isRespawningPlayer = false;
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
        PlayerData.RestoreFullHP();
        RefreshCounters(forceRefresh: true);
    }
}

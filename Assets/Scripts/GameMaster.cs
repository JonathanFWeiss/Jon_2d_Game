using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

public class GameMaster : MonoBehaviour
{
    private static readonly BindingFlags PlayerStateBindingFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Header("Respawn")]
    [Tooltip("Player object to respawn. If left empty, GameMaster will try to find one.")]
    public Transform playerTransform;

    [Tooltip("Optional respawn point. If left empty, the player's starting position is used.")]
    public Transform respawnPoint;

    [Header("Fall Damage")]
    [Tooltip("If the player falls below this Y position, GameMaster applies fatal damage.")]
    public float fallDeathY = -50f;

    [Tooltip("Damage applied when the player falls below the kill plane.")]
    public int fallDamage = 5000;

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
        TryApplyFallDamage();

        if (!isRespawningPlayer && PlayerData.HP <= 0)
        {
            RespawnPlayer();
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

    private void TryApplyFallDamage()
    {
        if (isRespawningPlayer || playerTransform == null || fallDamage <= 0)
            return;

        if (PlayerData.HP <= 0)
            return;

        if (playerTransform.position.y < fallDeathY)
        {
            PlayerData.RemoveHP(fallDamage);
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

    private void RespawnPlayer()
    {
        if (playerTransform == null)
            return;

        isRespawningPlayer = true;

        Vector3 respawnPosition = hasFallbackRespawnPosition
            ? fallbackRespawnPosition
            : playerTransform.position;

        if (respawnPoint != null)
        {
            respawnPosition = respawnPoint.position;
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
        isRespawningPlayer = false;
    }
}

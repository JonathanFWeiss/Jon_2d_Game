using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class DiscoverableVisibilityTrigger : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string PlayerTag = "Player";

    [Header("Visibility Targets")]
    [Tooltip("Root searched for renderers when the target lists are empty. Defaults to this object.")]
    [SerializeField] private Transform targetRoot;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Tilemap[] tilemaps;
    [SerializeField] private CanvasGroup[] canvasGroups;
    [SerializeField] private bool includeInactiveTargets = true;

    [Header("Discovery")]
    [Tooltip("Layer mask used to identify the player. Leave empty to also allow controller/tag detection.")]
    [SerializeField] private LayerMask playerLayer;
     private bool rememberTriggered = false; // this is not working right. removing from inspector.
    [Tooltip("Optional stable save key. Leave blank to derive one from scene and hierarchy path.")]
    [SerializeField] private string persistentKey;
    [SerializeField] private bool disableTriggerAfterActivation = true;

    [Header("Feedback")]
    [Min(0f)]
    [SerializeField] private float fadeTime = 0.5f;
    [SerializeField] private bool playSound = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip discoverySound;

    private SpriteRenderer[] resolvedSpriteRenderers;
    private Tilemap[] resolvedTilemaps;
    private CanvasGroup[] resolvedCanvasGroups;
    private float[] spriteVisibleAlphas;
    private float[] tilemapVisibleAlphas;
    private float[] canvasGroupVisibleAlphas;
    private Coroutine fadeCoroutine;
    private bool isActivated;

    protected abstract string StoragePrefix { get; }
    protected abstract float InitialHiddenProgress { get; }
    protected abstract float TriggeredHiddenProgress { get; }

    protected virtual void Reset()
    {
        targetRoot = transform;
        playerLayer = LayerMask.GetMask(PlayerLayerName);
        audioSource = GetComponent<AudioSource>();
        PopulateTargetsFromRoot();
        EnsureTriggerCollider();
    }

    protected virtual void Awake()
    {
        if (targetRoot == null)
        {
            targetRoot = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        EnsureTriggerCollider();
        ResolveTargets();
        CacheVisibleAlphas();

        isActivated = rememberTriggered && PlayerPrefs.GetInt(GetStorageKey(), 0) == 1;
        ApplyHiddenProgress(isActivated ? TriggeredHiddenProgress : InitialHiddenProgress);

        if (isActivated)
        {
            SetTriggerEnabled(false);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        Activate();
    }

    protected void Activate()
    {
        if (isActivated)
            return;

        isActivated = true;

        if (rememberTriggered)
        {
            PlayerPrefs.SetInt(GetStorageKey(), 1);
            PlayerPrefs.Save();
        }

        PlayDiscoverySound();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        if (fadeTime <= 0f)
        {
            ApplyHiddenProgress(TriggeredHiddenProgress);
            SetTriggerEnabled(false);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeVisibility(InitialHiddenProgress, TriggeredHiddenProgress));
    }

    protected void ResetVisibility()
    {
        isActivated = false;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (rememberTriggered)
        {
            PlayerPrefs.DeleteKey(GetStorageKey());
            PlayerPrefs.Save();
        }

        ApplyHiddenProgress(InitialHiddenProgress);
        SetTriggerEnabled(true);
    }

    private IEnumerator FadeVisibility(float fromHiddenProgress, float toHiddenProgress)
    {
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeTime);
            ApplyHiddenProgress(Mathf.Lerp(fromHiddenProgress, toHiddenProgress, progress));
            yield return null;
        }

        ApplyHiddenProgress(toHiddenProgress);
        SetTriggerEnabled(false);
        fadeCoroutine = null;
    }

    private void PopulateTargetsFromRoot()
    {
        Transform root = targetRoot != null ? targetRoot : transform;
        spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactiveTargets);
        tilemaps = root.GetComponentsInChildren<Tilemap>(includeInactiveTargets);
        canvasGroups = root.GetComponentsInChildren<CanvasGroup>(includeInactiveTargets);
    }

    private void ResolveTargets()
    {
        Transform root = targetRoot != null ? targetRoot : transform;

        resolvedSpriteRenderers = HasTargets(spriteRenderers)
            ? spriteRenderers
            : root.GetComponentsInChildren<SpriteRenderer>(includeInactiveTargets);

        resolvedTilemaps = HasTargets(tilemaps)
            ? tilemaps
            : root.GetComponentsInChildren<Tilemap>(includeInactiveTargets);

        resolvedCanvasGroups = HasTargets(canvasGroups)
            ? canvasGroups
            : root.GetComponentsInChildren<CanvasGroup>(includeInactiveTargets);
    }

    private void CacheVisibleAlphas()
    {
        spriteVisibleAlphas = new float[resolvedSpriteRenderers.Length];
        for (int i = 0; i < resolvedSpriteRenderers.Length; i++)
        {
            spriteVisibleAlphas[i] = resolvedSpriteRenderers[i] != null
                ? resolvedSpriteRenderers[i].color.a
                : 1f;
        }

        tilemapVisibleAlphas = new float[resolvedTilemaps.Length];
        for (int i = 0; i < resolvedTilemaps.Length; i++)
        {
            tilemapVisibleAlphas[i] = resolvedTilemaps[i] != null
                ? resolvedTilemaps[i].color.a
                : 1f;
        }

        canvasGroupVisibleAlphas = new float[resolvedCanvasGroups.Length];
        for (int i = 0; i < resolvedCanvasGroups.Length; i++)
        {
            canvasGroupVisibleAlphas[i] = resolvedCanvasGroups[i] != null
                ? resolvedCanvasGroups[i].alpha
                : 1f;
        }
    }

    private void ApplyHiddenProgress(float hiddenProgress)
    {
        float clampedProgress = Mathf.Clamp01(hiddenProgress);

        for (int i = 0; i < resolvedSpriteRenderers.Length; i++)
        {
            SpriteRenderer target = resolvedSpriteRenderers[i];
            if (target == null)
                continue;

            Color color = target.color;
            color.a = Mathf.Lerp(spriteVisibleAlphas[i], 0f, clampedProgress);
            target.color = color;
        }

        for (int i = 0; i < resolvedTilemaps.Length; i++)
        {
            Tilemap target = resolvedTilemaps[i];
            if (target == null)
                continue;

            Color color = target.color;
            color.a = Mathf.Lerp(tilemapVisibleAlphas[i], 0f, clampedProgress);
            target.color = color;
        }

        for (int i = 0; i < resolvedCanvasGroups.Length; i++)
        {
            CanvasGroup target = resolvedCanvasGroups[i];
            if (target == null)
                continue;

            target.alpha = Mathf.Lerp(canvasGroupVisibleAlphas[i], 0f, clampedProgress);
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (IsInPlayerLayer(other.gameObject))
            return true;

        if (other.attachedRigidbody != null && IsInPlayerLayer(other.attachedRigidbody.gameObject))
            return true;

        if (HasPlayerComponent(other))
            return true;

        if (other.CompareTag(PlayerTag))
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(PlayerTag))
            return true;

        return other.transform.root != null && other.transform.root.CompareTag(PlayerTag);
    }

    private bool HasPlayerComponent(Collider2D other)
    {
        if (other.attachedRigidbody != null)
        {
            if (other.attachedRigidbody.GetComponent<JonCharacterController>() != null)
                return true;

            if (other.attachedRigidbody.GetComponent<Hero>() != null)
                return true;
        }

        if (other.GetComponentInParent<JonCharacterController>() != null)
            return true;

        return other.GetComponentInParent<Hero>() != null;
    }

    private bool IsInPlayerLayer(GameObject target)
    {
        return target != null &&
            playerLayer.value != 0 &&
            (playerLayer.value & (1 << target.layer)) != 0;
    }

    private void PlayDiscoverySound()
    {
        if (!playSound || audioSource == null)
            return;

        if (discoverySound != null)
        {
            audioSource.PlayOneShot(discoverySound);
            return;
        }

        audioSource.Play();
    }

    private void SetTriggerEnabled(bool enabled)
    {
        if (!disableTriggerAfterActivation && !enabled)
            return;

        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = enabled;
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private string GetStorageKey()
    {
        if (!string.IsNullOrWhiteSpace(persistentKey))
        {
            return StoragePrefix + persistentKey.Trim();
        }

        string scenePath = gameObject.scene.IsValid() ? gameObject.scene.path : string.Empty;
        return StoragePrefix + scenePath + "." + GetHierarchyPath(transform);
    }

    private static string GetHierarchyPath(Transform current)
    {
        if (current == null)
            return string.Empty;

        string pathPart = current.GetSiblingIndex() + ":" + current.name;
        if (current.parent == null)
            return pathPart;

        return GetHierarchyPath(current.parent) + "/" + pathPart;
    }

    private static bool HasTargets(Object[] targets)
    {
        if (targets == null || targets.Length == 0)
            return false;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                return true;
        }

        return false;
    }
}

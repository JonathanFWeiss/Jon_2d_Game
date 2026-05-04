using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
public class Unmasker : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string PlayerTag = "Player";
    private const string PlayerPrefsPrefix = "Unmasker.";

    [Header("Mask Targets")]
    [Tooltip("Root searched for mask renderers when the target lists are empty. Defaults to this object.")]
    [SerializeField] private Transform maskRoot;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Tilemap[] tilemaps;
    [SerializeField] private CanvasGroup[] canvasGroups;
    [SerializeField] private bool includeInactiveTargets = true;

    [Header("Discovery")]
    [Tooltip("Layer mask used to identify the player. Leave empty to also allow controller/tag detection.")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool rememberUncovered = true;
    [Tooltip("Optional stable save key. Leave blank to derive one from scene and hierarchy path.")]
    [SerializeField] private string persistentKey;
    [SerializeField] private bool disableTriggerAfterUncover = true;

    [Header("Feedback")]
    [Min(0f)]
    [SerializeField] private float fadeTime = 0.5f;
    [SerializeField] private bool playSound = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip discoverySound;

    private SpriteRenderer[] resolvedSpriteRenderers;
    private Tilemap[] resolvedTilemaps;
    private CanvasGroup[] resolvedCanvasGroups;
    private float[] spriteStartAlphas;
    private float[] tilemapStartAlphas;
    private float[] canvasGroupStartAlphas;
    private Coroutine fadeCoroutine;
    private bool isUncovered;

    private void Reset()
    {
        maskRoot = transform;
        playerLayer = LayerMask.GetMask(PlayerLayerName);
        audioSource = GetComponent<AudioSource>();
        PopulateTargetsFromRoot();
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        if (maskRoot == null)
        {
            maskRoot = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        EnsureTriggerCollider();
        ResolveTargets();
        CacheStartAlphas();

        if (rememberUncovered && PlayerPrefs.GetInt(GetStorageKey(), 0) == 1)
        {
            isUncovered = true;
            ApplyRevealProgress(1f);
            SetTriggerEnabled(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        Uncover();
    }

    public void Uncover()
    {
        if (isUncovered)
            return;

        isUncovered = true;

        if (rememberUncovered)
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
            ApplyRevealProgress(1f);
            SetTriggerEnabled(false);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeOutMask());
    }

    public void Cover()
    {
        isUncovered = false;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (rememberUncovered)
        {
            PlayerPrefs.DeleteKey(GetStorageKey());
            PlayerPrefs.Save();
        }

        ApplyRevealProgress(0f);
        SetTriggerEnabled(true);
    }

    private IEnumerator FadeOutMask()
    {
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            ApplyRevealProgress(elapsed / fadeTime);
            yield return null;
        }

        ApplyRevealProgress(1f);
        SetTriggerEnabled(false);
        fadeCoroutine = null;
    }

    private void PopulateTargetsFromRoot()
    {
        Transform root = maskRoot != null ? maskRoot : transform;
        spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactiveTargets);
        tilemaps = root.GetComponentsInChildren<Tilemap>(includeInactiveTargets);
        canvasGroups = root.GetComponentsInChildren<CanvasGroup>(includeInactiveTargets);
    }

    private void ResolveTargets()
    {
        Transform root = maskRoot != null ? maskRoot : transform;

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

    private void CacheStartAlphas()
    {
        spriteStartAlphas = new float[resolvedSpriteRenderers.Length];
        for (int i = 0; i < resolvedSpriteRenderers.Length; i++)
        {
            spriteStartAlphas[i] = resolvedSpriteRenderers[i] != null
                ? resolvedSpriteRenderers[i].color.a
                : 1f;
        }

        tilemapStartAlphas = new float[resolvedTilemaps.Length];
        for (int i = 0; i < resolvedTilemaps.Length; i++)
        {
            tilemapStartAlphas[i] = resolvedTilemaps[i] != null
                ? resolvedTilemaps[i].color.a
                : 1f;
        }

        canvasGroupStartAlphas = new float[resolvedCanvasGroups.Length];
        for (int i = 0; i < resolvedCanvasGroups.Length; i++)
        {
            canvasGroupStartAlphas[i] = resolvedCanvasGroups[i] != null
                ? resolvedCanvasGroups[i].alpha
                : 1f;
        }
    }

    private void ApplyRevealProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        for (int i = 0; i < resolvedSpriteRenderers.Length; i++)
        {
            SpriteRenderer target = resolvedSpriteRenderers[i];
            if (target == null)
                continue;

            Color color = target.color;
            color.a = Mathf.Lerp(spriteStartAlphas[i], 0f, clampedProgress);
            target.color = color;
        }

        for (int i = 0; i < resolvedTilemaps.Length; i++)
        {
            Tilemap target = resolvedTilemaps[i];
            if (target == null)
                continue;

            Color color = target.color;
            color.a = Mathf.Lerp(tilemapStartAlphas[i], 0f, clampedProgress);
            target.color = color;
        }

        for (int i = 0; i < resolvedCanvasGroups.Length; i++)
        {
            CanvasGroup target = resolvedCanvasGroups[i];
            if (target == null)
                continue;

            target.alpha = Mathf.Lerp(canvasGroupStartAlphas[i], 0f, clampedProgress);
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
        if (!disableTriggerAfterUncover && !enabled)
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
            return PlayerPrefsPrefix + persistentKey.Trim();
        }

        string scenePath = gameObject.scene.IsValid() ? gameObject.scene.path : string.Empty;
        return PlayerPrefsPrefix + scenePath + "." + GetHierarchyPath(transform);
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

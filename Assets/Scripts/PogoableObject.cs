using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PogoableObject : EnemyBase
{
    [Header("Pogoable Object")]
    [SerializeField]
    [Min(0f)]
    private float resetDelay = 1f;

    private Collider2D[] objectColliders;
    private bool[] startingColliderEnabledStates;
    private Renderer[] objectRenderers;
    private bool[] startingRendererEnabledStates;
    private Vector3 startingLocalPosition;
    private Quaternion startingLocalRotation;
    private Vector3 startingLocalScale;
    private Coroutine resetCoroutine;

    private void Reset()
    {
        hp = 1;
        moveSpeed = 0f;
        contactDamage = 0;
        coinDropCount = 0;
        ConfigureRigidbody();
        ConfigureCollidersAsTriggers();
    }

    private void OnValidate()
    {
        hp = 1;
        moveSpeed = 0f;
        contactDamage = 0;
        coinDropCount = 0;
        resetDelay = Mathf.Max(0f, resetDelay);
        ConfigureRigidbody();
        ConfigureCollidersAsTriggers();
    }

    protected override void Awake()
    {
        base.Awake();

        hp = 1;
        moveSpeed = 0f;
        contactDamage = 0;
        coinDropCount = 0;

        startingLocalPosition = transform.localPosition;
        startingLocalRotation = transform.localRotation;
        startingLocalScale = transform.localScale;

        CacheObjectColliders();
        CacheObjectRenderers();
        ConfigureRigidbody();
        ConfigureCollidersAsTriggers();
    }

    private void OnEnable()
    {
        if (!isDead)
            return;

        ResetPogoableState();
        SetPogoableActive(true);
    }

    public override void TakeDamage(int amount)
    {
        if (isDead)
            return;

        base.TakeDamage(amount);

        if (amount > 0 && hp <= 0)
        {
            Die();
        }
    }

    protected override void Move()
    {
    }

    protected override void FixedUpdate()
    {
        ConfigureCollidersAsTriggers();
    }

    protected override void TryDamagePlayer(GameObject hitObject)
    {
    }

    protected override void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        RestoreHitFlashMaterials();

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
        }

        resetCoroutine = StartCoroutine(ResetAfterDelay());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        resetCoroutine = null;
    }

    private IEnumerator ResetAfterDelay()
    {
        SetPogoableActive(false);

        if (resetDelay > 0f)
        {
            yield return new WaitForSeconds(resetDelay);
        }
        else
        {
            yield return null;
        }

        ResetPogoableState();
        SetPogoableActive(true);
        resetCoroutine = null;
    }

    private void ResetPogoableState()
    {
        hp = 1;
        isDead = false;
        lastContactDamageTime = -Mathf.Infinity;

        transform.localPosition = startingLocalPosition;
        transform.localRotation = startingLocalRotation;
        transform.localScale = startingLocalScale;
        facingDirection = Mathf.Sign(startingLocalScale.x);

        if (Mathf.Approximately(facingDirection, 0f))
        {
            facingDirection = 1f;
        }

        ConfigureRigidbody();
        ConfigureCollidersAsTriggers();
    }

    private void SetPogoableActive(bool active)
    {
        if (objectRenderers != null)
        {
            for (int i = 0; i < objectRenderers.Length; i++)
            {
                Renderer objectRenderer = objectRenderers[i];

                if (objectRenderer == null)
                    continue;

                objectRenderer.enabled = active &&
                    startingRendererEnabledStates != null &&
                    i < startingRendererEnabledStates.Length &&
                    startingRendererEnabledStates[i];
            }
        }

        if (objectColliders != null)
        {
            for (int i = 0; i < objectColliders.Length; i++)
            {
                Collider2D objectCollider = objectColliders[i];

                if (objectCollider == null)
                    continue;

                objectCollider.isTrigger = true;
                objectCollider.enabled = active &&
                    startingColliderEnabledStates != null &&
                    i < startingColliderEnabledStates.Length &&
                    startingColliderEnabledStates[i];
            }
        }
    }

    private void CacheObjectColliders()
    {
        objectColliders = GetComponentsInChildren<Collider2D>(true);
        startingColliderEnabledStates = new bool[objectColliders.Length];

        for (int i = 0; i < objectColliders.Length; i++)
        {
            startingColliderEnabledStates[i] = objectColliders[i] != null && objectColliders[i].enabled;
        }
    }

    private void CacheObjectRenderers()
    {
        objectRenderers = GetComponentsInChildren<Renderer>(true);
        startingRendererEnabledStates = new bool[objectRenderers.Length];

        for (int i = 0; i < objectRenderers.Length; i++)
        {
            startingRendererEnabledStates[i] = objectRenderers[i] != null && objectRenderers[i].enabled;
        }
    }

    private void ConfigureRigidbody()
    {
        Rigidbody2D objectRigidbody = rb2d != null ? rb2d : GetComponent<Rigidbody2D>();

        if (objectRigidbody == null)
            return;

        objectRigidbody.bodyType = RigidbodyType2D.Kinematic;
        objectRigidbody.gravityScale = 0f;
        objectRigidbody.freezeRotation = true;
        objectRigidbody.linearVelocity = Vector2.zero;
        objectRigidbody.angularVelocity = 0f;
        objectRigidbody.position = transform.position;
        objectRigidbody.rotation = transform.eulerAngles.z;
    }

    private void ConfigureCollidersAsTriggers()
    {
        Collider2D[] colliders = objectColliders;

        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponentsInChildren<Collider2D>(true);
        }

        foreach (Collider2D objectCollider in colliders)
        {
            if (objectCollider != null)
            {
                objectCollider.isTrigger = true;
            }
        }
    }
}

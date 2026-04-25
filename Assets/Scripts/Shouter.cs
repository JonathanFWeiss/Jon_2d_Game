using System.Collections.Generic;
using UnityEngine;

public class Shouter : GroundStationaryEnemy
{//shouter activates his shout animation every 2 seconds for 1 second
    [Header("Shouter")]
    [Tooltip("Time in seconds between each shout activation.")]
    public float shoutInterval = 2f;

    [Tooltip("Duration in seconds for which the shout animation is active.")]
    public float shoutDuration = 1f;

    [Header("Shout Force")]
    [Tooltip("Optional point to use as the center of the shout. If empty, the enemy's rigidbody center is used.")]
    public Transform shoutOrigin;

    [Tooltip("How far the shout reaches when active.")]
    public float shoutRadius = 5f;

    [Tooltip("Force applied to the player while the shout is active. X pushes away from the shouter; Y is upward force.")]
    public Vector2 shoutForce = new Vector2(25f, 0f);

    private float shoutTimer = 0f;
    private bool isShouting = false;
    private Animator anim;
    private readonly HashSet<Rigidbody2D> pushedPlayerBodies = new HashSet<Rigidbody2D>();

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("Shouter Awake");
        anim = GetComponent<Animator>();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        shoutTimer += Time.fixedDeltaTime;

        if (!isShouting && shoutTimer >= shoutInterval)
        {
            ActivateShout();
        }
        else if (isShouting && shoutTimer >= shoutDuration)
        {
            DeactivateShout();
        }

        if (isShouting)
        {
            ApplyShoutForce();
        }
    }

    private void ActivateShout()
    {
        isShouting = true;
        shoutTimer = 0f;
        if (anim != null)
        {
            anim.SetBool("isShouting", true);
        }
    }
    private void DeactivateShout()
    {
        isShouting = false;
        shoutTimer = 0f;
        if (anim != null)
        {
            anim.SetBool("isShouting", false);
        }
    }

    private void ApplyShoutForce()
    {
        pushedPlayerBodies.Clear();

        Vector2 origin = GetShoutOrigin();
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, Mathf.Max(0f, shoutRadius), playerLayerMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            GameObject rootObject = hit.transform.root.gameObject;

            if (!IsPlayerObject(rootObject))
                continue;

            Rigidbody2D playerRb = hit.attachedRigidbody;

            if (playerRb == null)
            {
                playerRb = hit.GetComponentInParent<Rigidbody2D>();
            }

            if (playerRb == null || !pushedPlayerBodies.Add(playerRb))
                continue;

            Vector2 direction = playerRb.worldCenterOfMass - origin;
            float horizontalDirection = Mathf.Sign(direction.x);

            if (Mathf.Approximately(horizontalDirection, 0f))
            {
                horizontalDirection = facingDirection == 0f ? 1f : facingDirection;
            }

            Vector2 force = new Vector2(
                Mathf.Abs(shoutForce.x) * horizontalDirection,
                shoutForce.y
            );

            playerRb.AddForce(force, ForceMode2D.Force);
        }
    }

    private Vector2 GetShoutOrigin()
    {
        if (shoutOrigin != null)
        {
            return shoutOrigin.position;
        }

        if (rb2d != null)
        {
            return rb2d.worldCenterOfMass;
        }

        return transform.position;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetShoutOrigin(), Mathf.Max(0f, shoutRadius));
    }
}

    



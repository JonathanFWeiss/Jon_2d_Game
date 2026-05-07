using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class BeamHazard : SpikeHazard
{
    private static readonly List<BeamHazard> ActiveBeamHazards = new List<BeamHazard>();

    [SerializeField] private float playerSearchInterval = 0.25f;

    private readonly Dictionary<JonCharacterController, Coroutine> restoreCollisionCoroutines =
        new Dictionary<JonCharacterController, Coroutine>();

    private readonly Dictionary<JonCharacterController, List<IgnoredCollisionPair>> ignoredCollisionPairsByPlayer =
        new Dictionary<JonCharacterController, List<IgnoredCollisionPair>>();

    private readonly List<JonCharacterController> playerControllers = new List<JonCharacterController>();
    private float nextPlayerSearchTime = float.NegativeInfinity;

    public static void IgnorePlayerCollisionsForDash(JonCharacterController playerController)
    {
        if (playerController == null)
            return;

        for (int beamIndex = ActiveBeamHazards.Count - 1; beamIndex >= 0; beamIndex--)
        {
            BeamHazard beamHazard = ActiveBeamHazards[beamIndex];
            if (beamHazard == null)
            {
                ActiveBeamHazards.RemoveAt(beamIndex);
                continue;
            }

            if (beamHazard.isActiveAndEnabled)
            {
                beamHazard.IgnorePlayerCollisionsDuringDash(playerController);
            }
        }
    }

    private void OnEnable()
    {
        if (!ActiveBeamHazards.Contains(this))
        {
            ActiveBeamHazards.Add(this);
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        RefreshPlayerControllersIfNeeded();

        for (int playerIndex = 0; playerIndex < playerControllers.Count; playerIndex++)
        {
            JonCharacterController playerController = playerControllers[playerIndex];
            if (playerController != null && playerController.IsDashing)
            {
                IgnorePlayerCollisionsDuringDash(playerController);
            }
        }
    }

    protected override bool ShouldIgnoreCollision(Collision2D collision)
    {
        JonCharacterController playerController = GetPlayerController(collision);
        if (playerController == null || !playerController.IsDashing)
            return false;

        IgnorePlayerCollisionsDuringDash(playerController);
        return true;
    }

    protected override void OnDisable()
    {
        ActiveBeamHazards.Remove(this);
        RestoreAllIgnoredCollisions();
        base.OnDisable();
    }

    private void IgnorePlayerCollisionsDuringDash(JonCharacterController playerController)
    {
        Collider2D[] beamColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders = playerController.GetComponentsInChildren<Collider2D>();

        for (int beamIndex = 0; beamIndex < beamColliders.Length; beamIndex++)
        {
            Collider2D beamCollider = beamColliders[beamIndex];
            if (beamCollider == null || !beamCollider.enabled)
                continue;

            for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
            {
                Collider2D playerCollider = playerColliders[playerIndex];
                if (playerCollider == null || !playerCollider.enabled || playerCollider == beamCollider)
                    continue;

                Physics2D.IgnoreCollision(beamCollider, playerCollider, true);
                TrackIgnoredCollisionPair(playerController, beamCollider, playerCollider);
            }
        }

        if (!restoreCollisionCoroutines.ContainsKey(playerController))
        {
            restoreCollisionCoroutines[playerController] =
                StartCoroutine(RestorePlayerCollisionsAfterDash(playerController));
        }
    }

    private IEnumerator RestorePlayerCollisionsAfterDash(JonCharacterController playerController)
    {
        while (playerController != null && playerController.IsDashing)
        {
            yield return new WaitForFixedUpdate();
        }

        RestoreIgnoredCollisions(playerController);
        restoreCollisionCoroutines.Remove(playerController);
    }

    private void TrackIgnoredCollisionPair(
        JonCharacterController playerController,
        Collider2D beamCollider,
        Collider2D playerCollider)
    {
        if (!ignoredCollisionPairsByPlayer.TryGetValue(
            playerController,
            out List<IgnoredCollisionPair> ignoredCollisionPairs))
        {
            ignoredCollisionPairs = new List<IgnoredCollisionPair>();
            ignoredCollisionPairsByPlayer[playerController] = ignoredCollisionPairs;
        }

        for (int pairIndex = 0; pairIndex < ignoredCollisionPairs.Count; pairIndex++)
        {
            IgnoredCollisionPair ignoredCollisionPair = ignoredCollisionPairs[pairIndex];
            if (ignoredCollisionPair.BeamCollider == beamCollider &&
                ignoredCollisionPair.PlayerCollider == playerCollider)
            {
                return;
            }
        }

        ignoredCollisionPairs.Add(new IgnoredCollisionPair(beamCollider, playerCollider));
    }

    private void RestoreIgnoredCollisions(JonCharacterController playerController)
    {
        if (!ignoredCollisionPairsByPlayer.TryGetValue(
            playerController,
            out List<IgnoredCollisionPair> ignoredCollisionPairs))
        {
            return;
        }

        for (int pairIndex = 0; pairIndex < ignoredCollisionPairs.Count; pairIndex++)
        {
            IgnoredCollisionPair ignoredCollisionPair = ignoredCollisionPairs[pairIndex];
            if (ignoredCollisionPair.BeamCollider != null && ignoredCollisionPair.PlayerCollider != null)
            {
                Physics2D.IgnoreCollision(
                    ignoredCollisionPair.BeamCollider,
                    ignoredCollisionPair.PlayerCollider,
                    false);
            }
        }

        ignoredCollisionPairsByPlayer.Remove(playerController);
    }

    private void RestoreAllIgnoredCollisions()
    {
        foreach (KeyValuePair<JonCharacterController, Coroutine> restoreCollisionCoroutine in restoreCollisionCoroutines)
        {
            if (restoreCollisionCoroutine.Value != null)
            {
                StopCoroutine(restoreCollisionCoroutine.Value);
            }
        }

        restoreCollisionCoroutines.Clear();

        foreach (JonCharacterController playerController in
            new List<JonCharacterController>(ignoredCollisionPairsByPlayer.Keys))
        {
            RestoreIgnoredCollisions(playerController);
        }
    }

    private void RefreshPlayerControllersIfNeeded()
    {
        if (Time.time < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);
        playerControllers.Clear();
        playerControllers.AddRange(Object.FindObjectsByType<JonCharacterController>());
    }

    private struct IgnoredCollisionPair
    {
        public IgnoredCollisionPair(Collider2D beamCollider, Collider2D playerCollider)
        {
            BeamCollider = beamCollider;
            PlayerCollider = playerCollider;
        }

        public Collider2D BeamCollider { get; private set; }
        public Collider2D PlayerCollider { get; private set; }
    }
}

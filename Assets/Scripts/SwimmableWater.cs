using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SwimmableWater : MonoBehaviour
{
    private readonly Dictionary<Collider2D, JonCharacterController> swimmingColliders =
        new Dictionary<Collider2D, JonCharacterController>();

    private readonly Dictionary<JonCharacterController, int> swimmerContactCounts =
        new Dictionary<JonCharacterController, int>();

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        JonCharacterController playerController = GetPlayerController(other);
        if (playerController == null)
            return;

        if (swimmingColliders.ContainsKey(other))
            return;

        swimmingColliders[other] = playerController;
        if (!swimmerContactCounts.ContainsKey(playerController))
        {
            swimmerContactCounts[playerController] = 0;
        }

        swimmerContactCounts[playerController]++;
        playerController.isSwimming = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!swimmingColliders.TryGetValue(other, out JonCharacterController playerController))
            return;

        swimmingColliders.Remove(other);
        if (!swimmerContactCounts.TryGetValue(playerController, out int contactCount))
            return;

        contactCount--;
        if (contactCount > 0)
        {
            swimmerContactCounts[playerController] = contactCount;
            return;
        }

        swimmerContactCounts.Remove(playerController);
        playerController.isSwimming = false;
    }

    private void OnDisable()
    {
        foreach (JonCharacterController playerController in swimmerContactCounts.Keys)
        {
            if (playerController != null)
            {
                playerController.isSwimming = false;
            }
        }

        swimmingColliders.Clear();
        swimmerContactCounts.Clear();
    }

    private static JonCharacterController GetPlayerController(Collider2D other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController;
        }

        return other.GetComponentInParent<JonCharacterController>();
    }

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}

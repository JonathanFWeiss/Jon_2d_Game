using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SecretRoomCameraSwitch : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string PlayerTag = "Player";

    [Header("Camera")]
    [SerializeField] private CinemachineCamera alternativeCamera;
    [SerializeField] private bool deactivateCameraOnAwake = true;

    [Header("Player Detection")]
    [Tooltip("Layer mask used to identify the player. Leave empty to allow controller and tag detection.")]
    [SerializeField] private LayerMask playerLayer;

    private readonly HashSet<Collider2D> playerCollidersInside = new HashSet<Collider2D>();

    private void Reset()
    {
        playerLayer = LayerMask.GetMask(PlayerLayerName);
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();

        if (deactivateCameraOnAwake)
        {
            SetAlternativeCameraActive(false);
        }
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        playerCollidersInside.Clear();
        SetAlternativeCameraActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerCollidersInside.Add(other);
        SetAlternativeCameraActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        playerCollidersInside.Remove(other);

        if (playerCollidersInside.Count == 0)
        {
            SetAlternativeCameraActive(false);
        }
    }

    private void SetAlternativeCameraActive(bool active)
    {
        if (alternativeCamera == null)
        {
            return;
        }

        GameObject cameraObject = alternativeCamera.gameObject;
        if (cameraObject.activeSelf != active)
        {
            cameraObject.SetActive(active);
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

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}

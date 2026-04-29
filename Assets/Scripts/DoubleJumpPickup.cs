using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class DoubleJumpPickup : MonoBehaviour
{
    private const string DoubleJumpGrantedMessageText = "Double Jump Granted.";
    private const string DoubleJumpGrantedMessageLabelName = "DoubleJumpGrantedMessage";

    [Tooltip("Layer mask used to identify the player object. Leave empty to use JonCharacterController detection only.")]
    public LayerMask playerLayer;

    [Tooltip("Time in seconds before the pickup can be collected after spawning.")]
    public float pickupDelay = 0f;

    [Header("UI")]
    [Tooltip("If left empty, the first UIDocument in the scene is used.")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float messageDuration = 1.5f;
    [SerializeField] private float messageTopOffset = 86f;

    private float currentPickupDelay;
    private bool collected;
    private bool warnedMissingUiDocument;

    private void Awake()
    {
        currentPickupDelay = pickupDelay;
    }

    private void Update()
    {
        if (currentPickupDelay > 0f)
        {
            currentPickupDelay -= Time.deltaTime;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || currentPickupDelay > 0f || other == null)
            return;

        JonCharacterController playerController = GetPlayerController(other);
        if (playerController == null || !IsPlayerLayerMatch(other, playerController))
            return;

        collected = true;
        playerController.CanDoubleJump = true;
        PlayerData.AddInventoryItem(PlayerData.DoubleJumpPickupItemName);
        ShowDoubleJumpGrantedMessage();

        Debug.Log("Double jump granted. Inventory count: " + PlayerData.GetInventoryItemCount(PlayerData.DoubleJumpPickupItemName));
        Destroy(gameObject);
    }

    private bool IsPlayerLayerMatch(Collider2D other, JonCharacterController playerController)
    {
        if (playerLayer.value == 0)
            return true;

        if ((playerLayer.value & (1 << other.gameObject.layer)) != 0)
            return true;

        if (other.attachedRigidbody != null &&
            (playerLayer.value & (1 << other.attachedRigidbody.gameObject.layer)) != 0)
        {
            return true;
        }

        return (playerLayer.value & (1 << playerController.gameObject.layer)) != 0;
    }

    private static JonCharacterController GetPlayerController(Collider2D other)
    {
        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController;
        }

        JonCharacterController playerController = other.GetComponentInParent<JonCharacterController>();
        if (playerController != null)
            return playerController;

        return other.GetComponent<JonCharacterController>();
    }

    private void ShowDoubleJumpGrantedMessage()
    {
        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        VisualElement root = resolvedUiDocument.rootVisualElement;

        Label existingMessage = root.Q<Label>(DoubleJumpGrantedMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label doubleJumpGrantedMessage = new Label(DoubleJumpGrantedMessageText)
        {
            name = DoubleJumpGrantedMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        doubleJumpGrantedMessage.style.position = Position.Absolute;
        doubleJumpGrantedMessage.style.top = messageTopOffset;
        doubleJumpGrantedMessage.style.left = 0f;
        doubleJumpGrantedMessage.style.right = 0f;
        doubleJumpGrantedMessage.style.marginLeft = StyleKeyword.Auto;
        doubleJumpGrantedMessage.style.marginRight = StyleKeyword.Auto;
        doubleJumpGrantedMessage.style.paddingLeft = 14f;
        doubleJumpGrantedMessage.style.paddingRight = 14f;
        doubleJumpGrantedMessage.style.paddingTop = 8f;
        doubleJumpGrantedMessage.style.paddingBottom = 8f;
        doubleJumpGrantedMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        doubleJumpGrantedMessage.style.color = Color.white;
        doubleJumpGrantedMessage.style.fontSize = 22f;
        doubleJumpGrantedMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        doubleJumpGrantedMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        doubleJumpGrantedMessage.style.borderTopLeftRadius = 8f;
        doubleJumpGrantedMessage.style.borderTopRightRadius = 8f;
        doubleJumpGrantedMessage.style.borderBottomLeftRadius = 8f;
        doubleJumpGrantedMessage.style.borderBottomRightRadius = 8f;
        doubleJumpGrantedMessage.style.alignSelf = Align.Center;

        root.Add(doubleJumpGrantedMessage);
        ScheduleMessageRemoval(root, doubleJumpGrantedMessage);
    }

    private UIDocument ResolveUiDocument()
    {
        if (uiDocument != null)
            return uiDocument;

        uiDocument = FindAnyObjectByType<UIDocument>();
        if (uiDocument != null)
            return uiDocument;

        if (!warnedMissingUiDocument)
        {
            warnedMissingUiDocument = true;
            Debug.LogWarning("DoubleJumpPickup could not find a UIDocument to display the double-jump-granted message.");
        }

        return null;
    }

    private void ScheduleMessageRemoval(VisualElement root, Label doubleJumpGrantedMessage)
    {
        int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, messageDuration) * 1000f);
        if (delayMilliseconds <= 0)
        {
            doubleJumpGrantedMessage.RemoveFromHierarchy();
            return;
        }

        root.schedule.Execute(() =>
        {
            if (doubleJumpGrantedMessage != null && doubleJumpGrantedMessage.parent != null)
            {
                doubleJumpGrantedMessage.RemoveFromHierarchy();
            }
        }).StartingIn(delayMilliseconds);
    }
}

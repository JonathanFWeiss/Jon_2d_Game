using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class WallJumpPickup : MonoBehaviour
{
    private const string WallJumpGrantedMessageText = "Wall Jump Granted.";
    private const string WallJumpGrantedMessageLabelName = "WallJumpGrantedMessage";

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
        playerController.CanWallJump = true;
        PlayerData.AddInventoryItem(PlayerData.WallJumpPickupItemName);
        ShowWallJumpGrantedMessage();

        Debug.Log("Wall jump granted. Inventory count: " + PlayerData.GetInventoryItemCount(PlayerData.WallJumpPickupItemName));
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

    private void ShowWallJumpGrantedMessage()
    {
        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        VisualElement root = resolvedUiDocument.rootVisualElement;

        Label existingMessage = root.Q<Label>(WallJumpGrantedMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label wallJumpGrantedMessage = new Label(WallJumpGrantedMessageText)
        {
            name = WallJumpGrantedMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        wallJumpGrantedMessage.style.position = Position.Absolute;
        wallJumpGrantedMessage.style.top = messageTopOffset;
        wallJumpGrantedMessage.style.left = 0f;
        wallJumpGrantedMessage.style.right = 0f;
        wallJumpGrantedMessage.style.marginLeft = StyleKeyword.Auto;
        wallJumpGrantedMessage.style.marginRight = StyleKeyword.Auto;
        wallJumpGrantedMessage.style.paddingLeft = 14f;
        wallJumpGrantedMessage.style.paddingRight = 14f;
        wallJumpGrantedMessage.style.paddingTop = 8f;
        wallJumpGrantedMessage.style.paddingBottom = 8f;
        wallJumpGrantedMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        wallJumpGrantedMessage.style.color = Color.white;
        wallJumpGrantedMessage.style.fontSize = 22f;
        wallJumpGrantedMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        wallJumpGrantedMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        wallJumpGrantedMessage.style.borderTopLeftRadius = 8f;
        wallJumpGrantedMessage.style.borderTopRightRadius = 8f;
        wallJumpGrantedMessage.style.borderBottomLeftRadius = 8f;
        wallJumpGrantedMessage.style.borderBottomRightRadius = 8f;
        wallJumpGrantedMessage.style.alignSelf = Align.Center;

        root.Add(wallJumpGrantedMessage);
        ScheduleMessageRemoval(root, wallJumpGrantedMessage);
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
            Debug.LogWarning("WallJumpPickup could not find a UIDocument to display the wall-jump-granted message.");
        }

        return null;
    }

    private void ScheduleMessageRemoval(VisualElement root, Label wallJumpGrantedMessage)
    {
        int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, messageDuration) * 1000f);
        if (delayMilliseconds <= 0)
        {
            wallJumpGrantedMessage.RemoveFromHierarchy();
            return;
        }

        root.schedule.Execute(() =>
        {
            if (wallJumpGrantedMessage != null && wallJumpGrantedMessage.parent != null)
            {
                wallJumpGrantedMessage.RemoveFromHierarchy();
            }
        }).StartingIn(delayMilliseconds);
    }
}

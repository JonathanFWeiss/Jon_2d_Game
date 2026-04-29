using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class DashPickup : MonoBehaviour
{
    private const string DashGrantedMessageText = "Dash Granted.";
    private const string DashGrantedMessageLabelName = "DashGrantedMessage";

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
        playerController.CanDash = true;
        PlayerData.AddInventoryItem(PlayerData.DashPickupItemName);
        ShowDashGrantedMessage();

        Debug.Log("Dash granted. Inventory count: " + PlayerData.GetInventoryItemCount(PlayerData.DashPickupItemName));
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

    private void ShowDashGrantedMessage()
    {
        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        VisualElement root = resolvedUiDocument.rootVisualElement;

        Label existingMessage = root.Q<Label>(DashGrantedMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label dashGrantedMessage = new Label(DashGrantedMessageText)
        {
            name = DashGrantedMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        dashGrantedMessage.style.position = Position.Absolute;
        dashGrantedMessage.style.top = messageTopOffset;
        dashGrantedMessage.style.left = 0f;
        dashGrantedMessage.style.right = 0f;
        dashGrantedMessage.style.marginLeft = StyleKeyword.Auto;
        dashGrantedMessage.style.marginRight = StyleKeyword.Auto;
        dashGrantedMessage.style.paddingLeft = 14f;
        dashGrantedMessage.style.paddingRight = 14f;
        dashGrantedMessage.style.paddingTop = 8f;
        dashGrantedMessage.style.paddingBottom = 8f;
        dashGrantedMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        dashGrantedMessage.style.color = Color.white;
        dashGrantedMessage.style.fontSize = 22f;
        dashGrantedMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        dashGrantedMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        dashGrantedMessage.style.borderTopLeftRadius = 8f;
        dashGrantedMessage.style.borderTopRightRadius = 8f;
        dashGrantedMessage.style.borderBottomLeftRadius = 8f;
        dashGrantedMessage.style.borderBottomRightRadius = 8f;
        dashGrantedMessage.style.alignSelf = Align.Center;

        root.Add(dashGrantedMessage);
        ScheduleMessageRemoval(root, dashGrantedMessage);
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
            Debug.LogWarning("DashPickup could not find a UIDocument to display the dash-granted message.");
        }

        return null;
    }

    private void ScheduleMessageRemoval(VisualElement root, Label dashGrantedMessage)
    {
        int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, messageDuration) * 1000f);
        if (delayMilliseconds <= 0)
        {
            dashGrantedMessage.RemoveFromHierarchy();
            return;
        }

        root.schedule.Execute(() =>
        {
            if (dashGrantedMessage != null && dashGrantedMessage.parent != null)
            {
                dashGrantedMessage.RemoveFromHierarchy();
            }
        }).StartingIn(delayMilliseconds);
    }
}

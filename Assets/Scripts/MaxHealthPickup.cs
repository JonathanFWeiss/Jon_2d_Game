using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class MaxHealthPickup : MonoBehaviour
{
    private const string MaxHealthGrantedMessageText = "Max Health Increased.";
    private const string MaxHealthGrantedMessageLabelName = "MaxHealthGrantedMessage";

    [Tooltip("Layer mask used to identify the player object. Leave empty to use JonCharacterController detection only.")]
    public LayerMask playerLayer;

    [Tooltip("Time in seconds before the pickup can be collected after spawning.")]
    public float pickupDelay = 0f;

    [Min(1)]
    [Tooltip("How many hearts this pickup adds while it remains in the player's inventory.")]
    [SerializeField] private int maxHealthIncrease = 1;

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
        PlayerData.AddMaxHealthPickup(maxHealthIncrease);
        ShowMaxHealthGrantedMessage();

        Debug.Log("Max health increased. Inventory count: " + PlayerData.GetInventoryItemCount(PlayerData.MaxHealthPickupItemName));
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

    private void ShowMaxHealthGrantedMessage()
    {
        UIDocument resolvedUiDocument = ResolveUiDocument();
        if (resolvedUiDocument == null || resolvedUiDocument.rootVisualElement == null)
            return;

        VisualElement root = resolvedUiDocument.rootVisualElement;

        Label existingMessage = root.Q<Label>(MaxHealthGrantedMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label maxHealthGrantedMessage = new Label(MaxHealthGrantedMessageText)
        {
            name = MaxHealthGrantedMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        maxHealthGrantedMessage.style.position = Position.Absolute;
        maxHealthGrantedMessage.style.top = messageTopOffset;
        maxHealthGrantedMessage.style.left = 0f;
        maxHealthGrantedMessage.style.right = 0f;
        maxHealthGrantedMessage.style.marginLeft = StyleKeyword.Auto;
        maxHealthGrantedMessage.style.marginRight = StyleKeyword.Auto;
        maxHealthGrantedMessage.style.paddingLeft = 14f;
        maxHealthGrantedMessage.style.paddingRight = 14f;
        maxHealthGrantedMessage.style.paddingTop = 8f;
        maxHealthGrantedMessage.style.paddingBottom = 8f;
        maxHealthGrantedMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        maxHealthGrantedMessage.style.color = Color.white;
        maxHealthGrantedMessage.style.fontSize = 22f;
        maxHealthGrantedMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        maxHealthGrantedMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        maxHealthGrantedMessage.style.borderTopLeftRadius = 8f;
        maxHealthGrantedMessage.style.borderTopRightRadius = 8f;
        maxHealthGrantedMessage.style.borderBottomLeftRadius = 8f;
        maxHealthGrantedMessage.style.borderBottomRightRadius = 8f;
        maxHealthGrantedMessage.style.alignSelf = Align.Center;

        root.Add(maxHealthGrantedMessage);
        ScheduleMessageRemoval(root, maxHealthGrantedMessage);
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
            Debug.LogWarning("MaxHealthPickup could not find a UIDocument to display the max-health-granted message.");
        }

        return null;
    }

    private void ScheduleMessageRemoval(VisualElement root, Label maxHealthGrantedMessage)
    {
        int delayMilliseconds = Mathf.RoundToInt(Mathf.Max(0f, messageDuration) * 1000f);
        if (delayMilliseconds <= 0)
        {
            maxHealthGrantedMessage.RemoveFromHierarchy();
            return;
        }

        root.schedule.Execute(() =>
        {
            if (maxHealthGrantedMessage != null && maxHealthGrantedMessage.parent != null)
            {
                maxHealthGrantedMessage.RemoveFromHierarchy();
            }
        }).StartingIn(delayMilliseconds);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    private const string CheckpointMessageText = "Checkpoint!";
    private const string CheckpointMessageLabelName = "CheckpointMessage";

    [Min(0f)]
    [SerializeField] private float minimumSpawnUpdateDistance = 20f;
    [Min(0f)]
    [SerializeField] private float messageDuration = 1.5f;

    private GameMaster gameMaster;
    private bool warnedMissingGameMaster;
    private bool warnedMissingUiDocument;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveGameMaster();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform playerTransform = GetPlayerTransform(other);
        if (playerTransform == null)
            return;

        if (!ResolveGameMaster())
            return;

        bool checkpointActivated = gameMaster.TrySetCheckpointRespawnPosition(
            playerTransform.position,
            minimumSpawnUpdateDistance
        );

        if (checkpointActivated)
        {
            ShowCheckpointMessage();
        }
    }

    private bool ResolveGameMaster()
    {
        if (gameMaster != null)
            return true;

        gameMaster = FindFirstObjectByType<GameMaster>();
        if (gameMaster != null)
            return true;

        if (!warnedMissingGameMaster)
        {
            warnedMissingGameMaster = true;
            Debug.LogWarning("Checkpoint could not find a GameMaster to update the respawn point.");
        }

        return false;
    }

    private void ShowCheckpointMessage()
    {
        UIDocument uiDocument = ResolveUiDocument();
        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
            return;

        Label existingMessage = root.Q<Label>(CheckpointMessageLabelName);
        if (existingMessage != null)
        {
            existingMessage.RemoveFromHierarchy();
        }

        Label checkpointMessage = new Label(CheckpointMessageText)
        {
            name = CheckpointMessageLabelName,
            pickingMode = PickingMode.Ignore
        };

        checkpointMessage.style.position = Position.Absolute;
        checkpointMessage.style.top = 48f;
        checkpointMessage.style.left = 0f;
        checkpointMessage.style.right = 0f;
        checkpointMessage.style.marginLeft = StyleKeyword.Auto;
        checkpointMessage.style.marginRight = StyleKeyword.Auto;
        checkpointMessage.style.paddingLeft = 14f;
        checkpointMessage.style.paddingRight = 14f;
        checkpointMessage.style.paddingTop = 8f;
        checkpointMessage.style.paddingBottom = 8f;
        checkpointMessage.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        checkpointMessage.style.color = Color.white;
        checkpointMessage.style.fontSize = 22f;
        checkpointMessage.style.unityFontStyleAndWeight = FontStyle.Bold;
        checkpointMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        checkpointMessage.style.borderTopLeftRadius = 8f;
        checkpointMessage.style.borderTopRightRadius = 8f;
        checkpointMessage.style.borderBottomLeftRadius = 8f;
        checkpointMessage.style.borderBottomRightRadius = 8f;
        checkpointMessage.style.alignSelf = Align.Center;

        root.Add(checkpointMessage);
        StartCoroutine(HideCheckpointMessageAfterDelay(checkpointMessage));
    }

    private UIDocument ResolveUiDocument()
    {
        if (gameMaster != null && gameMaster.uiDocument != null)
            return gameMaster.uiDocument;

        UIDocument uiDocument = FindFirstObjectByType<UIDocument>();
        if (uiDocument != null)
            return uiDocument;

        if (!warnedMissingUiDocument)
        {
            warnedMissingUiDocument = true;
            Debug.LogWarning("Checkpoint could not find a UIDocument to display the checkpoint message.");
        }

        return null;
    }

    private IEnumerator HideCheckpointMessageAfterDelay(Label checkpointMessage)
    {
        if (messageDuration > 0f)
        {
            yield return new WaitForSeconds(messageDuration);
        }

        if (checkpointMessage != null)
        {
            checkpointMessage.RemoveFromHierarchy();
        }
    }

    private static Transform GetPlayerTransform(Collider2D other)
    {
        if (other == null)
            return null;

        if (other.attachedRigidbody != null)
        {
            JonCharacterController rigidbodyController =
                other.attachedRigidbody.GetComponent<JonCharacterController>();

            if (rigidbodyController != null)
                return rigidbodyController.transform;

            Hero rigidbodyHero = other.attachedRigidbody.GetComponent<Hero>();
            if (rigidbodyHero != null)
                return rigidbodyHero.transform;
        }

        JonCharacterController playerController = other.GetComponentInParent<JonCharacterController>();
        if (playerController != null)
            return playerController.transform;

        Hero hero = other.GetComponentInParent<Hero>();
        if (hero != null)
            return hero.transform;

        return null;
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

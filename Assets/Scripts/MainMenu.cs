using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private const string DefaultGameplaySceneName = "CastleStart";
    private const string SaveMenuRootName = "SaveSlotMenu";
    private const string MainMenuDefaultButtonName = "PlayButton";
    private const string UiActionMapName = "UI";
    private const float ButtonHeight = 76f;
    private const float SlotButtonHeight = 104f;
    private static readonly string[] MainMenuUiActionNames =
    {
        "Navigate",
        "Submit",
        "Cancel",
        "Point",
        "Click",
        "RightClick",
        "MiddleClick",
        "ScrollWheel"
    };

    [SerializeField] private string gameplaySceneName = DefaultGameplaySceneName;
    [SerializeField] private Canvas menuCanvas;

    private GameObject saveMenuRoot;
    private TMP_FontAsset menuFont;
    private Button mainMenuDefaultButton;
    private Button[] mainMenuButtons;
    private bool[] mainMenuButtonInteractableStates;
    private readonly List<Button> currentMenuButtons = new List<Button>();

    private void OnEnable()
    {
        RestoreMainMenuRuntimeState();
    }

    private void Start()
    {
        RestoreMainMenuRuntimeState();
        RestoreCanvasInteraction();
        CloseSaveMenu(restoreMainMenuSelection: false, restoreMainMenuButtons: true);
        RestoreFreshMainMenuButtons();
        RestoreMainMenuSelection();
        StartCoroutine(RestoreMainMenuInteractionAfterSceneActivation());
    }

    public void PlayGame()
    {
        ShowPlayChoiceMenu();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowPlayChoiceMenu()
    {
        if (!CreateMenuRoot("Choose a Game", out Transform contentRoot))
            return;

        AddButton(contentRoot, "Load Game", ButtonHeight, true, ShowLoadSlotMenu);
        AddButton(contentRoot, "New Game", ButtonHeight, true, ShowNewGameSlotMenu);
        AddButton(contentRoot, "Back", ButtonHeight, true, CloseSaveMenu);
    }

    private void ShowLoadSlotMenu()
    {
        if (!CreateMenuRoot("Load Game", out Transform contentRoot))
            return;

        bool hasSavedGame = false;
        for (int slotNumber = PlayerSaveSystem.FirstSlotNumber;
             slotNumber <= PlayerSaveSystem.LastSlotNumber;
             slotNumber++)
        {
            if (!PlayerSaveSystem.TryGetSlotInfo(slotNumber, out PlayerSaveSlotInfo slotInfo))
                continue;

            hasSavedGame = true;
            int capturedSlotNumber = slotNumber;
            AddButton(
                contentRoot,
                FormatLoadSlotLabel(capturedSlotNumber, slotInfo),
                SlotButtonHeight,
                true,
                () => LoadGame(capturedSlotNumber)
            );
        }

        if (!hasSavedGame)
        {
            AddMessage(contentRoot, "No saved games found.");
        }

        AddButton(contentRoot, "Back", ButtonHeight, true, ShowPlayChoiceMenu);
    }

    private void ShowNewGameSlotMenu()
    {
        ShowNewGameSlotMenu(null);
    }

    private void ShowNewGameSlotMenu(string statusMessage)
    {
        if (!CreateMenuRoot("Start New Game", out Transform contentRoot))
            return;

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            AddMessage(contentRoot, statusMessage);
        }

        bool hasUnusedSlot = false;
        for (int slotNumber = PlayerSaveSystem.FirstSlotNumber;
             slotNumber <= PlayerSaveSystem.LastSlotNumber;
             slotNumber++)
        {
            if (PlayerSaveSystem.SlotExists(slotNumber))
                continue;

            hasUnusedSlot = true;
            int capturedSlotNumber = slotNumber;
            AddButton(
                contentRoot,
                $"Slot {capturedSlotNumber}: New Game",
                ButtonHeight,
                true,
                () => StartNewGame(capturedSlotNumber)
            );
        }

        if (!hasUnusedSlot)
        {
            AddMessage(contentRoot, "All save slots are already in use.");
        }

        AddButton(contentRoot, "Back", ButtonHeight, true, ShowPlayChoiceMenu);
    }

    private void LoadGame(int slotNumber)
    {
        if (!PlayerSaveSystem.TryLoadFromSlot(slotNumber, GetGameplaySceneName(), out string sceneNameToLoad))
        {
            ShowLoadSlotMenu();
            return;
        }

        LoadGameplayScene(sceneNameToLoad);
    }

    private void StartNewGame(int slotNumber)
    {
        if (PlayerSaveSystem.SlotExists(slotNumber))
        {
            ShowNewGameSlotMenu($"Slot {slotNumber} is already in use.");
            return;
        }

        PlayerData.Reset();
        if (!PlayerSaveSystem.TrySaveToSlot(slotNumber))
        {
            ShowNewGameSlotMenu($"Could not create a save in slot {slotNumber}.");
            return;
        }

        LoadGameplayScene();
    }

    private void LoadGameplayScene()
    {
        LoadGameplayScene(GetGameplaySceneName());
    }

    private void LoadGameplayScene(string sceneName)
    {
        SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName) ? GetGameplaySceneName() : sceneName.Trim());
    }

    private string GetGameplaySceneName()
    {
        return string.IsNullOrWhiteSpace(gameplaySceneName)
            ? DefaultGameplaySceneName
            : gameplaySceneName.Trim();
    }

    private static void RestoreMainMenuRuntimeState()
    {
        PlayerData.RestoreDefaultPauseState();
        RestoreUiInputActions(InputSystem.actions);
        RestoreEventSystem();
    }

    private static void RestoreEventSystem()
    {
        EventSystem eventSystem = ResolveSceneEventSystem();
        if (eventSystem == null)
            return;

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            return;

        RestoreUiInputActions(inputModule.actionsAsset);
        inputModule.enabled = false;
        inputModule.enabled = true;
    }

    private static EventSystem ResolveSceneEventSystem()
    {
        if (EventSystem.current != null)
            return EventSystem.current;

        foreach (EventSystem eventSystem in Resources.FindObjectsOfTypeAll<EventSystem>())
        {
            if (eventSystem == null || !eventSystem.gameObject.scene.IsValid())
                continue;

            return eventSystem;
        }

        return null;
    }

    private static void RestoreUiInputActions(InputActionAsset inputActions)
    {
        if (inputActions == null)
            return;

        InputActionMap uiActionMap = inputActions.FindActionMap(UiActionMapName, throwIfNotFound: false);
        uiActionMap?.Enable();

        foreach (string actionName in MainMenuUiActionNames)
        {
            InputAction action = inputActions.FindAction(actionName, throwIfNotFound: false);
            action?.Enable();
        }
    }

    private IEnumerator RestoreMainMenuInteractionAfterSceneActivation()
    {
        yield return null;
        RestoreMainMenuRuntimeState();
        RestoreCanvasInteraction();
        RestoreFreshMainMenuButtons();
        RestoreMainMenuSelection();
    }

    private bool CreateMenuRoot(string title, out Transform contentRoot)
    {
        contentRoot = null;
        ResolveCanvas();
        if (menuCanvas == null)
        {
            Debug.LogWarning("MainMenu could not find a Canvas for the save slot menu.");
            return false;
        }

        CloseSaveMenu(restoreMainMenuSelection: false, restoreMainMenuButtons: false);
        DisableMainMenuButtons();
        currentMenuButtons.Clear();

        saveMenuRoot = new GameObject(SaveMenuRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        saveMenuRoot.transform.SetParent(menuCanvas.transform, false);
        saveMenuRoot.layer = menuCanvas.gameObject.layer;

        RectTransform rootRect = saveMenuRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image scrim = saveMenuRoot.GetComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.74f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(saveMenuRoot.transform, false);
        panel.layer = saveMenuRoot.layer;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 720f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.04f, 0.04f, 0.92f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 36, 36);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddTitle(panel.transform, title);
        contentRoot = panel.transform;
        return true;
    }

    private void ResolveCanvas()
    {
        if (menuCanvas != null)
            return;

        menuCanvas = FindAnyObjectByType<Canvas>();
    }

    private void CloseSaveMenu()
    {
        CloseSaveMenu(restoreMainMenuSelection: true, restoreMainMenuButtons: true);
    }

    private void CloseSaveMenu(bool restoreMainMenuSelection, bool restoreMainMenuButtons)
    {
        if (saveMenuRoot != null)
        {
            saveMenuRoot.SetActive(false);
            Destroy(saveMenuRoot);
            saveMenuRoot = null;
        }

        currentMenuButtons.Clear();

        if (restoreMainMenuButtons)
        {
            RestoreMainMenuButtons();
        }

        if (restoreMainMenuSelection)
        {
            RestoreMainMenuSelection();
            return;
        }

        ClearCurrentSelection();
    }

    private void DisableMainMenuButtons()
    {
        if (mainMenuButtons != null)
            return;

        ResolveCanvas();
        if (menuCanvas == null)
            return;

        mainMenuButtons = menuCanvas.GetComponentsInChildren<Button>(true);
        mainMenuButtonInteractableStates = new bool[mainMenuButtons.Length];

        for (int i = 0; i < mainMenuButtons.Length; i++)
        {
            Button button = mainMenuButtons[i];
            if (button == null)
                continue;

            mainMenuButtonInteractableStates[i] = button.interactable;
            button.interactable = false;
        }
    }

    private void RestoreMainMenuButtons()
    {
        if (mainMenuButtons == null || mainMenuButtonInteractableStates == null)
            return;

        int buttonCount = Mathf.Min(mainMenuButtons.Length, mainMenuButtonInteractableStates.Length);
        for (int i = 0; i < buttonCount; i++)
        {
            Button button = mainMenuButtons[i];
            if (button != null)
            {
                button.interactable = mainMenuButtonInteractableStates[i];
            }
        }

        mainMenuButtons = null;
        mainMenuButtonInteractableStates = null;
    }

    private void RestoreFreshMainMenuButtons()
    {
        ResolveCanvas();
        if (menuCanvas == null)
            return;

        foreach (Button button in menuCanvas.GetComponentsInChildren<Button>(true))
        {
            if (button != null)
            {
                button.interactable = true;
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void RestoreCanvasInteraction()
    {
        ResolveCanvas();
        if (menuCanvas == null)
            return;

        menuCanvas.enabled = true;
        foreach (GraphicRaycaster raycaster in menuCanvas.GetComponentsInChildren<GraphicRaycaster>(true))
        {
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }
    }

    private void RestoreMainMenuSelection()
    {
        EventSystem eventSystem = ResolveSceneEventSystem();
        if (eventSystem == null)
            return;

        Button defaultButton = ResolveMainMenuDefaultButton();
        if (defaultButton == null || !defaultButton.interactable)
        {
            ClearCurrentSelection();
            return;
        }

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(defaultButton.gameObject);
    }

    private void ClearCurrentSelection()
    {
        EventSystem eventSystem = ResolveSceneEventSystem();
        if (eventSystem == null)
            return;

        eventSystem.SetSelectedGameObject(null);
    }

    private Button ResolveMainMenuDefaultButton()
    {
        if (mainMenuDefaultButton != null)
            return mainMenuDefaultButton;

        ResolveCanvas();
        if (menuCanvas == null)
            return null;

        Transform buttonTransform = FindChildRecursive(menuCanvas.transform, MainMenuDefaultButtonName);
        if (buttonTransform == null)
            return null;

        mainMenuDefaultButton = buttonTransform.GetComponent<Button>();
        return mainMenuDefaultButton;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform foundChild = FindChildRecursive(child, childName);
            if (foundChild != null)
                return foundChild;
        }

        return null;
    }

    private void AddTitle(Transform parent, string text)
    {
        TextMeshProUGUI title = CreateText(parent, "Title", text, 42f, FontStyles.Bold, TextAlignmentOptions.Center);
        LayoutElement layoutElement = title.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 64f;
        layoutElement.preferredHeight = 64f;
    }

    private void AddMessage(Transform parent, string text)
    {
        TextMeshProUGUI message = CreateText(parent, "Message", text, 26f, FontStyles.Normal, TextAlignmentOptions.Center);
        message.color = new Color(0.92f, 0.88f, 0.78f, 1f);

        LayoutElement layoutElement = message.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 66f;
        layoutElement.preferredHeight = 66f;
    }

    private Button AddButton(Transform parent, string label, float height, bool interactable, Action onClick)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.layer = parent.gameObject.layer;

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.interactable = interactable;
        button.targetGraphic = buttonImage;
        button.colors = CreateButtonColors();

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        TextMeshProUGUI labelText = CreateText(buttonObject.transform, "Label", label, 27f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(22f, 8f);
        labelRect.offsetMax = new Vector2(-22f, -8f);

        if (!interactable)
        {
            labelText.color = new Color(0.75f, 0.72f, 0.68f, 1f);
        }

        RegisterMenuButton(button);
        return button;
    }

    private void RegisterMenuButton(Button button)
    {
        if (button == null)
            return;

        currentMenuButtons.Add(button);
        ConfigureCurrentMenuNavigation();
        SelectFirstButton(button);
    }

    private void ConfigureCurrentMenuNavigation()
    {
        for (int i = 0; i < currentMenuButtons.Count; i++)
        {
            Button button = currentMenuButtons[i];
            if (button == null)
                continue;

            Selectable previousButton = currentMenuButtons[Mathf.Max(0, i - 1)];
            Selectable nextButton = currentMenuButtons[Mathf.Min(currentMenuButtons.Count - 1, i + 1)];

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = previousButton;
            navigation.selectOnDown = nextButton;
            navigation.selectOnLeft = button;
            navigation.selectOnRight = button;
            button.navigation = navigation;
        }
    }

    private TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        string text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.raycastTarget = false;
        textComponent.textWrappingMode = TextWrappingModes.Normal;

        TMP_FontAsset resolvedFont = ResolveFont();
        if (resolvedFont != null)
        {
            textComponent.font = resolvedFont;
        }

        return textComponent;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (menuFont != null)
            return menuFont;

        TMP_Text existingText = FindAnyObjectByType<TMP_Text>();
        if (existingText != null)
        {
            menuFont = existingText.font;
        }

        return menuFont;
    }

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.74f, 0.17f, 0.16f, 0.86f);
        colors.highlightedColor = new Color(0.98f, 0.65f, 0.32f, 1f);
        colors.pressedColor = new Color(0.82f, 0.24f, 0.18f, 1f);
        colors.selectedColor = new Color(0.93f, 0.44f, 0.24f, 1f);
        colors.disabledColor = new Color(0.2f, 0.18f, 0.18f, 0.78f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static string FormatLoadSlotLabel(int slotNumber, PlayerSaveSlotInfo slotInfo)
    {
        string savedAt = FormatSavedAt(slotInfo.savedAtUtc);
        return $"Slot {slotNumber}\nSaved {savedAt}\nCoins {slotInfo.coins}, HP {slotInfo.hp}, Energy {slotInfo.energy}";
    }

    private static string FormatSavedAt(string savedAtUtc)
    {
        if (DateTime.TryParse(
                savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime savedAt))
        {
            return savedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
        }

        return "at an unknown time";
    }

    private static void SelectFirstButton(Button button)
    {
        EventSystem eventSystem = ResolveSceneEventSystem();
        if (eventSystem == null ||
            button == null ||
            !button.interactable)
        {
            return;
        }

        GameObject selectedObject = eventSystem.currentSelectedGameObject;
        if (selectedObject != null && selectedObject.scene == button.gameObject.scene)
            return;

        eventSystem.SetSelectedGameObject(button.gameObject);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class EditorTweaksToolbarController : MonoBehaviour
{
    public static readonly Color NormalColor = new Color(0.13f, 0.13f, 0.13f, 0f);
    public static readonly Color HoverColor = new Color(0.28f, 0.28f, 0.28f, 0.62f);
    public static readonly Color PressedColor = new Color(0.40f, 0.40f, 0.40f, 0.86f);
    public static readonly Color SelectedColor = new Color(0.36f, 0.36f, 0.36f, 0.95f);

    private static readonly ToolDefinition[] Tools =
    {
        new("SelectButton", "Select", "选择", "Q"),
        new("MoveButton", "Move", "移动", "W"),
        new("RotateButton", "Rotate", "旋转", "E"),
        new("ScaleButton", "Scale", "缩放", "R"),
        new("RectButton", "Rect", "矩形 / 边界", "T"),
    };

    [SerializeField] private string selectedToolName = "Rect";
    [SerializeField] private float xSnapStep = 0.5f;
    [SerializeField] private float ySnapStep = 0.5f;

    private readonly List<EditorTweaksToolbarButtonView> buttons = new();
    private RectTransform rectTransform = null!;
    private RectTransform tooltipPanel = null!;
    private CanvasGroup tooltipCanvasGroup = null!;
    private Text tooltipText = null!;
    private RectTransform tooltipTarget = null!;
    private GameObject snapSettingsPanel = null!;
    private EditorTweaksToolbarUtilityButtonView snapSettingsButton = null!;
    private InputField xSnapInput = null!;
    private InputField ySnapInput = null!;
    private bool snapSettingsOpen;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        SetupDragHandle();
        SetupTooltip();
        SetupSnapSettingsPanel();

        foreach (ToolDefinition tool in Tools)
        {
            Register(tool);
        }

        SetupSnapSettingsButton();
        SelectTool(selectedToolName, instant: true);
    }

    public void ShowTooltip(RectTransform target, string label, string shortcut)
    {
        if (tooltipPanel == null || tooltipText == null)
        {
            return;
        }

        tooltipTarget = target;
        tooltipText.text = string.IsNullOrEmpty(shortcut) ? label : $"{label}   {shortcut}";
        tooltipPanel.gameObject.SetActive(true);
        if (tooltipCanvasGroup != null)
        {
            tooltipCanvasGroup.alpha = 1f;
            tooltipCanvasGroup.blocksRaycasts = false;
            tooltipCanvasGroup.interactable = false;
        }
        Canvas.ForceUpdateCanvases();

        float width = Mathf.Clamp(tooltipText.preferredWidth + 24f, 72f, 180f);
        tooltipPanel.sizeDelta = new Vector2(width, 28f);
        tooltipPanel.anchoredPosition = new Vector2(target.anchoredPosition.x, -50f);
    }

    public void HideTooltip()
    {
        tooltipTarget = null;
        if (tooltipPanel != null)
        {
            tooltipPanel.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (tooltipPanel == null || !tooltipPanel.gameObject.activeSelf || tooltipTarget == null)
        {
            return;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(tooltipTarget, Input.mousePosition, null))
        {
            HideTooltip();
        }
    }

    private void SetupDragHandle()
    {
        Transform dragHandle = transform.Find("DragHandle");
        if (dragHandle == null)
        {
            return;
        }

        Image hitArea = dragHandle.GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = dragHandle.gameObject.AddComponent<Image>();
        }

        hitArea.color = new Color(1f, 1f, 1f, 0f);
        hitArea.raycastTarget = true;

        EditorTweaksToolbarDragHandle handle = dragHandle.gameObject.GetComponent<EditorTweaksToolbarDragHandle>();
        if (handle == null)
        {
            handle = dragHandle.gameObject.AddComponent<EditorTweaksToolbarDragHandle>();
        }

        handle.Initialize(rectTransform);
    }

    private void Register(ToolDefinition tool)
    {
        Transform child = transform.Find(tool.ObjectName);
        if (child == null)
        {
            return;
        }

        Button button = child.GetComponent<Button>();
        Image background = child.GetComponent<Image>();
        if (button == null || background == null)
        {
            return;
        }

        EditorTweaksToolbarButtonView view = child.gameObject.GetComponent<EditorTweaksToolbarButtonView>();
        if (view == null)
        {
            view = child.gameObject.AddComponent<EditorTweaksToolbarButtonView>();
        }

        view.Initialize(tool, background, this, OnButtonClicked);
        buttons.Add(view);
        button.onClick.RemoveAllListeners();
    }

    private void OnButtonClicked(string toolName)
    {
        HideTooltip();
        SelectTool(toolName, instant: false);
        Debug.Log($"EditorTweaks toolbar selected: {toolName}");
    }

    private void SelectTool(string toolName, bool instant)
    {
        selectedToolName = toolName;
        foreach (EditorTweaksToolbarButtonView button in buttons)
        {
            button.SetSelected(button.ToolName == toolName, instant);
        }
    }

    private void SetupSnapSettingsButton()
    {
        Transform child = transform.Find("SnapSettingsButton");
        if (child == null)
        {
            Debug.LogWarning("EditorTweaksToolbar SnapSettingsButton object is missing.");
            return;
        }

        Button button = child.GetComponent<Button>();
        Image background = child.GetComponent<Image>();
        if (button == null || background == null)
        {
            return;
        }

        snapSettingsButton = child.gameObject.GetComponent<EditorTweaksToolbarUtilityButtonView>();
        if (snapSettingsButton == null)
        {
            snapSettingsButton = child.gameObject.AddComponent<EditorTweaksToolbarUtilityButtonView>();
        }

        snapSettingsButton.Initialize(
            "吸附设置",
            "X/Y",
            background,
            this,
            ToggleSnapSettingsPanel);
        button.onClick.RemoveAllListeners();
    }

    private void ToggleSnapSettingsPanel()
    {
        HideTooltip();
        snapSettingsOpen = !snapSettingsOpen;
        UpdateSnapSettingsPanel();
    }

    private void SetupSnapSettingsPanel()
    {
        Transform panel = transform.Find("SnapSettingsPanel");
        if (panel == null)
        {
            panel = transform.Find("MoveSnapPanel");
        }

        if (panel == null)
        {
            Debug.LogWarning("EditorTweaksToolbar SnapSettingsPanel object is missing.");
            return;
        }

        snapSettingsPanel = panel.gameObject;
        xSnapInput = panel.Find("XInput")?.GetComponent<InputField>();
        ySnapInput = panel.Find("YInput")?.GetComponent<InputField>();
        ConfigureSnapInput(xSnapInput, xSnapStep, value => xSnapStep = value);
        ConfigureSnapInput(ySnapInput, ySnapStep, value => ySnapStep = value);
        UpdateSnapSettingsPanel();
    }

    private static void ConfigureSnapInput(InputField input, float defaultValue, System.Action<float> onValueChanged)
    {
        if (input == null)
        {
            return;
        }

        Text text = input.transform.Find("Text")?.GetComponent<Text>();
        if (text != null)
        {
            input.textComponent = text;
            text.alignment = TextAnchor.MiddleCenter;
        }

        input.contentType = InputField.ContentType.DecimalNumber;
        input.lineType = InputField.LineType.SingleLine;
        input.SetTextWithoutNotify(defaultValue.ToString("0.###"));
        input.onEndEdit.RemoveAllListeners();
        input.onEndEdit.AddListener(raw =>
        {
            if (!float.TryParse(raw, out float parsed))
            {
                parsed = defaultValue;
            }

            parsed = Mathf.Max(0.0001f, parsed);
            input.SetTextWithoutNotify(parsed.ToString("0.###"));
            onValueChanged(parsed);
        });
    }

    private void UpdateSnapSettingsPanel()
    {
        if (snapSettingsPanel != null)
        {
            snapSettingsPanel.SetActive(snapSettingsOpen);
        }

        if (snapSettingsButton != null)
        {
            snapSettingsButton.SetActiveState(snapSettingsOpen, instant: false);
        }
    }

    private void SetupTooltip()
    {
        Transform tooltipTransform = transform.Find("Tooltip");
        if (tooltipTransform == null)
        {
            Debug.LogWarning("EditorTweaksToolbar Tooltip object is missing.");
            return;
        }

        tooltipPanel = (RectTransform)tooltipTransform;
        tooltipCanvasGroup = tooltipTransform.GetComponent<CanvasGroup>();
        if (tooltipCanvasGroup == null)
        {
            tooltipCanvasGroup = tooltipTransform.gameObject.AddComponent<CanvasGroup>();
        }

        tooltipCanvasGroup.alpha = 0f;
        tooltipCanvasGroup.blocksRaycasts = false;
        tooltipCanvasGroup.interactable = false;

        Image panelImage = tooltipTransform.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.raycastTarget = false;
        }

        tooltipText = tooltipTransform.Find("Text")?.GetComponent<Text>();
        if (tooltipText != null)
        {
            tooltipText.raycastTarget = false;
        }

        tooltipTransform.gameObject.SetActive(false);
    }

    public readonly struct ToolDefinition
    {
        public readonly string ObjectName;
        public readonly string ToolName;
        public readonly string Label;
        public readonly string Shortcut;

        public ToolDefinition(string objectName, string toolName, string label, string shortcut)
        {
            ObjectName = objectName;
            ToolName = toolName;
            Label = label;
            Shortcut = shortcut;
        }
    }
}

public sealed class EditorTweaksToolbarButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private const float ColorSpeed = 16f;
    private const float ScaleSpeed = 18f;

    private Image background = null!;
    private EditorTweaksToolbarController controller = null!;
    private EditorTweaksToolbarController.ToolDefinition tool;
    private System.Action<string> clicked = null!;
    private bool hovered;
    private bool pressed;
    private bool selected;
    private Color targetColor;
    private Vector3 targetScale;

    public string ToolName => tool.ToolName;

    public void Initialize(
        EditorTweaksToolbarController.ToolDefinition toolDefinition,
        Image backgroundImage,
        EditorTweaksToolbarController toolbarController,
        System.Action<string> onClicked)
    {
        tool = toolDefinition;
        background = backgroundImage;
        controller = toolbarController;
        clicked = onClicked;
        targetColor = EditorTweaksToolbarController.NormalColor;
        targetScale = Vector3.one;
        background.color = EditorTweaksToolbarController.NormalColor;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        if (controller != null)
        {
            controller.HideTooltip();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        pressed = false;
        RefreshTarget();
        clicked?.Invoke(ToolName);
    }

    public void SetSelected(bool value, bool instant)
    {
        selected = value;
        RefreshTarget();
        if (instant)
        {
            background.color = targetColor;
            transform.localScale = targetScale;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        controller.ShowTooltip((RectTransform)transform, tool.Label, tool.Shortcut);
        RefreshTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        controller.HideTooltip();
        RefreshTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        RefreshTarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        RefreshTarget();
    }

    private void Update()
    {
        if (background != null)
        {
            background.color = Color.Lerp(background.color, targetColor, Time.unscaledDeltaTime * ColorSpeed);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * ScaleSpeed);
    }

    private void RefreshTarget()
    {
        targetColor = selected
            ? EditorTweaksToolbarController.SelectedColor
            : hovered
                ? EditorTweaksToolbarController.HoverColor
                : EditorTweaksToolbarController.NormalColor;
        if (pressed)
        {
            targetColor = EditorTweaksToolbarController.PressedColor;
        }

        targetScale = pressed ? Vector3.one * 0.94f : hovered || selected ? Vector3.one * 1.04f : Vector3.one;
    }
}

public sealed class EditorTweaksToolbarUtilityButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private const float ColorSpeed = 16f;
    private const float ScaleSpeed = 18f;

    private Image background = null!;
    private EditorTweaksToolbarController controller = null!;
    private System.Action clicked = null!;
    private string label = string.Empty;
    private string shortcut = string.Empty;
    private bool hovered;
    private bool pressed;
    private bool active;
    private Color targetColor;
    private Vector3 targetScale;

    public void Initialize(
        string tooltipLabel,
        string tooltipShortcut,
        Image backgroundImage,
        EditorTweaksToolbarController toolbarController,
        System.Action onClicked)
    {
        label = tooltipLabel;
        shortcut = tooltipShortcut;
        background = backgroundImage;
        controller = toolbarController;
        clicked = onClicked;
        targetColor = EditorTweaksToolbarController.NormalColor;
        targetScale = Vector3.one;
        background.color = EditorTweaksToolbarController.NormalColor;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        if (controller != null)
        {
            controller.HideTooltip();
        }
    }

    public void SetActiveState(bool value, bool instant)
    {
        active = value;
        RefreshTarget();
        if (instant)
        {
            background.color = targetColor;
            transform.localScale = targetScale;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        pressed = false;
        RefreshTarget();
        clicked?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        controller.ShowTooltip((RectTransform)transform, label, shortcut);
        RefreshTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        controller.HideTooltip();
        RefreshTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        RefreshTarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        RefreshTarget();
    }

    private void Update()
    {
        if (background != null)
        {
            background.color = Color.Lerp(background.color, targetColor, Time.unscaledDeltaTime * ColorSpeed);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * ScaleSpeed);
    }

    private void RefreshTarget()
    {
        targetColor = active
            ? EditorTweaksToolbarController.SelectedColor
            : hovered
                ? EditorTweaksToolbarController.HoverColor
                : EditorTweaksToolbarController.NormalColor;
        if (pressed)
        {
            targetColor = EditorTweaksToolbarController.PressedColor;
        }

        targetScale = pressed ? Vector3.one * 0.94f : hovered || active ? Vector3.one * 1.04f : Vector3.one;
    }
}

public sealed class EditorTweaksToolbarDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private const float CanvasMargin = 8f;

    private RectTransform toolbar = null!;
    private RectTransform canvasRect = null!;
    private Vector2 pointerStart;
    private Vector2 toolbarStart;

    public void Initialize(RectTransform targetToolbar)
    {
        toolbar = targetToolbar;
        Canvas canvas = toolbar.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.transform : null!;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (toolbar == null || canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out pointerStart);
        toolbarStart = toolbar.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (toolbar == null || canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerCurrent);

        Vector2 next = toolbarStart + pointerCurrent - pointerStart;
        toolbar.anchoredPosition = ClampToCanvas(next);
    }

    private Vector2 ClampToCanvas(Vector2 position)
    {
        Rect canvas = canvasRect.rect;
        Rect rect = toolbar.rect;

        float maxX = canvas.width * 0.5f - rect.width * 0.5f - CanvasMargin;
        position.x = Mathf.Clamp(position.x, -maxX, maxX);

        float maxY = -CanvasMargin;
        float minY = -canvas.height + rect.height + CanvasMargin;
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }
}

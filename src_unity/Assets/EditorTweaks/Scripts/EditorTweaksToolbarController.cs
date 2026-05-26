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

    [SerializeField] private string selectedToolName = "Move";

    private readonly List<EditorTweaksToolbarButtonView> buttons = new();

    private void Awake()
    {
        Register("SelectButton", "Select");
        Register("MoveButton", "Move");
        Register("RotateButton", "Rotate");
        Register("ScaleButton", "Scale");
        Register("RectButton", "Rect");
        Register("PivotButton", "Pivot");
        Register("SpaceButton", "Space");
        Register("SnapButton", "Snap");
        Register("GridButton", "Grid");
        Register("VisibilityButton", "Visibility");
        Register("SettingsButton", "Settings");

        SelectTool(selectedToolName, instant: true);
    }

    private void Register(string objectName, string toolName)
    {
        Transform child = transform.Find(objectName);
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

        view.Initialize(toolName, background, OnButtonClicked);
        buttons.Add(view);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(view.Click);
    }

    private void OnButtonClicked(string toolName)
    {
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
}

public sealed class EditorTweaksToolbarButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float ColorSpeed = 16f;
    private const float ScaleSpeed = 18f;

    private Image background = null!;
    private System.Action<string> clicked = null!;
    private bool hovered;
    private bool pressed;
    private bool selected;
    private Color targetColor;
    private Vector3 targetScale;

    public string ToolName { get; private set; } = string.Empty;

    public void Initialize(string toolName, Image backgroundImage, System.Action<string> onClicked)
    {
        ToolName = toolName;
        background = backgroundImage;
        clicked = onClicked;
        targetColor = EditorTweaksToolbarController.NormalColor;
        targetScale = Vector3.one;
        background.color = EditorTweaksToolbarController.NormalColor;
    }

    public void Click()
    {
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
        RefreshTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
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

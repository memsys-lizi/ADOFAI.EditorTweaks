using System.Globalization;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.EditorOverlay
{
    internal sealed class EditorTweaksOverlayWindow : MonoBehaviour
    {
        private const int WindowId = 0x7E71A01;
        private const float WindowWidth = 400f;
        private const float MinWindowWidth = 320f;
        private const float ExpandedHeight = 286f;
        private const float CollapsedHeight = 42f;
        private const float OldDefaultX = 16f;
        private const float OldDefaultY = 96f;

        private static EditorTweaksOverlayWindow? instance;

        private Rect windowRect;
        private GUIStyle? windowStyle;
        private GUIStyle? titleStyle;
        private GUIStyle? labelStyle;
        private GUIStyle? hintStyle;
        private GUIStyle? inputStyle;
        private GUIStyle? buttonStyle;
        private GUIStyle? sectionStyle;
        private GUIStyle? rowStyle;
        private Texture2D? pixel;

        private string snapStepText = string.Empty;
        private string floatStepText = string.Empty;
        private string intStepText = string.Empty;
        private string decimalsText = string.Empty;

        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }

            GameObject host = new GameObject("ADOFAI.EditorTweaks.Overlay");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<EditorTweaksOverlayWindow>();
        }

        public static void Destroy()
        {
            if (instance == null)
            {
                return;
            }

            Object.Destroy(instance.gameObject);
            instance = null;
        }

        private void Awake()
        {
            windowRect = GetInitialWindowRect();
            SyncTextFields();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnGUI()
        {
            if (!ShouldDraw())
            {
                return;
            }

            EnsureStyles();
            ClampToScreen();

            Rect oldRect = windowRect;
            GUI.depth = -900;
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, GUIContent.none, windowStyle);
            windowRect.width = GetWindowWidth();
            windowRect.height = Main.Settings.EditorOverlayCollapsed ? CollapsedHeight : ExpandedHeight;

            if (Vector2.Distance(oldRect.position, windowRect.position) > 0.1f)
            {
                Main.Settings.EditorOverlayX = windowRect.x;
                Main.Settings.EditorOverlayY = windowRect.y;
                SaveSettings();
            }
        }

        private static bool ShouldDraw()
        {
            if (!Main.Settings.ShowEditorOverlay)
            {
                return false;
            }

            return ADOBase.isEditingLevel && ADOBase.editor != null;
        }

        private void DrawWindow(int id)
        {
            float width = windowRect.width;
            DrawRect(new Rect(0f, 0f, width, 40f), new Color(0.09f, 0.11f, 0.13f, 0.72f));
            DrawRect(new Rect(0f, 39f, width, 1f), new Color(0.32f, 0.56f, 0.64f, 0.44f));
            DrawWindowBorder(width, windowRect.height);
            DrawRect(new Rect(14f, 12f, 4f, 16f), new Color(0.42f, 0.78f, 0.82f, 0.95f));

            GUI.Label(new Rect(26f, 8f, width - 78f, 24f), Settings.Text("overlayTitle"), titleStyle);
            string collapseText = Main.Settings.EditorOverlayCollapsed ? "+" : "-";
            if (GUI.Button(new Rect(width - 42f, 8f, 26f, 24f), collapseText, buttonStyle))
            {
                Main.Settings.EditorOverlayCollapsed = !Main.Settings.EditorOverlayCollapsed;
                windowRect.height = Main.Settings.EditorOverlayCollapsed ? CollapsedHeight : ExpandedHeight;
                SaveSettings();
            }

            if (Main.Settings.EditorOverlayCollapsed)
            {
                GUI.DragWindow(new Rect(0f, 0f, width - 42f, CollapsedHeight));
                return;
            }

            float y = 54f;
            DrawSectionLabel(Settings.Text("decorationSection"), y, Settings.Text("zeroDisables"));
            y += 30f;
            DrawFloatRow(y, Settings.Text("decorationMoveSnapStep"), ref snapStepText, 0f, value => Main.Settings.DecorationMoveSnapStep = value);

            y += 44f;
            DrawSectionLabel(Settings.Text("numericSection"), y);
            y += 30f;
            DrawFloatRow(y, Settings.Text("floatStepPerPixel"), ref floatStepText, 0.0001f, value => Main.Settings.FloatStepPerPixel = value);
            y += 36f;
            DrawFloatRow(y, Settings.Text("intStepPerPixel"), ref intStepText, 0.0001f, value => Main.Settings.IntStepPerPixel = value);
            y += 36f;
            DrawIntRow(y, Settings.Text("maxFloatDecimals"), ref decimalsText, 0, 8, value => Main.Settings.MaxFloatingPoints = value);

            GUI.DragWindow(new Rect(0f, 0f, width - 42f, 40f));
        }

        private void DrawSectionLabel(string text, float y, string? hint = null)
        {
            float width = windowRect.width;
            GUI.Label(new Rect(22f, y, width * 0.55f, 22f), text, sectionStyle);
            if (!string.IsNullOrEmpty(hint))
            {
                GUI.Label(new Rect(width - 112f, y + 1f, 88f, 20f), hint, hintStyle);
            }

            DrawRect(new Rect(22f, y + 23f, width - 44f, 1f), new Color(0.28f, 0.48f, 0.56f, 0.36f));
        }

        private void DrawFloatRow(float y, string label, ref string text, float min, System.Action<float> apply)
        {
            DrawValueRow(y, label, text, out Rect inputRect);
            string next = GUI.TextField(inputRect, text, inputStyle);

            if (next == text)
            {
                return;
            }

            text = next;
            if (TryParseFloat(text, out float value))
            {
                apply(Mathf.Max(min, value));
                SaveSettings();
            }
        }

        private void DrawIntRow(float y, string label, ref string text, int min, int max, System.Action<int> apply)
        {
            DrawValueRow(y, label, text, out Rect inputRect);
            string next = GUI.TextField(inputRect, text, inputStyle);

            if (next == text)
            {
                return;
            }

            text = next;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                apply(Mathf.Clamp(value, min, max));
                SaveSettings();
            }
        }

        private void DrawValueRow(float y, string label, string value, out Rect inputRect)
        {
            float width = windowRect.width;
            Rect rowRect = new Rect(20f, y, width - 40f, 32f);
            GUI.Box(rowRect, GUIContent.none, rowStyle);
            DrawRect(new Rect(rowRect.x, rowRect.y, 3f, rowRect.height), new Color(0.44f, 0.76f, 0.80f, 0.38f));

            inputRect = new Rect(width - 112f, y + 5f, 86f, 22f);
            Rect labelRect = new Rect(34f, y + 6f, inputRect.x - 44f, 20f);
            GUI.Label(labelRect, label, labelStyle);
        }

        private void SyncTextFields()
        {
            snapStepText = FormatFloat(Main.Settings.DecorationMoveSnapStep);
            floatStepText = FormatFloat(Main.Settings.FloatStepPerPixel);
            intStepText = FormatFloat(Main.Settings.IntStepPerPixel);
            decimalsText = Main.Settings.MaxFloatingPoints.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private void ClampToScreen()
        {
            windowRect.height = Main.Settings.EditorOverlayCollapsed ? CollapsedHeight : ExpandedHeight;
            windowRect.width = GetWindowWidth();
            float maxX = Mathf.Max(0f, Screen.width - windowRect.width - 8f);
            float maxY = Mathf.Max(0f, Screen.height - windowRect.height - 8f);
            windowRect.x = Mathf.Clamp(windowRect.x, 8f, maxX);
            windowRect.y = Mathf.Clamp(windowRect.y, 8f, maxY);
        }

        private static float GetWindowWidth()
        {
            return Mathf.Clamp(Screen.width - 16f, MinWindowWidth, WindowWidth);
        }

        private static Rect GetInitialWindowRect()
        {
            bool useDefaultPosition = Main.Settings.EditorOverlayX < 0f
                || Main.Settings.EditorOverlayY < 0f
                || (Mathf.Abs(Main.Settings.EditorOverlayX - OldDefaultX) < 0.1f
                    && Mathf.Abs(Main.Settings.EditorOverlayY - OldDefaultY) < 0.1f);

            float height = Main.Settings.EditorOverlayCollapsed ? CollapsedHeight : ExpandedHeight;
            if (!useDefaultPosition)
            {
                return new Rect(Main.Settings.EditorOverlayX, Main.Settings.EditorOverlayY, GetWindowWidth(), height);
            }

            float width = GetWindowWidth();
            float x = Mathf.Clamp(Screen.width * 0.63f, 8f, Mathf.Max(8f, Screen.width - width - 8f));
            float y = Mathf.Clamp(14f, 8f, Mathf.Max(8f, Screen.height - height - 8f));
            Main.Settings.EditorOverlayX = x;
            Main.Settings.EditorOverlayY = y;
            SaveSettings();
            return new Rect(x, y, width, height);
        }

        private void EnsureStyles()
        {
            if (windowStyle != null)
            {
                return;
            }

            Texture2D windowBackground = MakeTexture(new Color(0.035f, 0.040f, 0.046f, 0.93f));
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(10, 10, 10, 10),
                normal = { background = windowBackground },
                focused = { background = windowBackground },
                active = { background = windowBackground },
                hover = { background = windowBackground },
                onNormal = { background = windowBackground },
                onFocused = { background = windowBackground },
                onActive = { background = windowBackground },
                onHover = { background = windowBackground }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.94f, 0.98f, 1f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 8, 0, 0),
                normal = { textColor = new Color(0.83f, 0.88f, 0.90f, 1f) }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.72f, 0.70f, 0.58f, 1f) }
            };
            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 2, 2),
                normal =
                {
                    background = MakeTexture(new Color(0.075f, 0.083f, 0.092f, 1f)),
                    textColor = Color.white
                },
                focused =
                {
                    background = MakeTexture(new Color(0.12f, 0.15f, 0.16f, 1f)),
                    textColor = Color.white
                }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 2),
                normal =
                {
                    background = MakeTexture(new Color(0.12f, 0.15f, 0.16f, 0.92f)),
                    textColor = new Color(0.88f, 0.92f, 0.96f, 1f)
                },
                hover =
                {
                    background = MakeTexture(new Color(0.20f, 0.28f, 0.30f, 0.98f)),
                    textColor = Color.white
                }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.84f, 0.95f, 0.98f, 1f) }
            };
            rowStyle = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 1, 1),
                normal = { background = MakeTexture(new Color(0.10f, 0.115f, 0.128f, 0.58f)) }
            };
        }

        private void DrawWindowBorder(float width, float height)
        {
            Color outer = new Color(0.78f, 0.88f, 0.92f, 0.82f);
            Color inner = new Color(0.34f, 0.53f, 0.58f, 0.34f);

            DrawRect(new Rect(0f, 0f, width, 1f), outer);
            DrawRect(new Rect(0f, height - 1f, width, 1f), outer);
            DrawRect(new Rect(0f, 0f, 1f, height), outer);
            DrawRect(new Rect(width - 1f, 0f, 1f, height), outer);

            DrawRect(new Rect(1f, 1f, width - 2f, 1f), inner);
            DrawRect(new Rect(1f, height - 2f, width - 2f, 1f), inner);
            DrawRect(new Rect(1f, 1f, 1f, height - 2f), inner);
            DrawRect(new Rect(width - 2f, 1f, 1f, height - 2f), inner);
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private static void SaveSettings()
        {
            if (Main.Mod != null)
            {
                Main.Settings.Save(Main.Mod);
            }
        }
    }
}

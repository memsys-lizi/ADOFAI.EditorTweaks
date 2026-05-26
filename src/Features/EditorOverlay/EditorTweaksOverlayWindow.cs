using System.Globalization;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.EditorOverlay
{
    internal sealed class EditorTweaksOverlayWindow : MonoBehaviour
    {
        private const int WindowId = 0x7E71A01;
        private const float WindowWidth = 320f;
        private const float ExpandedHeight = 236f;
        private const float CollapsedHeight = 38f;
        private const float OldDefaultX = 16f;
        private const float OldDefaultY = 96f;

        private static EditorTweaksOverlayWindow? instance;

        private Rect windowRect;
        private GUIStyle? windowStyle;
        private GUIStyle? headerStyle;
        private GUIStyle? titleStyle;
        private GUIStyle? labelStyle;
        private GUIStyle? hintStyle;
        private GUIStyle? inputStyle;
        private GUIStyle? buttonStyle;
        private GUIStyle? sectionStyle;
        private GUIStyle? rowStyle;

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
            windowRect.width = WindowWidth;
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
            GUI.Label(new Rect(16f, 8f, 220f, 22f), Settings.Text("overlayTitle"), titleStyle);
            string collapseText = Main.Settings.EditorOverlayCollapsed ? "+" : "-";
            if (GUI.Button(new Rect(WindowWidth - 40f, 8f, 26f, 22f), collapseText, buttonStyle))
            {
                Main.Settings.EditorOverlayCollapsed = !Main.Settings.EditorOverlayCollapsed;
                windowRect.height = Main.Settings.EditorOverlayCollapsed ? CollapsedHeight : ExpandedHeight;
                SaveSettings();
            }

            if (Main.Settings.EditorOverlayCollapsed)
            {
                GUI.DragWindow(new Rect(0f, 0f, WindowWidth - 34f, CollapsedHeight));
                return;
            }

            float y = 42f;
            DrawSectionLabel(Settings.Text("decorationSection"), y);
            y += 24f;
            DrawFloatRow(y, Settings.Text("decorationMoveSnapStep"), ref snapStepText, 0f, value => Main.Settings.DecorationMoveSnapStep = value);
            y += 24f;
            GUI.Label(new Rect(220f, y - 4f, 84f, 18f), Settings.Text("zeroDisables"), hintStyle);

            y += 24f;
            DrawSectionLabel(Settings.Text("numericSection"), y);
            y += 24f;
            DrawFloatRow(y, Settings.Text("floatStepPerPixel"), ref floatStepText, 0.0001f, value => Main.Settings.FloatStepPerPixel = value);
            y += 26f;
            DrawFloatRow(y, Settings.Text("intStepPerPixel"), ref intStepText, 0.0001f, value => Main.Settings.IntStepPerPixel = value);
            y += 26f;
            DrawIntRow(y, Settings.Text("maxFloatDecimals"), ref decimalsText, 0, 8, value => Main.Settings.MaxFloatingPoints = value);

            GUI.DragWindow(new Rect(0f, 0f, WindowWidth - 32f, 30f));
        }

        private void DrawSectionLabel(string text, float y)
        {
            GUI.Label(new Rect(20f, y, 260f, 22f), text, sectionStyle);
        }

        private void DrawFloatRow(float y, string label, ref string text, float min, System.Action<float> apply)
        {
            GUI.Box(new Rect(18f, y, WindowWidth - 36f, 23f), GUIContent.none, rowStyle);
            GUI.Label(new Rect(28f, y + 1f, 170f, 21f), label, labelStyle);
            string next = GUI.TextField(new Rect(220f, y + 2f, 84f, 19f), text, inputStyle);

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
            GUI.Box(new Rect(18f, y, WindowWidth - 36f, 23f), GUIContent.none, rowStyle);
            GUI.Label(new Rect(28f, y + 1f, 170f, 21f), label, labelStyle);
            string next = GUI.TextField(new Rect(220f, y + 2f, 84f, 19f), text, inputStyle);

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
            float maxX = Mathf.Max(0f, Screen.width - WindowWidth - 8f);
            float maxY = Mathf.Max(0f, Screen.height - windowRect.height - 8f);
            windowRect.x = Mathf.Clamp(windowRect.x, 8f, maxX);
            windowRect.y = Mathf.Clamp(windowRect.y, 8f, maxY);
            windowRect.width = WindowWidth;
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
                return new Rect(Main.Settings.EditorOverlayX, Main.Settings.EditorOverlayY, WindowWidth, height);
            }

            float x = Mathf.Clamp(Screen.width * 0.63f, 8f, Mathf.Max(8f, Screen.width - WindowWidth - 8f));
            float y = Mathf.Clamp(14f, 8f, Mathf.Max(8f, Screen.height - height - 8f));
            Main.Settings.EditorOverlayX = x;
            Main.Settings.EditorOverlayY = y;
            SaveSettings();
            return new Rect(x, y, WindowWidth, height);
        }

        private void EnsureStyles()
        {
            if (windowStyle != null)
            {
                return;
            }

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(12, 12, 8, 12),
                normal = { background = MakeTexture(new Color(0.025f, 0.027f, 0.032f, 0.88f)) }
            };
            headerStyle = new GUIStyle
            {
                padding = new RectOffset(4, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.97f, 1f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 0, 0, 0),
                normal = { textColor = new Color(0.82f, 0.87f, 0.92f, 1f) }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.58f, 0.64f, 0.70f, 1f) }
            };
            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = MakeTexture(new Color(0.08f, 0.09f, 0.12f, 1f)),
                    textColor = Color.white
                },
                focused =
                {
                    background = MakeTexture(new Color(0.14f, 0.16f, 0.20f, 1f)),
                    textColor = Color.white
                }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    background = MakeTexture(new Color(0.10f, 0.11f, 0.14f, 0.88f)),
                    textColor = new Color(0.88f, 0.92f, 0.96f, 1f)
                },
                hover =
                {
                    background = MakeTexture(new Color(0.18f, 0.20f, 0.25f, 0.95f)),
                    textColor = Color.white
                }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 0, 0, 0),
                normal = { textColor = new Color(0.94f, 0.97f, 1f, 1f) }
            };
            rowStyle = new GUIStyle
            {
                padding = new RectOffset(0, 8, 2, 2),
                margin = new RectOffset(0, 0, 1, 1),
                normal = { background = MakeTexture(new Color(0.09f, 0.10f, 0.12f, 0.34f)) }
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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

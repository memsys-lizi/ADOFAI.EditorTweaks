using System.Globalization;
using UnityModManagerNet;
using UnityEngine;

namespace ADOFAI.EditorTweaks
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableNumericDrag = true;

        public bool EnableCameraRelativeDecorationDragFix = true;

        public bool EnableDecorationPivotFix = true;

        public bool EnableVideoBackgroundSyncFix = true;

        public bool PersistEditorPreferences = true;

        public bool ShowEditorOverlay = true;

        public bool EditorOverlayCollapsed = false;

        public float EditorOverlayX = -1f;

        public float EditorOverlayY = -1f;

        public float DecorationMoveSnapStep = 0.5f;

        public float FloatStepPerPixel = 0.1f;

        public float IntStepPerPixel = 1f;

        public int MaxFloatingPoints = 3;

        private static GUIStyle? panelStyle;

        private static GUIStyle? titleStyle;

        private static GUIStyle? sectionStyle;

        private static GUIStyle? labelStyle;

        private static GUIStyle? hintStyle;

        private static GUIStyle? toggleStyle;

        private static GUIStyle? textFieldStyle;

        private string snapStepText = string.Empty;

        private string floatStepText = string.Empty;

        private string intStepText = string.Empty;

        private string decimalsText = string.Empty;

        private bool textFieldsInitialized;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            EnsureStyles();
            EnsureTextFields();

            GUILayout.BeginVertical(panelStyle);
            GUILayout.Label(Text("title"), titleStyle);
            GUILayout.Space(4);

            DrawSection(Text("fixesSection"));
            EnableCameraRelativeDecorationDragFix = DrawToggleRow(EnableCameraRelativeDecorationDragFix, Text("fixCameraRelativeDecorationDrag"));
            EnableDecorationPivotFix = DrawToggleRow(EnableDecorationPivotFix, Text("fixDecorationPivot"));
            EnableVideoBackgroundSyncFix = DrawToggleRow(EnableVideoBackgroundSyncFix, Text("fixVideoBackgroundSync"));
            PersistEditorPreferences = DrawToggleRow(PersistEditorPreferences, Text("persistEditorPreferences"));

            DrawSection(Text("overlaySection"));
            ShowEditorOverlay = DrawToggleRow(ShowEditorOverlay, Text("showEditorOverlay"));

            DrawSection(Text("numericSection"));
            EnableNumericDrag = DrawToggleRow(EnableNumericDrag, Text("enableNumericDrag"));
            FloatStepPerPixel = DrawFloatRow(Text("floatStepPerPixel"), FloatStepPerPixel, ref floatStepText, 0.0001f);
            IntStepPerPixel = DrawFloatRow(Text("intStepPerPixel"), IntStepPerPixel, ref intStepText, 0.0001f);
            MaxFloatingPoints = DrawIntRow(Text("maxFloatDecimals"), MaxFloatingPoints, ref decimalsText, 0, 8);

            DrawSection(Text("decorationSection"));
            DecorationMoveSnapStep = DrawFloatRow(Text("decorationMoveSnapStep"), DecorationMoveSnapStep, ref snapStepText, 0f, Text("zeroDisables"));

            GUILayout.Space(2);
            GUILayout.EndVertical();
        }

        private void EnsureTextFields()
        {
            if (textFieldsInitialized)
            {
                return;
            }

            snapStepText = FormatFloat(DecorationMoveSnapStep);
            floatStepText = FormatFloat(FloatStepPerPixel);
            intStepText = FormatFloat(IntStepPerPixel);
            decimalsText = MaxFloatingPoints.ToString(CultureInfo.InvariantCulture);
            textFieldsInitialized = true;
        }

        private static void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 12, 14),
                margin = new RectOffset(4, 4, 4, 4)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.98f, 1f, 1f) }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 5, 2),
                normal = { textColor = new Color(0.74f, 0.88f, 1f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 8, 0, 0)
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.72f, 0.72f, 0.72f, 1f) }
            };
            toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 3, 3),
                padding = new RectOffset(24, 6, 3, 3)
            };
            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 22
            };
        }

        private static void DrawSection(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, sectionStyle);
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorTweaksGui.DrawRect(rect, new Color(0.35f, 0.52f, 0.70f, 0.45f));
            GUILayout.Space(2);
        }

        private static bool DrawToggleRow(bool value, string text)
        {
            GUILayout.BeginHorizontal();
            bool next = GUILayout.Toggle(value, text, toggleStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return next;
        }

        private static float DrawFloatRow(string label, float value, ref string text, float min, string? hint = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(96));
            if (TryParseFloat(text, out float next))
            {
                value = Mathf.Max(min, next);
            }

            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Label(hint, hintStyle, GUILayout.Width(90));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return value;
        }

        private static int DrawIntRow(string label, int value, ref string text, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(96));
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int next))
            {
                value = Mathf.Clamp(next, min, max);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return value;
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

        public static string Text(string key)
        {
            return Localization.Text(key);
        }

        public void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Save(modEntry);
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public static Settings Load(UnityModManager.ModEntry modEntry)
        {
            return Load<Settings>(modEntry);
        }

        private static class EditorTweaksGui
        {
            private static Texture2D? pixel;

            public static void DrawRect(Rect rect, Color color)
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
        }
    }
}

using System.Globalization;
using System.IO;
using ADOFAI.EditorTweaks.Features.ChartRendering;
using UnityModManagerNet;
using UnityEngine;

namespace ADOFAI.EditorTweaks
{
    public class Settings : UnityModManager.ModSettings
    {
        private const int MinChartRenderSize = 16;
        private const int MaxChartRenderWidth = 7680;
        private const int MaxChartRenderHeight = 4320;
        private const int MinChartRenderFps = 1;
        private const int MaxChartRenderFps = 240;
        private const int MinChartRenderCrf = 0;
        private const int MaxChartRenderCrf = 51;
        private const int DefaultChartRenderWidth = 1920;
        private const int DefaultChartRenderHeight = 1080;
        private const int DefaultChartRenderFps = 60;
        private const int DefaultChartRenderCrf = 18;
        private const int DefaultChartRenderBitrateMbps = ChartRenderBitratePresets.AutoBitrateMbps;
        private const float DefaultChartRenderCompletionTailSeconds = 5f;
        private const float DefaultChartRenderAudioSyncOffsetMs = 0f;
        private const float MinChartRenderAudioSyncOffsetMs = -5000f;
        private const float MaxChartRenderAudioSyncOffsetMs = 5000f;
        private const bool DefaultChartRenderShowHitJudgments = true;
        private const bool DefaultChartRenderUseSelectedRange = false;
        private const bool DefaultChartRenderAdvancedSettingsExpanded = false;
        private const string DefaultChartRenderPreset = "veryfast";
        private const string DefaultChartRenderEncoderMode = ChartRenderOptionValues.EncoderAutoBalanced;
        private const string DefaultChartRenderCaptureFormat = ChartRenderOptionValues.CaptureRgba;
        private const string DefaultChartRenderAudioFormat = ChartRenderOptionValues.AudioFormatAac;
        private const string DefaultChartRenderPreviewMode = ChartRenderOptionValues.PreviewFull;

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

        public string ChartRenderWorkspaceDirectory = string.Empty;

        public string ChartRenderExportDirectory = string.Empty;

        public int ChartRenderWidth = 1920;

        public int ChartRenderHeight = 1080;

        public int ChartRenderFps = 60;

        public int ChartRenderCrf = 18;

        public int ChartRenderBitrateMbps = DefaultChartRenderBitrateMbps;

        public string ChartRenderPreset = "veryfast";

        public string ChartRenderEncoderMode = DefaultChartRenderEncoderMode;

        public string ChartRenderCaptureFormat = DefaultChartRenderCaptureFormat;

        public string ChartRenderPreviewMode = DefaultChartRenderPreviewMode;

        public string ChartRenderAudioFormat = DefaultChartRenderAudioFormat;

        public float ChartRenderCompletionTailSeconds = 5f;

        public float ChartRenderAudioSyncOffsetMs = 0f;

        public bool ChartRenderShowHitJudgments = true;

        public bool ChartRenderUseSelectedRange = DefaultChartRenderUseSelectedRange;

        public bool ChartRenderAdvancedSettingsExpanded = false;

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

        private string renderWidthText = string.Empty;

        private string renderHeightText = string.Empty;

        private string renderFpsText = string.Empty;

        private string renderCrfText = string.Empty;

        private string renderBitrateText = string.Empty;

        private string renderPresetText = string.Empty;

        private string renderTailSecondsText = string.Empty;

        private string renderAudioSyncOffsetText = string.Empty;

        private bool textFieldsInitialized;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            EnsureStyles();
            EnsureTextFields();

            int oldRenderWidth = ChartRenderWidth;
            int oldRenderHeight = ChartRenderHeight;
            int oldRenderFps = ChartRenderFps;
            int oldRenderCrf = ChartRenderCrf;
            int oldRenderBitrate = ChartRenderBitrateMbps;
            string oldRenderPreset = ChartRenderPreset;
            string oldRenderEncoderMode = ChartRenderEncoderMode;
            string oldRenderCaptureFormat = ChartRenderCaptureFormat;
            string oldRenderPreviewMode = ChartRenderPreviewMode;
            string oldRenderAudioFormat = ChartRenderAudioFormat;
            float oldRenderTail = ChartRenderCompletionTailSeconds;
            float oldRenderAudioSyncOffset = ChartRenderAudioSyncOffsetMs;
            bool oldRenderJudgments = ChartRenderShowHitJudgments;
            bool oldRenderUseSelectedRange = ChartRenderUseSelectedRange;
            bool oldAdvancedSettingsExpanded = ChartRenderAdvancedSettingsExpanded;
            string oldWorkspaceDirectory = ChartRenderWorkspaceDirectory;
            string oldExportDirectory = ChartRenderExportDirectory;

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

            DrawSection(Text("renderSection"));
            GUILayout.Label(Text("chartRenderBasicHint"), hintStyle);
            ChartRenderExportDirectory = DrawTextSettingRow(Text("chartRenderExportDirectory"), Text("chartRenderExportDirectoryHint"), ChartRenderExportDirectory, GetDefaultExportDirectory(modEntry));
            DrawResolutionPresetRow();
            ChartRenderWidth = DrawIntSettingRow(Text("chartRenderWidth"), Text("chartRenderWidthHint"), ChartRenderWidth, ref renderWidthText, MinChartRenderSize, MaxChartRenderWidth, DefaultChartRenderWidth);
            ChartRenderHeight = DrawIntSettingRow(Text("chartRenderHeight"), Text("chartRenderHeightHint"), ChartRenderHeight, ref renderHeightText, MinChartRenderSize, MaxChartRenderHeight, DefaultChartRenderHeight);
            DrawFpsPresetRow();
            ChartRenderFps = DrawIntSettingRow(Text("chartRenderFps"), Text("chartRenderFpsHint"), ChartRenderFps, ref renderFpsText, MinChartRenderFps, MaxChartRenderFps, DefaultChartRenderFps);
            ChartRenderCompletionTailSeconds = DrawFloatSettingRow(Text("chartRenderCompletionTailSeconds"), Text("chartRenderCompletionTailSecondsHint"), ChartRenderCompletionTailSeconds, ref renderTailSecondsText, 0f, DefaultChartRenderCompletionTailSeconds);
            ChartRenderShowHitJudgments = DrawToggleSettingRow(Text("chartRenderShowHitJudgments"), Text("chartRenderShowHitJudgmentsHint"), ChartRenderShowHitJudgments, DefaultChartRenderShowHitJudgments);
            ChartRenderUseSelectedRange = DrawToggleSettingRow(Text("chartRenderUseSelectedRange"), Text("chartRenderUseSelectedRangeHint"), ChartRenderUseSelectedRange, DefaultChartRenderUseSelectedRange);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("chartRenderResetAll"), GUILayout.Width(150)))
            {
                ResetChartRenderDefaults(modEntry);
            }

            if (GUILayout.Button(ChartRenderAdvancedSettingsExpanded ? Text("chartRenderHideAdvanced") : Text("chartRenderShowAdvanced"), GUILayout.Width(170)))
            {
                ChartRenderAdvancedSettingsExpanded = !ChartRenderAdvancedSettingsExpanded;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (ChartRenderAdvancedSettingsExpanded)
            {
                GUILayout.Label(Text("chartRenderAdvancedHint"), hintStyle);
                ChartRenderWorkspaceDirectory = DrawTextSettingRow(Text("chartRenderWorkspaceDirectory"), Text("chartRenderWorkspaceDirectoryHint"), ChartRenderWorkspaceDirectory, GetDefaultWorkspaceDirectory(modEntry));
                ChartRenderEncoderMode = DrawChoiceSettingRow(Text("chartRenderEncoderMode"), Text("chartRenderEncoderModeHint"), ChartRenderEncoderMode, ChartRenderOptionValues.EncoderModes, GetEncoderModeLabels(), DefaultChartRenderEncoderMode);
                ChartRenderCrf = DrawIntSettingRow(Text("chartRenderCrf"), Text("chartRenderCrfHint"), ChartRenderCrf, ref renderCrfText, MinChartRenderCrf, MaxChartRenderCrf, DefaultChartRenderCrf);
                ChartRenderBitrateMbps = DrawIntSettingRow(Text("chartRenderBitrateMbps"), GetBitrateHint(), ChartRenderBitrateMbps, ref renderBitrateText, ChartRenderBitratePresets.AutoBitrateMbps, ChartRenderBitratePresets.MaxBitrateMbps, DefaultChartRenderBitrateMbps);
                if (ChartRenderOptionValues.NormalizeEncoderMode(ChartRenderEncoderMode) == ChartRenderOptionValues.EncoderCustom)
                {
                    ChartRenderPreset = DrawStringSettingRow(Text("chartRenderPreset"), Text("chartRenderPresetHint"), ChartRenderPreset, ref renderPresetText, DefaultChartRenderPreset);
                }

                ChartRenderCaptureFormat = DrawChoiceSettingRow(Text("chartRenderCaptureFormat"), Text("chartRenderCaptureFormatHint"), ChartRenderCaptureFormat, ChartRenderOptionValues.CaptureFormats, GetCaptureFormatLabels(), DefaultChartRenderCaptureFormat);
                ChartRenderAudioFormat = DrawChoiceSettingRow(Text("chartRenderAudioFormat"), Text("chartRenderAudioFormatHint"), ChartRenderAudioFormat, ChartRenderOptionValues.AudioFormats, GetAudioFormatLabels(), DefaultChartRenderAudioFormat);
                ChartRenderPreviewMode = DrawChoiceSettingRow(Text("chartRenderPreviewMode"), Text("chartRenderPreviewModeHint"), ChartRenderPreviewMode, ChartRenderOptionValues.PreviewModes, GetPreviewModeLabels(), DefaultChartRenderPreviewMode);
                ChartRenderAudioSyncOffsetMs = DrawFloatSettingRow(Text("chartRenderAudioSyncOffsetMs"), Text("chartRenderAudioSyncOffsetMsHint"), ChartRenderAudioSyncOffsetMs, ref renderAudioSyncOffsetText, MinChartRenderAudioSyncOffsetMs, DefaultChartRenderAudioSyncOffsetMs);
            }

            GUILayout.Space(2);
            GUILayout.EndVertical();

            NormalizeChartRenderSettings();
            if (oldRenderWidth != ChartRenderWidth
                || oldRenderHeight != ChartRenderHeight
                || oldRenderFps != ChartRenderFps
                || oldRenderCrf != ChartRenderCrf
                || oldRenderBitrate != ChartRenderBitrateMbps
                || oldRenderPreset != ChartRenderPreset
                || oldRenderEncoderMode != ChartRenderEncoderMode
                || oldRenderCaptureFormat != ChartRenderCaptureFormat
                || oldRenderPreviewMode != ChartRenderPreviewMode
                || oldRenderAudioFormat != ChartRenderAudioFormat
                || oldRenderTail != ChartRenderCompletionTailSeconds
                || oldRenderAudioSyncOffset != ChartRenderAudioSyncOffsetMs
                || oldRenderJudgments != ChartRenderShowHitJudgments
                || oldRenderUseSelectedRange != ChartRenderUseSelectedRange
                || oldAdvancedSettingsExpanded != ChartRenderAdvancedSettingsExpanded
                || oldWorkspaceDirectory != ChartRenderWorkspaceDirectory
                || oldExportDirectory != ChartRenderExportDirectory)
            {
                Save(modEntry);
            }
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
            renderWidthText = ChartRenderWidth.ToString(CultureInfo.InvariantCulture);
            renderHeightText = ChartRenderHeight.ToString(CultureInfo.InvariantCulture);
            renderFpsText = ChartRenderFps.ToString(CultureInfo.InvariantCulture);
            renderCrfText = ChartRenderCrf.ToString(CultureInfo.InvariantCulture);
            renderBitrateText = ChartRenderBitrateMbps.ToString(CultureInfo.InvariantCulture);
            renderPresetText = ChartRenderPreset;
            renderTailSecondsText = FormatFloat(ChartRenderCompletionTailSeconds);
            renderAudioSyncOffsetText = FormatFloat(ChartRenderAudioSyncOffsetMs);
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

        private static string DrawTextRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            string next = GUILayout.TextField(value ?? string.Empty, textFieldStyle, GUILayout.MinWidth(220), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return next;
        }

        private static int DrawIntSettingRow(string label, string hint, int value, ref string text, int min, int max, int defaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(96));
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int next))
            {
                value = Mathf.Clamp(next, min, max);
            }

            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                value = defaultValue;
                text = value.ToString(CultureInfo.InvariantCulture);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return value;
        }

        private static float DrawFloatSettingRow(string label, string hint, float value, ref string text, float min, float defaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(96));
            if (TryParseFloat(text, out float next))
            {
                value = Mathf.Max(min, next);
            }

            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                value = defaultValue;
                text = FormatFloat(value);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return value;
        }

        private static string DrawTextSettingRow(string label, string hint, string value, string defaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            string next = GUILayout.TextField(value ?? string.Empty, textFieldStyle, GUILayout.MinWidth(220), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                next = defaultValue;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return next;
        }

        private static string DrawStringSettingRow(string label, string hint, string value, ref string text, string defaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            text = GUILayout.TextField(text ?? string.Empty, textFieldStyle, GUILayout.Width(140));
            if (!string.IsNullOrWhiteSpace(text))
            {
                value = text.Trim();
            }

            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                value = defaultValue;
                text = defaultValue;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return value;
        }

        private void DrawResolutionPresetRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("chartRenderResolutionPreset"), labelStyle, GUILayout.Width(190));
            if (GUILayout.Button("1080p", GUILayout.Width(82)))
            {
                ApplyChartRenderResolutionPreset(1920, 1080);
            }

            if (GUILayout.Button("2K", GUILayout.Width(82)))
            {
                ApplyChartRenderResolutionPreset(2560, 1440);
            }

            if (GUILayout.Button("4K", GUILayout.Width(82)))
            {
                ApplyChartRenderResolutionPreset(3840, 2160);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(Text("chartRenderResolutionPresetHint"), hintStyle);
        }

        private void DrawFpsPresetRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("chartRenderFpsPreset"), labelStyle, GUILayout.Width(190));
            if (GUILayout.Button("30", GUILayout.Width(82)))
            {
                ApplyChartRenderFpsPreset(30);
            }

            if (GUILayout.Button("60", GUILayout.Width(82)))
            {
                ApplyChartRenderFpsPreset(60);
            }

            if (GUILayout.Button("120", GUILayout.Width(82)))
            {
                ApplyChartRenderFpsPreset(120);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(Text("chartRenderFpsPresetHint"), hintStyle);
        }

        private void ApplyChartRenderResolutionPreset(int width, int height)
        {
            ChartRenderWidth = width;
            ChartRenderHeight = height;
            renderWidthText = ChartRenderWidth.ToString(CultureInfo.InvariantCulture);
            renderHeightText = ChartRenderHeight.ToString(CultureInfo.InvariantCulture);
        }

        private void ApplyChartRenderFpsPreset(int fps)
        {
            ChartRenderFps = Mathf.Clamp(fps, MinChartRenderFps, MaxChartRenderFps);
            renderFpsText = ChartRenderFps.ToString(CultureInfo.InvariantCulture);
        }

        private static string DrawChoiceSettingRow(string label, string hint, string value, string[] values, string[] labels, string defaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(190));
            int current = IndexOf(values, value);
            if (current < 0)
            {
                current = IndexOf(values, defaultValue);
            }

            current = Mathf.Max(0, current);
            int columns = Mathf.Clamp(labels.Length, 1, 3);
            int next = GUILayout.SelectionGrid(current, labels, columns, GUILayout.MinWidth(280), GUILayout.ExpandWidth(true));
            if (next >= 0 && next < values.Length)
            {
                value = values[next];
            }

            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                value = defaultValue;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return value;
        }

        private static bool DrawToggleSettingRow(string label, string hint, bool value, bool defaultValue)
        {
            GUILayout.BeginHorizontal();
            value = GUILayout.Toggle(value, label, toggleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Text("chartRenderReset"), GUILayout.Width(64)))
            {
                value = defaultValue;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(hint, hintStyle);
            return value;
        }

        private static int IndexOf(string[] values, string value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] GetEncoderModeLabels()
        {
            return new[]
            {
                Text("chartRenderEncoderAutoBalanced"),
                Text("chartRenderEncoderFastest"),
                Text("chartRenderEncoderBalanced"),
                Text("chartRenderEncoderQuality"),
                Text("chartRenderEncoderCpuCompatibility"),
                Text("chartRenderEncoderCustom")
            };
        }

        private static string[] GetCaptureFormatLabels()
        {
            return new[]
            {
                Text("chartRenderCaptureRgba"),
                Text("chartRenderCaptureBgra")
            };
        }

        private static string[] GetPreviewModeLabels()
        {
            return new[]
            {
                Text("chartRenderPreviewFull"),
                Text("chartRenderPreviewDim"),
                Text("chartRenderPreviewMinimal")
            };
        }

        private static string[] GetAudioFormatLabels()
        {
            return new[]
            {
                Text("chartRenderAudioFormatAac"),
                Text("chartRenderAudioFormatFlac"),
                Text("chartRenderAudioFormatAlac")
            };
        }

        private string GetBitrateHint()
        {
            int recommended = ChartRenderBitratePresets.GetRecommendedBitrateMbps(ChartRenderWidth, ChartRenderHeight, ChartRenderFps);
            int effective = ChartRenderBitratePresets.ResolveTargetBitrateMbps(ChartRenderBitrateMbps, ChartRenderWidth, ChartRenderHeight, ChartRenderFps);
            return Text("chartRenderBitrateMbpsHint")
                + " "
                + string.Format(CultureInfo.InvariantCulture, Text("chartRenderBitrateRecommendedHint"), recommended, effective);
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
            NormalizeChartRenderSettings();
            Save(this, modEntry);
        }

        public void EnsureDefaults(UnityModManager.ModEntry modEntry)
        {
            if (string.IsNullOrWhiteSpace(ChartRenderWorkspaceDirectory))
            {
                ChartRenderWorkspaceDirectory = GetDefaultWorkspaceDirectory(modEntry);
            }

            if (string.IsNullOrWhiteSpace(ChartRenderExportDirectory))
            {
                ChartRenderExportDirectory = GetDefaultExportDirectory(modEntry);
            }

            NormalizeChartRenderSettings();
        }

        private void NormalizeChartRenderSettings()
        {
            ChartRenderWidth = MakeEven(Mathf.Clamp(ChartRenderWidth, MinChartRenderSize, MaxChartRenderWidth));
            ChartRenderHeight = MakeEven(Mathf.Clamp(ChartRenderHeight, MinChartRenderSize, MaxChartRenderHeight));
            ChartRenderFps = Mathf.Clamp(ChartRenderFps, MinChartRenderFps, MaxChartRenderFps);
            ChartRenderCrf = Mathf.Clamp(ChartRenderCrf, MinChartRenderCrf, MaxChartRenderCrf);
            ChartRenderBitrateMbps = Mathf.Clamp(ChartRenderBitrateMbps, ChartRenderBitratePresets.AutoBitrateMbps, ChartRenderBitratePresets.MaxBitrateMbps);
            ChartRenderPreset = string.IsNullOrWhiteSpace(ChartRenderPreset)
                ? DefaultChartRenderPreset
                : ChartRenderPreset.Trim();
            ChartRenderEncoderMode = ChartRenderOptionValues.NormalizeEncoderMode(ChartRenderEncoderMode);
            ChartRenderCaptureFormat = ChartRenderOptionValues.NormalizeCaptureFormat(ChartRenderCaptureFormat);
            ChartRenderPreviewMode = ChartRenderOptionValues.NormalizePreviewMode(ChartRenderPreviewMode);
            ChartRenderAudioFormat = ChartRenderOptionValues.NormalizeAudioFormat(ChartRenderAudioFormat);
            ChartRenderCompletionTailSeconds = Mathf.Max(0f, ChartRenderCompletionTailSeconds);
            ChartRenderAudioSyncOffsetMs = Mathf.Clamp(ChartRenderAudioSyncOffsetMs, MinChartRenderAudioSyncOffsetMs, MaxChartRenderAudioSyncOffsetMs);
        }

        private static int MakeEven(int value)
        {
            return value % 2 == 0 ? value : value + 1;
        }

        private void ResetChartRenderDefaults(UnityModManager.ModEntry modEntry)
        {
            ChartRenderWorkspaceDirectory = GetDefaultWorkspaceDirectory(modEntry);
            ChartRenderExportDirectory = GetDefaultExportDirectory(modEntry);
            ChartRenderWidth = DefaultChartRenderWidth;
            ChartRenderHeight = DefaultChartRenderHeight;
            ChartRenderFps = DefaultChartRenderFps;
            ChartRenderCrf = DefaultChartRenderCrf;
            ChartRenderBitrateMbps = DefaultChartRenderBitrateMbps;
            ChartRenderPreset = DefaultChartRenderPreset;
            ChartRenderEncoderMode = DefaultChartRenderEncoderMode;
            ChartRenderCaptureFormat = DefaultChartRenderCaptureFormat;
            ChartRenderPreviewMode = DefaultChartRenderPreviewMode;
            ChartRenderAudioFormat = DefaultChartRenderAudioFormat;
            ChartRenderCompletionTailSeconds = DefaultChartRenderCompletionTailSeconds;
            ChartRenderAudioSyncOffsetMs = DefaultChartRenderAudioSyncOffsetMs;
            ChartRenderShowHitJudgments = DefaultChartRenderShowHitJudgments;
            ChartRenderUseSelectedRange = DefaultChartRenderUseSelectedRange;
            ChartRenderAdvancedSettingsExpanded = DefaultChartRenderAdvancedSettingsExpanded;
            SyncChartRenderTextFields();
        }

        private void SyncChartRenderTextFields()
        {
            renderWidthText = ChartRenderWidth.ToString(CultureInfo.InvariantCulture);
            renderHeightText = ChartRenderHeight.ToString(CultureInfo.InvariantCulture);
            renderFpsText = ChartRenderFps.ToString(CultureInfo.InvariantCulture);
            renderCrfText = ChartRenderCrf.ToString(CultureInfo.InvariantCulture);
            renderBitrateText = ChartRenderBitrateMbps.ToString(CultureInfo.InvariantCulture);
            renderPresetText = ChartRenderPreset;
            renderTailSecondsText = FormatFloat(ChartRenderCompletionTailSeconds);
            renderAudioSyncOffsetText = FormatFloat(ChartRenderAudioSyncOffsetMs);
        }

        private static string GetDefaultWorkspaceDirectory(UnityModManager.ModEntry modEntry)
        {
            return Path.Combine(modEntry.Path, "Workspace");
        }

        private static string GetDefaultExportDirectory(UnityModManager.ModEntry modEntry)
        {
            string videos = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos);
            return string.IsNullOrWhiteSpace(videos)
                ? Path.Combine(GetDefaultWorkspaceDirectory(modEntry), "Exports")
                : Path.Combine(videos, "ADOFAI Renders");
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

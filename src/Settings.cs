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

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label(Text("title"));
            EnableNumericDrag = GUILayout.Toggle(EnableNumericDrag, Text("enableNumericDrag"));
            EnableCameraRelativeDecorationDragFix = GUILayout.Toggle(EnableCameraRelativeDecorationDragFix, Text("fixCameraRelativeDecorationDrag"));
            EnableDecorationPivotFix = GUILayout.Toggle(EnableDecorationPivotFix, Text("fixDecorationPivot"));
            EnableVideoBackgroundSyncFix = GUILayout.Toggle(EnableVideoBackgroundSyncFix, Text("fixVideoBackgroundSync"));
            PersistEditorPreferences = GUILayout.Toggle(PersistEditorPreferences, Text("persistEditorPreferences"));
            ShowEditorOverlay = GUILayout.Toggle(ShowEditorOverlay, Text("showEditorOverlay"));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("decorationMoveSnapStep"), GUILayout.Width(170));
            if (float.TryParse(GUILayout.TextField(DecorationMoveSnapStep.ToString("0.###"), GUILayout.Width(100)), out float snapStep))
            {
                DecorationMoveSnapStep = Mathf.Max(0f, snapStep);
            }
            GUILayout.Label(Text("zeroDisables"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("floatStepPerPixel"), GUILayout.Width(170));
            if (float.TryParse(GUILayout.TextField(FloatStepPerPixel.ToString("0.###"), GUILayout.Width(100)), out float floatStep))
            {
                FloatStepPerPixel = Mathf.Max(0.0001f, floatStep);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("intStepPerPixel"), GUILayout.Width(170));
            if (float.TryParse(GUILayout.TextField(IntStepPerPixel.ToString("0.###"), GUILayout.Width(100)), out float intStep))
            {
                IntStepPerPixel = Mathf.Max(0.0001f, intStep);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("maxFloatDecimals"), GUILayout.Width(170));
            if (int.TryParse(GUILayout.TextField(MaxFloatingPoints.ToString(), GUILayout.Width(100)), out int decimals))
            {
                MaxFloatingPoints = Mathf.Clamp(decimals, 0, 8);
            }
            GUILayout.EndHorizontal();
        }

        public static string Text(string key)
        {
            bool chinese = RDString.language == SystemLanguage.Chinese
                || RDString.language == SystemLanguage.ChineseSimplified
                || RDString.language == SystemLanguage.ChineseTraditional;

            if (chinese)
            {
                switch (key)
                {
                    case "title":
                        return "ADOFAI 编辑器优化";
                    case "overlayTitle":
                        return "Editor Tweaks";
                    case "decorationSection":
                        return "装饰";
                    case "numericSection":
                        return "数值拖动";
                    case "fixesSection":
                        return "修复";
                    case "enableNumericDrag":
                        return "启用数值输入框拖动调节";
                    case "fixCameraRelativeDecorationDrag":
                        return "修复镜头相对装饰拖动";
                    case "fixDecorationPivot":
                        return "修复镜头/视差装饰轴心显示";
                    case "fixVideoBackgroundSync":
                        return "修复中途播放时视频背景延迟";
                    case "persistEditorPreferences":
                        return "持久化官方编辑器偏好设置";
                    case "showEditorOverlay":
                        return "在编辑器内显示快捷设置浮窗";
                    case "decorationMoveSnapStep":
                        return "装饰移动吸附精度";
                    case "zeroDisables":
                        return "0 = 关闭";
                    case "floatStepPerPixel":
                        return "小数每像素步进";
                    case "intStepPerPixel":
                        return "整数每像素步进";
                    case "maxFloatDecimals":
                        return "小数最大位数";
                }
            }

            switch (key)
            {
                case "title":
                    return "ADOFAI Editor Tweaks";
                case "overlayTitle":
                    return "Editor Tweaks";
                case "decorationSection":
                    return "Decoration";
                case "numericSection":
                    return "Numeric drag";
                case "fixesSection":
                    return "Fixes";
                case "enableNumericDrag":
                    return "Enable numeric drag fields";
                case "fixCameraRelativeDecorationDrag":
                    return "Fix camera-relative decoration dragging";
                case "fixDecorationPivot":
                    return "Fix camera/parallax decoration pivot";
                case "fixVideoBackgroundSync":
                    return "Fix video background sync from checkpoints";
                case "persistEditorPreferences":
                    return "Persist official editor preferences";
                case "showEditorOverlay":
                    return "Show editor quick settings overlay";
                case "decorationMoveSnapStep":
                    return "Decoration move snap step";
                case "zeroDisables":
                    return "0 = off";
                case "floatStepPerPixel":
                    return "Float step per pixel";
                case "intStepPerPixel":
                    return "Int step per pixel";
                case "maxFloatDecimals":
                    return "Max float decimals";
                default:
                    return key;
            }
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
    }
}

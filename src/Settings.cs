using UnityModManagerNet;
using UnityEngine;

namespace ADOFAI.EditorTweaks
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableNumericDrag = true;

        public bool EnableCameraRelativeDecorationDragFix = true;

        public bool PersistEditorPreferences = true;

        public float FloatStepPerPixel = 0.1f;

        public float IntStepPerPixel = 1f;

        public int MaxFloatingPoints = 3;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label(Text("title"));
            EnableNumericDrag = GUILayout.Toggle(EnableNumericDrag, Text("enableNumericDrag"));
            EnableCameraRelativeDecorationDragFix = GUILayout.Toggle(EnableCameraRelativeDecorationDragFix, Text("fixCameraRelativeDecorationDrag"));
            PersistEditorPreferences = GUILayout.Toggle(PersistEditorPreferences, Text("persistEditorPreferences"));

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

        private static string Text(string key)
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
                    case "enableNumericDrag":
                        return "启用数值输入框拖动调节";
                    case "fixCameraRelativeDecorationDrag":
                        return "修复镜头相对装饰拖动";
                    case "persistEditorPreferences":
                        return "持久化官方编辑器偏好设置";
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
                case "enableNumericDrag":
                    return "Enable numeric drag fields";
                case "fixCameraRelativeDecorationDrag":
                    return "Fix camera-relative decoration dragging";
                case "persistEditorPreferences":
                    return "Persist official editor preferences";
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

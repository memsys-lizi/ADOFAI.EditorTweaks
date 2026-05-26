using UnityModManagerNet;
using UnityEngine;

namespace ADOFAI.EditorTweaks
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool EnableNumericDrag = true;

        public float FloatStepPerPixel = 0.1f;

        public float IntStepPerPixel = 1f;

        public int MaxFloatingPoints = 3;

        public bool LiveApplyWhileDragging = false;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("ADOFAI.EditorTweaks");
            EnableNumericDrag = GUILayout.Toggle(EnableNumericDrag, "Enable numeric drag fields");
            LiveApplyWhileDragging = GUILayout.Toggle(LiveApplyWhileDragging, "Apply while dragging (experimental)");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Float step per pixel", GUILayout.Width(170));
            if (float.TryParse(GUILayout.TextField(FloatStepPerPixel.ToString("0.###"), GUILayout.Width(100)), out float floatStep))
            {
                FloatStepPerPixel = Mathf.Max(0.0001f, floatStep);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Int step per pixel", GUILayout.Width(170));
            if (float.TryParse(GUILayout.TextField(IntStepPerPixel.ToString("0.###"), GUILayout.Width(100)), out float intStep))
            {
                IntStepPerPixel = Mathf.Max(0.0001f, intStep);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max float decimals", GUILayout.Width(170));
            if (int.TryParse(GUILayout.TextField(MaxFloatingPoints.ToString(), GUILayout.Width(100)), out int decimals))
            {
                MaxFloatingPoints = Mathf.Clamp(decimals, 0, 8);
            }
            GUILayout.EndHorizontal();
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

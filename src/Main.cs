using System.IO;
using System.Reflection;
using ADOFAI.EditorTweaks.Features.EditorOverlay;
using HarmonyLib;
using UnityModManagerNet;

namespace ADOFAI.EditorTweaks
{
    public static class Main
    {
        public static UnityModManager.ModEntry? Mod { get; private set; }

        public static Harmony? Harmony { get; private set; }

        public static Settings Settings { get; private set; } = null!;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Localization.Load(modEntry);
            Settings = Settings.Load(modEntry);
            Settings.EnsureDefaults(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = Settings.OnGUI;
            modEntry.OnSaveGUI = Settings.OnSaveGUI;

            Harmony = new Harmony(modEntry.Info.Id);

            if (!Settings.HasShownReadme)
            {
                Settings.HasShownReadme = true;
                Settings.Save(modEntry);
                OpenReadme(modEntry);
            }

            modEntry.Logger.Log("ADOFAI.EditorTweaks loaded.");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value)
            {
                modEntry.Logger.Log("ADOFAI.EditorTweaks enabled.");
                Harmony?.PatchAll(Assembly.GetExecutingAssembly());
                EditorTweaksOverlayWindow.Ensure();
            }
            else
            {
                modEntry.Logger.Log("ADOFAI.EditorTweaks disabled.");
                Harmony?.UnpatchAll(modEntry.Info.Id);
                EditorTweaksOverlayWindow.Destroy();
            }

            return true;
        }

        public static void Log(string message)
        {
            Mod?.Logger.Log(message);
        }

        public static void OpenReadme(UnityModManager.ModEntry modEntry)
        {
            string readmePath = Path.Combine(modEntry.Path, "Resources", "README.html");
            if (File.Exists(readmePath))
            {
                System.Diagnostics.Process.Start(readmePath);
            }
            else
            {
                modEntry.Logger.Log("README.html not found at: " + readmePath);
            }
        }
    }
}

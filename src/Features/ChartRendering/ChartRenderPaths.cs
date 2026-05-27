using System;
using System.IO;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderPaths
    {
        public static string GetFfmpegPath()
        {
            return Main.Mod == null ? string.Empty : Path.Combine(Main.Mod.Path, "Tools", "ffmpeg.exe");
        }

        public static string GetWorkspaceDirectory(Settings settings)
        {
            return ExpandPath(settings.ChartRenderWorkspaceDirectory);
        }

        public static string GetExportDirectory(Settings settings)
        {
            return ExpandPath(settings.ChartRenderExportDirectory);
        }

        public static string MakeSafeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = "ADOFAI_Render";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }

            return raw.Trim();
        }

        private static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded);
        }
    }
}

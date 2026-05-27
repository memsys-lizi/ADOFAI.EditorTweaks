using System;
using HarmonyLib;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderClock
    {
        private static double forcedSongPosition;

        public static bool IsActive { get; private set; }

        public static void Begin()
        {
            forcedSongPosition = 0.0;
            IsActive = true;
        }

        public static void End()
        {
            IsActive = false;
            forcedSongPosition = 0.0;
        }

        public static void SetOutputTime(double seconds, float pitch)
        {
            forcedSongPosition = Math.Max(0.0, seconds * Math.Max(0.01f, pitch));
        }

        public static bool TryGetSongPosition(out double value)
        {
            value = forcedSongPosition;
            return IsActive;
        }
    }

    [HarmonyPatch(typeof(scrConductor), "set_songposition_minusi")]
    internal static class ChartRenderConductorSongPositionPatch
    {
        private static void Prefix(ref double value)
        {
            if (ChartRenderClock.TryGetSongPosition(out double forced))
            {
                value = forced;
            }
        }
    }
}

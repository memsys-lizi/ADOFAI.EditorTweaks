using HarmonyLib;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderVisualClock
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

        public static void SetFrameTime(double seconds, float pitch)
        {
            forcedSongPosition = seconds * pitch;
        }

        public static bool TryGetSongPosition(out double songPosition)
        {
            songPosition = forcedSongPosition;
            return IsActive && ChartRenderSession.IsRendering;
        }
    }

    [HarmonyPatch(typeof(scrConductor), "set_songposition_minusi")]
    internal static class ChartRenderConductorSongPositionPatch
    {
        private static void Prefix(ref double value)
        {
            if (ChartRenderVisualClock.TryGetSongPosition(out double songPosition))
            {
                value = songPosition;
            }
        }
    }
}

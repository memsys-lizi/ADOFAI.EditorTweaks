using HarmonyLib;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    [HarmonyPatch(typeof(scrController), nameof(scrController.ShowHitText), typeof(HitMargin), typeof(Vector3), typeof(float))]
    internal static class ChartRenderJudgmentPatches
    {
        private static bool Prefix()
        {
            return !ChartRenderSession.IsRendering || Main.Settings.ChartRenderShowHitJudgments;
        }
    }
}

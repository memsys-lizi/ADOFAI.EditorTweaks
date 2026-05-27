using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    [HarmonyPatch(typeof(scrSfx), nameof(scrSfx.PlaySfx), typeof(AudioClip), typeof(MixerGroup), typeof(float), typeof(float), typeof(float))]
    internal static class ChartRenderAudioPatches
    {
        private static bool Prefix(AudioClip clip, MixerGroup group, ref AudioClip __result)
        {
            if (!ChartRenderSession.IsRendering || group != MixerGroup.InterfaceParent)
            {
                return true;
            }

            __result = clip;
            return false;
        }
    }

    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.Play), typeof(string), typeof(double), typeof(AudioMixerGroup), typeof(float), typeof(int))]
    internal static class ChartRenderAudioManagerPlayPatch
    {
        private static void Prefix(string snd, double time, float volume)
        {
            if (!ChartRenderSession.IsRendering)
            {
                return;
            }

            ChartRenderAudioRecorder.RecordScheduledSound(snd, time, volume);
        }
    }

    [HarmonyPatch(typeof(scrConductor), nameof(scrConductor.PlayWithEndTime), typeof(string), typeof(double), typeof(double), typeof(float), typeof(int))]
    internal static class ChartRenderHoldLoopPatch
    {
        private static void Prefix(string snd, double time, double endTime, float volume)
        {
            if (!ChartRenderSession.IsRendering)
            {
                return;
            }

            ChartRenderAudioRecorder.RecordScheduledSound(snd, time, volume, endTime);
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderAutoPlayer
    {
        private const int MaxHitsPerFrame = 16;

        public static void CatchUp()
        {
            if (!ChartRenderSession.IsRendering || !ChartRenderSession.IsAutoPlaybackReady || !ChartRenderVisualClock.IsActive)
            {
                return;
            }

            scrConductor conductor = ADOBase.conductor;
            scrController controller = ADOBase.controller;
            if (conductor == null
                || controller == null
                || controller.paused
                || controller.state != States.PlayerControl
                || controller.playerManager == null)
            {
                return;
            }

            List<scrPlayer> players = controller.playerManager.GetActivePlayers();
            foreach (scrPlayer player in players)
            {
                CatchUpPlayer(player, conductor, controller);
            }
        }

        private static void CatchUpPlayer(scrPlayer player, scrConductor conductor, scrController controller)
        {
            int hitsThisFrame = 0;
            while (hitsThisFrame < MaxHitsPerFrame && ShouldHitNow(player, conductor))
            {
                scrFloor beforeFloor = player.currFloor;
                double songPosition = conductor.songposition_minusi;
                bool success = HitPerfect(player, controller);
                hitsThisFrame++;
                ChartRenderDiagnostics.LogAutoHit(player, beforeFloor, songPosition, success, hitsThisFrame);

                if (!success)
                {
                    break;
                }
            }

            if (hitsThisFrame >= MaxHitsPerFrame)
            {
                ChartRenderDiagnostics.Log("AUTO_HIT_GUARD_REACHED player=" + player.playerID + " floor=" + (player.currFloor == null ? -1 : player.currFloor.seqID));
            }
        }

        private static bool ShouldHitNow(scrPlayer player, scrConductor conductor)
        {
            if (player == null || !player.alive || player.currFloor == null)
            {
                return false;
            }

            scrFloor current = player.currFloor;
            if (current.nextfloor == null || ADOBase.lm == null || ADOBase.lm.listFloors == null || current.seqID >= ADOBase.lm.listFloors.Count - 1)
            {
                return false;
            }

            scrPlanet? planet = player.planetarySystem?.chosenPlanet;
            planet?.Update_RefreshAngles();

            double tolerance = Math.Max(0.0001, conductor.crotchetAtStart * 0.001);
            return conductor.songposition_minusi + tolerance >= current.nextfloor.entryTime;
        }

        private static bool HitPerfect(scrPlayer player, scrController controller)
        {
            scrFloor current = player.currFloor;
            scrPlanet planet = player.planetarySystem.chosenPlanet;
            bool oldAuto = RDC.auto;

            try
            {
                RDC.auto = true;
                controller.responsive = true;
                controller.paused = false;
                controller.multipressPenalty = false;
                controller.multipressAndHasPressedFirstPress = false;
                player.consecMultipressCounter = 0;
                player.keyTimes.Clear();

                if (current != null && !current.midSpin)
                {
                    planet.angle = planet.targetExitAngle;
                    planet.cachedAngle = planet.targetExitAngle;
                }

                if (current != null && current.holdLength > -1 && current.holdRenderer != null)
                {
                    current.holdRenderer.Hit();
                }

                return player.Hit(isAuto: true);
            }
            finally
            {
                RDC.auto = oldAuto;
            }
        }
    }

    [HarmonyPatch(typeof(scrConductor), "Update")]
    internal static class ChartRenderConductorUpdatePatch
    {
        private static void Postfix()
        {
            ChartRenderAutoPlayer.CatchUp();
        }
    }

    [HarmonyPatch(typeof(AsyncInputUtils), nameof(AsyncInputUtils.AdjustAngle), typeof(scrPlayer), typeof(ulong))]
    internal static class ChartRenderAsyncAnglePatch
    {
        private static bool Prefix()
        {
            if (!ChartRenderSession.IsRendering)
            {
                return true;
            }

            ChartRenderDiagnostics.RecordAsyncAdjustSuppressed();
            return false;
        }
    }
}

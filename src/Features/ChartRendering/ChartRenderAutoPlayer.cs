using HarmonyLib;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.ChartRendering
{
    internal static class ChartRenderAutoPlayer
    {
        public static void CatchUp()
        {
            if (!ChartRenderClock.IsActive || !ChartRenderSession.IsRendering)
            {
                return;
            }

            scrController controller = scrController.instance;
            scrConductor conductor = ADOBase.conductor;
            if (controller == null
                || conductor == null
                || controller.paused
                || controller.currFloor == null
                || controller.chosenPlanet == null
                || controller.currentState != States.PlayerControl)
            {
                return;
            }

            int guard = 0;
            while (guard++ < 16 && ShouldHitNow(controller, conductor))
            {
                HitPerfect(controller);
            }
        }

        private static bool ShouldHitNow(scrController controller, scrConductor conductor)
        {
            scrFloor current = controller.currFloor;
            if (current == null || current.nextfloor == null)
            {
                return false;
            }

            if (ADOBase.lm == null || ADOBase.lm.listFloors == null || current.seqID >= ADOBase.lm.listFloors.Count - 1)
            {
                return false;
            }

            double targetTime = current.nextfloor.entryTime;
            double tolerance = System.Math.Max(0.0001, conductor.crotchetAtStart * 0.001);
            return conductor.songposition_minusi + tolerance >= targetTime;
        }

        private static void HitPerfect(scrController controller)
        {
            bool oldAuto = RDC.auto;
            try
            {
                RDC.auto = true;
                controller.responsive = true;
                controller.paused = false;
                controller.consecMultipressCounter = 0;
                controller.multipressPenalty = false;
                controller.keyTimes.Clear();

                scrPlanet planet = controller.chosenPlanet;
                if (planet != null && controller.currFloor != null && !controller.currFloor.midSpin)
                {
                    planet.angle = planet.targetExitAngle;
                    planet.cachedAngle = planet.targetExitAngle;
                }

                if (controller.currFloor != null && controller.currFloor.holdLength > -1 && controller.currFloor.holdRenderer != null)
                {
                    controller.currFloor.holdRenderer.Hit();
                }

                controller.Hit(isAuto: true);
                while (controller.currFloor != null && controller.currFloor.midSpin && controller.currFloor.nextfloor != null)
                {
                    controller.Hit(isAuto: true);
                }
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
}

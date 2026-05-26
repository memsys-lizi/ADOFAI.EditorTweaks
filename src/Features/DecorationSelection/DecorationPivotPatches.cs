using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.DecorationSelection
{
    internal static class DecorationPivotPatches
    {
        [HarmonyPatch(typeof(DecorationPivot), nameof(DecorationPivot.UpdatePivotCrossImage))]
        private static class DecorationPivotUpdatePivotCrossImagePatch
        {
            private static bool Prefix(DecorationPivot __instance, bool enable)
            {
                if (!Main.Settings.EnableDecorationPivotFix)
                {
                    return true;
                }

                bool hide = ADOBase.editor.SelectionDecorationIsEmpty() || !ADOBase.editor.SelectionDecorationIsSingle();
                if (!hide)
                {
                    scrDecoration decoration = scrDecorationManager.GetDecoration(ADOBase.editor.selectedDecorations[0]);
                    if (decoration != null)
                    {
                        SetPivotCrossPosition(__instance, decoration);
                    }
                }

                if (__instance.gizmoTransform != null)
                {
                    __instance.gizmoTransform.gameObject.SetActive(enable && !hide);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(scrDecoration), "UpdateScreenClamp")]
        private static class ScrDecorationUpdateScreenClampPatch
        {
            private static void Postfix(scrDecoration __instance)
            {
                if (!Main.Settings.EnableDecorationPivotFix || __instance == null || __instance.parallax == null)
                {
                    return;
                }

                if (!IsScreenRelative(__instance.placementType))
                {
                    return;
                }

                Vector2 pivot = __instance.pivotPosVec;
                if (__instance.placementType == DecPlacementType.CameraAspect && Screen.width != 0)
                {
                    pivot.x *= (float)Screen.height / Screen.width;
                }

                __instance.parallax.clampToScreen = true;
                __instance.parallax.screenRelativePos = pivot / 20f + new Vector2(0.5f, 0.5f);
            }
        }

        [HarmonyPatch(typeof(scrParallax), nameof(scrParallax.SetTrans))]
        private static class ScrParallaxSetTransPatch
        {
            private static void Postfix(scrParallax __instance)
            {
                if (!Main.Settings.EnableDecorationPivotFix || __instance == null || __instance.decoration == null)
                {
                    return;
                }

                scnEditor editor = ADOBase.editor;
                if (editor == null
                    || editor.decPivot == null
                    || !editor.SelectionDecorationIsSingle()
                    || editor.selectedDecorations[0] != __instance.decoration.sourceLevelEvent)
                {
                    return;
                }

                SetPivotCrossPosition(editor.decPivot, __instance.decoration);
            }
        }

        private static bool IsScreenRelative(DecPlacementType placementType)
        {
            return placementType == DecPlacementType.Camera || placementType == DecPlacementType.CameraAspect;
        }

        private static void SetPivotCrossPosition(DecorationPivot pivot, scrDecoration decoration)
        {
            if (pivot.gizmoTransform == null)
            {
                return;
            }

            pivot.gizmoTransform.transform.position = decoration.transform.position;
        }
    }
}

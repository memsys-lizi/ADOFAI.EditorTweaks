using ADOFAI.Editor.Components;
using ADOFAI.LevelEditor.Controls;
using TMPro;
using UnityEngine;

namespace ADOFAI.EditorTweaks.Features.NumericDrag
{
    internal static class NumericDragFeature
    {
        public static void Attach(PropertyControl_Text control)
        {
            if (!Main.Settings.EnableNumericDrag || control == null || control.inputField == null || control.propertyInfo == null)
            {
                return;
            }

            PropertyType type = control.propertyInfo.type;
            if (type != PropertyType.Int && type != PropertyType.Float && type != PropertyType.Tile)
            {
                return;
            }

            bool integer = type == PropertyType.Int || type == PropertyType.Tile;
            Attach(control.inputField, control.propertyInfo, () => control.inputField.onEndEdit.Invoke(control.inputField.text), integer);
        }

        public static void Attach(PropertyControl_Vector2 control)
        {
            if (!Main.Settings.EnableNumericDrag || control == null || control.propertyInfo == null)
            {
                return;
            }

            Attach(
                control.inputX,
                control.propertyInfo,
                () => control.inputX.onEndEdit.Invoke(control.inputX.text),
                integer: false,
                min: control.propertyInfo.minVec.x,
                max: control.propertyInfo.maxVec.x);
            Attach(
                control.inputY,
                control.propertyInfo,
                () => control.inputY.onEndEdit.Invoke(control.inputY.text),
                integer: false,
                min: control.propertyInfo.minVec.y,
                max: control.propertyInfo.maxVec.y);
        }

        private static void Attach(TMP_InputField field, PropertyInfo propertyInfo, System.Action commit, bool integer, float? min = null, float? max = null)
        {
            if (field == null || field.gameObject.GetComponent<EditorTweaksNumericDragMarker>() != null)
            {
                return;
            }

            EditorTweaksNumericDragMarker marker = field.gameObject.AddComponent<EditorTweaksNumericDragMarker>();
            marker.Commit = commit;

            DraggableNumberInputField drag = field.gameObject.AddComponent<DraggableNumberInputField>();
            drag.field = field;
            drag.arrows = new GameObject[0];
            drag.axis = DraggableNumberInputField.Axis.Horizontal;
            drag.clamp = !propertyInfo.ignoreRange;
            drag.stepPerPixel = integer ? Main.Settings.IntStepPerPixel : Main.Settings.FloatStepPerPixel;
            drag.maxFloatingPoints = integer ? 0 : Main.Settings.MaxFloatingPoints;

            if (integer)
            {
                drag.min = propertyInfo.type == PropertyType.Tile ? propertyInfo.int_min : GetIntMin(propertyInfo);
                drag.max = propertyInfo.type == PropertyType.Tile ? propertyInfo.int_max : GetIntMax(propertyInfo);
            }
            else
            {
                drag.min = min ?? propertyInfo.float_min;
                drag.max = max ?? propertyInfo.float_max;
            }

            if (Main.Settings.LiveApplyWhileDragging)
            {
                drag.onDrag.AddListener(marker.CommitNow);
            }
        }

        private static int GetIntMin(PropertyInfo propertyInfo)
        {
            return propertyInfo.name == "floor" ? 0 : propertyInfo.int_min;
        }

        private static int GetIntMax(PropertyInfo propertyInfo)
        {
            if (propertyInfo.name != "floor" || scnGame.instance == null || scnGame.instance.levelMaker == null)
            {
                return propertyInfo.int_max;
            }

            return scnGame.instance.levelMaker.listFloors.Count - 1;
        }
    }
}

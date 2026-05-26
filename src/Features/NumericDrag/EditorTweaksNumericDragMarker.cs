using UnityEngine;
using UnityEngine.EventSystems;

namespace ADOFAI.EditorTweaks.Features.NumericDrag
{
    internal sealed class EditorTweaksNumericDragMarker : MonoBehaviour
    {
        public System.Action? Commit;

        public static bool IsDragButton(PointerEventData eventData)
        {
            return eventData.button == PointerEventData.InputButton.Right;
        }

        public void CommitAfterDrag()
        {
            CommitNow();
        }

        public void CommitNow()
        {
            Commit?.Invoke();
        }
    }
}

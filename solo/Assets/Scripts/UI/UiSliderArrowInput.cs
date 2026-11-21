using UnityEngine;
using UnityEngine.EventSystems;

namespace Wizard.Solo.UI
{
    /// <summary>◀ ▶ 클릭.</summary>
    public sealed class UiSliderArrowInput : MonoBehaviour, IPointerClickHandler
    {
        public UiSlider Owner;
        public int Direction;
        public void OnPointerClick(PointerEventData e)
        {
            if (Owner == null || e.button != PointerEventData.InputButton.Left) return;
            Owner.Group?.SetCursor(Owner, false);
            Owner.Nudge(Direction);
        }
    }
}

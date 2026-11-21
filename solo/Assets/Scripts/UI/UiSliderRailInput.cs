using UnityEngine;
using UnityEngine.EventSystems;

namespace Wizard.Solo.UI
{
    /// <summary>막대 위 끌기·클릭을 슬라이더로 넘긴다(UiKit.Slider가 막대에 붙인다).</summary>
    public sealed class UiSliderRailInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public UiSlider Owner;
        public void OnPointerDown(PointerEventData e) { if (Owner != null && e.button == PointerEventData.InputButton.Left) Owner.SetFromScreenPoint(e.position, e.pressEventCamera); }
        public void OnDrag(PointerEventData e) { if (Owner != null && e.button == PointerEventData.InputButton.Left) Owner.SetFromScreenPoint(e.position, e.pressEventCamera); }
    }
}

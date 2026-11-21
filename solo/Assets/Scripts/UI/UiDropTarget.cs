using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 끌어다 놓기의 받는 쪽(UiKit.MakeDropTarget). 끌려 온 것이 위에 있으면 HoverChanged(true)(받을 수 있을 때만),
    /// 놓으면 CanAccept(값)이 참일 때 Dropped(값). 이 GameObject에 마우스를 받는 Graphic이 있어야 한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<object, bool> CanAccept;
        public Action<object> Dropped;
        public Action<bool> HoverChanged;

        static UiDragSource SourceOf(PointerEventData e) =>
            e != null && e.pointerDrag != null ? e.pointerDrag.GetComponent<UiDragSource>() : null;

        bool Accepts(UiDragSource src) => src != null && src.Dragging && (CanAccept == null || CanAccept(src.Payload));

        public void OnPointerEnter(PointerEventData e)
        {
            var src = SourceOf(e);
            if (Accepts(src)) HoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            var src = SourceOf(e);
            if (src != null) HoverChanged?.Invoke(false);
        }

        public void OnDrop(PointerEventData e)
        {
            var src = SourceOf(e);
            HoverChanged?.Invoke(false);
            if (!Accepts(src)) return;
            src.Dropped = true;
            try { Dropped?.Invoke(src.Payload); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}

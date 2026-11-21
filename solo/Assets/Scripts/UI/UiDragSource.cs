using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 끌어다 놓기의 끄는 쪽(UiKit.MakeDraggable). uGUI의 IBeginDragHandler·IDragHandler·IEndDragHandler(InputSystemUIInputModule이 보낸다).
    /// 끄는 동안 끌기 그림자(K-72: 원래 그림의 60% 불투명 사본, 코드로 만든다)가 포인터를 따라간다. 그림자는 마우스를 막지 않는다.
    /// 놓은 곳의 UiDropTarget이 받으면 Ended(true), 아니면 Ended(false). 키보드만으로도 같은 일을 할 수 있게 화면이 J/K 길을 따로 둔다(08 1.2절).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiDragSource : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>놓는 곳에 넘길 값(블록 id, 칸 번호 등).</summary>
        public object Payload;
        /// <summary>지금 끌 수 있는가(null이면 언제나).</summary>
        public Func<bool> CanDrag;
        public Action Began;
        /// <summary>끌기가 끝났다(true = 받은 곳이 있다).</summary>
        public Action<bool> Ended;
        /// <summary>그림자 그림(null이면 이 GameObject 아래 첫 Image의 그림).</summary>
        public Sprite ShadowSprite;
        public float ShadowAlpha = 0.6f;

        RectTransform _shadow;
        RectTransform _shadowParent;
        bool _dragging;
        internal bool Dropped;

        public bool Dragging => _dragging;

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = true; }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (CanDrag != null && !CanDrag()) { e.pointerDrag = null; return; }
            _dragging = true;
            Dropped = false;
            CreateShadow();
            Move(e);
            try { Began?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
        }

        public void OnDrag(PointerEventData e) { if (_dragging) Move(e); }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_shadow != null) Destroy(_shadow.gameObject);
            _shadow = null;
            try { Ended?.Invoke(Dropped); } catch (Exception ex) { Debug.LogException(ex); }
        }

        void OnDisable()
        {
            if (_shadow != null) Destroy(_shadow.gameObject);
            _shadow = null;
            _dragging = false;
        }

        void CreateShadow()
        {
            // 그림자는 맨 위 캔버스(그 화면 캔버스의 루트)에 둔다.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            _shadowParent = canvas != null ? (RectTransform)canvas.transform : (RectTransform)transform.parent;
            var go = new GameObject("DragShadow", typeof(RectTransform), typeof(CanvasGroup), typeof(UImage));
            _shadow = (RectTransform)go.transform;
            _shadow.SetParent(_shadowParent, false);
            _shadow.SetAsLastSibling();
            var src = (RectTransform)transform;
            Vector2 size = src.rect.size;
            _shadow.anchorMin = _shadow.anchorMax = new Vector2(0.5f, 0.5f);
            _shadow.pivot = new Vector2(0.5f, 0.5f);
            _shadow.sizeDelta = size;
            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = ShadowAlpha;
            var img = go.GetComponent<UImage>();
            img.raycastTarget = false;
            Sprite s = ShadowSprite;
            if (s == null)
            {
                var imgs = GetComponentsInChildren<UImage>();
                for (int i = 0; i < imgs.Length && s == null; i++) if (imgs[i].sprite != null && imgs[i].enabled) s = imgs[i].sprite;
            }
            if (s != null) { img.sprite = s; img.preserveAspect = true; img.color = Color.white; }
            else img.color = UiTheme.WoodLight;
        }

        void Move(PointerEventData e)
        {
            if (_shadow == null || _shadowParent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_shadowParent, e.position, e.pressEventCamera, out var local))
                _shadow.localPosition = local;
        }
    }
}

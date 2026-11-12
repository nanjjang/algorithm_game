#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Unity.UI.Common
{
    // 판(자리 표시). 먹색 2px 바깥 테 + 바탕 + 윗변 1px 밝은 선(spec 14 4.2 ink·wood·wood_light).
    // 그림(K 부품의 9조각, V-09)이 들어오면 이 위젯의 겉만 바꾼다.
    public class UiPanel : UiWidget
    {
        public const int BorderPx = 2;      // spec 14 4.2 ink: 판 바깥 외곽선 2px
        public const int TopLinePx = 1;     // spec 14 4.2 wood_light: 판의 윗변 1px

        private readonly Image _border;
        private readonly Image _fill;
        private readonly Image _topLine;
        private readonly RectTransform _fillRect;
        private readonly RectTransform _topLineRect;

        public UiPanel(string name, UiWidget parent, PixelRect localRect, bool framed = true)
            : base(name, parent, localRect)
        {
            (_border, _fill, _topLine, _fillRect, _topLineRect) = Build(this, framed);
            Layout();
        }

        public UiPanel(string name, RectTransform parentTransform, PixelRect localRect, bool framed = true)
            : base(name, parentTransform, localRect)
        {
            (_border, _fill, _topLine, _fillRect, _topLineRect) = Build(this, framed);
            Layout();
        }

        public bool Framed { get; private set; }

        public Color32 FillColor => _fill.color;

        public Color32 BorderColor => _border.color;

        public Color32 TopLineColor => _topLine.color;

        public bool TopLineShown => _topLine.gameObject.activeSelf;

        // 판의 안쪽(테를 뺀 곳). 자식 위젯을 놓는 기준은 판 전체(LocalRect)의 왼쪽 위다
        public PixelRect InnerRect => PixelRect.FromSize(LocalRect.Width, LocalRect.Height).Inset(Framed ? BorderPx : 0);

        public void SetFill(Color32 color) => _fill.color = color;

        public void SetBorder(Color32 color) => _border.color = color;

        public void SetTopLine(Color32 color, bool shown = true)
        {
            _topLine.color = color;
            _topLine.gameObject.SetActive(shown);
        }

        public override void SetRect(PixelRect localRect)
        {
            base.SetRect(localRect);
            if (_fill != null)
            {
                Layout();
            }
        }

        private static (Image border, Image fill, Image topLine, RectTransform fillRect, RectTransform topLineRect) Build(UiPanel panel, bool framed)
        {
            panel.Framed = framed;
            Image border = UiObjects.AddImage(panel.GameObject, UiPalette.Ink);
            border.enabled = framed;
            RectTransform fillRect = UiObjects.CreateRect("Fill", panel.Transform);
            Image fill = UiObjects.AddImage(fillRect.gameObject, UiPalette.Wood);
            RectTransform topRect = UiObjects.CreateRect("TopLine", panel.Transform);
            Image top = UiObjects.AddImage(topRect.gameObject, UiPalette.WoodLight);
            topRect.gameObject.SetActive(framed);
            return (border, fill, top, fillRect, topRect);
        }

        private void Layout()
        {
            PixelRect inner = InnerRect;
            UiObjects.Place(_fillRect, inner);
            UiObjects.Place(_topLineRect, new PixelRect(inner.X, inner.Y, inner.Width, TopLinePx));
        }
    }
}

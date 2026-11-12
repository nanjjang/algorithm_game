#nullable enable
using UnityEngine;
using UnityEngine.UI;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 선택 괄호와 확정 체크의 자리 표시 도형(spec 14 4.3 "선택 괄호", spec 13 1.3). 그림(K-40·K-41, V-09)이 오기 전까지 사각형으로 그린다.
    //   - 대상의 네 모서리 바깥에 ㄱ자 넷. 선 굵기 4px, 바깥에 먹색 2px 테. 색은 Tone(선택 노랑 / 꺼진 것이면 회청).
    //   - 대상 가장자리에서 4px 바깥(프레임 0). 커서(Moving)는 0.5초 주기 2프레임으로 2px 벌어졌다 오므라든다(프레임 1 = 6px 바깥).
    //     어긋남은 Brackets.PulseOffset(MAN-ui frame_offsets [[-4,-4],[-6,-6]])이다. 확정(Still)은 프레임 0 에 멈춘다.
    //   - 팔 길이는 크기(작은 것 12, 보통 20, 큰 것 28px). 크기는 Interactable 의 그림 크기로 정한다(Brackets.SizeFor).
    //   - 확정 체크(K-41)는 오른쪽 위 모서리에 놓는다. 자리 표시는 먹색 테를 두른 분필색 네모다 [임시: 그림이 없다].
    // 어느 상태에서 무엇을 그릴지는 InteractableGroup.OverlayOf 가 정한다. 여기서는 받은 BracketOverlay 를 그리기만 한다.
    public sealed class UiBracket : UiWidget
    {
        public const int CheckSizePx = 12;      // [임시] K-41 자리 표시 한 변

        private const int Corners = 4;
        private const int BarsPerCorner = 2;

        private readonly RectTransform[] _outline = new RectTransform[Corners * BarsPerCorner];
        private readonly Image[] _bars = new Image[Corners * BarsPerCorner];
        private readonly RectTransform[] _barRects = new RectTransform[Corners * BarsPerCorner];
        private readonly RectTransform _checkOutline;
        private readonly RectTransform _check;
        private PixelSize _target;

        public UiBracket(UiWidget target)
            : base("Bracket", target, PixelRect.FromSize(target.LocalRect.Width, target.LocalRect.Height))
        {
            for (int i = 0; i < _bars.Length; i++)
            {
                _outline[i] = UiObjects.CreateRect("Outline" + i, Transform);
                UiObjects.AddImage(_outline[i].gameObject, UiPalette.Ink);
            }
            for (int i = 0; i < _bars.Length; i++)
            {
                _barRects[i] = UiObjects.CreateRect("Bar" + i, Transform);
                _bars[i] = UiObjects.AddImage(_barRects[i].gameObject, UiPalette.SelectYellow);
            }
            _checkOutline = UiObjects.CreateRect("CheckOutline", Transform);
            UiObjects.AddImage(_checkOutline.gameObject, UiPalette.Ink);
            _check = UiObjects.CreateRect("Check", Transform);
            UiObjects.AddImage(_check.gameObject, UiPalette.Chalk);
            _target = target.LocalRect.Size;
            Apply(new BracketOverlay(BracketMotion.None, BracketTone.Select, false, null, Brackets.SizeFor(_target.Width, _target.Height)), 0);
        }

        public BracketMotion Motion { get; private set; }

        public BracketTone Tone { get; private set; }

        public BracketSize Size { get; private set; }

        // 대상 가장자리에서 선의 안쪽 변까지의 거리(4 또는 6px)
        public int GapPx { get; private set; }

        public bool CornersShown { get; private set; }

        public bool CheckShown { get; private set; }

        public Color32 BarColor => _bars[0].color;

        // 대상 크기가 바뀌면 부른다
        public void SetTargetSize(PixelSize size)
        {
            _target = size;
            SetRect(PixelRect.FromSize(size.Width, size.Height));
        }

        // 왼쪽 위 모서리 ㄱ자의 가로 막대(대상 왼쪽 위 기준). 나머지 셋은 이것을 뒤집은 자리다
        public PixelRect TopLeftHorizontalBar
        {
            get
            {
                int arm = Brackets.MetricsOf(Size).ArmPx;
                int edge = GapPx + Brackets.LineWidthPx;
                return new PixelRect(-edge, -edge, arm, Brackets.LineWidthPx);
            }
        }

        public PixelRect TopLeftVerticalBar
        {
            get
            {
                int arm = Brackets.MetricsOf(Size).ArmPx;
                int edge = GapPx + Brackets.LineWidthPx;
                return new PixelRect(-edge, -edge, Brackets.LineWidthPx, arm);
            }
        }

        public PixelRect CheckRect => new PixelRect(_target.Width - CheckSizePx / 2, -CheckSizePx / 2, CheckSizePx, CheckSizePx);

        // pulseFrame: 커서 괄호의 지금 프레임(InteractableGroup.CursorPulseFrame). Moving 일 때만 본다
        public void Apply(in BracketOverlay overlay, int pulseFrame)
        {
            Motion = overlay.Motion;
            Tone = overlay.Tone;
            Size = overlay.Size;
            int frame = overlay.Motion == BracketMotion.Moving ? pulseFrame : 0;
            GapPx = -Brackets.PulseOffset(frame).X;
            CornersShown = overlay.HasBracket;
            CheckShown = overlay.ConfirmCheck;

            Color32 color = UiPalette.ColorOf(overlay.Tone);
            PixelRect h = TopLeftHorizontalBar;
            PixelRect v = TopLeftVerticalBar;
            for (int c = 0; c < Corners; c++)
            {
                bool right = c == 1 || c == 3;
                bool bottom = c == 2 || c == 3;
                PlaceBar(c * BarsPerCorner, Mirror(h, right, bottom), color);
                PlaceBar(c * BarsPerCorner + 1, Mirror(v, right, bottom), color);
            }
            for (int i = 0; i < _bars.Length; i++)
            {
                _outline[i].gameObject.SetActive(CornersShown);
                _barRects[i].gameObject.SetActive(CornersShown);
            }

            PixelRect check = CheckRect;
            UiObjects.Place(_checkOutline, check.Inset(-Brackets.OutlinePx));
            UiObjects.Place(_check, check);
            _checkOutline.gameObject.SetActive(CheckShown);
            _check.gameObject.SetActive(CheckShown);
        }

        // 왼쪽 위 모서리의 막대를 대상의 다른 모서리로 뒤집는다(MAN-ui flip_for_other_corners)
        public PixelRect Mirror(PixelRect topLeftBar, bool right, bool bottom)
        {
            int x = right ? _target.Width - topLeftBar.X - topLeftBar.Width : topLeftBar.X;
            int y = bottom ? _target.Height - topLeftBar.Y - topLeftBar.Height : topLeftBar.Y;
            return new PixelRect(x, y, topLeftBar.Width, topLeftBar.Height);
        }

        private void PlaceBar(int index, PixelRect bar, Color32 color)
        {
            UiObjects.Place(_barRects[index], bar);
            UiObjects.Place(_outline[index], bar.Inset(-Brackets.OutlinePx));
            _bars[index].color = color;
        }
    }
}

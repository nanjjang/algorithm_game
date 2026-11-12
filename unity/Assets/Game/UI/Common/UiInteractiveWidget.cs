#nullable enable
using System;
using UnityEngine;
using Wizard.ViewLogic.Common;

namespace Wizard.Unity.UI.Common
{
    // 누를 수 있는 위젯(버튼, 탭, 칸, 목록 줄)의 공통부. 상태는 ViewLogic 의 Interactable·InteractableGroup 에 있고
    // 위젯은 그것을 읽어 그리기만 한다(spec 13 1.3, spec 14 4.3, CONTRACTS 13: 뷰에는 규칙이 없다).
    //
    // 짜임: 뿌리(누름 판정 사각형) ─ Skin(판: 바탕·테·글) ─ Bracket(괄호·확정 체크) ─ Reason(꺼진 이유 한 줄)
    // 그림 상태(SkinOf)마다의 자리 표시 모습(spec 14 4.3 표):
    //   normal    나무 바탕, 먹색 테, 윗변 1px 나무 밝음
    //   hover     테가 나무 밝음으로 한 단계 밝아진다(안쪽 1px 밝은 선은 윗변 선을 분필색으로 바꿔 대신한다)
    //   pressed   그림 전체가 2px 아래로. 자리 표시에는 아랫변 그늘이 없으므로 바탕을 나무 어두움으로, 윗변 선을 지워 누름을 보인다
    //   on        바탕이 나무 밝음, 아랫변에 분필 흰색 4px 줄
    //   disabled  채도 0·밝기 60% 의 회색, 글자 회청
    // 꺼진 것의 이유 한 줄은 OverlayOf 가 ReasonKey 를 줄 때만 대상 바로 아래에 F-12 로 찍는다 [임시: 자리는 명세가 정하지 않았다].
    public abstract class UiInteractiveWidget : UiWidget
    {
        public const int PressedShiftPx = 2;    // spec 14 4.3 "그림 전체가 2px 아래로"
        public const int OnLinePx = 4;          // spec 14 4.3 "아랫변에 분필 흰색 4px 줄"
        public const int ReasonGapPx = 4;       // [임시] 대상과 이유 줄 사이

        private readonly UiPanel _onLine;
        private readonly UiLabel _reason;

        protected UiInteractiveWidget(UiScreenView owner, UiWidget parent, PixelRect localRect, Interactable control, string name)
            : base(name, parent, localRect)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Control = control ?? throw new ArgumentNullException(nameof(control));
            InteractableGroup group = owner.Group ?? throw new InvalidOperationException("the screen view has no InteractableGroup yet (UseGroup or CreateGroup first)");
            if (!Contains(group, control))
            {
                group.Add(control);
            }
            control.WidthPx = localRect.Width;
            control.HeightPx = localRect.Height;

            Skin = new UiPanel("Skin", this, PixelRect.FromSize(localRect.Width, localRect.Height));
            _onLine = new UiPanel("OnLine", Skin, new PixelRect(0, localRect.Height - OnLinePx, localRect.Width, OnLinePx), framed: false);
            _onLine.SetFill(UiPalette.Chalk);
            _onLine.Visible = false;
            Bracket = new UiBracket(this);
            int reasonHeight = UiFonts.F12.LineHeightPx;
            _reason = new UiLabel(owner.Context, this, new PixelRect(0, localRect.Height + ReasonGapPx, Math.Max(localRect.Width, MinReasonWidthPx), reasonHeight), UiFontId.F12);
            _reason.SetColor(UiPalette.Slate);
            _reason.Visible = false;
            owner.Register(this);
        }

        private const int MinReasonWidthPx = 256;   // [임시] 좁은 칸의 이유 줄도 한 줄이 들게

        public UiScreenView Owner { get; }

        public Interactable Control { get; }

        public InteractableGroup Group => Owner.Group!;

        public UiPanel Skin { get; }

        public UiBracket Bracket { get; }

        public UiLabel ReasonLine => _reason;

        // 마지막 Refresh 에서 고른 그림 상태
        public SkinState SkinState { get; private set; }

        public bool OnLineShown => _onLine.Visible;

        // 화면이 끝까지 누른 것(Activated)을 받는다. 모델이 묶음을 쥔 화면은 모델이 Raised 를 직접 듣는다
        public event Action<UiInteractiveWidget>? Activated;

        // 방향키로 옮길 곳을 위젯이 따로 정할 때(목록 줄의 위·아래). null 을 돌려주면 화면이 자리로 찾는다
        public Func<UiDirection, UiInteractiveWidget?>? NavigationOverride { get; set; }

        public override void SetRect(PixelRect localRect)
        {
            base.SetRect(localRect);
            if (Skin == null)
            {
                return;                                     // 기반 생성자에서 부를 때
            }
            Control.WidthPx = localRect.Width;
            Control.HeightPx = localRect.Height;
            Skin.SetRect(PixelRect.FromSize(localRect.Width, localRect.Height));
            _onLine.SetRect(new PixelRect(0, localRect.Height - OnLinePx, localRect.Width, OnLinePx));
            Bracket.SetTargetSize(new PixelSize(localRect.Width, localRect.Height));
            _reason.SetRect(new PixelRect(0, localRect.Height + ReasonGapPx, Math.Max(localRect.Width, MinReasonWidthPx), _reason.LocalRect.Height));
            OnResized(localRect);
        }

        // 모델의 상태를 읽어 그린다. 화면 뷰가 프레임마다(그리고 입력 뒤에) 부른다
        public void Refresh()
        {
            InteractableGroup group = Group;
            SkinState = group.SkinOf(Control);
            ApplySkin(SkinState);
            BracketOverlay overlay = group.OverlayOf(Control);
            Bracket.Apply(overlay, group.CursorPulseFrame);
            if (overlay.ReasonKey != null)
            {
                _reason.SetKey(overlay.ReasonKey);
                _reason.Visible = true;
            }
            else
            {
                _reason.Visible = false;
            }
            OnRefresh();
        }

        internal void RaiseActivated() => Activated?.Invoke(this);

        protected virtual void OnRefresh()
        {
        }

        protected virtual void OnResized(PixelRect localRect)
        {
        }

        // 글자색을 상태에 맞춘다(하위 위젯이 쓴다)
        protected static Color32 TextColorOf(SkinState state) => state == SkinState.Disabled ? UiPalette.Slate : UiPalette.Chalk;

        protected virtual void ApplySkin(SkinState state)
        {
            int shift = state == SkinState.Pressed ? PressedShiftPx : 0;
            Skin.SetRect(new PixelRect(0, shift, LocalRect.Width, LocalRect.Height));
            switch (state)
            {
                case SkinState.Disabled:
                    Skin.SetFill(UiPalette.DisabledFill);
                    Skin.SetBorder(UiPalette.Ink);
                    Skin.SetTopLine(UiPalette.DisabledBorder);
                    break;
                case SkinState.Pressed:
                    Skin.SetFill(UiPalette.WoodDark);
                    Skin.SetBorder(UiPalette.Ink);
                    Skin.SetTopLine(UiPalette.WoodLight, shown: false);
                    break;
                case SkinState.On:
                    Skin.SetFill(UiPalette.WoodLight);
                    Skin.SetBorder(UiPalette.Ink);
                    Skin.SetTopLine(UiPalette.WoodLight);
                    break;
                case SkinState.Hover:
                    Skin.SetFill(UiPalette.Wood);
                    Skin.SetBorder(UiPalette.WoodLight);
                    Skin.SetTopLine(UiPalette.Chalk);
                    break;
                default:
                    Skin.SetFill(UiPalette.Wood);
                    Skin.SetBorder(UiPalette.Ink);
                    Skin.SetTopLine(UiPalette.WoodLight);
                    break;
            }
            // 켜짐 줄은 바탕 그림 상태와 따로 켜짐(On) 자체를 따른다(꺼진 탭이 켜져 있어도 줄은 보인다)
            _onLine.Visible = Control.On;
        }

        private static bool Contains(InteractableGroup group, Interactable control)
        {
            foreach (Interactable i in group.Items)
            {
                if (ReferenceEquals(i, control))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

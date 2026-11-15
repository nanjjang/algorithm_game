using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Net;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 내 온라인 기록(ScreenId.OnlineProfile, 11-online.md 6.8절). 서버 프로필: 이름·레이팅·전적·골드, 캐릭터마다 서버 지팡이 단계와
    /// [강화](서버가 굴린다, 골드만 — 재료 창고는 아직 없다), 오른쪽에 결투 순위표(레이팅 20위). 오프라인 프로필과 따로다.
    /// </summary>
    public sealed class OnlineProfileScreen : ScreenBase
    {
        const float BoardW = 1500f, BoardH = 800f, Pad = 32f, RowH = 64f, Gap = 12f;

        OnlineService _svc;
        UiLabel _head, _status, _note;
        readonly List<UiLabel> _wandLines = new List<UiLabel>();
        readonly List<UiButton> _enhanceButtons = new List<UiButton>();
        readonly List<UiLabel> _topLines = new List<UiLabel>();

        public override ScreenId Id => ScreenId.OnlineProfile;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, board, T(OnlineKeys.ProfileTitle), UiFontId.F32, Pad, Pad - 8f, BoardW - 2f * Pad, th, UiTheme.Chalk, TextAnchor.MiddleCenter);
            float y = Pad + th;
            _head = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, y, BoardW - 2f * Pad, 40f, UiTheme.SelectYellow, TextAnchor.MiddleCenter);
            y += 52f;

            float leftW = 900f;
            for (int c = 1; c <= Wizard.Solo.Ids.Char.Count; c++)
            {
                int ch = c;
                var panel = UiKit.Panel(Ctx, board, UiPanelKind.Recess, Pad, y, leftW, RowH).rectTransform;
                _wandLines.Add(UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, 16f, 0f, leftW - 220f, RowH, UiTheme.Chalk));
                _enhanceButtons.Add(UiKit.Button(Ctx, panel, T(OnlineKeys.ProfileEnhance), leftW - 196f, 8f, 180f, RowH - 16f, () => OnEnhance(ch), UiButtonStyle.Primary, Focus));
                y += RowH + Gap;
            }
            float lh = UiTheme.LineHeight(UiFontId.F16);
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, y, leftW, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            y += 44f;
            _note = UiKit.Label(Ctx, board, T(OnlineKeys.ProfileNote), UiFontId.F12, Pad, y, leftW, lh * 2f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);

            float rx = Pad + leftW + 24f, rw = BoardW - rx - Pad;
            UiKit.Label(Ctx, board, T(OnlineKeys.ProfileTop), UiFontId.F24, rx, Pad + th + 52f, rw, 36f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            var top = UiKit.Panel(Ctx, board, UiPanelKind.Recess, rx, Pad + th + 96f, rw, 26f * 20 + 16f).rectTransform;
            for (int i = 0; i < 20; i++)
                _topLines.Add(UiKit.Label(Ctx, top, string.Empty, UiFontId.F12, 12f, 8f + 26f * i, rw - 24f, 26f, UiTheme.Chalk));

            UiKit.Button(Ctx, board, T(OnlineKeys.Leave), BoardW - Pad - 200f, BoardH - Pad - 56f, 200f, 56f, () => Close(), UiButtonStyle.Medium, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null) _status.SetText(T(OnlineKeys.ErrorServer));
            else
            {
                var top = _svc.Leaderboard();
                for (int i = 0; i < _topLines.Count; i++)
                {
                    var p = top != null && i < top.Count ? top[i] : null;
                    _topLines[i].SetText(p == null ? string.Empty
                        : F(OnlineKeys.ProfileTopLine, "rank", i + 1, "name", p.Name, "rating", p.Rating, "w", p.DuelWins, "l", p.DuelLosses));
                }
            }
            Refresh();
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            base.OnExit();
        }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            var cfg = Ctx != null && Ctx.Content != null ? Ctx.Content.Enhance : null;
            _head.SetText(p != null
                ? F(OnlineKeys.AccountLine, "name", p.Name, "rating", p.Rating, "w", p.DuelWins, "l", p.DuelLosses, "gold", N(p.Gold), "wave", p.CoopBestWave)
                : T(OnlineKeys.ProfileGuest));
            for (int i = 0; i < _wandLines.Count; i++)
            {
                int stage = p != null && p.WandStages != null && i < p.WandStages.Length ? p.WandStages[i] : 0;
                int aura = p != null && p.Auras != null && i < p.Auras.Length ? p.Auras[i] : 0;
                bool max = cfg != null && stage >= cfg.MaxStage;
                int pct = cfg != null && !max ? EnhanceRules.SuccessPct(cfg, stage, aura, 0) : 0;
                long cost = cfg != null && !max ? EnhanceRules.Cost(cfg, stage) : 0;
                _wandLines[i].SetText(F(OnlineKeys.ProfileWandLine, "who", T(Wizard.Solo.Ids.Char.Of(i + 1)), "stage", stage,
                    "pct", pct, "cost", N(cost)) + (max ? "  " + T(OnlineKeys.EnhanceMax) : string.Empty));
                _enhanceButtons[i].Enabled = p != null && !max && p.Gold >= cost;
            }
        }

        void OnEnhance(int character)
        {
            if (_svc == null) return;
            var rep = _svc.Enhance(character);
            if (rep == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            var r = rep.Value;
            if (r.Result == AccountStore.EnhanceResult.NotEnoughGold) _status.SetText(T(OnlineKeys.EnhanceGold));
            else if (r.Result != AccountStore.EnhanceResult.Ok) _status.SetText(T(OnlineKeys.ErrorServer));
            else
            {
                int after = r.Profile.WandStages[character - 1];
                _status.SetText(T(Wizard.Solo.Ids.Char.Of(character)) + "  +" + r.StageBefore + " → +" + after + "  " + T(OnlineKeys.Outcome + r.Outcome));
            }
            Refresh();
        }
    }
}

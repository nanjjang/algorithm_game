using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Net;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 내 온라인 기록(ScreenId.OnlineProfile, 11-online.md 6.8~6.9절). 서버 프로필: 이름·레이팅·전적·골드·재료, 캐릭터마다 서버 지팡이
    /// (단계·변이)와 [강화](서버가 굴린다: 골드·강화석, 부적, 제물), 변이가 나오면 [받기]/[거절]. 아래에 제물로 고를 블록(서버 창고),
    /// 오른쪽에 결투 순위표(레이팅 20위). 오프라인 프로필과 따로다.
    /// </summary>
    public sealed class OnlineProfileScreen : ScreenBase
    {
        const float BoardW = 1600f, BoardH = 980f, Pad = 32f, RowH = 64f, Gap = 10f;
        const int BlockChips = 12;

        OnlineService _svc;
        UiLabel _head, _status, _note, _items, _sacLine;
        UiButton _charmToggle, _sacClear;
        bool _useCharm;
        readonly List<string> _sacrifices = new List<string>();
        readonly List<UiLabel> _wandLines = new List<UiLabel>();
        readonly List<UiButton> _enhanceButtons = new List<UiButton>();
        readonly List<UiButton> _assemblyButtons = new List<UiButton>();
        readonly List<UiButton> _acceptButtons = new List<UiButton>();
        readonly List<UiButton> _declineButtons = new List<UiButton>();
        readonly List<UiButton> _blockButtons = new List<UiButton>();
        readonly List<string> _blockIds = new List<string>();
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
            y += 44f;
            float leftW = 1000f;
            _items = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, leftW - 260f, 40f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            _charmToggle = UiKit.Button(Ctx, board, string.Empty, Pad + leftW - 250f, y, 250f, 40f, ToggleCharm, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            y += 50f;
            for (int c = 1; c <= Wizard.Solo.Ids.Char.Count; c++)
            {
                int ch = c;
                var panel = UiKit.Panel(Ctx, board, UiPanelKind.Recess, Pad, y, leftW, RowH).rectTransform;
                _wandLines.Add(UiKit.Label(Ctx, panel, string.Empty, UiFontId.F12, 16f, 0f, leftW - 420f, RowH, UiTheme.Chalk, TextAnchor.MiddleLeft, wrap: true));
                _assemblyButtons.Add(UiKit.Button(Ctx, panel, T(OnlineKeys.AssemblyButton), leftW - 400f, 8f, 110f, RowH - 16f,
                    () => OpenAssembly(ch), UiButtonStyle.Medium, Focus, null, UiFontId.F16));
                _enhanceButtons.Add(UiKit.Button(Ctx, panel, T(OnlineKeys.ProfileEnhance), leftW - 196f, 8f, 180f, RowH - 16f, () => OnEnhance(ch), UiButtonStyle.Primary, Focus));
                _acceptButtons.Add(UiKit.Button(Ctx, panel, T(OnlineKeys.MutationAccept), leftW - 280f, 8f, 132f, RowH - 16f, () => OnResolve(ch, true), UiButtonStyle.Primary, Focus, null, UiFontId.F16));
                _declineButtons.Add(UiKit.Button(Ctx, panel, T(OnlineKeys.MutationDecline), leftW - 140f, 8f, 124f, RowH - 16f, () => OnResolve(ch, false), UiButtonStyle.Medium, Focus, null, UiFontId.F16));
                y += RowH + Gap;
            }

            // 제물: 서버 창고의 블록. 누를 때마다 하나씩 더한다(상한 = 강화 설정의 제물 수).
            _sacLine = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, leftW - 220f, 36f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            _sacClear = UiKit.Button(Ctx, board, T(OnlineKeys.SacrificeClear), Pad + leftW - 200f, y, 200f, 36f, () => { _sacrifices.Clear(); Refresh(); }, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            y += 42f;
            float chipW = (leftW - 5f * Gap) / 6f;
            for (int i = 0; i < BlockChips; i++)
            {
                int k = i;
                float cx = Pad + (i % 6) * (chipW + Gap), cy = y + (i / 6) * (40f + Gap);
                _blockButtons.Add(UiKit.Button(Ctx, board, string.Empty, cx, cy, chipW, 40f, () => AddSacrifice(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            y += 2f * (40f + Gap);

            float lh = UiTheme.LineHeight(UiFontId.F16);
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, y, leftW, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            y += 42f;
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
            _sacrifices.Clear();
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

        void OpenAssembly(int ch)
        {
            var args = ScreenArgs.From(ScreenId.OnlineProfile).With(ScreenArgKeys.Character, ch);
            // 조립 화면이 닫히면 서버 프로필을 다시 받아 보인다(조립은 다른 연결로 저장했다).
            args.OnResult = _ => { _svc?.Dispose(); _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null); Refresh(); };
            Screens?.Push(ScreenId.OnlineAssembly, args);
        }

        void ToggleCharm() { _useCharm = !_useCharm; Refresh(); }

        int MaxSacrifices => Ctx != null && Ctx.Content != null && Ctx.Content.Enhance != null ? Ctx.Content.Enhance.Sacrifice.MaxBlocks : 0;

        /// <summary>창고에 남은 그 블록 수(고른 제물을 뺀 것).</summary>
        int FreeQty(ServerProfile p, string id)
        {
            int used = 0;
            foreach (var s in _sacrifices) if (s == id) used++;
            return p.Qty(id) - used;
        }

        void AddSacrifice(int chip)
        {
            var p = _svc != null ? _svc.Profile : null;
            if (p == null || chip >= _blockIds.Count || _sacrifices.Count >= MaxSacrifices) return;
            string id = _blockIds[chip];
            if (FreeQty(p, id) <= 0) return;
            _sacrifices.Add(id);
            Refresh();
        }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            var db = Ctx != null ? Ctx.Content : null;
            var cfg = db != null ? db.Enhance : null;
            int charms = p != null ? p.Qty(Wizard.Solo.Ids.Item.Charm) : 0;
            if (charms <= 0) _useCharm = false;
            _items.SetText(p == null ? string.Empty : F(OnlineKeys.ItemsLine,
                "low", p.Qty(Wizard.Solo.Ids.Item.StoneLow), "mid", p.Qty(Wizard.Solo.Ids.Item.StoneMid),
                "high", p.Qty(Wizard.Solo.Ids.Item.StoneHigh), "charm", charms));
            _charmToggle.SetText(T(_useCharm ? OnlineKeys.CharmOn : OnlineKeys.CharmOff));
            _charmToggle.Enabled = charms > 0;
            _head.SetText(p != null
                ? F(OnlineKeys.AccountLine, "name", p.Name, "rating", p.Rating, "w", p.DuelWins, "l", p.DuelLosses, "gold", N(p.Gold), "wave", p.CoopBestWave)
                : T(OnlineKeys.ProfileGuest));

            // 제물 칩: 창고의 블록 종류(이름 순).
            _blockIds.Clear();
            if (p != null && p.Items != null && db != null)
                foreach (var kv in p.Items) if (kv.Value > 0 && db.Block(kv.Key) != null) _blockIds.Add(kv.Key);
            _blockIds.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < _blockButtons.Count; i++)
            {
                bool on = i < _blockIds.Count;
                _blockButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                _blockButtons[i].SetText(T(_blockIds[i]) + " ×" + FreeQty(p, _blockIds[i]));
                _blockButtons[i].Enabled = FreeQty(p, _blockIds[i]) > 0 && _sacrifices.Count < MaxSacrifices;
            }
            var names = new List<string>();
            foreach (var s in _sacrifices) names.Add(T(s));
            _sacLine.SetText(F(OnlineKeys.SacrificeLine, "n", _sacrifices.Count, "max", MaxSacrifices,
                "list", names.Count > 0 ? string.Join(", ", names) : "-"));
            _sacClear.Enabled = _sacrifices.Count > 0;

            for (int i = 0; i < _wandLines.Count; i++)
            {
                int ch = i + 1;
                int stage = p != null && p.WandStages != null && i < p.WandStages.Length ? p.WandStages[i] : 0;
                int aura = p != null && p.Auras != null && i < p.Auras.Length ? p.Auras[i] : 0;
                int peak = stage;
                bool max = cfg != null && stage >= cfg.MaxStage;
                var def = db != null ? db.Character(Wizard.Solo.Ids.Char.Of(ch)) : null;
                var bd = cfg != null && !max
                    ? EnhanceRules.Breakdown(cfg, stage, peak, aura, EnhanceRules.SacrificeBonus(cfg, db, _sacrifices, def != null ? def.Element : null))
                    : default(Wizard.Solo.Meta.ProbBreakdown);
                bool hasStones = p != null && (bd.StoneId == null || p.Qty(bd.StoneId) >= bd.StoneCount);
                var muts = new List<string>();
                if (p != null) foreach (var m in p.MutationsOf(ch)) muts.Add(T(m.Id));
                var offer = p != null ? p.OfferOf(ch) : null;
                string line = F(OnlineKeys.ProfileWandLine, "who", T(Wizard.Solo.Ids.Char.Of(ch)), "stage", stage, "pct", bd.SuccessPct, "cost", N(bd.CostGold))
                    + (max ? "  " + T(OnlineKeys.EnhanceMax) : (bd.StoneId != null ? "  ·  " + T(bd.StoneId) + " ×" + bd.StoneCount : string.Empty))
                    + (muts.Count > 0 ? "\n" + F(OnlineKeys.MutationList, "list", string.Join(", ", muts)) : string.Empty);
                if (offer != null)
                    line = F(OnlineKeys.MutationOfferLine, "who", T(Wizard.Solo.Ids.Char.Of(ch)), "m", T(offer.Candidate))
                        + (offer.WouldPushOut != null ? "  " + F(OnlineKeys.MutationPush, "m", T(offer.WouldPushOut)) : string.Empty);
                _wandLines[i].SetText(line);
                _enhanceButtons[i].gameObject.SetActive(offer == null);
                _assemblyButtons[i].Enabled = p != null;
                var lo = p != null ? p.LoadoutOf(ch) : null;
                if (lo != null)
                {
                    var parts = new List<string> { T(string.IsNullOrEmpty(lo.Core) ? Wizard.Solo.Ids.Block.MagicBolt : lo.Core) };
                    foreach (var l in lo.Links) if (!string.IsNullOrEmpty(l)) parts.Add(T(l));
                    if (offer == null) _wandLines[i].SetText(_wandLines[i].FullText + "\n" + string.Join(" + ", parts));
                }
                _acceptButtons[i].gameObject.SetActive(offer != null);
                _declineButtons[i].gameObject.SetActive(offer != null);
                _enhanceButtons[i].Enabled = p != null && !max && p.Gold >= bd.CostGold && hasStones;
            }
        }

        void OnEnhance(int character)
        {
            if (_svc == null) return;
            var rep = _svc.Enhance(character, _useCharm, _sacrifices);
            Show(rep, character);
            if (rep != null && rep.Value.Result == AccountStore.EnhanceResult.Ok) _sacrifices.Clear();
            Refresh();
        }

        void OnResolve(int character, bool accept)
        {
            if (_svc == null) return;
            Show(_svc.ResolveMutation(character, accept), character);
            Refresh();
        }

        void Show(OnlineService.EnhanceReply? rep, int character)
        {
            if (rep == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            var r = rep.Value;
            if (r.Result == AccountStore.EnhanceResult.NotEnoughGold) _status.SetText(T(OnlineKeys.EnhanceGold));
            else if (r.Result == AccountStore.EnhanceResult.NotEnoughStones) _status.SetText(T(OnlineKeys.EnhanceStones));
            else if (r.Result == AccountStore.EnhanceResult.BadSacrifice) _status.SetText(T(OnlineKeys.SacrificeBad));
            else if (r.Result != AccountStore.EnhanceResult.Ok) _status.SetText(T(OnlineKeys.ErrorServer));
            else
            {
                int after = r.Profile.WandStages[character - 1];
                _status.SetText(T(Wizard.Solo.Ids.Char.Of(character)) + "  +" + r.StageBefore + " → +" + after + "  " + T(OnlineKeys.Outcome + r.Outcome));
            }
        }
    }
}

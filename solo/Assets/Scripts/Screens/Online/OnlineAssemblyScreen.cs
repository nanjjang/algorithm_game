using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 서버 지팡이 조립(ScreenId.OnlineAssembly, 11-online.md 6.10절). 인자 = 캐릭터(1~5).
    /// 위: 코어 + 연결 칸(칸을 눌러 고른다). 가운데: 서버 창고의 블록(+ 누구나 가진 마력탄) — 누르면 고른 칸에 끼운다.
    /// [빼기] = 고른 칸 비우기(코어면 마력탄으로), [저장] = 서버에 보낸다(모양·주문 컴파일·수량은 서버가 본다).
    /// </summary>
    public sealed class OnlineAssemblyScreen : ScreenBase
    {
        const float BoardW = 1400f, BoardH = 820f, Pad = 32f, SlotW = 180f, SlotH = 64f, Gap = 12f;
        const int MaxSlots = 8, Chips = 24;

        OnlineService _svc;
        int _character = 1;
        string _core;
        readonly List<string> _links = new List<string>();
        int _selected; // 0 = 코어, 1.. = 연결 칸
        UiLabel _title, _status;
        readonly List<UiButton> _slotButtons = new List<UiButton>();
        readonly List<UiButton> _chipButtons = new List<UiButton>();
        readonly List<string> _chipIds = new List<string>();
        readonly List<UiButton> _mutButtons = new List<UiButton>();
        UiLabel _mutHead;

        public override ScreenId Id => ScreenId.OnlineAssembly;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            float th = UiTheme.LineHeight(UiFontId.F32);
            _title = UiKit.Label(Ctx, board, string.Empty, UiFontId.F32, Pad, Pad - 8f, BoardW - 2f * Pad, th, UiTheme.Chalk, TextAnchor.MiddleCenter);
            float y = Pad + th + 12f;
            for (int i = 0; i <= MaxSlots; i++)
            {
                int k = i;
                float x = Pad + i * (SlotW / 1.2f + Gap);
                _slotButtons.Add(UiKit.Button(Ctx, board, string.Empty, x, y, SlotW / 1.2f, SlotH, () => { _selected = k; Refresh(); },
                    i == 0 ? UiButtonStyle.Primary : UiButtonStyle.Medium, Focus, null, UiFontId.F12));
            }
            y += SlotH + 24f;
            UiKit.Label(Ctx, board, T(OnlineKeys.AssemblyChips), UiFontId.F16, Pad, y, BoardW - 2f * Pad, 28f, UiTheme.SlateBlue);
            y += 32f;
            float chipW = (BoardW - 2f * Pad - 5f * Gap) / 6f;
            for (int i = 0; i < Chips; i++)
            {
                int k = i;
                _chipButtons.Add(UiKit.Button(Ctx, board, string.Empty, Pad + (i % 6) * (chipW + Gap), y + (i / 6) * (44f + Gap), chipW, 44f,
                    () => Put(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            y += 4f * (44f + Gap) + 8f;
            // 받은 변이: 누르면 지운다(골드).
            _mutHead = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, 360f, 44f, UiTheme.SlateBlue);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _mutButtons.Add(UiKit.Button(Ctx, board, string.Empty, Pad + 370f + i * (220f + Gap), y, 220f, 44f, () => RemoveMutation(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            y += 52f;
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, y, BoardW - 2f * Pad, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            float by = BoardH - Pad - 56f;
            UiKit.Button(Ctx, board, T(OnlineKeys.AssemblyClear), Pad, by, 220f, 56f, Clear, UiButtonStyle.Medium, Focus);
            UiKit.Button(Ctx, board, T(OnlineKeys.AssemblySave), BoardW - Pad - 220f - Gap - 200f, by, 220f, 56f, Save, UiButtonStyle.Primary, Focus);
            UiKit.Button(Ctx, board, T(OnlineKeys.Leave), BoardW - Pad - 200f, by, 200f, 56f, () => Close(), UiButtonStyle.Medium, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _character = args != null ? args.Get(ScreenArgKeys.Character, 1) : 1;
            _selected = 0;
            _status.SetText(string.Empty);
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null || _svc.Profile == null) _status.SetText(T(OnlineKeys.ErrorServer));
            LoadFromProfile();
            Refresh();
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            base.OnExit();
        }

        /// <summary>창고의 남은 수(편집 중 끼운 것 반영: 서버에 끼워 둔 것은 돌아온 것으로, 편집본에 끼운 것은 빠진 것으로).</summary>
        int Free(string id)
        {
            var p = _svc != null ? _svc.Profile : null;
            if (p == null || id == Wizard.Solo.Ids.Block.MagicBolt) return 99;
            int n = p.Qty(id);
            var lo = p.LoadoutOf(_character);
            if (lo != null)
            {
                if (lo.Core == id) n++;
                foreach (var l in lo.Links) if (l == id) n++;
            }
            if (_core == id) n--;
            foreach (var l in _links) if (l == id) n--;
            return n;
        }

        void LoadFromProfile()
        {
            var p = _svc != null ? _svc.Profile : null;
            var db = Ctx != null ? Ctx.Content : null;
            var def = db != null ? db.Character(Wizard.Solo.Ids.Char.Of(_character)) : null;
            int n = (def != null ? def.BaseLinkSlots : 0) + (p != null && p.SlotsExtra != null ? p.SlotsExtra[_character - 1] : 0);
            var lo = p != null ? p.LoadoutOf(_character) : null;
            _core = lo != null && !string.IsNullOrEmpty(lo.Core) ? lo.Core : Wizard.Solo.Ids.Block.MagicBolt;
            _links.Clear();
            for (int i = 0; i < n; i++) _links.Add(lo != null && i < lo.Links.Count ? lo.Links[i] : null);
        }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            var db = Ctx != null ? Ctx.Content : null;
            int stage = p != null && p.WandStages != null ? p.WandStages[_character - 1] : 0;
            _title.SetText(F(OnlineKeys.AssemblyTitle, "who", T(Wizard.Solo.Ids.Char.Of(_character)), "stage", stage));
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                bool on = i == 0 || i - 1 < _links.Count;
                _slotButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                string id = i == 0 ? _core : _links[i - 1];
                string head = i == 0 ? T(OnlineKeys.AssemblyCore) : F(OnlineKeys.AssemblyLink, "n", i);
                _slotButtons[i].SetText((i == _selected ? "▶ " : string.Empty) + head + "\n" + (string.IsNullOrEmpty(id) ? "-" : T(id)));
            }
            var cfg = db != null ? db.Enhance : null;
            var mlist = p != null ? p.MutationsOf(_character) : new List<Wizard.Solo.Meta.MutationEntry>();
            _mutHead.SetText(F(OnlineKeys.MutationRemoveHead, "gold", N(cfg != null ? cfg.MutationRemoveGold : 0)));
            for (int i = 0; i < _mutButtons.Count; i++)
            {
                bool on = i < mlist.Count;
                _mutButtons[i].gameObject.SetActive(on);
                if (on) _mutButtons[i].SetText("✕ " + T(mlist[i].Id));
            }
            _chipIds.Clear();
            _chipIds.Add(Wizard.Solo.Ids.Block.MagicBolt);
            var ids = new SortedSet<string>(System.StringComparer.Ordinal);
            if (p != null && p.Items != null && db != null)
                foreach (var kv in p.Items) if (db.Block(kv.Key) != null && kv.Key != Wizard.Solo.Ids.Block.MagicBolt) ids.Add(kv.Key);
            var lo = p != null ? p.LoadoutOf(_character) : null;
            if (lo != null && db != null)
            {
                if (!string.IsNullOrEmpty(lo.Core) && lo.Core != Wizard.Solo.Ids.Block.MagicBolt) ids.Add(lo.Core);
                foreach (var l in lo.Links) if (!string.IsNullOrEmpty(l) && l != Wizard.Solo.Ids.Block.MagicBolt) ids.Add(l);
            }
            _chipIds.AddRange(ids);
            for (int i = 0; i < _chipButtons.Count; i++)
            {
                bool on = i < _chipIds.Count;
                _chipButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                string id = _chipIds[i];
                int free = Free(id);
                _chipButtons[i].SetText(T(id) + (id == Wizard.Solo.Ids.Block.MagicBolt ? string.Empty : " ×" + free));
                bool projectile = db != null && db.Block(id) != null && db.Block(id).IsProjectile;
                _chipButtons[i].Enabled = free > 0 && (_selected != 0 || projectile);
            }
        }

        void RemoveMutation(int k)
        {
            var p = _svc != null ? _svc.Profile : null;
            if (p == null) return;
            var muts = p.MutationsOf(_character);
            if (k >= muts.Count) return;
            var rep = _svc.RemoveMutation(_character, muts[k].Id);
            if (rep == null) _status.SetText(T(OnlineKeys.ErrorServer));
            else if (rep.Value.Result == AccountStore.EnhanceResult.NotEnoughGold) _status.SetText(T(OnlineKeys.EnhanceGold));
            else if (rep.Value.Result == AccountStore.EnhanceResult.Ok) _status.SetText(T(OnlineKeys.MutationRemoved));
            else _status.SetText(T(OnlineKeys.AssemblyBad));
            Refresh();
        }

        void Put(int chip)
        {
            if (chip >= _chipIds.Count) return;
            string id = _chipIds[chip];
            if (Free(id) <= 0) return;
            if (_selected == 0) _core = id;
            else if (_selected - 1 < _links.Count) _links[_selected - 1] = id;
            if (_selected < _links.Count) _selected++;
            Refresh();
        }

        void Clear()
        {
            if (_selected == 0) _core = Wizard.Solo.Ids.Block.MagicBolt;
            else if (_selected - 1 < _links.Count) _links[_selected - 1] = null;
            Refresh();
        }

        void Save()
        {
            if (_svc == null) return;
            var res = _svc.SetLoadout(_character, _core, _links);
            if (res == null) _status.SetText(T(OnlineKeys.ErrorServer));
            else if (res.Value == AccountStore.LoadoutResult.Ok) _status.SetText(T(OnlineKeys.AssemblySaved));
            else if (res.Value == AccountStore.LoadoutResult.CompileError) _status.SetText(T(OnlineKeys.AssemblyCompile));
            else if (res.Value == AccountStore.LoadoutResult.NotOwned) _status.SetText(T(OnlineKeys.AssemblyNotOwned));
            else _status.SetText(T(OnlineKeys.AssemblyBad));
            LoadFromProfile();
            Refresh();
        }
    }
}

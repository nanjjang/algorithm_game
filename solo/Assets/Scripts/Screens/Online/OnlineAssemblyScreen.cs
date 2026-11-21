using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 서버 지팡이 조립(ScreenId.OnlineAssembly, 11-online.md 6.10절, docs/ui-assets.md 4.11절). 인자 = 캐릭터(1~5).
    /// 작업대 장면 위: 위 판에 지팡이 그림과 "회로" — 코어 칸(K-63) → 이음(K-65) → 연결 칸(K-60)들, 칸마다 블록 아이콘과 이름.
    /// 칸을 눌러 고르고(노란 괄호), 아래 창고 격자(블록 아이콘 + 남은 수)를 누르면 끼운다. 코어에는 투사체만.
    /// 아래 왼쪽: 받은 변이 꼬리표([지우기], 골드). [칸 비우기] = 고른 칸 비우기(코어면 마력탄으로), [저장] = 서버에 보낸다.
    /// </summary>
    public sealed class OnlineAssemblyScreen : ScreenBase
    {
        const int MaxSlots = 8, Chips = 24;
        const float TopX = 48f, TopY = 112f, TopW = 1824f, TopH = 320f;
        const float SlotSize = 88f, LinkW = 36f;

        OnlineService _svc;
        int _character = 1;
        string _core;
        readonly List<string> _links = new List<string>();
        int _selected; // 0 = 코어, 1.. = 연결 칸
        UiLabel _title, _status, _mutHead;
        UnityEngine.UI.Image _wandArt;
        RectTransform _top;
        readonly List<UiButton> _slotButtons = new List<UiButton>();
        readonly List<UnityEngine.UI.Image> _slotIcons = new List<UnityEngine.UI.Image>();
        readonly List<UiLabel> _slotNames = new List<UiLabel>(), _slotCaps = new List<UiLabel>();
        readonly List<RectTransform> _linkLines = new List<RectTransform>();
        readonly List<UiButton> _chipButtons = new List<UiButton>();
        readonly List<UnityEngine.UI.Image> _chipIcons = new List<UnityEngine.UI.Image>();
        readonly List<UiLabel> _chipQty = new List<UiLabel>();
        readonly List<string> _chipIds = new List<string>();
        readonly List<UiButton> _mutButtons = new List<UiButton>();

        public override ScreenId Id => ScreenId.OnlineAssembly;
        protected override bool HasFrame => false;

        Sprite Icon(string id, int size) { try { return Ctx != null && Ctx.Art != null && id != null ? Ctx.Art.Icon(id, size) : null; } catch (System.Exception) { return null; } }

        protected override void BuildContent(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "enhance.workbench", null, 0.55f);
            _title = OnlineUi.HeaderBand(Ctx, content, "I-F1", "library", string.Empty, out var band);
            UiKit.Button(Ctx, band, T(OnlineKeys.Leave), UiScale.RefWidth - OnlineUi.Edge - 176f, 16f, 176f, 48f, () => Close(), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "back"));

            // 위 판: 지팡이 + 회로.
            var top = UiKit.Panel(Ctx, content, UiPanelKind.Board, TopX, TopY, TopW, TopH).rectTransform;
            UiKit.Bevel(top);
            _top = top;
            OnlineUi.Img(Ctx, top, "S-0602", "wand_pedestal", 40f, 248f, 224f, 48f);
            _wandArt = UiKit.Image(top, null, 64f, 40f, 176f, 208f, Color.white, true);
            float x = 320f, cy = 96f;
            for (int i = 0; i <= MaxSlots; i++)
            {
                int k = i;
                if (i > 0)
                {
                    var line = OnlineUi.Img(Ctx, top, "K-65", null, x - LinkW - 4f, cy + SlotSize * 0.5f - 6f, LinkW, 12f).rectTransform;
                    _linkLines.Add(line);
                }
                var b = UiKit.Button(Ctx, top, null, x, cy, SlotSize, SlotSize, () => { _selected = k; Refresh(); }, UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                if (i == 0) OnlineUi.Img(Ctx, r, "K-63", "core_slot_filled", 0f, 0f, SlotSize, SlotSize);
                else OnlineUi.Img(Ctx, r, "K-60", "item_slot_empty", 0f, 0f, SlotSize, SlotSize);
                _slotIcons.Add(UiKit.Image(r, null, (SlotSize - 56f) * 0.5f, (SlotSize - 56f) * 0.5f, 56f, 56f, Color.white, true));
                _slotNames.Add(UiKit.Label(Ctx, top, string.Empty, UiFontId.F16, x - 20f, cy + SlotSize + 8f, SlotSize + 40f, 48f, UiTheme.Chalk, TextAnchor.UpperCenter, wrap: true));
                _slotCaps.Add(UiKit.Label(Ctx, top, i == 0 ? T(OnlineKeys.AssemblyCore) : F(OnlineKeys.AssemblyLink, "n", i), UiFontId.F16, x - 8f, cy - 30f, SlotSize + 16f, 24f, i == 0 ? UiTheme.SelectYellow : UiTheme.SlateBlue, TextAnchor.MiddleCenter));
                _slotButtons.Add(b);
                x += SlotSize + LinkW + 8f;
            }

            // 창고 격자.
            var chipsHead = UiKit.Label(Ctx, content, T(OnlineKeys.AssemblyChips), UiFontId.F24, TopX, TopY + TopH + 12f, TopW, 36f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(chipsHead);
            float gy = TopY + TopH + 56f;
            for (int i = 0; i < Chips; i++)
            {
                int k = i;
                float gx = TopX + (i % 12) * 152f;
                float yy = gy + (i / 12) * 132f;
                var b = UiKit.Button(Ctx, content, null, gx, yy, 140f, 124f, () => Put(k), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.Panel(Ctx, r, UiPanelKind.Recess, 0f, 0f, 140f, 124f).raycastTarget = false;
                OnlineUi.Img(Ctx, r, "K-60", "item_slot_filled", 38f, 8f, 64f, 64f);
                _chipIcons.Add(UiKit.Image(r, null, 46f, 16f, 48f, 48f, Color.white, true));
                _chipQty.Add(UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 4f, 76f, 132f, 44f, UiTheme.Chalk, TextAnchor.UpperCenter, wrap: true));
                _chipButtons.Add(b);
            }

            // 받은 변이 + 버튼 줄.
            // 버튼 줄은 창고 격자(최대 2줄) 바로 아래.
            float by = TopY + TopH + 56f + 2f * 132f + 56f;
            _mutHead = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, TopX, by - 44f, 900f, 32f, UiTheme.SlateBlue);
            UiKit.DropShadow(_mutHead);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _mutButtons.Add(UiKit.Button(Ctx, content, string.Empty, TopX + i * 232f, by, 220f, 48f, () => RemoveMutation(k), UiButtonStyle.Small, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "trash")));
            }
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 980f, by - 52f, 892f, 40f, UiTheme.Amber, TextAnchor.MiddleRight);
            UiKit.DropShadow(_status);
            UiKit.Button(Ctx, content, T(OnlineKeys.AssemblyClear), UiScale.RefWidth - 48f - 240f - 16f - 320f, by - 8f, 240f, 64f, Clear, UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "minus"));
            UiKit.Button(Ctx, content, T(OnlineKeys.AssemblySave), UiScale.RefWidth - 48f - 320f, by - 8f, 320f, 64f, Save, UiButtonStyle.Primary, Focus, null, UiFontId.F32);
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
            string charId = Wizard.Solo.Ids.Char.Of(_character);
            _title.SetText(F(OnlineKeys.AssemblyTitle, "who", T(charId), "stage", stage));
            Sprite wa = null;
            try { wa = Ctx != null && Ctx.Art != null ? (Ctx.Art.WandInspection(charId, stage) ?? Ctx.Art.WandIcon(charId, stage)) : null; } catch (System.Exception) { }
            _wandArt.sprite = wa;
            _wandArt.enabled = wa != null;
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                bool on = i == 0 || i - 1 < _links.Count;
                _slotButtons[i].gameObject.SetActive(on);
                _slotNames[i].gameObject.SetActive(on);
                if (i > 0) _linkLines[i - 1].gameObject.SetActive(on);
                _slotCaps[i].gameObject.SetActive(on);
                if (!on) continue;
                string id = i == 0 ? _core : _links[i - 1];
                var ic = Icon(id, 56);
                _slotIcons[i].sprite = ic;
                _slotIcons[i].enabled = ic != null;
                _slotNames[i].SetText(string.IsNullOrEmpty(id) ? "-" : T(id));
                _slotButtons[i].Picked = i == _selected;
            }
            // 위 판은 칸 수에 맞춰 폭을 줄이고 가운데에 둔다.
            int slots = 1 + _links.Count;
            float tw = 320f + slots * (SlotSize + LinkW + 8f) + 24f;
            UiKit.Place(_top, (UiScale.RefWidth - tw) * 0.5f, TopY, tw, TopH);
            var cfg = db != null ? db.Enhance : null;
            var mlist = p != null ? p.MutationsOf(_character) : new List<Wizard.Solo.Meta.MutationEntry>();
            _mutHead.SetText(F(OnlineKeys.MutationRemoveHead, "gold", N(cfg != null ? cfg.MutationRemoveGold : 0)));
            _mutHead.gameObject.SetActive(mlist.Count > 0);
            for (int i = 0; i < _mutButtons.Count; i++)
            {
                bool on = i < mlist.Count;
                _mutButtons[i].gameObject.SetActive(on);
                if (on) _mutButtons[i].SetText(T(mlist[i].Id));
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
            int cols = Mathf.Min(12, _chipIds.Count);
            float gx0 = (UiScale.RefWidth - (cols * 152f - 12f)) * 0.5f;
            for (int i = 0; i < _chipButtons.Count; i++)
            {
                bool on = i < _chipIds.Count;
                _chipButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                UiKit.Place(_chipButtons[i].Rect, gx0 + (i % 12) * 152f, TopY + TopH + 56f + (i / 12) * 132f, 140f, 124f);
                string id = _chipIds[i];
                int free = Free(id);
                _chipIcons[i].sprite = Icon(id, 48);
                _chipQty[i].SetText(T(id) + (id == Wizard.Solo.Ids.Block.MagicBolt ? string.Empty : "  ×" + free));
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

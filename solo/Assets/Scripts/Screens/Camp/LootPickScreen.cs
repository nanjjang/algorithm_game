using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 전리품 고르기(08 요약서 8.1절, 06 요약서 4.4절). 판 위에 뜨는 판(뒤를 60% 어둡게, 전장이 비침).
    /// 상태의 주인은 시뮬레이션(w.Run.Loot)이고 이 화면은 명령만 보낸다: LootPick/LootUnpick/LootReroll/LootDiscard/LootConfirm.
    /// 여닫기는 Battle 화면이 한다(PhaseChanged). 다시 뽑기는 이 화면에만 있다(D-16).
    /// </summary>
    public sealed class LootPickScreen : ScreenBase
    {
        /// <summary>카드가 뒤집혀 뜨는 간격(08 8.1절 0.1초 = 6틱). 화면 60Hz 틱.</summary>
        const int RevealStepTicks = 6;
        /// <summary>보관함 줄(08 8.1절 (576,896) 768×64, 12칸). 화면 배치 상수.</summary>
        const float StorageX = 576f, StorageY = 896f, StorageW = 768f, StorageCell = 56f;

        public override ScreenId Id => ScreenId.LootPick;
        public override ScreenLayer Layer => ScreenLayer.Panel;
        public override bool BlocksInput => true;
        protected override bool HasFrame => false;

        UiLabel _title, _counter, _purse, _overflowTitle;
        UiButton _reroll, _confirm;
        RectTransform _cards, _storage;
        readonly List<UiButton> _cardButtons = new List<UiButton>();
        readonly List<UiButton> _storageButtons = new List<UiButton>();
        LootPickModel _model;
        string _sig;
        int _revealed;
        float _revealAcc;

        SimWorld World => Ctx != null && Ctx.Session != null ? Ctx.Session.MainWorld ?? Ctx.Session.World : null;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.ModalDim, true, "Dim");
            UiBackdrop.VignetteOver(content, 0.7f);
            UiBackdrop.Glow(content, 460f, 60f, 1000f, 220f, UiTheme.WithAlpha(UiTheme.Brass, 0.16f));
            _title = UiKit.Label(Ctx, content, string.Empty, UiFontId.F48, 560f, 120f, 800f, 56f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_title, 3f);
            UiKit.AccentRule(content, 760f, 178f, 400f);
            _counter = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 760f, 184f, 400f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _purse = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 1400f, 120f, 400f, 32f, UiTheme.Gold, TextAnchor.MiddleRight);
            _overflowTitle = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 360f, 848f, 1200f, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            _cards = UiKit.Node(content, "Cards", 0f, 240f, UiScale.RefWidth, LootPickModel.CardHeight + 40f);
            _reroll = UiKit.Button(Ctx, content, T("str.loot_pick.reroll"), 760f, 800f, 400f, 64f, OnReroll, UiButtonStyle.Large, Focus, "");
            UiKit.LabelKey(Ctx, content, "str.camp.storage", UiFontId.F16, StorageX, StorageY - 24f, 200f, 24f, UiTheme.SlateBlue);
            _storage = UiKit.Node(content, "Storage", StorageX, StorageY, StorageW, 64f);
            _confirm = UiKit.Button(Ctx, content, T("str.loot_pick.confirm"), 760f, 984f, 400f, 64f, OnConfirm, UiButtonStyle.Primary, Focus, "");
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _revealed = 0;
            _revealAcc = 0f;
            _sig = null;
            Refresh(true);
        }

        public override bool OnBack() => true;   // 고르기를 끝내야 닫힌다(Esc로 건너뛰지 않는다).

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel || key == MenuKey.Back)
            {
                // K: 커서 카드가 고른 카드면 빼기.
                int i = _cardButtons.IndexOf(Focus.Current as UiButton);
                if (i >= 0 && _model != null && i < _model.Cards.Count && _model.Cards[i].Picked) Toggle(i);
                return true;
            }
            return base.OnMenuKey(key);
        }

        public override void Tick(float dt)
        {
            if (_model != null && _revealed < _model.Cards.Count)
            {
                _revealAcc += dt;
                while (_revealAcc >= RevealStepTicks * SimConst.TickSeconds && _revealed < _model.Cards.Count)
                {
                    _revealAcc -= RevealStepTicks * SimConst.TickSeconds;
                    _revealed++;
                    PlayUi("ui.loot.card_flip");
                    ApplyReveal();
                }
            }
            Refresh(false);
        }

        static string Signature(RunState run)
        {
            var o = run.Loot;
            if (o == null) return "none";
            var sb = new System.Text.StringBuilder();
            sb.Append(run.Purse).Append('|').Append(run.Storage.Count).Append('|').Append(o.OverflowToDiscard).Append('|').Append(o.Rerolls).Append('|');
            for (int i = 0; i < o.Cards.Count; i++) sb.Append(o.Cards[i].BlockId).Append(o.Cards[i].Picked ? '+' : '-');
            for (int i = 0; i < run.Storage.Count; i++) sb.Append(run.Storage[i].Uid).Append(',');
            return sb.ToString();
        }

        void Refresh(bool force)
        {
            var w = World;
            if (w == null) return;
            string sig = Signature(w.Run);
            if (!force && sig == _sig) return;
            bool rerolled = _model != null && w.Run.Loot != null && _sig != null && w.Run.Loot.Rerolls != (_lastRerolls);
            _sig = sig;
            _model = LootPickModel.From(Ctx.Content, w.Run);
            _lastRerolls = w.Run.Loot != null ? w.Run.Loot.Rerolls : 0;
            if (rerolled) { _revealed = 0; _revealAcc = 0f; }
            Redraw(force);
        }

        int _lastRerolls;

        void Redraw(bool first)
        {
            var m = _model;
            _title.SetText(F("str.loot_pick.title", "pick_count", m.PickCount));
            _counter.SetText(F("str.loot_pick.pick_count", "picked", m.Picked, "max", m.PickCount));
            _purse.SetText(T("str.battle_hud.wallet") + " " + F("str.format.gold_amount", "n", N(m.Purse)));

            int keep = first ? -1 : Focus.Index;
            foreach (var b in _cardButtons) Focus.Remove(b);
            foreach (var b in _storageButtons) Focus.Remove(b);
            _cardButtons.Clear();
            _storageButtons.Clear();
            CampViews.Clear(_cards);
            CampViews.Clear(_storage);

            int count = m.Cards.Count + (m.BorrowedCard != null ? 1 : 0);
            float x0 = (UiScale.RefWidth - LootPickModel.RowWidth(count)) * 0.5f;
            for (int i = 0; i < m.Cards.Count; i++)
            {
                int idx = i;
                var b = BuildCard(m.Cards[i], x0 + i * (LootPickModel.CardWidth + LootPickModel.CardGap), () => Toggle(idx), true);
                _cardButtons.Add(b);
            }
            if (m.BorrowedCard != null)
                BuildCard(m.BorrowedCard, x0 + m.Cards.Count * (LootPickModel.CardWidth + LootPickModel.CardGap), null, false);

            bool overflow = m.Phase == LootPickPhase.OverflowDiscard;
            _overflowTitle.SetText(overflow ? T("str.loot_pick.overflow_title") + " " + F("str.loot_pick.discard_left", "n", m.OverflowToDiscard) : string.Empty);
            BuildStorage(overflow);

            _reroll.SetText(F("str.loot_pick.reroll_with_cost", "gold", N(m.RerollCost)));
            if (m.RerollEnabled) _reroll.SetEnabled(); else _reroll.SetDisabled(m.RerollDisabledReason);
            if (m.ConfirmEnabled) _confirm.SetEnabled(); else _confirm.SetDisabled(null);
            _reroll.gameObject.SetActive(!overflow);
            _confirm.gameObject.SetActive(!overflow);
            ApplyReveal();
            if (overflow && _storageButtons.Count > 0) Focus.SetCursor(_storageButtons[0], false);
            else if (keep >= 0 && keep < Focus.Items.Count) Focus.SetCursor(keep, false);
            else if (_cardButtons.Count > 0) Focus.SetCursor(_cardButtons[0], false);
        }

        UiButton BuildCard(LootCardView c, float x, System.Action onClick, bool focusable)
        {
            float w = LootPickModel.CardWidth, h = LootPickModel.CardHeight;
            var b = UiKit.Button(Ctx, _cards, null, x, 0f, w, h, onClick, UiButtonStyle.Plain, focusable ? Focus : null, "");
            b.name = c.Borrowed ? "BorrowedCard" : "Card" + c.Index;
            var cr = b.ContentRoot;
            UiKit.Panel(Ctx, cr, UiPanelKind.Board, 0f, 0f, w, h);
            // 연출: 카드가 켜질 때(차례 공개 포함) 아래에서 살짝 튀어 오른다.
            UiMotion.Entrance(b, c.Index * UiTheme.StaggerSeconds, UiTheme.PanelPopSeconds + 0.04f, new Vector2(0f, -36f), 0.9f);
            Color rc = CampViews.RarityColor(c.Rarity);
            if (c.Borrowed) CampViews.DashedFrame(cr, w, h, rc, 3, 12f, 8f);
            else UiFrame.Create(cr, rc, CampViews.RarityFrameThickness(c.Rarity) + 1);
            float y = 12f;
            if (c.KindKey != null) UiKit.LabelKey(Ctx, cr, c.KindKey, UiFontId.F12, 12f, y, 120f, 16f, UiTheme.SlateBlue);
            UiKit.LabelKey(Ctx, cr, c.GradeKey, UiFontId.F12, w - 132f, y, 120f, 16f, rc, TextAnchor.MiddleRight);
            y += 24f;
            UiKit.Icon(Ctx, cr, c.BlockId, 96, (w - 96f) * 0.5f, y);
            CampViews.ElementGlyph(Ctx, cr, c.Element, w - 32f, y);
            y += 104f;
            UiKit.Label(Ctx, cr, T(c.BlockId), UiFontId.F24, 8f, y, w - 16f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += 40f;
            for (int i = 0; i < c.StatLines.Count; i++)
            {
                UiKit.LabelKey(Ctx, cr, c.StatLines[i].Key, UiFontId.F16, 24f, y, 100f, 24f, UiTheme.SlateBlue);
                UiKit.Label(Ctx, cr, c.StatLines[i].Value, UiFontId.F16, w - 124f, y, 100f, 24f, UiTheme.Chalk, TextAnchor.MiddleRight);
                y += 24f;
            }
            if (c.Guaranteed) UiKit.LabelKey(Ctx, cr, "str.loot_pick.guaranteed", UiFontId.F12, 12f, h - 28f, 120f, 16f, UiTheme.SelectYellow);
            if (c.Borrowed)
            {
                UiKit.LabelKey(Ctx, cr, "str.loot_pick.borrowed", UiFontId.F16, w - 132f, h - 32f, 120f, 24f, rc, TextAnchor.MiddleRight);
                CampViews.BorrowedTag(Ctx, cr, 12f, h - 40f, 2); // 꼬리표 32×16을 2배로 왼쪽 아래 구석에
            }
            b.Picked = c.Picked;
            return b;
        }

        void BuildStorage(bool overflow)
        {
            var w = World;
            if (w == null) return;
            int cap = _model.StorageCap;
            float step = StorageW / Mathf.Max(1, cap);
            for (int i = 0; i < cap; i++)
            {
                var wb = i < w.Run.Storage.Count ? w.Run.Storage[i] : null;
                var item = wb != null ? new AssemblyItem { BlockId = wb.BlockId, Borrowed = wb.Borrowed, Wild = true } : default(AssemblyItem);
                int idx = i;
                var b = CampViews.BlockCell(Ctx, _storage, i * step, 0f, StorageCell, item, overflow && wb != null ? () => Discard(idx) : (System.Action)null,
                    overflow && wb != null ? Focus : null);
                if (overflow && wb != null)
                {
                    int gold = LootPickModel.DiscardGold(Ctx.Content, wb);
                    string text = F("str.loot_pick.overflow_gold", "gold", N(gold));
                    UiKit.AttachTooltip(b.gameObject, () => text);
                    UiKit.Label(Ctx, _storage, N(gold), UiFontId.F12, i * step, StorageCell + 2f, StorageCell, 16f, UiTheme.Gold, TextAnchor.MiddleCenter);
                    _storageButtons.Add(b);
                }
            }
        }

        void ApplyReveal()
        {
            for (int i = 0; i < _cardButtons.Count; i++) _cardButtons[i].gameObject.SetActive(i < _revealed);
        }

        void Toggle(int index)
        {
            var w = World;
            if (w == null || _model == null) return;
            RunCommand cmd;
            if (!_model.ToggleCommand(index, out cmd)) return;
            w.Enqueue(cmd);
            PlayUi(cmd.Kind == RunCommandKind.LootPick ? "ui.loot.pick" : "ui.loot.unpick");
        }

        void OnReroll()
        {
            var w = World;
            if (w == null || _model == null || !_model.RerollEnabled) return;
            w.Enqueue(new RunCommand(RunCommandKind.LootReroll));
            PlayUi("ui.loot.reroll");
        }

        void OnConfirm()
        {
            var w = World;
            if (w == null || _model == null || !_model.ConfirmEnabled) return;
            w.Enqueue(new RunCommand(RunCommandKind.LootConfirm));
            PlayUi(UiSounds.Press);
        }

        void Discard(int storageIndex)
        {
            var w = World;
            if (w == null) return;
            w.Enqueue(LootPickModel.DiscardCommand(storageIndex));
            PlayUi("ui.loot.discard");
        }
    }
}

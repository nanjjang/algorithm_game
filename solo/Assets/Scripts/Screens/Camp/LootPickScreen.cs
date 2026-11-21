using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 전리품 고르기(08 요약서 8.1절, 06 요약서 4.4절 + DataSolo/Loot.json 조정). 판 위에 뜨는 판(뒤를 60% 어둡게, 전장이 비침).
    /// 상태의 주인은 시뮬레이션(w.Run.Loot)이고 이 화면은 명령만 보낸다: LootPick/LootUnpick/LootReroll/LootSkip/LootDiscard/LootConfirm.
    /// 여닫기는 Battle 화면이 한다(PhaseChanged). 다시 뽑기는 이 화면에만 있다(D-16).
    /// 카드: 종류·등급 → 아이콘(별 겹치기면 ★) → 이름 → 열·피해·간격 → 효과 한 줄 → 맞물림 표식(처음 발견은 크게) → 끼웠을 때 미리보기.
    /// 등급 연출: 희귀는 파란 빛 번짐, 영웅은 큰 금빛 번짐 + 지나가는 빛 띠 + 뒤집힐 때 크게 튐.
    /// </summary>
    public sealed class LootPickScreen : ScreenBase
    {
        /// <summary>카드가 뒤집혀 뜨는 간격(08 8.1절 0.1초 = 6틱). 화면 60Hz 틱.</summary>
        const int RevealStepTicks = 6;
        /// <summary>
        /// 세로 배치(화면 좌표, 8px 리듬): 제목 56 → 고른 수·지갑 118 → 카드 168(높이 CardH) → 보조 단추 줄 → 보관함 구역.
        /// 가로는 카드 줄 폭에 맞춘다(보조 단추·보관함이 카드 줄과 같은 왼쪽·오른쪽 끝). 확인 = UiLayout.PrimaryRect.
        /// </summary>
        const float TitleY = 56f, CardsY = 168f, CardH = LootPickModel.CardHeight;
        /// <summary>보조 단추 폭: 셋이면 4장 카드 줄 폭과 같다.</summary>
        static readonly float ButtonW = (LootPickModel.RowWidth(4) - 2f * UiLayout.ColGap) / 3f;
        const float ButtonsY = CardsY + CardH + 24f, StorageSectionY = ButtonsY + UiLayout.SecondaryH + 24f + 24f, StorageCell = 56f;
        /// <summary>등급 빛 번짐의 여백(px)과 세기. 화면 연출 상수.</summary>

        public override ScreenId Id => ScreenId.LootPick;
        public override ScreenLayer Layer => ScreenLayer.Panel;
        public override bool BlocksInput => true;
        protected override bool HasFrame => false;

        UiLabel _title, _counter, _purse, _overflowTitle, _rerollNext;
        UiButton _reroll, _confirm, _skipGold, _skipHeal;
        RectTransform _cards, _storage;
        UiButton _details;
        bool _showDetails;
        readonly List<UiButton> _cardButtons = new List<UiButton>();
        readonly List<UiButton> _storageButtons = new List<UiButton>();
        LootPickModel _model;
        string _sig;
        int _revealed;
        float _revealAcc;
        int _lastRerolls;
        /// <summary>카드 등장 연출을 다음 그리기에 한 번 돌린다(처음 열 때·다시 뽑기 뒤). 고르기·지갑 변화로 다시 그릴 때는 튀지 않는다.</summary>
        bool _animate;

        SimWorld World => Ctx != null && Ctx.Session != null ? Ctx.Session.MainWorld ?? Ctx.Session.World : null;

        /// <summary>이 화면의 주인 = 이 기기의 나(솔로 = 슬롯 0, 협동 = 내 슬롯). 협동에서는 사람마다 자기 제시를 따로 고른다.</summary>
        PlayerState Player
        {
            get
            {
                var w = World;
                if (w == null) return null;
                var me = w.Me;
                if (me != null && me.Active) return me;
                for (int i = 0; i < w.Players.Length; i++) if (w.Players[i].Active) return w.Players[i];
                return null;
            }
        }

        /// <summary>내 보상 상태(제시·보관함).</summary>
        static SlotReward Mine(SimWorld w, PlayerState p) => w.Run.Rewards[p != null && p.Slot >= 0 && p.Slot < w.Run.Rewards.Length ? p.Slot : 0];

        /// <summary>내가 고르기를 끝내고 동료를 기다리는 중인가(협동). n = 끝낸 사람, m = 고르는 사람 전체.</summary>
        static bool WaitingForParty(SimWorld w, PlayerState p, out int n, out int m)
        {
            n = m = 0;
            if (w == null || w.Run.Phase != RunPhase.LootPick || !Mine(w, p).LootDone) return false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var q = w.Players[i];
                if (!q.Active || q.Left) continue;
                m++;
                if (w.Run.Rewards[i].LootDone) n++;
            }
            return true;
        }

        /// <summary>내 슬롯을 찍어 보낸다(온라인은 서버가 다시 찍는다).</summary>
        void Send(SimWorld w, RunCommand cmd)
        {
            var p = Player;
            if (p != null) cmd.Slot = p.Slot;
            w.Enqueue(cmd);
        }

        bool _waiting;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.ModalDim, true, "Dim");
            UiBackdrop.VignetteOver(content, 0.7f);
            // 머리: 큰 제목 한 줄 + 그 아래 가운데 한 줄에 [고른 수] ◆ [지갑]. 흩어진 글을 한 덩어리로 묶는다.
            _title = UiKit.Label(Ctx, content, string.Empty, UiFontId.F48, 560f, TitleY, 800f, 56f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_title, 3f);
            float chipY = TitleY + 62f;
            _counter = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 560f, chipY, 380f, 32f, UiTheme.SelectYellow, TextAnchor.MiddleRight);
            UiKit.Diamond(content, UiScale.RefWidth * 0.5f, chipY + 16f, 10f, UiTheme.Brass);
            UiKit.Icon(Ctx, content, Ids.Item.Purse, 24, 980f, chipY + 4f);
            _purse = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 1012f, chipY, 360f, 32f, UiTheme.Gold, TextAnchor.MiddleLeft);
            _cards = UiKit.Node(content, "Cards", 0f, CardsY, UiScale.RefWidth, CardH + 40f);

            // 카드 아래: 덜 중요한 선택 셋(건너뛰기·다시 뽑기·건너뛰기)은 보조 단추 한 줄. 자리는 Redraw가 카드 줄 폭에 맞춘다.
            _skipGold = UiLayout.SecondaryButton(Ctx, content, string.Empty, 0f, ButtonsY, ButtonW, () => OnSkip(false), Focus);
            _reroll = UiLayout.SecondaryButton(Ctx, content, T("str.loot_pick.reroll"), 0f, ButtonsY, ButtonW, OnReroll, Focus);
            _skipHeal = UiLayout.SecondaryButton(Ctx, content, string.Empty, 0f, ButtonsY, ButtonW, () => OnSkip(true), Focus);
            _rerollNext = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 0f, ButtonsY + UiLayout.SecondaryH + 2f, 320f, 24f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);

            // 넘침 안내: 넘침 때는 보조 단추 줄이 숨으므로 그 자리에 크게.
            _overflowTitle = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, UiLayout.Outer, ButtonsY + 16f, UiLayout.InnerW, 32f, UiTheme.Amber, TextAnchor.MiddleCenter);
            // 보관함: 구역 이름(Section) + 칸 한 줄. 자리는 Redraw가 카드 줄 폭에 맞춘다.
            _storageSection = UiKit.Node(content, "StorageSection", 0f, StorageSectionY, 400f, UiLayout.SectionH);
            _storage = UiKit.Node(content, "Storage", 0f, StorageSectionY + UiLayout.SectionH, 400f, StorageCell + 24f);
            // 주 행동 하나: 확인은 늘 같은 자리(PrimaryRect). 보관함 칸은 그 왼쪽에서 끝난다.
            _details = UiLayout.SecondaryButton(Ctx, content, T("str.ui.details"), UiLayout.Outer, chipY - 8f,
                184f, () =>
                {
                    _showDetails = !_showDetails;
                    _details.SetText(T(_showDetails ? "str.ui.hide_details" : "str.ui.details"));
                    Redraw(false);
                }, Focus, 48f);
            _confirm = UiLayout.PrimaryButton(Ctx, content, T("str.loot_pick.confirm"), OnConfirm, Focus, 0f);
        }

        RectTransform _storageSection;
        float _rowX, _rowW;

        /// <summary>카드 줄 폭에 보조 단추 줄·보관함을 맞춘다.</summary>
        void LayoutRows(int cardCount)
        {
            _rowW = LootPickModel.RowWidth(Mathf.Max(3, cardCount));
            _rowX = (UiScale.RefWidth - _rowW) * 0.5f;
            float gap = UiLayout.ColGap, bw = ButtonW;
            bool gold = _skipGold.gameObject.activeSelf, heal = _skipHeal.gameObject.activeSelf, reroll = _reroll.gameObject.activeSelf;
            int n = (reroll ? 1 : 0) + (gold ? 1 : 0) + (heal ? 1 : 0);
            float x = (UiScale.RefWidth - (n * bw + (n - 1) * gap)) * 0.5f;
            float hy = ButtonsY + UiLayout.SecondaryH + 2f;
            if (gold) { MoveX(_skipGold.Rect, x); x += bw + gap; }
            if (reroll) { MoveX(_reroll.Rect, x); UiKit.Place(_rerollNext.Rect, x, hy, bw, 24f); x += bw + gap; }
            if (heal) MoveX(_skipHeal.Rect, x);
            float sw = Mathf.Min(_rowW, UiLayout.PrimaryRect.x - UiLayout.ColGap - _rowX);
            CampViews.Clear(_storageSection);
            UiKit.Place(_storageSection, _rowX, StorageSectionY, sw, UiLayout.SectionH);
            UiLayout.Section(Ctx, _storageSection, 0f, 0f, sw, T("str.camp.storage"));
            UiKit.Place(_storage, _rowX, StorageSectionY + UiLayout.SectionH, sw, StorageCell + 24f);
        }

        static void MoveX(RectTransform rt, float x) => rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _showDetails = false;
            _details.SetText(T("str.ui.details"));
            _revealed = 0;
            _revealAcc = 0f;
            _sig = null;
            _animate = true;
            Refresh(true);
        }

        public override bool OnBack() => true;   // 고르기를 끝내야 닫힌다(Esc로 건너뛰지 않는다 — 건너뛰기는 단추로만).

        public override bool OnMenuKey(MenuKey key)
        {
            if (_waiting) return true;
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
            if (!_waiting && _model != null && _revealed < _model.Cards.Count)
            {
                _revealAcc += dt;
                while (_revealAcc >= RevealStepTicks * SimConst.TickSeconds && _revealed < _model.Cards.Count)
                {
                    _revealAcc -= RevealStepTicks * SimConst.TickSeconds;
                    _revealed++;
                    OnCardRevealed(_revealed - 1);
                    ApplyReveal();
                }
            }
            Refresh(false);
        }

        /// <summary>카드 하나가 뒤집힐 때: 등급·맞물림에 따라 소리와 튐을 키운다.</summary>
        void OnCardRevealed(int i)
        {
            if (_model == null || i < 0 || i >= _model.Cards.Count) return;
            var c = _model.Cards[i];
            if (c.Rarity == Ids.Rarity.Hero) PlayUi("ui.loot.reveal_hero");
            else if (c.Rarity == Ids.Rarity.Rare) PlayUi("ui.loot.reveal_rare");
            else PlayUi("ui.loot.card_flip");
            if (c.SynergyFirst) PlayUi("ui.loot.synergy_new");
            if (i < _cardButtons.Count && c.Rarity == Ids.Rarity.Hero) UiMotion.Pop(_cardButtons[i], 1.12f, 0.32f);
        }

        static string Signature(SimWorld w, PlayerState p)
        {
            var run = w.Run;
            int n, m;
            if (WaitingForParty(w, p, out n, out m)) return "wait|" + n + "/" + m;
            var mine = Mine(w, p);
            var o = mine.Loot;
            if (o == null) return "none";
            var sb = new System.Text.StringBuilder();
            sb.Append(run.Purse).Append('|').Append(mine.Storage.Count).Append('|').Append(o.OverflowToDiscard).Append('|').Append(o.Rerolls).Append('|');
            sb.Append(p != null ? p.Hp : 0).Append('|');
            for (int i = 0; i < o.Cards.Count; i++) sb.Append(o.Cards[i].BlockId).Append(o.Cards[i].Picked ? '+' : '-');
            for (int i = 0; i < mine.Storage.Count; i++) sb.Append(mine.Storage[i].Uid).Append(',');
            return sb.ToString();
        }

        void Refresh(bool force)
        {
            var w = World;
            if (w == null) return;
            var p = Player;
            string sig = Signature(w, p);
            if (!force && sig == _sig) return;
            int doneN, allN;
            _waiting = WaitingForParty(w, p, out doneN, out allN);
            if (_waiting)
            {
                _sig = sig;
                RedrawWaiting(doneN, allN);
                return;
            }
            var mineLoot = Mine(w, p).Loot;
            bool rerolled = _model != null && mineLoot != null && _sig != null && mineLoot.Rerolls != _lastRerolls;
            _sig = sig;
            _model = LootPickModel.From(Ctx.Content, w.Run, Ctx.Rules, p);
            _lastRerolls = mineLoot != null ? mineLoot.Rerolls : 0;
            if (rerolled) { _revealed = 0; _revealAcc = 0f; _animate = true; }
            Redraw(force);
            _animate = false;
        }

        /// <summary>협동: 나는 다 골랐고 동료가 고르는 중. 카드·단추를 걷고 기다림 한 줄만 둔다(다 고르면 Battle이 화면을 닫는다).</summary>
        void RedrawWaiting(int done, int all)
        {
            foreach (var b in _cardButtons) Focus.Remove(b);
            foreach (var b in _storageButtons) Focus.Remove(b);
            _cardButtons.Clear();
            _storageButtons.Clear();
            CampViews.Clear(_cards);
            CampViews.Clear(_storage);
            CampViews.Clear(_storageSection);
            _title.SetText(F("str.coop.wait_party_loot", "n", done, "m", all));
            _counter.SetText(string.Empty);
            _overflowTitle.SetText(string.Empty);
            _reroll.gameObject.SetActive(false);
            _confirm.gameObject.SetActive(false);
            _details.gameObject.SetActive(false);
            _skipGold.gameObject.SetActive(false);
            _skipHeal.gameObject.SetActive(false);
            _rerollNext.gameObject.SetActive(false);
        }

        void Redraw(bool first)
        {
            var m = _model;
            _title.SetText(F("str.loot_pick.title", "pick_count", m.PickCount));
            _counter.SetText(F("str.loot_pick.pick_count", "picked", m.Picked, "max", m.PickCount));
            _counter.Color = m.Picked >= m.PickCount ? UiTheme.Good : UiTheme.SelectYellow;
            _purse.SetText(F("str.format.gold_amount", "n", N(m.Purse)));

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
            _rerollNext.SetText(m.NextRerollCost > m.RerollCost ? F("str.loot_pick.reroll_next", "gold", N(m.NextRerollCost)) : string.Empty);
            if (m.RerollEnabled) _reroll.SetEnabled(); else _reroll.SetDisabled(m.RerollDisabledReason);
            _skipGold.SetText(F("str.loot_pick.skip_gold", "gold", N(m.SkipGold)));
            if (m.SkipGoldEnabled) _skipGold.SetEnabled(); else _skipGold.SetDisabled(null);
            _skipHeal.SetText(F("str.loot_pick.skip_heal", "n", m.SkipHealCells));
            if (m.SkipHealEnabled) _skipHeal.SetEnabled(); else _skipHeal.SetDisabled(m.PlayerHurt ? null : "str.loot_pick.skip_heal_full");
            if (m.ConfirmEnabled) _confirm.SetEnabled(); else _confirm.SetDisabled(null);
            _reroll.gameObject.SetActive(!overflow);
            _confirm.gameObject.SetActive(!overflow);
            _details.gameObject.SetActive(!overflow);
            _skipGold.gameObject.SetActive(!overflow && m.SkipGold > 0);
            _skipHeal.gameObject.SetActive(!overflow && m.SkipHealCells > 0);
            _rerollNext.gameObject.SetActive(!overflow);
            LayoutRows(count);
            ApplyReveal();
            if (overflow && _storageButtons.Count > 0) Focus.SetCursor(_storageButtons[0], false);
            else if (keep >= 0 && keep < Focus.Items.Count) Focus.SetCursor(keep, false);
            else if (_cardButtons.Count > 0) Focus.SetCursor(_cardButtons[0], false);
        }

        UiButton BuildCard(LootCardView c, float x, System.Action onClick, bool focusable)
        {
            float w = LootPickModel.CardWidth, h = CardH;
            Color rc = CampViews.RarityColor(c.Rarity);
            var b = UiKit.Button(Ctx, _cards, null, x, 0f, w, h, onClick, UiButtonStyle.Plain, focusable ? Focus : null, "");
            b.name = c.Borrowed ? "BorrowedCard" : "Card" + c.Index;
            var cr = b.ContentRoot;
            UiKit.Panel(Ctx, cr, UiPanelKind.Board, 0f, 0f, w, h);
            // 연출: 카드가 켜질 때(차례 공개 포함) 아래에서 살짝 튀어 오른다.
            if (_animate) UiMotion.Entrance(b, c.Index * UiTheme.StaggerSeconds, UiTheme.PanelPopSeconds + 0.04f, new Vector2(0f, -12f), 1f);
            if (c.Borrowed) CampViews.DashedFrame(cr, w, h, rc, 3, 12f, 8f);
            else UiKit.Rect(cr, 0f, 0f, w, CampViews.RarityFrameThickness(c.Rarity), rc, false, "RarityRule");

            const float P = 16f, RowH = 28f, IconPx = 72f;
            float halfW = w * 0.5f - P;
            float y = 8f;
            if (c.KindKey != null) UiKit.LabelKey(Ctx, cr, c.KindKey, UiFontId.F24, P, y, halfW, RowH, UiTheme.Paper);
            if (c.Rarity != null && c.Rarity != Ids.Rarity.Common)
                UiKit.LabelKey(Ctx, cr, c.GradeKey, UiFontId.F24, w * 0.5f, y, halfW, RowH, rc, TextAnchor.MiddleRight);
            y += RowH + 4f;
            UiKit.Icon(Ctx, cr, c.BlockId, (int)IconPx, (w - IconPx) * 0.5f, y);
            CampViews.ElementGlyph(Ctx, cr, c.Element, w - 36f, y);
            if (c.StarTarget > 0) CampViews.StarBadge(Ctx, cr, P, y + 4f, c.StarTarget, 20f);
            y += IconPx;
            UiKit.Label(Ctx, cr, T(c.BlockId), UiFontId.F32, P, y, w - 2f * P, 40f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += 44f;
            for (int i = 0; _showDetails && i < c.StatLines.Count; i++)
            {
                UiKit.LabelKey(Ctx, cr, c.StatLines[i].Key, UiFontId.F24, P, y, halfW, RowH, UiTheme.Paper);
                UiKit.Label(Ctx, cr, c.StatLines[i].Value, UiFontId.F24, w * 0.5f, y, halfW, RowH, UiTheme.Chalk, TextAnchor.MiddleRight);
                y += RowH;
            }

            float textW = w - 2f * P;
            y = Mathf.Max(y, 188f) + 8f;
            if (c.Desc.Key != null)
                y += CardParagraph(cr, c.Desc.Format(Ctx.Names), P, y, textW, UiTheme.Chalk) + 8f;
            if (c.OnCore.Key != null)
                y += CardParagraph(cr, c.OnCore.Format(Ctx.Names), P, y, textW, UiTheme.Good) + 8f;
            if (_showDetails && c.StarTarget > 0)
            {
                string star = c.StarLine.Key != null ? c.StarLine.Format(Ctx.Names) : F("str.loot_pick.star_up", "n", c.StarTarget);
                y += CardParagraph(cr, star, P, y, textW, UiTheme.SelectYellow) + 4f;
            }

            if (_showDetails && c.Synergy != LootSynergy.None)
            {
                string partner = c.SynergyPartnerId != null ? T(c.SynergyPartnerId) : string.Empty;
                string tag = F(c.Synergy == LootSynergy.Pair ? "str.loot_pick.synergy.pair" : "str.loot_pick.synergy.element", "partner", partner);
                if (c.SynergyFirst)
                {
                    tag = T("str.loot_pick.synergy.new") + "\n" + tag;
                    var band = UiKit.Rect(cr, 8f, y - 2f, w - 16f, 1f, UiTheme.WithAlpha(UiTheme.Good, 0.22f), false, "SynergyBand");
                    float tagH = CardParagraph(cr, tag, P, y, textW, UiTheme.Good);
                    UiKit.Place(band.rectTransform, 8f, y - 2f, w - 16f, tagH + 4f);
                    UiPulse.Attach(band, 0.10f, 0.30f, 1.2f);
                    y += tagH + 4f;
                }
                else
                {
                    y += CardParagraph(cr, tag, P, y, textW, UiTheme.Good) + 4f;
                }
            }
            if (_showDetails || c.PreviewKind == LootPreviewKind.NoSlot)
                BuildPreview(cr, c, w, Mathf.Max(y + 4f, h - 132f));

            if (c.Guaranteed) UiKit.LabelKey(Ctx, cr, "str.loot_pick.guaranteed", UiFontId.F16, 12f, h - 28f, 120f, 24f, UiTheme.SelectYellow);
            if (c.Borrowed)
            {
                UiKit.LabelKey(Ctx, cr, "str.loot_pick.borrowed", UiFontId.F16, w - 132f, h - 26f, 120f, 22f, rc, TextAnchor.MiddleRight);
                CampViews.BorrowedTag(Ctx, cr, 12f, h - 36f, 2); // 꼬리표 32×16을 2배로 왼쪽 아래 구석에
            }
            b.Picked = c.Picked;
            return b;
        }

        float CardParagraph(RectTransform parent, string text, float x, float y, float w, Color color)
        {
            var label = UiKit.Label(Ctx, parent, text, UiFontId.F24, x, y, w, 32f, color, TextAnchor.UpperLeft, wrap: true);
            float h = Mathf.Max(28f, label.Text.preferredHeight);
            UiKit.Place(label.Rect, x, y, w, h);
            return h;
        }

        /// <summary>끼웠을 때 미리보기 두 줄: 어디에(빈 칸 / 대신 / 별) + 탄 수·열·허수아비 DPS 변화.</summary>
        void BuildPreview(RectTransform cr, LootCardView c, float w, float y)
        {
            if (c.PreviewKind == LootPreviewKind.None) return;
            UiKit.Rect(cr, 8f, y, w - 16f, 1f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.6f), false, "PreviewRule");
            y += 4f;
            if (c.PreviewKind == LootPreviewKind.NoSlot)
            {
                CardParagraph(cr, T("str.loot_pick.preview.none"), 16f, y, w - 32f, UiTheme.Paper);
                return;
            }
            string slot = c.PreviewSlot == 0 ? T("str.loot_pick.preview.core") : F("str.loot_pick.preview.slot_n", "n", c.PreviewSlot);
            string where;
            if (c.PreviewKind == LootPreviewKind.Star) where = F("str.loot_pick.preview.star", "slot", slot, "n", c.StarTarget);
            else if (c.PreviewKind == LootPreviewKind.Replace) where = F("str.loot_pick.preview.replace", "slot", slot, "name", T(c.ReplacedId));
            else where = F("str.loot_pick.preview.slot", "slot", slot);
            y += CardParagraph(cr, where, 16f, y, w - 32f, UiTheme.Paper) + 4f;

            int dShots = c.ShotsAfter - c.ShotsBefore;
            float dHeat = c.HeatAfter - c.HeatBefore;
            float pct = c.DpsPct;
            // 탄 수가 그대로면 그 항목을 뺀다("탄 0"은 아무것도 알려 주지 않는다).
            // 허수아비로 가치가 안 잡히는 효과(튕김·냉기·유도·관통·적중 시 발동)는 오르지 않은 DPS를 음수 % 대신 꼬리표로 보인다.
            bool blind = pct <= 0.5f && BlockDescriber.DummyBlind(Ctx.Content != null ? Ctx.Content.Block(c.BlockId) : null);
            string sep = T("str.block_desc.sep");
            var sb = new System.Text.StringBuilder();
            if (dShots != 0) sb.Append(F("str.loot_pick.preview.shots", "shots", Signed(dShots)));
            if (Mathf.Abs(dHeat) >= 0.05f)
            {
                if (sb.Length > 0) sb.Append(sep);
                sb.Append(F("str.loot_pick.preview.heat", "heat", SignedOne(dHeat)));
            }
            if (blind || Mathf.RoundToInt(pct) != 0)
            {
                if (sb.Length > 0) sb.Append(sep);
                sb.Append(blind
                    ? T("str.loot_pick.preview.dps_blind")
                    : F("str.loot_pick.preview.dps", "dps", F("str.format.percent", "n", SignedPct(pct))));
            }
            string line = sb.Length > 0 ? sb.ToString() : T("str.loot_pick.preview.unchanged");
            Color col = blind ? UiTheme.Chalk : pct > 0.5f ? UiTheme.Good : pct < -0.5f ? UiTheme.Amber : UiTheme.Chalk;
            CardParagraph(cr, line, 16f, y, w - 32f, col);
        }

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        static string Signed(int v) => (v > 0 ? "+" : string.Empty) + v.ToString(Inv);
        static string SignedOne(float v) => (v > 0.05f ? "+" : string.Empty) + CampFormat.OneDecimal(v);
        static string SignedPct(float v)
        {
            int r = Mathf.RoundToInt(v);
            return (r > 0 ? "+" : string.Empty) + r.ToString(Inv);
        }

        void BuildStorage(bool overflow)
        {
            var w = World;
            if (w == null) return;
            int cap = _model.StorageCap;
            float step = Mathf.Min(StorageCell + 8f, _storage.rect.width / Mathf.Max(1, cap));
            for (int i = 0; i < cap; i++)
            {
                var storage = Mine(w, Player).Storage;
                var wb = i < storage.Count ? storage[i] : null;
                var item = wb != null ? new AssemblyItem { BlockId = wb.BlockId, Borrowed = wb.Borrowed, Wild = true, Stars = wb.Stars } : default(AssemblyItem);
                int idx = i;
                var b = CampViews.BlockCell(Ctx, _storage, i * step, 0f, StorageCell, item, overflow && wb != null ? () => Discard(idx) : (System.Action)null,
                    overflow && wb != null ? Focus : null);
                if (overflow && wb != null)
                {
                    int gold = LootPickModel.DiscardGold(Ctx.Content, wb);
                    string text = F("str.loot_pick.overflow_gold", "gold", N(gold));
                    UiKit.AttachTooltip(b.gameObject, () => text);
                    UiKit.Label(Ctx, _storage, N(gold), UiFontId.F16, i * step, StorageCell + 2f, StorageCell, 22f, UiTheme.Gold, TextAnchor.MiddleCenter);
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
            Send(w, cmd);
            bool star = cmd.Kind == RunCommandKind.LootPick && index < _model.Cards.Count && _model.Cards[index].StarTarget > 0;
            PlayUi(cmd.Kind == RunCommandKind.LootPick ? (star ? "ui.loot.star_up" : "ui.loot.pick") : "ui.loot.unpick");
        }

        void OnReroll()
        {
            var w = World;
            if (w == null || _model == null || !_model.RerollEnabled) return;
            Send(w, new RunCommand(RunCommandKind.LootReroll));
            PlayUi("ui.loot.reroll");
        }

        void OnSkip(bool heal)
        {
            var w = World;
            var p = Player;
            if (w == null || _model == null || p == null) return;
            if (heal ? !_model.SkipHealEnabled : !_model.SkipGoldEnabled) return;
            w.Enqueue(LootPickModel.SkipCommand(heal, p.Slot));
            PlayUi(heal ? "ui.camp.heal" : "ui.loot.skip");
        }

        void OnConfirm()
        {
            var w = World;
            if (w == null || _model == null || !_model.ConfirmEnabled) return;
            Send(w, new RunCommand(RunCommandKind.LootConfirm));
            PlayUi(UiSounds.Press);
        }

        void Discard(int storageIndex)
        {
            var w = World;
            if (w == null) return;
            Send(w, LootPickModel.DiscardCommand(storageIndex));
            PlayUi("ui.loot.discard");
        }
    }
}

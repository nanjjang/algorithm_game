using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

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
        /// <summary>보관함 줄(08 8.1절 (576,896) 768×64, 12칸). 화면 배치 상수.</summary>
        const float StorageX = 576f, StorageY = 896f, StorageW = 768f, StorageCell = 56f;
        /// <summary>카드 줄의 윗변과 단추 줄(다시 뽑기 가운데, 건너뛰기 양옆). 화면 배치 상수.</summary>
        const float CardsY = 232f, ButtonsY = 712f, ButtonW = 360f, ButtonH = 64f, ButtonGap = 24f;
        /// <summary>등급 빛 번짐의 여백(px)과 세기. 화면 연출 상수.</summary>
        const float RareGlowPad = 36f, HeroGlowPad = 96f, RareGlowAlpha = 0.30f, HeroGlowAlpha = 0.55f;

        public override ScreenId Id => ScreenId.LootPick;
        public override ScreenLayer Layer => ScreenLayer.Panel;
        public override bool BlocksInput => true;
        protected override bool HasFrame => false;

        UiLabel _title, _counter, _purse, _overflowTitle, _rerollNext, _skipHint;
        UiButton _reroll, _confirm, _skipGold, _skipHeal;
        RectTransform _cards, _storage;
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

        PlayerState Player
        {
            get
            {
                var w = World;
                if (w == null) return null;
                for (int i = 0; i < w.Players.Length; i++) if (w.Players[i].Active) return w.Players[i];
                return null;
            }
        }

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.ModalDim, true, "Dim");
            UiBackdrop.VignetteOver(content, 0.7f);
            UiBackdrop.Glow(content, 460f, 60f, 1000f, 220f, UiTheme.WithAlpha(UiTheme.Brass, 0.16f));
            _title = UiKit.Label(Ctx, content, string.Empty, UiFontId.F48, 560f, 100f, 800f, 56f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_title, 3f);
            UiKit.AccentRule(content, 760f, 158f, 400f);
            _counter = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 760f, 164f, 400f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _purse = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 1400f, 100f, 400f, 32f, UiTheme.Gold, TextAnchor.MiddleRight);
            _overflowTitle = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, 360f, 848f, 1200f, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            _cards = UiKit.Node(content, "Cards", 0f, CardsY, UiScale.RefWidth, LootPickModel.CardHeight + 40f);

            float bx = (UiScale.RefWidth - (ButtonW * 3f + ButtonGap * 2f)) * 0.5f;
            _skipGold = UiKit.Button(Ctx, content, string.Empty, bx, ButtonsY, ButtonW, ButtonH, () => OnSkip(false), UiButtonStyle.Small, Focus, "");
            _reroll = UiKit.Button(Ctx, content, T("str.loot_pick.reroll"), bx + ButtonW + ButtonGap, ButtonsY, ButtonW, ButtonH, OnReroll, UiButtonStyle.Large, Focus, "");
            _skipHeal = UiKit.Button(Ctx, content, string.Empty, bx + (ButtonW + ButtonGap) * 2f, ButtonsY, ButtonW, ButtonH, () => OnSkip(true), UiButtonStyle.Small, Focus, "");
            _rerollNext = UiKit.Label(Ctx, content, string.Empty, UiFontId.F12, bx + ButtonW + ButtonGap, ButtonsY + ButtonH + 2f, ButtonW, 18f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            _skipHint = UiKit.LabelKey(Ctx, content, "str.loot_pick.skip_hint", UiFontId.F12, bx, ButtonsY + ButtonH + 2f, ButtonW, 18f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);

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
            _animate = true;
            Refresh(true);
        }

        public override bool OnBack() => true;   // 고르기를 끝내야 닫힌다(Esc로 건너뛰지 않는다 — 건너뛰기는 단추로만).

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

        static string Signature(RunState run, PlayerState p)
        {
            var o = run.Loot;
            if (o == null) return "none";
            var sb = new System.Text.StringBuilder();
            sb.Append(run.Purse).Append('|').Append(run.Storage.Count).Append('|').Append(o.OverflowToDiscard).Append('|').Append(o.Rerolls).Append('|');
            sb.Append(p != null ? p.Hp : 0).Append('|');
            for (int i = 0; i < o.Cards.Count; i++) sb.Append(o.Cards[i].BlockId).Append(o.Cards[i].Picked ? '+' : '-');
            for (int i = 0; i < run.Storage.Count; i++) sb.Append(run.Storage[i].Uid).Append(',');
            return sb.ToString();
        }

        void Refresh(bool force)
        {
            var w = World;
            if (w == null) return;
            var p = Player;
            string sig = Signature(w.Run, p);
            if (!force && sig == _sig) return;
            bool rerolled = _model != null && w.Run.Loot != null && _sig != null && w.Run.Loot.Rerolls != _lastRerolls;
            _sig = sig;
            _model = LootPickModel.From(Ctx.Content, w.Run, Ctx.Rules, p);
            _lastRerolls = w.Run.Loot != null ? w.Run.Loot.Rerolls : 0;
            if (rerolled) { _revealed = 0; _revealAcc = 0f; _animate = true; }
            Redraw(force);
            _animate = false;
        }

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
            _rerollNext.SetText(m.NextRerollCost > m.RerollCost ? F("str.loot_pick.reroll_next", "gold", N(m.NextRerollCost)) : string.Empty);
            if (m.RerollEnabled) _reroll.SetEnabled(); else _reroll.SetDisabled(m.RerollDisabledReason);
            _skipGold.SetText(F("str.loot_pick.skip_gold", "gold", N(m.SkipGold)));
            if (m.SkipGoldEnabled) _skipGold.SetEnabled(); else _skipGold.SetDisabled(null);
            _skipHeal.SetText(F("str.loot_pick.skip_heal", "n", m.SkipHealCells));
            if (m.SkipHealEnabled) _skipHeal.SetEnabled(); else _skipHeal.SetDisabled(m.PlayerHurt ? null : "str.loot_pick.skip_heal_full");
            if (m.ConfirmEnabled) _confirm.SetEnabled(); else _confirm.SetDisabled(null);
            _reroll.gameObject.SetActive(!overflow);
            _confirm.gameObject.SetActive(!overflow);
            _skipGold.gameObject.SetActive(!overflow && m.SkipGold > 0);
            _skipHeal.gameObject.SetActive(!overflow && m.SkipHealCells > 0);
            _skipHint.gameObject.SetActive(!overflow && (m.SkipGold > 0 || m.SkipHealCells > 0));
            _rerollNext.gameObject.SetActive(!overflow);
            ApplyReveal();
            if (overflow && _storageButtons.Count > 0) Focus.SetCursor(_storageButtons[0], false);
            else if (keep >= 0 && keep < Focus.Items.Count) Focus.SetCursor(keep, false);
            else if (_cardButtons.Count > 0) Focus.SetCursor(_cardButtons[0], false);
        }

        UiButton BuildCard(LootCardView c, float x, System.Action onClick, bool focusable)
        {
            float w = LootPickModel.CardWidth, h = LootPickModel.CardHeight;
            Color rc = CampViews.RarityColor(c.Rarity);
            // 등급 빛 번짐(카드 뒤). 희귀 = 파랑, 영웅 = 크고 진한 금빛. 일반은 없다.
            bool hero = c.Rarity == Ids.Rarity.Hero, rare = c.Rarity == Ids.Rarity.Rare;
            UImage glow = null;
            if (hero || rare)
            {
                float pad = hero ? HeroGlowPad : RareGlowPad;
                glow = UiBackdrop.Glow(_cards, x - pad, -pad * 0.6f, w + pad * 2f, h + pad * 1.2f, UiTheme.WithAlpha(rc, hero ? HeroGlowAlpha : RareGlowAlpha));
                glow.name = "RarityGlow";
                glow.raycastTarget = false;
                UiPulse.Attach(glow, (hero ? HeroGlowAlpha : RareGlowAlpha) * 0.45f, hero ? HeroGlowAlpha : RareGlowAlpha, hero ? 1.4f : 2.0f);
            }

            var b = UiKit.Button(Ctx, _cards, null, x, 0f, w, h, onClick, UiButtonStyle.Plain, focusable ? Focus : null, "");
            b.name = c.Borrowed ? "BorrowedCard" : "Card" + c.Index;
            if (glow != null) glow.transform.SetParent(b.transform, true);   // 카드와 같이 켜지고 꺼진다(차례 공개)
            if (glow != null) glow.transform.SetAsFirstSibling();
            var cr = b.ContentRoot;
            UiKit.Panel(Ctx, cr, UiPanelKind.Board, 0f, 0f, w, h);
            // 연출: 카드가 켜질 때(차례 공개 포함) 아래에서 살짝 튀어 오른다.
            if (_animate) UiMotion.Entrance(b, c.Index * UiTheme.StaggerSeconds, UiTheme.PanelPopSeconds + 0.04f, new Vector2(0f, -36f), 0.9f);
            if (c.Borrowed) CampViews.DashedFrame(cr, w, h, rc, 3, 12f, 8f);
            else UiFrame.Create(cr, rc, CampViews.RarityFrameThickness(c.Rarity) + 1);
            if (hero) UiFrame.Create(cr, UiTheme.WithAlpha(UiTheme.Gold, 0.45f), 1).name = "HeroInnerFrame";
            if (hero || rare) AddShine(cr, w, h, hero, c.Index);

            float y = 12f;
            if (c.KindKey != null) UiKit.LabelKey(Ctx, cr, c.KindKey, UiFontId.F12, 12f, y, 120f, 16f, UiTheme.SlateBlue);
            UiKit.LabelKey(Ctx, cr, c.GradeKey, UiFontId.F12, w - 132f, y, 120f, 16f, rc, TextAnchor.MiddleRight);
            y += 22f;
            UiKit.Icon(Ctx, cr, c.BlockId, 96, (w - 96f) * 0.5f, y);
            CampViews.ElementGlyph(Ctx, cr, c.Element, w - 32f, y);
            if (c.StarTarget > 0) CampViews.StarBadge(Ctx, cr, 12f, y + 4f, c.StarTarget, 20f);
            y += 100f;
            UiKit.Label(Ctx, cr, T(c.BlockId), UiFontId.F24, 8f, y, w - 16f, 30f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += 32f;
            for (int i = 0; i < c.StatLines.Count; i++)
            {
                UiKit.LabelKey(Ctx, cr, c.StatLines[i].Key, UiFontId.F16, 24f, y, 100f, 22f, UiTheme.SlateBlue);
                UiKit.Label(Ctx, cr, c.StatLines[i].Value, UiFontId.F16, w - 124f, y, 100f, 22f, UiTheme.Chalk, TextAnchor.MiddleRight);
                y += 22f;
            }
            y = Mathf.Max(y, 254f);
            // 효과 한 줄(+ 별 겹치기면 그 별의 몫)
            string desc = c.Desc.Key != null ? c.Desc.Format(Ctx.Names) : string.Empty;
            UiKit.Label(Ctx, cr, desc, UiFontId.F12, 12f, y, w - 24f, 44f, UiTheme.Chalk, TextAnchor.UpperLeft, wrap: true);
            y += 46f;
            if (c.StarTarget > 0)
            {
                // 별 몫 한 줄(★n: 피해 +x% · 열 −y%). 몫이 없는 블록이면 "겹치기 ★n". 아이콘 위 ★ 표식이 겹치기임을 따로 알린다.
                string star = c.StarLine.Key != null ? c.StarLine.Format(Ctx.Names) : F("str.loot_pick.star_up", "n", c.StarTarget);
                UiKit.Label(Ctx, cr, star, UiFontId.F12, 12f, y, w - 24f, 16f, UiTheme.SelectYellow);
                y += 18f;
            }
            // 맞물림 표식: 처음 보는 맞물림은 띠를 깔고 크게, 다음부터는 한 줄로 작게.
            if (c.Synergy != LootSynergy.None)
            {
                string partner = c.SynergyPartnerId != null ? T(c.SynergyPartnerId) : string.Empty;
                string tag = F(c.Synergy == LootSynergy.Pair ? "str.loot_pick.synergy.pair" : "str.loot_pick.synergy.element", "partner", partner);
                if (c.SynergyFirst)
                {
                    var band = UiKit.Rect(cr, 6f, y, w - 12f, 40f, UiTheme.WithAlpha(UiTheme.Good, 0.22f), false, "SynergyBand");
                    UiPulse.Attach(band, 0.10f, 0.30f, 1.2f);
                    UiKit.LabelKey(Ctx, cr, "str.loot_pick.synergy.new", UiFontId.F16, 12f, y + 1f, w - 24f, 20f, UiTheme.Good);
                    UiKit.Label(Ctx, cr, tag, UiFontId.F12, 12f, y + 22f, w - 24f, 16f, UiTheme.Good);
                    y += 42f;
                }
                else
                {
                    UiKit.Label(Ctx, cr, tag, UiFontId.F12, 12f, y, w - 24f, 16f, UiTheme.WithAlpha(UiTheme.Good, 0.85f));
                    y += 18f;
                }
            }
            BuildPreview(cr, c, w, Mathf.Max(y + 2f, h - 84f));

            if (c.Guaranteed) UiKit.LabelKey(Ctx, cr, "str.loot_pick.guaranteed", UiFontId.F12, 12f, h - 22f, 120f, 16f, UiTheme.SelectYellow);
            if (c.Borrowed)
            {
                UiKit.LabelKey(Ctx, cr, "str.loot_pick.borrowed", UiFontId.F16, w - 132f, h - 26f, 120f, 22f, rc, TextAnchor.MiddleRight);
                CampViews.BorrowedTag(Ctx, cr, 12f, h - 36f, 2); // 꼬리표 32×16을 2배로 왼쪽 아래 구석에
            }
            b.Picked = c.Picked;
            return b;
        }

        /// <summary>끼웠을 때 미리보기 두 줄: 어디에(빈 칸 / 대신 / 별) + 탄 수·열·허수아비 DPS 변화.</summary>
        void BuildPreview(RectTransform cr, LootCardView c, float w, float y)
        {
            if (c.PreviewKind == LootPreviewKind.None) return;
            UiKit.Rect(cr, 8f, y, w - 16f, 1f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.6f), false, "PreviewRule");
            y += 4f;
            if (c.PreviewKind == LootPreviewKind.NoSlot)
            {
                UiKit.LabelKey(Ctx, cr, "str.loot_pick.preview.none", UiFontId.F12, 12f, y, w - 24f, 32f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
                return;
            }
            string slot = c.PreviewSlot == 0 ? T("str.loot_pick.preview.core") : F("str.loot_pick.preview.slot_n", "n", c.PreviewSlot);
            string where;
            if (c.PreviewKind == LootPreviewKind.Star) where = F("str.loot_pick.preview.star", "slot", slot, "name", T(c.BlockId), "n", c.StarTarget);
            else if (c.PreviewKind == LootPreviewKind.Replace) where = F("str.loot_pick.preview.replace", "slot", slot, "name", T(c.ReplacedId));
            else where = F("str.loot_pick.preview.slot", "slot", slot);
            UiKit.Label(Ctx, cr, where, UiFontId.F12, 12f, y, w - 24f, 16f, UiTheme.SlateBlue);
            y += 18f;

            int dShots = c.ShotsAfter - c.ShotsBefore;
            float dHeat = c.HeatAfter - c.HeatBefore;
            float pct = c.DpsPct;
            string shots = Signed(dShots);
            string heat = SignedOne(dHeat);
            string dps = F("str.format.percent", "n", SignedPct(pct));
            string line = F("str.loot_pick.preview.delta", "shots", shots, "heat", heat, "dps", dps);
            Color col = pct > 0.5f ? UiTheme.Good : pct < -0.5f ? UiTheme.Amber : UiTheme.Chalk;
            UiKit.Label(Ctx, cr, line, UiFontId.F12, 12f, y, w - 24f, 34f, col, TextAnchor.UpperLeft, wrap: true);
        }

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        static string Signed(int v) => (v > 0 ? "+" : string.Empty) + v.ToString(Inv);
        static string SignedOne(float v) => (v > 0.05f ? "+" : string.Empty) + CampFormat.OneDecimal(v);
        static string SignedPct(float v)
        {
            int r = Mathf.RoundToInt(v);
            return (r > 0 ? "+" : string.Empty) + r.ToString(Inv);
        }

        /// <summary>희귀 이상 카드의 지나가는 빛 띠(카드 안으로 잘린다). 영웅은 굵고 자주.</summary>
        void AddShine(RectTransform cr, float w, float h, bool hero, int index)
        {
            var clip = UiKit.Node(cr, "ShineClip", 0f, 0f, w, h);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            float bw = hero ? 56f : 32f;
            var band = UiKit.Rect(clip, -bw, -h * 0.25f, bw, h * 1.5f, UiTheme.WithAlpha(Color.white, 0f), false, "Shine");
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var s = clip.gameObject.AddComponent<LootCardShine>();
            s.Init(band, w, hero ? 2.2f : 3.4f, 0.4f + index * 0.15f);
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
                var item = wb != null ? new AssemblyItem { BlockId = wb.BlockId, Borrowed = wb.Borrowed, Wild = true, Stars = wb.Stars } : default(AssemblyItem);
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
            bool star = cmd.Kind == RunCommandKind.LootPick && index < _model.Cards.Count && _model.Cards[index].StarTarget > 0;
            PlayUi(cmd.Kind == RunCommandKind.LootPick ? (star ? "ui.loot.star_up" : "ui.loot.pick") : "ui.loot.unpick");
        }

        void OnReroll()
        {
            var w = World;
            if (w == null || _model == null || !_model.RerollEnabled) return;
            w.Enqueue(new RunCommand(RunCommandKind.LootReroll));
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

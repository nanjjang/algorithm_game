using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 골드 상점(ScreenId.Shop, 08 11.3절, 07 8절, UA 4.23). 가운데 1200×800: 머리 1200×96(행상 + 내 골드), 탭 1200×48
    /// (부적 / 지팡이 / 그 밖), 품목 카드 216×280 5열 간격 24. 꾸미기 탭은 그리지 않는다(D-65). 유료 탭·잔액·구매 버튼·안내문,
    /// 닉네임 바꾸기는 아예 없다(TV-198, 07 12.14절, Q-04). 카드를 고르면 ShopBuyConfirm(Sku·Qty·Character). 규칙은 ShopModel.
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        const float BoardW = 1200f, BoardH = 800f, HeadH = 96f, TabH = 48f, CardW = 216f, CardH = 280f, CardGap = 24f, Pad = 24f;
        const int Columns = 5;
        const float IconSize = 96f;

        RectTransform _cardsArea;
        UiLabel _gold;
        ShopTab _tab = ShopTab.Charm;
        readonly List<UiButton> _cards = new List<UiButton>();

        public override ScreenId Id => ScreenId.Shop;
        protected override string TitleKey => MetaKeys.ShopTitle;

        protected override void BuildContent(RectTransform content)
        {
            float bx = (UiScale.RefWidth - BoardW) * 0.5f, by = (ContentHeight - BoardH) * 0.5f;
            var board = UiKit.Plate(Ctx, content, UiPanelKind.Board, bx, by, BoardW, BoardH).rectTransform;
            board.name = "ShopBoard";

            var head = UiKit.Panel(Ctx, board, UiPanelKind.Heading, 0f, 0f, BoardW, HeadH).rectTransform;
            float lh = UiTheme.LineHeight(UiFontId.F24);
            UiKit.Label(Ctx, head, T(MetaKeys.ShopKindGold), UiFontId.F24, Pad, (HeadH - lh) * 0.5f - 14f, 480f, lh, UiTheme.Chalk);
            UiKit.Label(Ctx, head, T(MetaKeys.NpcPeddler), UiFontId.F16, Pad, (HeadH - lh) * 0.5f + 18f, 480f,
                UiTheme.LineHeight(UiFontId.F16), UiTheme.SlateBlue);
            _gold = UiKit.Label(Ctx, head, string.Empty, UiFontId.F24, BoardW - Pad - 480f, (HeadH - lh) * 0.5f, 480f, lh, UiTheme.Gold,
                TextAnchor.MiddleRight);

            var labels = new List<string>();
            var keys = ShopModel.TabKeys();
            for (int i = 0; i < keys.Count; i++) labels.Add(T(keys[i]));
            float tabW = (BoardW - 4f * (labels.Count - 1)) / labels.Count;
            Tabs = UiKit.Tabs(Ctx, board, labels, 0f, HeadH, tabW, TabH, i => SelectTab(ShopModel.Tabs[i]), 0, false, 4f);

            float gridW = Columns * CardW + (Columns - 1) * CardGap;
            _cardsArea = UiKit.Node(board, "ShopCards", (BoardW - gridW) * 0.5f, HeadH + TabH + 32f, gridW, BoardH - HeadH - TabH - 64f);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Refresh();
        }

        void SelectTab(ShopTab tab)
        {
            _tab = tab;
            Refresh();
        }

        void Refresh()
        {
            var st = Ctx != null && Ctx.Shop != null ? Ctx.Shop.GetState() : null;
            _gold.SetText(F(MetaKeys.FormatGoldAmount, "n", N(st != null ? st.Gold : 0)));
            for (int i = 0; i < _cards.Count; i++) Focus.Remove(_cards[i]);
            _cards.Clear();
            MetaUi.ClearChildren(_cardsArea);
            var cards = ShopModel.Cards(_tab, st, Ctx != null ? Ctx.Profile : null);
            for (int i = 0; i < cards.Count; i++) BuildCard(cards[i], i);
            if (_cards.Count > 0) Focus.SetCursor(_cards[0], false);
        }

        void BuildCard(ShopCard c, int index)
        {
            var pos = UiKit.GridCell(index, Columns, CardW, CardH, CardGap, CardGap);
            var card = c;
            var b = UiKit.Button(Ctx, _cardsArea, null, pos.x, pos.y, CardW, CardH, () => OpenBuy(card), UiButtonStyle.Plain, Focus);
            b.name = "Card." + c.Sku + (c.Character > 0 ? "." + c.Character : string.Empty);
            var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
            UiKit.Panel(Ctx, root, UiPanelKind.Paper, 0f, 0f, CardW, CardH);

            float ix = (CardW - IconSize) * 0.5f, iy = 16f;
            Sprite wandIcon = c.Sku == Ids.Item.WandBox && Ctx != null && Ctx.Art != null ? Ctx.Art.WandIcon(Ids.Char.Of(c.Character), 0) : null;
            if (wandIcon != null) UiKit.Image(root, wandIcon, ix, iy, IconSize, IconSize, null, true);
            else UiKit.Icon(Ctx, root, c.Sku, (int)IconSize, ix, iy);

            float y = iy + IconSize + 8f, w = CardW - 24f;
            float lh = UiTheme.LineHeight(UiFontId.F16), th = UiTheme.LineHeight(UiFontId.F12);
            UiKit.Label(Ctx, root, T(c.NameKey), UiFontId.F16, 12f, y, w, lh * 2f, UiTheme.PaperText, TextAnchor.UpperCenter, true);
            y += lh * 2f;
            if (c.Character > 0)
            {
                UiKit.Label(Ctx, root, T(MetaKeys.Character(c.Character)), UiFontId.F16, 12f, y, w, lh, UiTheme.PaperText, TextAnchor.MiddleCenter);
                y += lh;
            }
            UiKit.Label(Ctx, root, T(MetaKeys.ShopPrice) + " " + F(MetaKeys.FormatGoldAmount, "n", N(c.Price)), UiFontId.F16, 12f, y, w, lh,
                UiTheme.PaperText, TextAnchor.MiddleCenter);
            y += lh;
            if (c.ShowWeeklyLimit)
            {
                UiKit.Label(Ctx, root, F(MetaKeys.ShopWeeklyLimit, "n", c.Bought, "max", c.Limit), UiFontId.F12, 12f, y, w, th,
                    UiTheme.PaperText, TextAnchor.MiddleCenter);
                y += th;
                if (c.SoldOut)
                    UiKit.Label(Ctx, root, T(MetaKeys.ShopSoldOut), UiFontId.F12, 12f, y, w, th, UiTheme.PaperText, TextAnchor.MiddleCenter);
            }
            if (c.Sku == Ids.Item.WandBox)
                UiKit.Label(Ctx, root, F(MetaKeys.ShopWandCount, "n", c.WandCount), UiFontId.F12, 12f, y, w, th, UiTheme.PaperText,
                    TextAnchor.MiddleCenter);
            if (c.Sku == Ids.Item.LibraryExpansion && c.LibraryStep > 0)
                UiKit.Label(Ctx, root, F(MetaKeys.FormatMoreCount, "n", c.LibraryStep), UiFontId.F12, 12f, y, w, th, UiTheme.PaperText,
                    TextAnchor.MiddleCenter);

            if (!c.Enabled) b.SetDisabled(c.DisabledReason);
            _cards.Add(b);
        }

        void OpenBuy(ShopCard card)
        {
            var a = ShopModel.BuyArgs(card);
            a.OnResult = _ => Refresh();
            Screens?.Push(ScreenId.ShopBuyConfirm, a);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}

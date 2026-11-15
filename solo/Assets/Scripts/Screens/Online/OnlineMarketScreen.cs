using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 장터(ScreenId.OnlineMarket, 11-online.md 6.12절). 왼쪽 = 올라온 것(개당 값 싼 순, 누르면 산다), 오른쪽 = 팔기:
    /// 내 창고에서 하나 고르고 개수·값을 넣어 [올리기](보증금 1%), 아래에 내가 올린 것(누르면 내린다 — 보증금은 사라진다).
    /// 팔리면 값에서 수수료 5%를 뺀 골드와 보증금이 판 사람에게 바로 간다. 48시간이 지나면 물건이 돌아온다.
    /// </summary>
    public sealed class OnlineMarketScreen : ScreenBase
    {
        const float BoardW = 1600f, BoardH = 900f, Pad = 28f, RowH = 40f, Gap = 6f;
        const int BuyRows = 16, MineRows = 6, InvChips = 12;

        OnlineService _svc;
        UiLabel _head, _status, _sellPick, _empty;
        readonly List<UiButton> _buyRows = new List<UiButton>();
        readonly List<MarketListing> _listings = new List<MarketListing>();
        readonly List<UiButton> _mineRows = new List<UiButton>();
        readonly List<MarketListing> _mine = new List<MarketListing>();
        readonly List<UiButton> _invChips = new List<UiButton>();
        readonly List<string> _invIds = new List<string>();
        UInputField _qty, _price;
        string _pick;

        public override ScreenId Id => ScreenId.OnlineMarket;
        protected override bool HasFrame => false;

        UInputField Input(RectTransform parent, float x, float y, float w, string hint)
        {
            var bg = UiKit.NineSlice(Ctx, parent, "K-33", "input", null, x, y, w, 44f, UiTheme.Chalk);
            bg.raycastTarget = true;
            var f = bg.gameObject.AddComponent<UInputField>();
            var t = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F16, 8f, 0f, w - 16f, 44f, UiTheme.PaperText);
            f.textComponent = t.GetComponent<UText>();
            f.contentType = UInputField.ContentType.IntegerNumber;
            f.characterLimit = 10;
            var ph = UiKit.Label(Ctx, bg.rectTransform, hint, UiFontId.F16, 8f, 0f, w - 16f, 44f, UiTheme.SlateBlue);
            f.placeholder = ph.GetComponent<UText>();
            return f;
        }

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            _head = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, Pad, BoardW - 2f * Pad, 40f, UiTheme.SelectYellow, TextAnchor.MiddleCenter);
            float top = Pad + 52f, half = (BoardW - 3f * Pad) / 2f;
            UiKit.Label(Ctx, board, T(OnlineKeys.MarketBuyHead), UiFontId.F16, Pad, top, half, 28f, UiTheme.Chalk);
            _empty = UiKit.Label(Ctx, board, T(OnlineKeys.MarketEmpty), UiFontId.F16, Pad, top + 40f, half, 32f, UiTheme.SlateBlue);
            for (int i = 0; i < BuyRows; i++)
            {
                int k = i;
                _buyRows.Add(UiKit.Button(Ctx, board, string.Empty, Pad, top + 32f + i * (RowH + Gap), half, RowH, () => Buy(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            float rx = 2f * Pad + half, y = top;
            UiKit.Label(Ctx, board, T(OnlineKeys.MarketSellHead), UiFontId.F16, rx, y, half, 28f, UiTheme.Chalk);
            y += 32f;
            float chipW = (half - 3f * Gap) / 4f;
            for (int i = 0; i < InvChips; i++)
            {
                int k = i;
                _invChips.Add(UiKit.Button(Ctx, board, string.Empty, rx + (i % 4) * (chipW + Gap), y + (i / 4) * (RowH + Gap), chipW, RowH, () => Pick(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            y += 3f * (RowH + Gap) + 8f;
            _sellPick = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, rx, y, half, 28f, UiTheme.SelectYellow);
            y += 32f;
            _qty = Input(board, rx, y, 160f, T(OnlineKeys.MarketQty));
            _price = Input(board, rx + 170f, y, 220f, T(OnlineKeys.MarketPrice));
            UiKit.Button(Ctx, board, T(OnlineKeys.MarketSell), rx + 400f, y, 180f, 44f, Sell, UiButtonStyle.Primary, Focus);
            y += 56f;
            UiKit.Label(Ctx, board, T(OnlineKeys.MarketMineHead), UiFontId.F16, rx, y, half, 28f, UiTheme.Chalk);
            y += 32f;
            for (int i = 0; i < MineRows; i++)
            {
                int k = i;
                _mineRows.Add(UiKit.Button(Ctx, board, string.Empty, rx, y + i * (RowH + Gap), half, RowH, () => Unlist(k), UiButtonStyle.Small, Focus, null, UiFontId.F12));
            }
            _status = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, BoardH - Pad - 100f, BoardW - 2f * Pad, 40f, UiTheme.Amber, TextAnchor.MiddleCenter);
            UiKit.Button(Ctx, board, T(OnlineKeys.MarketRefresh), BoardW - Pad - 420f, BoardH - Pad - 56f, 200f, 56f, Reload, UiButtonStyle.Medium, Focus);
            UiKit.Button(Ctx, board, T(OnlineKeys.Leave), BoardW - Pad - 200f, BoardH - Pad - 56f, 200f, 56f, () => Close(), UiButtonStyle.Medium, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _pick = null;
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null) _status.SetText(T(OnlineKeys.ErrorServer));
            Reload();
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        public override void Tick(float dt)
        {
            if (Screens != null) Screens.CaptureKeys = _qty.isFocused || _price.isFocused;
        }

        void Reload()
        {
            _listings.Clear();
            _mine.Clear();
            if (_svc != null)
            {
                var a = _svc.MarketBrowse();
                if (a != null) _listings.AddRange(a);
                var b = _svc.MarketMine();
                if (b != null) _mine.AddRange(b);
            }
            Refresh();
        }

        string ListingText(MarketListing l, bool mine)
        {
            long each = l.Qty > 0 ? l.Price / l.Qty : l.Price;
            return F(mine ? OnlineKeys.MarketMineLine : OnlineKeys.MarketLine, "item", T(l.ItemId), "qty", l.Qty, "price", N(l.Price), "each", N(each), "who", l.SellerName);
        }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            _head.SetText(p != null ? F(OnlineKeys.MarketHead, "gold", N(p.Gold), "n", _mine.Count, "max", AccountStore.MaxListings) : T(OnlineKeys.ProfileGuest));
            _empty.gameObject.SetActive(_listings.Count == 0);
            for (int i = 0; i < _buyRows.Count; i++)
            {
                bool on = i < _listings.Count;
                _buyRows[i].gameObject.SetActive(on);
                if (!on) continue;
                var l = _listings[i];
                _buyRows[i].SetText(ListingText(l, false));
                _buyRows[i].Enabled = p != null && l.SellerId != p.AccountId && p.Gold >= l.Price;
            }
            for (int i = 0; i < _mineRows.Count; i++)
            {
                bool on = i < _mine.Count;
                _mineRows[i].gameObject.SetActive(on);
                if (on) _mineRows[i].SetText(ListingText(_mine[i], true));
            }
            _invIds.Clear();
            if (p != null && p.Items != null) foreach (var kv in p.Items) if (kv.Value > 0) _invIds.Add(kv.Key);
            _invIds.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < _invChips.Count; i++)
            {
                bool on = i < _invIds.Count;
                _invChips[i].gameObject.SetActive(on);
                if (on) _invChips[i].SetText(T(_invIds[i]) + " ×" + p.Qty(_invIds[i]));
            }
            _sellPick.SetText(_pick != null ? F(OnlineKeys.MarketPick, "item", T(_pick), "have", p != null ? p.Qty(_pick) : 0) : T(OnlineKeys.MarketPickNone));
        }

        void Pick(int k) { if (k < _invIds.Count) { _pick = _invIds[k]; Refresh(); } }

        void Show(AccountStore.MarketResult? r)
        {
            if (r == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            _status.SetText(T(OnlineKeys.MarketResult + r.Value));
        }

        void Sell()
        {
            if (_svc == null || _pick == null) return;
            int.TryParse(_qty.text, out int q);
            long.TryParse(_price.text, out long pr);
            if (q <= 0) q = 1;
            Show(_svc.MarketSell(_pick, q, pr));
            Reload();
        }

        void Buy(int k)
        {
            if (_svc == null || k >= _listings.Count) return;
            Show(_svc.MarketBuy(_listings[k].Id));
            Reload();
        }

        void Unlist(int k)
        {
            if (_svc == null || k >= _mine.Count) return;
            Show(_svc.MarketUnlist(_mine[k].Id));
            Reload();
        }
    }
}

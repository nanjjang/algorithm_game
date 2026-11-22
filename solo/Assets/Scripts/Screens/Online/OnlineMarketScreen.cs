using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 장터(ScreenId.OnlineMarket, 11-online.md 6.12절, docs/ui-assets.md 4.23절). 장터 거리(S-2307) 위:
    ///  - 왼쪽: 행상 수레 띠(S-2305) 아래 품목 카드(S-2306, 5열 × 2줄): 아이콘 2배 · 이름 · 묶음 값(금색) · 개당 값 · 판 사람 · [사기].
    ///    개당 값 싼 순. 내가 못 사는 것(내 물건, 골드 모자람)은 꺼진 카드.
    ///  - 오른쪽 판: 창고 격자(누르면 고름) → 개수·값 → 영수증(묶음 값, 지금 내는 보증금 1%, 팔리면 수수료 5%, 받는 골드) → [올리기].
    ///    아래에 내가 올린 것(남은 시간, [내리기] — 보증금은 돌아오지 않는다).
    /// 팔리면 값에서 수수료 5%를 뺀 골드와 보증금이 판 사람에게 바로 간다. 48시간이 지나면 물건이 돌아온다.
    /// </summary>
    public sealed class OnlineMarketScreen : ScreenBase
    {
        const float LeftX = 48f, LeftY = 112f, LeftW = 1224f;
        const float CardW = 216f, CardH = 280f, CardGap = 24f;
        const int Cols = 5, CardRows = 2, InvCells = 12, MineRows = 4;
        const float RightX = 1304f, RightY = 112f, RightW = 568f, RightH = 872f, RP = 28f;

        OnlineService _svc;
        UiLabel _gold, _count, _status, _browseEmpty, _mineEmpty, _pickLine;
        UImage _browseEmptyArt;
        readonly List<MarketListing> _listings = new List<MarketListing>();
        readonly List<MarketListing> _mine = new List<MarketListing>();
        readonly List<string> _invIds = new List<string>();
        string _pick;

        sealed class Card { public UiButton Button; public UImage Icon; public UiLabel Name, Price, Each, Who; }
        readonly List<Card> _cards = new List<Card>();
        readonly List<UiButton> _inv = new List<UiButton>();
        readonly List<UImage> _invIcons = new List<UImage>();
        readonly List<UiLabel> _invQty = new List<UiLabel>();
        sealed class MineRow { public RectTransform Root; public UImage Icon; public UiLabel Text, Left; public UiButton Unlist; }
        readonly List<MineRow> _mineRows = new List<MineRow>();
        UInputField _qty, _price;
        UiLabel _rPrice, _rDeposit, _rFee, _rGet;
        UiButton _sell;
        int _page;

        public override ScreenId Id => ScreenId.OnlineMarket;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "S-2307", "market_street", 0.5f, "bg_market");
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "market", T(OnlineKeys.MarketButton), out var band);
            _gold = OnlineUi.IconText(Ctx, band, "I-C2", "gold", string.Empty, 360f, 0f, 220f, 80f, UiFontId.F24, UiTheme.Gold);
            _count = UiKit.Label(Ctx, band, string.Empty, UiFontId.F16, 600f, 0f, 400f, 80f, UiTheme.SlateBlue);
            float bw = 176f, bx = UiScale.RefWidth - OnlineUi.Edge - 2f * bw - 8f;
            UiKit.Button(Ctx, band, T(OnlineKeys.MarketRefresh), bx, 16f, bw, 48f, Reload, UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "refresh"));
            UiKit.Button(Ctx, band, T(OnlineKeys.Leave), bx + bw + 8f, 16f, bw, 48f, () => Close(), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "back"));

            // 왼쪽: 수레 띠 + 카드.
            var banner = OnlineUi.Img(Ctx, content, "S-2305", "shop_banner_cart", LeftX + (LeftW - 1200f) * 0.5f, LeftY, 1200f, 96f).rectTransform;
            UiKit.Rect(banner, 0f, 0f, 600f, 96f, UiTheme.WithAlpha(UiTheme.Ink, 0.55f), false, "BannerShade");
            var bt = UiKit.Label(Ctx, banner, T(OnlineKeys.MarketBuyHead), UiFontId.F24, 32f, 0f, 560f, 96f, UiTheme.Chalk, TextAnchor.MiddleLeft);
            UiKit.DropShadow(bt);
            float gx = LeftX + (LeftW - (Cols * CardW + (Cols - 1) * CardGap)) * 0.5f, gy = LeftY + 120f;
            for (int i = 0; i < Cols * CardRows; i++)
            {
                int k = i;
                float x = gx + (i % Cols) * (CardW + CardGap), y = gy + (i / Cols) * (CardH + CardGap);
                var b = UiKit.Button(Ctx, content, null, x, y, CardW, CardH, () => Buy(k), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                OnlineUi.Img(Ctx, r, "S-2306", "catalog_card", 0f, 0f, CardW, CardH, "normal");
                var c = new Card { Button = b };
                c.Icon = UiKit.Image(r, null, (CardW - 96f) * 0.5f, 28f, 96f, 96f, Color.white, true);
                c.Name = UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 12f, 132f, CardW - 24f, 24f, UiTheme.PaperText, TextAnchor.MiddleCenter);
                c.Price = OnlineUi.IconText(Ctx, r, "I-C2", "gold", string.Empty, 40f, 164f, CardW - 64f, 32f, UiFontId.F24, UiTheme.PaperText);
                c.Each = UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 12f, 198f, CardW - 24f, 20f, UiTheme.WithAlpha(UiTheme.PaperText, 0.7f), TextAnchor.MiddleCenter);
                c.Who = UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 12f, 222f, CardW - 24f, 20f, UiTheme.WithAlpha(UiTheme.PaperText, 0.7f), TextAnchor.MiddleCenter);
                _cards.Add(c);
            }
            _browseEmptyArt = OnlineUi.OnlineImg(content, "empty_market", LeftX + (LeftW - 256f) * 0.5f, gy + 60f, 256f, 256f);
            _browseEmpty = UiKit.Label(Ctx, content, T(OnlineKeys.MktBrowseEmpty), UiFontId.F24, LeftX, gy + 340f, LeftW, 48f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_browseEmpty);
            float py = gy + CardRows * (CardH + CardGap);
            UiKit.IconButton(Ctx, content, OnlineUi.Sprite(Ctx, "K-38", "page"), LeftX + LeftW * 0.5f - 72f, py, 48f, 48f, () => { _page = Math.Max(0, _page - 1); Refresh(); });
            UiKit.IconButton(Ctx, content, OnlineUi.Sprite(Ctx, "K-38", "page"), LeftX + LeftW * 0.5f + 24f, py, 48f, 48f, () => { _page++; Refresh(); }).Rect.localScale = new Vector3(-1f, 1f, 1f);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, LeftX, 1000f, LeftW, 40f, UiTheme.Amber, TextAnchor.MiddleLeft);
            UiKit.DropShadow(_status);

            BuildSellPanel(content);
        }

        void BuildSellPanel(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Board, RightX, RightY, RightW, RightH).rectTransform;
            UiKit.Bevel(panel);
            float inner = RightW - 2f * RP, y = 20f;
            UiKit.Label(Ctx, panel, T(OnlineKeys.MarketSell), UiFontId.F32, RP, y, inner, 44f, UiTheme.Chalk);
            y += 52f;
            for (int i = 0; i < InvCells; i++)
            {
                int k = i;
                float x = RP + (i % 6) * 86f, cy = y + (i / 6) * 92f;
                var b = UiKit.Button(Ctx, panel, null, x, cy, 80f, 88f, () => Pick(k), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                OnlineUi.Img(Ctx, r, "K-60", "item_slot_filled", 8f, 0f, 64f, 64f);
                _invIcons.Add(UiKit.Image(r, null, 16f, 8f, 48f, 48f, Color.white, true));
                _invQty.Add(UiKit.Label(Ctx, r, string.Empty, UiFontId.F16, 0f, 64f, 80f, 22f, UiTheme.Chalk, TextAnchor.MiddleCenter));
                UiKit.AttachTooltip(b.gameObject, () => k < _invIds.Count ? T(_invIds[k]) : string.Empty);
                _inv.Add(b);
            }
            y += 2f * 92f + 4f;
            _pickLine = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, RP, y, inner, 28f, UiTheme.SelectYellow);
            y += 32f;
            _qty = OnlineUi.Field(Ctx, Focus, panel, T(OnlineKeys.MarketQty), "1", RP, y, 140f, 6);
            _qty.contentType = UInputField.ContentType.IntegerNumber;
            _price = OnlineUi.Field(Ctx, Focus, panel, T(OnlineKeys.MarketPrice), "0", RP + 156f, y, inner - 156f, 10);
            _price.contentType = UInputField.ContentType.IntegerNumber;
            _qty.onValueChanged.AddListener(_ => Receipt());
            _price.onValueChanged.AddListener(_ => Receipt());
            y += 84f;
            // 영수증(쪽지).
            // 받아 온 영수증 쪽지(frame_receipt)는 테두리와 밀랍 도장이 있어 글자 칸보다 조금 크게 깔고, 값은 도장 왼쪽에서 끝낸다.
            bool receiptArt = OnlineUi.OnlineImg(panel, "frame_receipt", RP - 16f, y - 12f, inner + 32f, 156f) != null;
            _receiptValueInset = receiptArt ? 64f : 0f;
            var paper = receiptArt ? UiKit.Node(panel, "Receipt", RP, y, inner, 132f) : UiKit.Panel(Ctx, panel, UiPanelKind.Paper, RP, y, inner, 132f).rectTransform;
            _rPrice = ReceiptRow(paper, 10f, T(OnlineKeys.MktReceiptPrice));
            _rDeposit = ReceiptRow(paper, 38f, T(OnlineKeys.MktReceiptDeposit));
            _rFee = ReceiptRow(paper, 66f, T(OnlineKeys.MktReceiptFee));
            UiKit.Rect(paper, 16f, 96f, inner - 32f, 2f, UiTheme.WithAlpha(UiTheme.PaperText, 0.4f), false, "Rule");
            _rGet = ReceiptRow(paper, 100f, T(OnlineKeys.MktReceiptGet));
            y += 144f;
            _sell = UiKit.Button(Ctx, panel, T(OnlineKeys.MarketSell), RP, y, inner, 64f, Sell, UiButtonStyle.Primary, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F2", "plus"));
            y += 80f;
            UiKit.Divider(Ctx, panel, RP, y, inner);
            y += 12f;
            UiKit.Label(Ctx, panel, T(OnlineKeys.MktOwn), UiFontId.F16, RP, y, inner, 24f, UiTheme.SlateBlue);
            y += 28f;
            for (int i = 0; i < MineRows; i++)
            {
                int k = i;
                var row = new MineRow { Root = UiKit.Node(panel, "Mine" + i, RP, y + i * 48f, inner, 44f) };
                UiKit.Panel(Ctx, row.Root, UiPanelKind.Recess, 0f, 0f, inner, 44f);
                row.Icon = UiKit.Image(row.Root, null, 6f, 6f, 32f, 32f, Color.white, true);
                row.Text = UiKit.Label(Ctx, row.Root, string.Empty, UiFontId.F16, 46f, 0f, inner - 250f, 44f, UiTheme.Chalk);
                row.Left = UiKit.Label(Ctx, row.Root, string.Empty, UiFontId.F16, inner - 200f, 0f, 90f, 44f, UiTheme.SlateBlue, TextAnchor.MiddleRight);
                row.Unlist = UiKit.Button(Ctx, row.Root, T(OnlineKeys.MktUnlist), inner - 100f, 6f, 94f, 32f, () => Unlist(k), UiButtonStyle.Small, Focus, null, UiFontId.F16);
                _mineRows.Add(row);
            }
            _mineEmpty = UiKit.Label(Ctx, panel, T(OnlineKeys.MktMineEmpty), UiFontId.F16, RP, y, inner, 40f, UiTheme.SlateBlue);
        }

        float _receiptValueInset;

        UiLabel ReceiptRow(RectTransform paper, float y, string caption)
        {
            float w = RightW - 2f * RP;
            UiKit.Label(Ctx, paper, caption, UiFontId.F16, 16f, y, w - 180f, 26f, UiTheme.PaperText);
            return UiKit.Label(Ctx, paper, string.Empty, UiFontId.F16, w - 176f - _receiptValueInset, y, 160f, 24f, UiTheme.PaperText, TextAnchor.MiddleRight);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _pick = null;
            _page = 0;
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

        Sprite Icon(string id, int size) { try { return Ctx != null && Ctx.Art != null ? Ctx.Art.Icon(id, size) : null; } catch (Exception) { return null; } }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            _gold.SetText(p != null ? N(p.Gold) : "-");
            _count.SetText(p != null ? F(OnlineKeys.MktCount, "n", _mine.Count, "max", AccountStore.MaxListings) : string.Empty);
            int per = Cols * CardRows;
            int pages = Math.Max(1, (_listings.Count + per - 1) / per);
            if (_page >= pages) _page = pages - 1;
            _browseEmpty.gameObject.SetActive(_listings.Count == 0);
            if (_browseEmptyArt != null) _browseEmptyArt.gameObject.SetActive(_listings.Count == 0);
            for (int i = 0; i < _cards.Count; i++)
            {
                int li = _page * per + i;
                var c = _cards[i];
                bool on = li < _listings.Count;
                c.Button.gameObject.SetActive(on);
                if (!on) continue;
                var l = _listings[li];
                long each = l.Qty > 0 ? l.Price / l.Qty : l.Price;
                c.Icon.sprite = Icon(l.ItemId, 96);
                c.Name.SetText(T(l.ItemId) + " ×" + l.Qty);
                c.Price.SetText(N(l.Price));
                c.Each.SetText(F(OnlineKeys.MktEach, "each", N(each)));
                c.Who.SetText(l.SellerName);
                c.Button.Enabled = p != null && l.SellerId != p.AccountId && p.Gold >= l.Price;
            }

            _invIds.Clear();
            if (p != null && p.Items != null) foreach (var kv in p.Items) if (kv.Value > 0) _invIds.Add(kv.Key);
            _invIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < _inv.Count; i++)
            {
                bool on = i < _invIds.Count;
                _inv[i].gameObject.SetActive(on);
                if (!on) continue;
                _invIcons[i].sprite = Icon(_invIds[i], 48);
                _invQty[i].SetText("×" + p.Qty(_invIds[i]));
                _inv[i].Picked = _invIds[i] == _pick;
            }
            _pickLine.SetText(_pick != null ? F(OnlineKeys.MarketPick, "item", T(_pick), "have", p != null ? p.Qty(_pick) : 0) : T(OnlineKeys.MktPickFirst));

            long now = DateTime.UtcNow.Ticks;
            _mineEmpty.gameObject.SetActive(_mine.Count == 0);
            for (int i = 0; i < _mineRows.Count; i++)
            {
                var row = _mineRows[i];
                bool on = i < _mine.Count;
                row.Root.gameObject.SetActive(on);
                if (!on) continue;
                var l = _mine[i];
                row.Icon.sprite = Icon(l.ItemId, 32);
                row.Text.SetText(T(l.ItemId) + " ×" + l.Qty + "  ·  " + N(l.Price));
                double h = AccountStore.ListingHours - TimeSpan.FromTicks(Math.Max(0, now - l.CreatedTicks)).TotalHours;
                row.Left.SetText(F(OnlineKeys.MktLeft, "h", Math.Max(0, (int)Math.Ceiling(h))));
            }
            Receipt();
        }

        void Receipt()
        {
            long.TryParse(_price != null ? _price.text : null, out long pr);
            int.TryParse(_qty != null ? _qty.text : null, out int q);
            if (pr < 0) pr = 0;
            _rPrice.SetText(N(pr));
            _rDeposit.SetText("− " + N(AccountStore.DepositFor(pr)));
            _rFee.SetText("− " + N(AccountStore.MarketFee(pr)));
            _rGet.SetText(N(Math.Max(0, pr - AccountStore.MarketFee(pr))));
            var p = _svc != null ? _svc.Profile : null;
            _sell.Enabled = _pick != null && pr > 0 && p != null && p.Gold >= AccountStore.DepositFor(pr) && p.Qty(_pick) >= Math.Max(1, q);
        }

        void Pick(int k) { if (k < _invIds.Count) { _pick = _invIds[k]; Refresh(); } }

        void Show(AccountStore.MarketResult? r)
        {
            if (r == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            _status.Text.color = r.Value == AccountStore.MarketResult.Ok ? UiTheme.Good : UiTheme.Amber;
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
            int li = _page * Cols * CardRows + k;
            if (_svc == null || li >= _listings.Count) return;
            Show(_svc.MarketBuy(_listings[li].Id));
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

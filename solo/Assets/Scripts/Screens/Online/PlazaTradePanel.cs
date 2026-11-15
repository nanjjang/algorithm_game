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
    /// 광장 직접 거래 판(11-online.md 6.11절). 서버가 보낸 거래 상태(PlazaClient.Trade)를 그리고, 내가 내놓는 것을 바꿔 보낸다.
    /// 왼쪽 = 내가 내놓는 것(누르면 하나 내림), 오른쪽 = 상대가 내놓는 것, 아래 = 내 창고(누르면 하나 올림), 골드 칸 + [올리기],
    /// [확인](둘 다 확인하면 서버가 맞바꾼다 — 어느 쪽이든 바꾸면 확인이 풀린다), [그만].
    /// </summary>
    public sealed class PlazaTradePanel
    {
        const float W = 1100f, H = 720f, Pad = 24f, ChipW = 200f, ChipH = 36f, Gap = 8f;
        const int OfferChips = 10, InvChips = 15;

        readonly GameContext _ctx;
        readonly System.Func<string, string> _t;
        readonly PlazaClient _client;
        readonly RectTransform _root;
        readonly UiLabel _title, _mineHead, _theirHead, _theirList, _status;
        readonly List<UiButton> _mineChips = new List<UiButton>();
        readonly List<string> _mineIds = new List<string>();
        readonly List<UiButton> _invChips = new List<UiButton>();
        readonly List<string> _invIds = new List<string>();
        readonly UInputField _gold;
        readonly UiButton _confirm;
        AccountStore.TradeOffer _edit = new AccountStore.TradeOffer();

        public bool Visible => _root.gameObject.activeSelf;
        public bool Typing => _gold.isFocused;

        public PlazaTradePanel(GameContext ctx, RectTransform parent, UiFocusGroup focus, PlazaClient client, System.Func<string, string> t)
        {
            _ctx = ctx;
            _t = t;
            _client = client;
            var board = UiKit.Panel(ctx, parent, UiPanelKind.Board, 0f, 0f, W, H).rectTransform;
            UiKit.Center(board, W, H);
            _root = board;
            _title = UiKit.Label(ctx, board, string.Empty, UiFontId.F24, Pad, Pad, W - 2f * Pad, 36f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            float y = Pad + 44f, half = (W - 3f * Pad) / 2f;
            _mineHead = UiKit.Label(ctx, board, string.Empty, UiFontId.F16, Pad, y, half, 28f, UiTheme.SelectYellow);
            _theirHead = UiKit.Label(ctx, board, string.Empty, UiFontId.F16, 2f * Pad + half, y, half, 28f, UiTheme.SelectYellow);
            y += 32f;
            for (int i = 0; i < OfferChips; i++)
            {
                int k = i;
                _mineChips.Add(UiKit.Button(ctx, board, string.Empty, Pad + (i % 2) * (ChipW + Gap), y + (i / 2) * (ChipH + Gap), ChipW, ChipH,
                    () => Remove(k), UiButtonStyle.Small, focus, null, UiFontId.F12));
            }
            _theirList = UiKit.Label(ctx, board, string.Empty, UiFontId.F16, 2f * Pad + half, y, half, 5f * (ChipH + Gap), UiTheme.Chalk, TextAnchor.UpperLeft, wrap: true);
            y += 5f * (ChipH + Gap) + 8f;
            UiKit.Label(ctx, board, t(OnlineKeys.TradeInventory), UiFontId.F12, Pad, y, W - 2f * Pad, 24f, UiTheme.SlateBlue);
            y += 28f;
            for (int i = 0; i < InvChips; i++)
            {
                int k = i;
                _invChips.Add(UiKit.Button(ctx, board, string.Empty, Pad + (i % 5) * (ChipW + Gap), y + (i / 5) * (ChipH + Gap), ChipW, ChipH,
                    () => Add(k), UiButtonStyle.Small, focus, null, UiFontId.F12));
            }
            y += 3f * (ChipH + Gap) + 8f;
            var bg = UiKit.NineSlice(ctx, board, "K-33", "input", null, Pad, y, 200f, 44f, UiTheme.Chalk);
            bg.raycastTarget = true;
            _gold = bg.gameObject.AddComponent<UInputField>();
            var txt = UiKit.Label(ctx, bg.rectTransform, string.Empty, UiFontId.F16, 8f, 0f, 184f, 44f, UiTheme.PaperText);
            _gold.textComponent = txt.GetComponent<UText>();
            _gold.contentType = UInputField.ContentType.IntegerNumber;
            _gold.characterLimit = 9;
            UiKit.Button(ctx, board, t(OnlineKeys.TradeGold).Replace("{n}", string.Empty).Trim(), Pad + 210f, y, 140f, 44f, SetGold, UiButtonStyle.Medium, focus, null, UiFontId.F16);
            _status = UiKit.Label(ctx, board, string.Empty, UiFontId.F16, Pad + 360f, y, W - 2f * Pad - 360f, 44f, UiTheme.Amber, TextAnchor.MiddleLeft);
            float by = H - Pad - 52f;
            _confirm = UiKit.Button(ctx, board, t(OnlineKeys.TradeConfirm), W - Pad - 2f * 200f - Gap, by, 200f, 52f, () => _client.ConfirmTrade(), UiButtonStyle.Primary, focus);
            UiKit.Button(ctx, board, t(OnlineKeys.TradeCancel), W - Pad - 200f, by, 200f, 52f, () => _client.CancelTrade(), UiButtonStyle.Medium, focus);
            board.gameObject.SetActive(false);
        }

        static AccountStore.TradeOffer Copy(AccountStore.TradeOffer o)
        {
            var c = new AccountStore.TradeOffer { Gold = o.Gold };
            foreach (var kv in o.Items) c.Items[kv.Key] = kv.Value;
            return c;
        }

        int Free(string id)
        {
            var inv = _client.Trade.Inventory;
            int have = inv.Items.TryGetValue(id, out int n) ? n : 0;
            int put = _edit.Items.TryGetValue(id, out int m) ? m : 0;
            return have - put;
        }

        void Add(int k)
        {
            if (k >= _invIds.Count || Free(_invIds[k]) <= 0) return;
            string id = _invIds[k];
            _edit.Items.TryGetValue(id, out int n);
            _edit.Items[id] = n + 1;
            _client.OfferTrade(_edit);
        }

        void Remove(int k)
        {
            if (k >= _mineIds.Count) return;
            string id = _mineIds[k];
            if (!_edit.Items.TryGetValue(id, out int n)) return;
            if (n <= 1) _edit.Items.Remove(id); else _edit.Items[id] = n - 1;
            _client.OfferTrade(_edit);
        }

        void SetGold()
        {
            long.TryParse(_gold.text, out long g);
            _edit.Gold = System.Math.Max(0, System.Math.Min(g, _client.Trade.Inventory.Gold));
            _client.OfferTrade(_edit);
        }

        /// <summary>프레임마다: 서버 상태를 그린다. 거래가 열려 있으면 true.</summary>
        public bool Render()
        {
            var tr = _client.Trade;
            if (_client.TradeResultFresh)
            {
                _client.TradeResultFresh = false;
                LastResult = _t(OnlineKeys.TradeResult + tr.Result);
            }
            bool open = tr.Open;
            if (_root.gameObject.activeSelf != open) _root.gameObject.SetActive(open);
            if (!open) return false;
            _edit = Copy(tr.Mine);
            _title.SetText(_t(OnlineKeys.TradeTitle).Replace("{name}", tr.Partner ?? "?"));
            _mineHead.SetText(_t(OnlineKeys.TradeMine) + "  ·  " + _t(OnlineKeys.TradeGold).Replace("{n}", tr.Mine.Gold.ToString())
                + (tr.MyConfirm ? "  " + _t(OnlineKeys.TradeConfirmed) : string.Empty));
            _theirHead.SetText(_t(OnlineKeys.TradeTheirs) + "  ·  " + _t(OnlineKeys.TradeGold).Replace("{n}", tr.Theirs.Gold.ToString())
                + (tr.TheirConfirm ? "  " + _t(OnlineKeys.TradeConfirmed) : string.Empty));
            _mineIds.Clear();
            foreach (var kv in tr.Mine.Items) _mineIds.Add(kv.Key);
            _mineIds.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < _mineChips.Count; i++)
            {
                bool on = i < _mineIds.Count;
                _mineChips[i].gameObject.SetActive(on);
                if (on) _mineChips[i].SetText(_t(_mineIds[i]) + " ×" + tr.Mine.Items[_mineIds[i]]);
            }
            var theirs = new List<string>();
            foreach (var kv in tr.Theirs.Items) theirs.Add(_t(kv.Key) + " ×" + kv.Value);
            _theirList.SetText(string.Join("\n", theirs));
            _invIds.Clear();
            foreach (var kv in tr.Inventory.Items) _invIds.Add(kv.Key);
            _invIds.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < _invChips.Count; i++)
            {
                bool on = i < _invIds.Count;
                _invChips[i].gameObject.SetActive(on);
                if (!on) continue;
                int free = Free(_invIds[i]);
                _invChips[i].SetText(_t(_invIds[i]) + " ×" + free);
                _invChips[i].Enabled = free > 0;
            }
            _confirm.Enabled = !tr.MyConfirm;
            _status.SetText(LastResult ?? string.Empty);
            return true;
        }

        /// <summary>마지막 거래 결과 글(광장 화면도 보인다).</summary>
        public string LastResult;
    }
}

using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 구매 확인(ScreenId.ShopBuyConfirm, 08 11.3절). 가운데 560×360. 인자 Sku(string)·Qty(int)·Character(int, 지팡이 상자만 — 00 8.1절).
    /// 수량 조절 "− n +"(좌우 키도), 합계, [산다] → ctx.Shop.Buy(sku, qty, character). 성공하면 ui.shop.buy를 내고 닫는다(결과 true),
    /// 실패하면 오류 코드의 문구(error.*)를 창 안에 보인다. Esc·K·[취소] = 사지 않고 닫기.
    /// </summary>
    public sealed class ShopBuyConfirmScreen : ScreenBase
    {
        const float W = 560f, H = 360f, Pad = 24f, BtnW = 200f, BtnH = 56f, StepW = 56f;
        public const string BuySound = "ui.shop.buy";

        ShopBuyConfirmModel _m;
        UiLabel _name, _qty, _total, _error;
        UiButton _minus, _plus, _buy, _cancel;

        public override ScreenId Id => ScreenId.ShopBuyConfirm;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        protected override void BuildContent(RectTransform content)
        {
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, W, H).rectTransform;
            UiKit.Center(board, W, H);
            board.name = "ShopBuyConfirmBoard";
            float th = UiTheme.LineHeight(UiFontId.F32), lh = UiTheme.LineHeight(UiFontId.F24), sh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, board, T(MetaKeys.ShopBuyConfirmTitle), UiFontId.F32, Pad, Pad - 8f, W - 2f * Pad, th, UiTheme.Chalk,
                TextAnchor.MiddleCenter);
            float y = Pad + th + 8f;
            _name = UiKit.Label(Ctx, board, string.Empty, UiFontId.F24, Pad, y, W - 2f * Pad, lh, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += lh + 16f;
            float cx = W * 0.5f;
            _minus = UiKit.Button(Ctx, board, T(MetaKeys.ShopBuyConfirmMinus), cx - 80f - StepW, y, StepW, StepW, () => Step(-1),
                UiButtonStyle.Small, null);
            _qty = UiKit.Label(Ctx, board, string.Empty, UiFontId.F32, cx - 72f, y, 144f, StepW, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _plus = UiKit.Button(Ctx, board, T(MetaKeys.ShopBuyConfirmPlus), cx + 80f, y, StepW, StepW, () => Step(1),
                UiButtonStyle.Small, null);
            y += StepW + 12f;
            _total = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, W - 2f * Pad, sh, UiTheme.Chalk, TextAnchor.MiddleCenter);
            y += sh + 4f;
            _error = UiKit.Label(Ctx, board, string.Empty, UiFontId.F16, Pad, y, W - 2f * Pad, sh, UiTheme.Amber, TextAnchor.MiddleCenter);

            float by = H - Pad - BtnH;
            _buy = UiKit.Button(Ctx, board, T(MetaKeys.ShopBuy), cx - 16f - BtnW, by, BtnW, BtnH, Buy, UiButtonStyle.Primary, Focus);
            _cancel = UiKit.Button(Ctx, board, T(MetaKeys.CommonCancel), cx + 16f, by, BtnW, BtnH, () => Close(false), UiButtonStyle.Medium, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _m = ShopBuyConfirmModel.From(Args, Ctx != null && Ctx.Shop != null ? Ctx.Shop.GetState() : null);
            string name = T(_m.Sku ?? string.Empty);
            if (_m.Sku == Ids.Item.WandBox && _m.Character > 0) name += " · " + T(MetaKeys.Character(_m.Character));
            _name.SetText(name);
            _error.SetText(string.Empty);
            RefreshQty();
            Focus.SetCursor(_buy, false);
        }

        void RefreshQty()
        {
            _qty.SetText(N(_m.Qty));
            _total.SetText(F(MetaKeys.ShopBuyConfirmTotal, "price", F(MetaKeys.FormatGoldAmount, "n", N(_m.Total))));
            if (_m.CanDecrease) _minus.SetEnabled(); else _minus.Enabled = false;
            if (_m.CanIncrease) _plus.SetEnabled(); else _plus.Enabled = false;
        }

        void Step(int dir)
        {
            if (_m != null && _m.Step(dir))
            {
                _error.SetText(string.Empty);
                RefreshQty();
            }
        }

        void Buy()
        {
            if (_m == null) return;
            if (_m.Buy(Ctx != null ? Ctx.Shop : null))
            {
                Ctx?.Sound?.Play(BuySound, SoundBus.Ui);
                Close(true);
                return;
            }
            _error.SetText(T(_m.ErrorKey));
        }

        public override bool OnMenuKey(MenuKey key)
        {
            // 좌우 = 수량, 위아래 = [산다]·[취소] 사이(둘이 한 줄이라 좌우를 수량에 쓴다).
            if (key == MenuKey.Left) { Step(-1); return true; }
            if (key == MenuKey.Right) { Step(1); return true; }
            if (key == MenuKey.Up || key == MenuKey.Down)
            {
                Focus.SetCursor(Focus.Current == (IUiFocusable)_buy ? (IUiFocusable)_cancel : _buy);
                return true;
            }
            if (key == MenuKey.Cancel) { Close(false); return true; }
            return base.OnMenuKey(key);
        }
    }
}

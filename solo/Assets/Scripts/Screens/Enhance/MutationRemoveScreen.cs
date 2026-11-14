using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 변이 제거(08 10.10절, 07 4.7절) 720×480. 인자 WandId(long), MutationId(string — 처음 고른 것. 없으면 가장 오래된 것).
    /// 지울 변이를 고르고 값(1개당 3,000골드, TV-142)을 확인한다. 단계·기운·슬롯·새기기 누적은 그대로다.
    /// 원정 중·변이 결정 대기·골드 부족이면 [지운다]가 꺼진다(규칙 판정은 IEnhanceService.RemoveMutation).
    /// </summary>
    public sealed class MutationRemoveScreen : ScreenBase
    {
        public const float Width = 720f, Height = 480f;
        RectTransform _panel, _list;
        UiLabel _cost, _gold, _none;
        UiButton _confirm, _cancel;
        readonly List<UiButton> _rows = new List<UiButton>();
        WandData _wand;
        string _selected;

        public override ScreenId Id => ScreenId.MutationRemove;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;
        public string Selected => _selected;

        protected override void BuildContent(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            _panel = panel.rectTransform;
            UiKit.Center(_panel, Width, Height);
            panel.name = "MutationRemove";
            const float Pad = 32f;
            float w = Width - 2 * Pad;
            UiKit.LabelKey(Ctx, _panel, EnhanceNameKeys.MutationRemoveTitle, UiFontId.F24, Pad, Pad, w, 0f, UiTheme.PaperText);
            UiKit.LabelKey(Ctx, _panel, EnhanceNameKeys.RemovePick, UiFontId.F16, Pad, 80f, w, 0f, UiTheme.PaperText);
            _list = UiKit.Node(_panel, "List", Pad, 112f, w, 200f);
            _none = UiKit.LabelKey(Ctx, _list, EnhanceNameKeys.RemoveNone, UiFontId.F16, 0f, 0f, w, 0f, UiTheme.PaperText);
            _cost = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 320f, w, 0f, UiTheme.PaperText);
            _gold = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 348f, w, 0f, UiTheme.PaperText);
            const float BtnW = 240f, BtnH = 64f;
            float by = Height - Pad - BtnH;
            _confirm = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.RemoveConfirm), Pad, by, BtnW, BtnH, DoRemove, UiButtonStyle.Caution, null,
                EnhanceSoundIds.MutationRemove);
            _cancel = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.ReleaseCancel), Width - Pad - BtnW, by, BtnW, BtnH, () => Close(false), UiButtonStyle.Large);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            long id = Args.Get(ScreenArgKeys.WandId, 0L);
            _wand = Ctx != null && Ctx.Profile != null ? Ctx.Profile.Wand(id) : null;
            _selected = Args.Get<string>(ScreenArgKeys.MutationId);
            if (_wand != null && (_selected == null || !_wand.HasMutation(_selected)))
                _selected = _wand.Mutations.Count > 0 ? _wand.Mutations[0].Id : null;
            Rebuild();
        }

        void Rebuild()
        {
            foreach (var b in _rows) if (b != null) EnhanceUi.Destroy(b.gameObject);
            _rows.Clear();
            Focus.Clear();
            int count = _wand != null ? _wand.Mutations.Count : 0;
            _none.gameObject.SetActive(count == 0);
            const float RowH = 56f;
            for (int i = 0; i < count; i++)
            {
                string mid = _wand.Mutations[i].Id;
                var b = UiKit.Button(Ctx, _list, T(mid), 0f, i * (RowH + 8f), _list.rect.width > 0 ? _list.rect.width : Width - 64f, RowH,
                    () => Select(mid), UiButtonStyle.Plain, Focus, string.Empty, UiFontId.F16, UiKit.IconSprite(Ctx, mid, 32));
                b.Picked = mid == _selected;
                _rows.Add(b);
            }
            Focus.Add(_confirm);
            Focus.Add(_cancel);
            long price = Ctx != null && Ctx.Content != null && Ctx.Content.Enhance != null ? Ctx.Content.Enhance.MutationRemoveGold : 0;
            long gold = Ctx != null && Ctx.Profile != null ? Ctx.Profile.Currencies.Gold : 0;
            _cost.SetText(F(EnhanceNameKeys.RemoveCost, "gold", N(price)));
            _gold.SetText(F(EnhanceNameKeys.FormatGold, "n", N(gold)));
            if (_selected == null) _confirm.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.InvalidArgument));
            else if (_wand.PendingOffer != null) _confirm.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.MutationPending));
            else if (gold < price) _confirm.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.NotEnoughGold));
            else _confirm.SetEnabled();
            Focus.SetCursor(_rows.Count > 0 ? (IUiFocusable)_rows[0] : _cancel, false);
        }

        void Select(string mid)
        {
            _selected = mid;
            for (int i = 0; i < _rows.Count; i++) _rows[i].Picked = _wand.Mutations[i].Id == mid;
            Focus.SetCursor(_confirm, false);
        }

        void DoRemove()
        {
            if (Ctx == null || Ctx.Enhance == null || _wand == null || _selected == null) return;
            var r = Ctx.Enhance.RemoveMutation(_wand.Id, _selected);
            if (!r.Ok) { Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error))); Rebuild(); return; }
            Close(true);
        }

        public override bool OnBack() { Close(false); return true; }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(false); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }
}

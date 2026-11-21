using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 한 줄 알림(IScreenManager.Toast). 오른쪽 위 480×96(K-11 쪽지), 위에서 내려오고 seconds 뒤 스스로 닫힌다.
    /// Toast 층이라 Top·입력에 세지 않는다. 여럿이면 아래로 쌓인다. id는 ScreenId.Toast(Register 하지 않고 관리자가 직접 올린다).
    /// </summary>
    public sealed class UiToastScreen : ScreenBase
    {
        readonly ScreenManager _owner;
        readonly float _seconds;
        float _age;
        int _slot;
        RectTransform _box;
        UiLabel _text;

        public UiToastScreen(ScreenManager owner, float seconds)
        {
            _owner = owner;
            _seconds = seconds > 0f ? seconds : 3f;
        }

        public override ScreenId Id => ScreenId.Toast;
        public override ScreenLayer Layer => ScreenLayer.Toast;
        public override bool BlocksInput => false;
        protected override bool HasFrame => false;
        public float Age => _age;

        protected override void BuildContent(RectTransform content)
        {
            var p = UiKit.Panel(Ctx, content, UiPanelKind.Toast, 0f, 0f, ScreenManager.ToastWidth, ScreenManager.ToastHeight);
            p.raycastTarget = false;
            _box = p.rectTransform;
            _box.name = "Toast";
            const float Pad = 16f;
            _text = UiKit.Label(Ctx, _box, string.Empty, UiFontId.F16, Pad, Pad, ScreenManager.ToastWidth - 2f * Pad,
                ScreenManager.ToastHeight - 2f * Pad, UiTheme.PaperText, TextAnchor.MiddleLeft, wrap: true);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _age = 0f;
            _text.SetText(Args.Get(ScreenArgKeys.Text, string.Empty));
            Layout();
        }

        public void SetSlotIndex(int i)
        {
            _slot = i;
            Layout();
        }

        void Layout()
        {
            if (_box == null) return;
            float slideSeconds = _owner != null ? _owner.Tuning.ToastSlideSeconds : ScreenManager.ToastSlideSeconds;
            float t = slideSeconds > 0f ? Mathf.Clamp01(_age / slideSeconds) : 1f;
            float y = ScreenManager.ToastMargin + _slot * (ScreenManager.ToastHeight + ScreenManager.ToastGap);
            float slide = (1f - t) * (ScreenManager.ToastHeight + ScreenManager.ToastMargin);
            UiKit.Place(_box, UiScale.RefWidth - ScreenManager.ToastMargin - ScreenManager.ToastWidth, y - slide,
                ScreenManager.ToastWidth, ScreenManager.ToastHeight);
        }

        public override void Tick(float dt)
        {
            _age += dt;
            Layout();
            if (_age >= _seconds) _owner?.Close(this);
        }

        public override bool OnBack() => false;
        public override bool OnMenuKey(MenuKey key) => false;
    }
}

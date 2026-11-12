using System;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 Loading: 한 줄 도움말, 진행률 0~1, 버튼 없음. 전체 화면. Esc 로 나가지 않는다(소비만)
    public sealed class LoadingModel : DialogScreen
    {
        public LoadingModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.Loading;
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;

        public string HintKey { get; private set; } = string.Empty;

        // 0 ~ 1. 거꾸로 가지 않는다(표시가 뒤로 물러나지 않게) [임시]
        public float Progress { get; private set; }

        public void SetProgress(float progress)
        {
            float p = float.IsNaN(progress) ? 0f : Math.Max(0f, Math.Min(1f, progress));
            if (p > Progress)
            {
                Progress = p;
            }
        }

        public void SetHint(string hintKey) => HintKey = hintKey ?? throw new ArgumentNullException(nameof(hintKey));

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            Progress = 0f;
            HintKey = args.Payload is LoadingRequest r ? r.HintKey : string.Empty;
            return null;
        }
    }
}

using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 불러오기 흐름(순수 클래스). 인자 NextScreen(ScreenId)·NextArgs(ScreenArgs)를 받아, 한 프레임 그린 뒤(두 번째 Step)
    /// 자신을 NextScreen(NextArgs)으로 Replace 한다. NextScreen이 없으면 Pop. 한 번만 넘긴다.
    /// 첫 Step은 Push와 같은 프레임에 올 수 있으므로(관리자의 키 처리 → Tick) 두 번째 Step에서 넘겨야 화면이 적어도 한 번 그려진다.
    /// </summary>
    public sealed class UiLoadingFlow
    {
        public const int StepsBeforeHandoff = 2;
        int _steps;

        public ScreenId Next { get; private set; }
        public ScreenArgs NextArgs { get; private set; }
        public bool Done { get; private set; }
        /// <summary>진행률 0~1(막대 640×16).</summary>
        public float Progress => Done ? 1f : Mathf.Clamp01(_steps / (float)StepsBeforeHandoff);

        public void Begin(ScreenArgs args)
        {
            _steps = 0;
            Done = false;
            Next = args != null ? args.Get(ScreenArgKeys.NextScreen, ScreenId.None) : ScreenId.None;
            NextArgs = args != null ? args.Get<ScreenArgs>(ScreenArgKeys.NextArgs) : null;
        }

        /// <summary>한 프레임. 넘겼으면 true.</summary>
        public bool Step(IScreenManager screens)
        {
            if (Done) return false;
            _steps++;
            if (_steps < StepsBeforeHandoff) return false;
            Done = true;
            if (screens == null) return true;
            if (Next == ScreenId.None || Next == ScreenId.Loading) screens.Pop();
            else screens.Replace(Next, NextArgs);
            return true;
        }
    }

    /// <summary>
    /// 불러오는 화면(ScreenId.Loading, 전체, 08 1.6절): 배경(S-2601), 작은 모닥불(K-44), 제목 str.loading.title, 진행 막대 640×16.
    /// ScreenArgs.ViaLoading(entry, next, nextArgs)로 연다. 버튼 없음, Esc 무시.
    /// </summary>
    public sealed class UiLoadingScreen : ScreenBase
    {
        public const float BarWidth = 640f, BarHeight = 16f;
        readonly UiLoadingFlow _flow = new UiLoadingFlow();
        UiProgressBar _bar;

        public override ScreenId Id => ScreenId.Loading;
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;
        public override bool BlocksInput => true;
        protected override bool HasFrame => false;
        public UiLoadingFlow Flow => _flow;

        protected override void BuildContent(RectTransform content)
        {
            var bg = UiKit.KitSprite(Ctx, "S-2601");
            var bgImg = UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            if (bg.IsValid) { UiKit.SetSprite(bgImg, bg); bgImg.color = Color.white; }
            float cx = (UiScale.RefWidth - BarWidth) * 0.5f, cy = UiScale.RefHeight * 0.5f;
            var fire = UiKit.KitSprite(Ctx, "K-44", "loading_campfire");
            if (fire.IsValid) UiKit.Image(content, fire.Sprite, (UiScale.RefWidth - 64f) * 0.5f, cy - 140f, 64f, 64f);
            float lh = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, content, T(UiNameKeys.LoadingTitle), UiFontId.F32, cx, cy - 64f, BarWidth, lh, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _bar = UiKit.ProgressBar(Ctx, content, cx, cy, BarWidth, BarHeight);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _flow.Begin(Args);
            if (_bar != null) _bar.Value01 = 0f;
        }

        public override void Tick(float dt)
        {
            if (_bar != null) _bar.Value01 = _flow.Progress;
            if (_flow.Step(Screens) && _bar != null) _bar.Value01 = 1f;
        }

        public override bool OnBack() => true;
        public override bool OnMenuKey(MenuKey key) => true;
    }
}

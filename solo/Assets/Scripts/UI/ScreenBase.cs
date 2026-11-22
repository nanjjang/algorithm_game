using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// IScreen의 기본 구현. 화면 작업은 이것을 물려받아 BuildContent만 채운다.
    /// - Fullscreen이면(HasFrame) 뼈대를 만든다(08 1.1절): 바탕(나무 어두움) + 위 띠 80px(K-05, 제목 32px) +
    ///   아래 조작 안내 띠 48px(K-06, "J 고르기 · K 뒤로 · Esc 나가기") + 그 사이 내용 952px(Content, 1920×952, 왼쪽 위 원점).
    /// - Panel·Modal·HudOverlay·Toast는 뼈대 없이 Content = Root(1920×1080).
    /// - 메뉴 키는 Focus(UiFocusGroup)로 넘긴다. Esc(OnBack)는 false(관리자가 Pop). 탭이 있으면 Tabs가 Q/E를 먼저 받는다.
    /// - 화면 글자는 T(열쇠)·F(열쇠, 이름, 값 …)로만.
    /// </summary>
    public abstract class ScreenBase : IScreen
    {
        public const float TopBarHeight = 80f, BottomBarHeight = 48f;
        public const float ContentHeight = UiScale.RefHeight - TopBarHeight - BottomBarHeight; // 952

        protected GameContext Ctx { get; private set; }
        protected RectTransform Root { get; private set; }
        /// <summary>내용 자리. Fullscreen 뼈대면 (0, 80)에서 1920×952, 아니면 Root.</summary>
        protected RectTransform Content { get; private set; }
        protected RectTransform TopBar { get; private set; }
        protected RectTransform BottomBar { get; private set; }
        protected UiLabel TitleLabel { get; private set; }
        protected UiLabel HintLabel { get; private set; }
        /// <summary>이 화면의 키보드 커서 묶음.</summary>
        protected UiFocusGroup Focus { get; private set; }
        /// <summary>Q/E를 먼저 받을 탭 줄(있으면).</summary>
        protected UiTabs Tabs;
        /// <summary>마지막 OnEnter의 인자.</summary>
        protected ScreenArgs Args { get; private set; }

        public abstract ScreenId Id { get; }
        public virtual ScreenLayer Layer => ScreenLayer.Fullscreen;
        /// <summary>기본: Modal과 Fullscreen은 아래 화면의 입력을 막는다.</summary>
        public virtual bool BlocksInput => Layer == ScreenLayer.Modal || Layer == ScreenLayer.Fullscreen;

        /// <summary>위·아래 띠 뼈대를 그리는가(기본 Fullscreen만).</summary>
        protected virtual bool HasFrame => Layer == ScreenLayer.Fullscreen;
        /// <summary>제목 이름 표 열쇠(null이면 제목 없음).</summary>
        protected virtual string TitleKey => null;
        /// <summary>아래 띠의 안내(null이면 기본 3마디).</summary>
        protected virtual UiKeyHint[] KeyHints => null;

        /// <summary>관리자(없으면 null — 테스트).</summary>
        protected ScreenManager Manager => Ctx != null ? Ctx.Screens as ScreenManager : null;
        protected IScreenManager Screens => Ctx != null ? Ctx.Screens : null;

        public void Build(GameContext ctx, RectTransform root)
        {
            Ctx = ctx;
            Root = root;
            var mgr = Manager;
            Focus = new UiFocusGroup(mgr != null ? mgr.InputMode : null, ctx != null ? ctx.Sound : null) { Tooltips = mgr };
            if (HasFrame)
            {
                // 바탕: 밤색 그라데이션 + 위 가운데 빛 번짐 + 비네트(빈 검은 바탕 대신). 화면이 자기 배경 그림을 깔면 그 아래에 깔린다.
                // 반응형: 바탕은 창 끝까지, 위·아래 띠의 판은 좌우와 바깥쪽(위/아래) 끝까지 넓힌다(UiFullBleed). 띠 안의 글·단추는
                // 1920 안전 영역 그대로(TopBar·BottomBar 좌표계는 바뀌지 않는다).
                var backdrop = UiBackdrop.Screen(root);
                backdrop.name = "Background";
                UiFullBleed.Fill(backdrop);
                var topBg = UiKit.Panel(ctx, root, UiPanelKind.ScreenHeading, 0f, 0f, UiScale.RefWidth, TopBarHeight);
                topBg.name = "TopBarBg";
                UiFullBleed.Fill(topBg.rectTransform, UiBleedEdges.Horizontal | UiBleedEdges.Top);
                // 제목 띠 아래 그림자와 놋쇠 장식 줄(둘 다 좌우 끝까지).
                var fade = UiBackdrop.Fade(root, 0f, TopBarHeight, UiScale.RefWidth, UiTheme.Space6, UiTheme.Shadow);
                UiFullBleed.Fill(fade.rectTransform, UiBleedEdges.Horizontal);
                var rule = UiKit.AccentRule(root, 0f, TopBarHeight - 3f, UiScale.RefWidth);
                for (int i = 0; i < rule.childCount; i++) UiFullBleed.StretchX((RectTransform)rule.GetChild(i));
                UiFullBleed.Fill(rule, UiBleedEdges.Horizontal);
                TopBar = UiKit.Node(root, "TopBar", 0f, 0f, UiScale.RefWidth, TopBarHeight);
                float lh = UiTheme.LineHeight(UiFontId.F32);
                UiKit.Diamond(TopBar, UiTheme.Gutter + 6f, TopBarHeight * 0.5f, 12f, UiTheme.Brass);
                TitleLabel = UiKit.Label(ctx, TopBar, TitleKey != null ? T(TitleKey) : string.Empty, UiFontId.F32,
                    UiTheme.Gutter + 28f, (TopBarHeight - lh) * 0.5f, UiScale.RefWidth - 96f - 28f, lh, UiTheme.Chalk, TextAnchor.MiddleLeft);
                UiKit.DropShadow(TitleLabel);
                Content = UiKit.Node(root, "Content", 0f, TopBarHeight, UiScale.RefWidth, ContentHeight);
                var bottomBg = UiKit.Panel(ctx, root, UiPanelKind.ControlStrip, 0f, UiScale.RefHeight - BottomBarHeight, UiScale.RefWidth, BottomBarHeight);
                bottomBg.name = "BottomBarBg";
                bottomBg.raycastTarget = false;
                UiFullBleed.Fill(bottomBg.rectTransform, UiBleedEdges.Horizontal | UiBleedEdges.Bottom);
                HintLabel = UiKit.KeyHintBar(ctx, root, 0f, UiScale.RefHeight - BottomBarHeight, UiScale.RefWidth, BottomBarHeight, KeyHints);
                BottomBar = (RectTransform)HintLabel.transform.parent;
                // 안내 띠의 판은 위 BottomBarBg가 창 끝까지 그린다 — 안전 영역 안의 같은 판은 숨긴다(글 자리는 그대로).
                var strip = BottomBar.GetComponent<UnityEngine.UI.Image>();
                if (strip != null) strip.enabled = false;
            }
            else
            {
                Content = root;
            }
            BuildContent(Content);
        }

        /// <summary>내용을 만든다(처음 열릴 때 한 번). 좌표는 Content의 왼쪽 위 원점 px.</summary>
        protected abstract void BuildContent(RectTransform content);

        public virtual void OnEnter(ScreenArgs args)
        {
            Args = args ?? new ScreenArgs();
            Focus?.UpdateTooltip();
        }

        /// <summary>닫힐 때 떠 있는 툴팁을 내리는가(Toast 층 화면 — 토스트·툴팁 자신 — 은 내리지 않는다).</summary>
        protected virtual bool HidesTooltipOnExit => Layer != ScreenLayer.Toast;

        public virtual void OnExit()
        {
            if (HidesTooltipOnExit) Manager?.HideTooltip();
        }

        public virtual bool OnBack() => false;

        public virtual bool OnMenuKey(MenuKey key)
        {
            if (Tabs != null && (key == MenuKey.PrevTab || key == MenuKey.NextTab) && Tabs.HandleKey(key)) return true;
            return Focus != null && Focus.HandleKey(key);
        }

        public virtual void Tick(float dt) { }

        // ---------------------------------------------------------------- 도우미

        /// <summary>
        /// 화면 뒤 배경 그림(키트 화면 그림 id·variant)을 Root 맨 아래(바탕 바로 위)에 흐리게 깔고 비네트를 얹는다. 그림이 없으면 아무것도 하지 않는다.
        /// alpha = 그림 불투명도(글이 잘 읽히게 낮게). 꾸밈만 — 화면 규칙과 무관.
        /// </summary>
        protected RectTransform AddBackdropArt(string id, string variant, float alpha = 0.4f)
        {
            if (Root == null) return null;
            var sp = UiKit.KitSprite(Ctx, id, variant);
            Sprite s = sp.IsValid ? sp.Sprite : null;
            if (s == null && Ctx != null && Ctx.Art != null)
            {
                try { s = Ctx.Art.Background(id); } catch (System.Exception) { s = null; }
            }
            if (s == null) return null;
            // 창 끝까지(UiFullBleed): 칸은 늘이고, 그림은 비율을 지켜 덮는다(가운데에서 잘림).
            var node = UiKit.Node(Root, "BackdropArt", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.Fill(node);
            var img = UiKit.Image(node, s, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WithAlpha(Color.white, alpha), false);
            UiFullBleed.Cover(img);
            UiBackdrop.VignetteOver(node, 0.75f);
            // 바탕(Background) 바로 위, 위 띠·내용 아래.
            var bg = Root.Find("Background");
            node.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);
            return node;
        }

        /// <summary>이름 표 글.</summary>
        protected string T(string key) => Ctx != null && Ctx.Names != null ? Ctx.Names.Get(key) : key;

        /// <summary>이름 표 틀 채우기: F("str.x", "n", 3).</summary>
        protected string F(string key, params object[] nameValuePairs) =>
            Ctx != null && Ctx.Names != null ? Ctx.Names.Format(key, nameValuePairs) : key;

        protected string N(long value) => Ctx != null && Ctx.Names != null ? Ctx.Names.Number(value) : UiTextFit.FormatThousands(value);

        /// <summary>제목을 바꾼다(이미 이름 표를 거친 글).</summary>
        protected void SetTitle(string text) { if (TitleLabel != null) TitleLabel.SetText(text); }

        /// <summary>아래 띠 안내를 바꾼다.</summary>
        protected void SetHints(UiKeyHint[] hints)
        {
            if (HintLabel != null) HintLabel.SetText(UiKeyHint.Compose(Ctx != null ? Ctx.Names : null, hints ?? UiKeyHint.Defaults));
        }

        /// <summary>화면 사건 소리(ui.* 열쇠).</summary>
        protected void PlayUi(string eventId) => UiSounds.Play(Ctx != null ? Ctx.Sound : null, eventId);

        /// <summary>이 화면을 닫는다(맨 위일 때 Pop, 아니면 그 칸만).</summary>
        protected void Close(object result = null)
        {
            Screens?.Close(this, result);
        }
    }
}

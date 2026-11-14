using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.Integration
{
    /// <summary>
    /// 연기 테스트용 타이틀 자리 화면. 진짜 타이틀(G18)이 등록되기 전에도 부팅 → 첫 화면 길이 그림·글자와 함께 도는지 본다.
    /// 글자는 전부 이름 표에서 가져온다(코드에 한국어 없음). 테스트 어셈블리에만 있고 게임에는 들어가지 않는다.
    /// </summary>
    public sealed class SmokeTitleScreen : ScreenBase
    {
        public const string TitleTextKey = "str.title.title";

        public override ScreenId Id => ScreenId.Title;
        protected override bool HasFrame => false;

        public UiLabel Title, StartHint;
        public UnityEngine.UI.Image Backdrop;

        protected override void BuildContent(RectTransform content)
        {
            Backdrop = UiKit.Image(content, Ctx.Art.Background("title"), 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, null, false);
            Title = UiKit.LabelKey(Ctx, content, TitleTextKey, UiFontId.F64, 0f, 160f, UiScale.RefWidth, 80f, UiTheme.Chalk, TextAnchor.MiddleCenter);
            Title.Text.gameObject.AddComponent<UnityEngine.UI.Outline>().effectColor = UiTheme.Ink;
            StartHint = UiKit.LabelKey(Ctx, content, "str.title.start_hint", UiFontId.F32, 0f, 820f, UiScale.RefWidth, 48f, UiTheme.SelectYellow, TextAnchor.MiddleCenter);
            UiKit.Panel(Ctx, content, UiPanelKind.Board, 48f, 936f, 520f, 96f);
            UiKit.LabelKey(Ctx, content, "str.title.profile.offline", UiFontId.F24, 72f, 952f, 480f, 32f);
            UiKit.Label(Ctx, content, F("str.title.build_version", "version", Ctx.BuildVersion ?? string.Empty), UiFontId.F16, 72f, 992f, 480f, 24f, UiTheme.SlateBlue);
            UiKit.LabelKey(Ctx, content, "str.title.credits_label", UiFontId.F12, 1700f, 1040f, 200f, 16f, UiTheme.SlateBlue, TextAnchor.MiddleRight);
        }
    }

    /// <summary>
    /// 연기 테스트용 메인 메뉴 자리 화면: 뼈대(위 띠·아래 조작 안내), 메뉴 버튼, 캐릭터 다섯의 선택 그림·초상·지팡이, 아이콘, 9조각 판,
    /// 진행 막대·토글·슬라이더·키 그림, 글자 크기 여섯 가지. UI 기반(F3)과 그림 창고(F2)가 실제 임포트된 에셋으로 붙는지 본다.
    /// </summary>
    public sealed class SmokeMainMenuScreen : ScreenBase
    {
        static readonly string[] MenuKeys =
        {
            "str.main_menu.mode.expedition", "str.main_menu.item.enhance", "str.main_menu.item.assembly_library",
            "str.main_menu.item.practice", "str.main_menu.item.records", "str.main_menu.item.wardrobe",
            "str.main_menu.item.shop", "str.main_menu.item.settings", "str.main_menu.item.exit"
        };

        static readonly string[] IconIds =
        {
            "block.magic_bolt", "block.fireball", "art.burst", "art.recoil", "element.fire", "element.ice", "currency.gold"
        };

        static readonly UiFontId[] FontSizes = { UiFontId.F12, UiFontId.F16, UiFontId.F24, UiFontId.F32, UiFontId.F48, UiFontId.F64 };

        public override ScreenId Id => ScreenId.MainMenu;
        protected override string TitleKey => "str.main_menu.title";

        public readonly UiButton[] Buttons = new UiButton[MenuKeys.Length];
        public readonly UnityEngine.UI.Image[] Selections = new UnityEngine.UI.Image[Ids.Char.Count];
        public int Clicks;

        protected override void BuildContent(RectTransform content)
        {
            // 왼쪽: 메뉴 버튼(세로)
            UiKit.Panel(Ctx, content, UiPanelKind.Board, 48f, 24f, 480f, 904f);
            for (int i = 0; i < MenuKeys.Length; i++)
            {
                var style = i == 0 ? UiButtonStyle.Primary : i == MenuKeys.Length - 1 ? UiButtonStyle.Caution : UiButtonStyle.Large;
                Buttons[i] = UiKit.Button(Ctx, content, T(MenuKeys[i]), 80f, 56f + i * 92f, 416f, 72f, () => Clicks++, style, Focus);
            }
            Focus.Wrap = true;

            // 가운데: 캐릭터 다섯(선택 그림 192, 이름, 초상 64, 지팡이 큰 그림과 아이콘)
            UiKit.Panel(Ctx, content, UiPanelKind.Recess, 560f, 24f, 1312f, 520f);
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                string id = Ids.Char.Of(c);
                float x = 592f + (c - 1) * 256f;
                Selections[c - 1] = UiKit.Image(content, Ctx.Art.CharacterSelection(id), x, 48f, 192f, 192f);
                UiKit.LabelKey(Ctx, content, id, UiFontId.F24, x, 248f, 192f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                UiKit.Image(content, Ctx.Art.CharacterPortrait(id, 0), x, 296f, 64f, 64f);
                UiKit.Image(content, Ctx.Art.WandIcon(id, (c - 1) * 3), x + 72f, 296f, 64f, 64f);
                UiKit.Image(content, Ctx.Art.WandInspection(id, 13), x, 368f, 160f, 160f);
            }

            // 아래: 종이 판 위의 아이콘·보스 초상·위젯
            UiKit.Panel(Ctx, content, UiPanelKind.Paper, 560f, 568f, 1312f, 360f);
            for (int i = 0; i < IconIds.Length; i++) UiKit.Icon(Ctx, content, IconIds[i], 48, 592f + i * 64f, 592f);
            UiKit.Image(content, Ctx.Art.BossPortrait(Ids.Boss.KilnBoar), 1080f, 584f, 64f, 64f);
            UiKit.Image(content, Ctx.Art.BossHudIcon(Ids.Boss.MirrorPhantom), 1160f, 600f, 32f, 32f);
            UiKit.ProgressBar(Ctx, content, 592f, 664f, 480f, 24f, 0.6f);
            UiKit.Toggle(Ctx, content, T("str.main_menu.item.settings"), 592f, 704f, 480f, 48f, true, _ => { }, null);
            UiKit.Slider(Ctx, content, 592f, 768f, 480f, 48f, 70, _ => { }, 0, 100, 1, null);
            UiKit.KeyCap(Ctx, content, Key.J, 592f, 840f, 32f);
            UiKit.KeyCap(Ctx, content, Key.K, 632f, 840f, 32f, true);
            UiKit.Divider(Ctx, content, 1100f, 664f, 240f, true);

            // 글자 크기 여섯 가지(종이 판 위 → 먹 글자)
            float y = 584f;
            for (int i = 0; i < FontSizes.Length; i++)
            {
                float lh = UiTheme.LineHeight(FontSizes[i]);
                UiKit.LabelKey(Ctx, content, i < 3 ? "str.title.start_hint" : "char.1", FontSizes[i], 1240f, y, 600f, lh, UiTheme.PaperText);
                y += lh + 4f;
            }
        }
    }
}

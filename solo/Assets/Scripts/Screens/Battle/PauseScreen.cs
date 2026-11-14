using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Battle
{
    /// <summary>
    /// 일시정지(ScreenId.Pause, Modal). 08 요약서 7절.
    /// 가운데 480×560: 버튼 다섯(계속, 설정, 조작 보기, 판 포기(조심), 메인 메뉴로). Esc = [계속].
    /// 왼쪽 400×560: 지금 판 요약(캐릭터 초상·이름, 지팡이 아이콘·단계, 조립 블록 아이콘 줄, 지금 점수, 누적 일시정지 시간 — 매 프레임).
    /// 여는 것은 Battle뿐이다(Paused = true는 Battle이 이미 했다).
    /// </summary>
    public sealed class PauseScreen : ScreenBase
    {
        public const float MenuW = 480f, MenuH = 560f, SummaryW = 400f, SummaryH = 560f, Gap = 16f;
        const float Pad = 24f, ButtonW = 400f, ButtonH = 64f, ButtonGap = 16f;
        const int PortraitPx = 64, WandIconPx = 64, BlockIconPx = 32;
        const float BlockGap = 8f;

        PauseController _pause;
        UImage _portrait, _wandIcon;
        UiLabel _charName, _wandStage, _scoreValue, _pauseTotal;
        RectTransform _loadoutRow;
        readonly List<GameObject> _blockIcons = new List<GameObject>();

        public override ScreenId Id => ScreenId.Pause;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public static float MenuX => (UiScale.RefWidth - MenuW) * 0.5f;
        public static float MenuY => (UiScale.RefHeight - MenuH) * 0.5f;
        public static float SummaryX => MenuX - Gap - SummaryW;

        protected override void BuildContent(RectTransform content)
        {
            _pause = new PauseController(Ctx, () => Close());
            BuildMenu(content);
            BuildSummary(content);
            Focus.Wrap = true;
        }

        void BuildMenu(RectTransform content)
        {
            var panel = UiKit.OrnatePanel(Ctx, content, MenuX, MenuY, MenuW, MenuH).rectTransform;
            panel.name = "PauseMenu";
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.DropShadow(UiKit.LabelKey(Ctx, panel, PauseMenuModel.TitleKey, UiFontId.F32, Pad, Pad, MenuW - Pad * 2f, th, UiTheme.Chalk, TextAnchor.MiddleCenter), 2f);
            UiKit.AccentRule(panel, MenuW * 0.25f, Pad + th + 2f, MenuW * 0.5f);
            var items = PauseMenuModel.Items;
            float y = Pad + th + Pad;
            float x = (MenuW - ButtonW) * 0.5f;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var style = PauseMenuModel.IsCaution(item) ? UiButtonStyle.Caution : UiButtonStyle.Large;
                UiKit.Button(Ctx, panel, T(PauseMenuModel.LabelKey(item)), x, y, ButtonW, ButtonH, () => _pause.Activate(item), style, Focus);
                y += ButtonH + ButtonGap;
            }
        }

        void BuildSummary(RectTransform content)
        {
            var panel = UiKit.Plate(Ctx, content, UiPanelKind.Board, SummaryX, MenuY, SummaryW, SummaryH).rectTransform;
            panel.name = "PauseSummary";
            float w = SummaryW - Pad * 2f;
            float y = Pad;
            float th = UiTheme.LineHeight(UiFontId.F24);
            float bh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.LabelKey(Ctx, panel, "str.pause.summary.title", UiFontId.F24, Pad, y, w, th, UiTheme.Chalk);
            y += th + 16f;

            // 캐릭터: 초상 + 표시 이름
            UiKit.LabelKey(Ctx, panel, "str.pause.summary.character", UiFontId.F16, Pad, y, w, bh, UiTheme.SlateBlue);
            y += bh + 4f;
            _portrait = UiKit.Image(panel, null, Pad, y, PortraitPx, PortraitPx);
            _charName = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, Pad + PortraitPx + 16f, y + (PortraitPx - th) * 0.5f, w - PortraitPx - 16f, th);
            y += PortraitPx + 16f;

            // 지팡이: 아이콘 + 단계
            UiKit.LabelKey(Ctx, panel, "str.pause.summary.wand", UiFontId.F16, Pad, y, w, bh, UiTheme.SlateBlue);
            y += bh + 4f;
            _wandIcon = UiKit.Image(panel, null, Pad, y, WandIconPx, WandIconPx);
            _wandStage = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, Pad + WandIconPx + 16f, y + (WandIconPx - th) * 0.5f, w - WandIconPx - 16f, th);
            y += WandIconPx + 16f;

            // 조립: 블록 아이콘 줄(코어 먼저)
            UiKit.LabelKey(Ctx, panel, "str.pause.summary.loadout", UiFontId.F16, Pad, y, w, bh, UiTheme.SlateBlue);
            y += bh + 4f;
            _loadoutRow = UiKit.Node(panel, "LoadoutRow", Pad, y, w, BlockIconPx);
            y += BlockIconPx + 16f;

            // 지금 점수
            UiKit.LabelKey(Ctx, panel, "str.battle_hud.score", UiFontId.F16, Pad, y, w, bh, UiTheme.SlateBlue);
            y += bh + 4f;
            _scoreValue = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F32, Pad, y, w, UiTheme.LineHeight(UiFontId.F32), UiTheme.Gold);
            y += UiTheme.LineHeight(UiFontId.F32) + 16f;

            // 누적 일시정지 시간(매 프레임)
            _pauseTotal = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, Pad, y, w, bh, UiTheme.Chalk);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            if (Focus != null && Focus.Items.Count > 0) Focus.SetCursor(0, false);
            Refresh();
        }

        void Refresh()
        {
            var s = Ctx != null ? Ctx.Session : null;
            var w = s != null ? s.MainWorld : null;
            var sum = PauseSummaryModel.Build(w, s != null ? s.PausedSeconds : 0f);
            var art = Ctx != null ? Ctx.Art : null;

            if (_portrait != null)
            {
                Sprite sp = art != null && sum.CharId != null ? art.CharacterPortrait(sum.CharId, 0) : null;
                if (sp == null && art != null) sp = art.Placeholder(PortraitPx, PortraitPx);
                _portrait.sprite = sp;
                _portrait.enabled = sp != null;
            }
            _charName?.SetText(sum.CharId != null ? T(sum.CharId) : T("str.common.none"));

            if (_wandIcon != null)
            {
                Sprite sp = art != null && sum.CharId != null ? art.WandIcon(sum.CharId, sum.WandStage) : null;
                if (sp == null && art != null) sp = art.Placeholder(WandIconPx, WandIconPx);
                _wandIcon.sprite = sp;
                _wandIcon.enabled = sp != null;
            }
            _wandStage?.SetText(F("str.format.wand_stage", "n", sum.WandStage));

            RebuildLoadout(sum);
            _scoreValue?.SetText(N(sum.Score));
            UpdatePauseTotal();
        }

        void RebuildLoadout(PauseSummary sum)
        {
            for (int i = 0; i < _blockIcons.Count; i++) if (_blockIcons[i] != null) Object.Destroy(_blockIcons[i]);
            _blockIcons.Clear();
            if (_loadoutRow == null) return;
            float x = 0f;
            AddBlock(sum.CoreBlockId, ref x);
            for (int i = 0; i < sum.LinkBlockIds.Count; i++) AddBlock(sum.LinkBlockIds[i], ref x);
        }

        void AddBlock(string blockId, ref float x)
        {
            if (x + BlockIconPx > SummaryW - Pad * 2f) return;
            GameObject go;
            if (blockId != null) go = UiKit.Icon(Ctx, _loadoutRow, blockId, BlockIconPx, x, 0f).gameObject;
            else go = UiKit.Panel(Ctx, _loadoutRow, UiPanelKind.Recess, x, 0f, BlockIconPx, BlockIconPx).gameObject;
            _blockIcons.Add(go);
            x += BlockIconPx + BlockGap;
        }

        void UpdatePauseTotal()
        {
            if (_pauseTotal == null) return;
            var s = Ctx != null ? Ctx.Session : null;
            _pauseTotal.SetText(PauseSummaryModel.PauseTotalText(Ctx != null ? Ctx.Names : null, s != null ? s.PausedSeconds : 0f));
        }

        public override void Tick(float dt)
        {
            UpdatePauseTotal();
        }

        /// <summary>Esc(판 열린 상태) = [계속](08 7절, N-02).</summary>
        public override bool OnBack()
        {
            Resume();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Resume(); return true; }
            return base.OnMenuKey(key);
        }

        void Resume()
        {
            if (_pause == null) _pause = new PauseController(Ctx, () => Close());
            _pause.Resume();
        }
    }
}

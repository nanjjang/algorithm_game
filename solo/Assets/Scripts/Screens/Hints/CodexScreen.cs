using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Hints
{
    /// <summary>
    /// 조작·도감(ScreenId.Codex, 일시정지 메뉴의 한 항목): 튜토리얼에서 배운 것을 언제든 다시 본다 — 조작(지금 키 배치), 과열, 스침,
    /// 콤보·결정, 비기(그 캐릭터의 트라이 11종 커맨드·비용·효과), 블록 16종, 변이 6종(이득·대가), 변덕 8종, 약점·저항.
    /// 가운데 1440×840 창. 왼쪽 세로 탭(방향키 위아래·Q/E), 오른쪽 내용. 아래 [힌트 다시 보기](본 문맥 힌트를 다시 띄운다) [닫기].
    /// 수치는 데이터에서 읽어 서식으로 넣는다(CodexModel).
    /// </summary>
    public sealed class CodexScreen : ScreenBase
    {
        public const string TitleKeyName = "str.codex.title";
        public const string ResetHintsKey = "str.codex.reset_hints";
        public const string ResetDoneKey = "str.settings.reset_hints_done";
        public const string CloseKey = "str.common.close";
        const float W = 1440f, H = 840f, Pad = 28f, TabW = 248f, TabH = 64f, TabGap = 8f, RowH = 46f;

        public override ScreenId Id => ScreenId.Codex;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        RectTransform _panel, _body;
        UiLabel _sectionTitle;
        readonly List<UiButton> _tabs = new List<UiButton>();
        int _section;

        public int Section => _section;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.ModalDim, true, "Dim");
            float x0 = (UiScale.RefWidth - W) * 0.5f, y0 = (UiScale.RefHeight - H) * 0.5f;
            _panel = UiKit.OrnatePanel(Ctx, content, x0, y0, W, H).rectTransform;
            _panel.name = "Codex";
            float l32 = UiTheme.LineHeight(UiFontId.F32);
            UiKit.DropShadow(UiKit.LabelKey(Ctx, _panel, TitleKeyName, UiFontId.F32, Pad, Pad - 6f, 600f, l32, UiTheme.Chalk), 2f);
            UiKit.AccentRule(_panel, Pad, Pad + l32 + 2f, W - Pad * 2f);
            float ty = Pad + l32 + 20f;
            for (int i = 0; i < CodexModel.Sections.Length; i++)
            {
                int idx = i;
                var b = UiKit.Button(Ctx, _panel, T(CodexModel.TabKey(CodexModel.Sections[i])), Pad, ty + i * (TabH + TabGap), TabW, TabH,
                    () => Show(idx), UiButtonStyle.TabVertical, Focus, null, UiFontId.F24);
                b.name = "CodexTab." + CodexModel.Sections[i];
                _tabs.Add(b);
            }
            float bx = Pad + TabW + Pad;
            var recess = UiKit.Panel(Ctx, _panel, UiPanelKind.Recess, bx, ty, W - bx - Pad, H - ty - Pad - 64f - 16f);
            recess.raycastTarget = false;
            _sectionTitle = UiKit.Label(Ctx, recess.rectTransform, string.Empty, UiFontId.F24, 20f, 12f, 600f, 32f, UiTheme.Brass);
            _body = UiKit.Node(recess.rectTransform, "CodexBody", 20f, 56f, W - bx - Pad - 40f, H - ty - Pad - 64f - 16f - 68f);

            float by = H - Pad - 56f;
            UiKit.Button(Ctx, _panel, T(ResetHintsKey), W - Pad - 280f - 16f - 200f, by, 280f, 56f, ResetHints, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            UiKit.Button(Ctx, _panel, T(CloseKey), W - Pad - 200f, by, 200f, 56f, () => Close(), UiButtonStyle.Primary, Focus, null, UiFontId.F24);
            UiKit.Label(Ctx, _panel,
                UiKeyHint.Compose(Ctx != null ? Ctx.Names : null, new[] { new UiKeyHint(Key.Escape, CloseKey) }),
                UiFontId.F16, Pad, by + 16f, 400f, 24f, UiTheme.SlateBlue);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Show(0);
            if (_tabs.Count > 0) Focus.SetCursor(_tabs[0], false);
        }

        public override bool OnBack()
        {
            Close();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.NextTab) { Show((_section + 1) % CodexModel.Sections.Length); return true; }
            if (key == MenuKey.PrevTab) { Show((_section + CodexModel.Sections.Length - 1) % CodexModel.Sections.Length); return true; }
            if (key == MenuKey.Cancel) { Close(); return true; }
            bool r = base.OnMenuKey(key);
            // 커서가 탭 위로 옮겨 가면 그 분류를 바로 보인다.
            int i = Focus != null ? _tabs.IndexOf(Focus.Current as UiButton) : -1;
            if (i >= 0 && i != _section) Show(i);
            return r;
        }

        void ResetHints()
        {
            HintBook.ResetAll(Ctx != null ? Ctx.Profile : null, Ctx != null ? Ctx.Profiles : null);
            Screens?.Toast(T(ResetDoneKey));
        }

        string KeyName(LogicalKey k)
        {
            var s = Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
            int bound = s != null ? KeyLayouts.PrimaryOf(s, k) : 0;
            if (bound <= 0)
            {
                var table = KeyLayouts.DefaultPrimary;
                bound = (int)k < table.Count ? table[(int)k] : 0;
            }
            if (k == LogicalKey.Pause && bound <= 0) bound = (int)Key.Escape;
            return UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, (Key)bound);
        }

        /// <summary>분류 하나를 그린다.</summary>
        public void Show(int index)
        {
            if (index < 0 || index >= CodexModel.Sections.Length) return;
            _section = index;
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].On = i == index;
            if (Focus != null && index < _tabs.Count && Focus.Current != (IUiFocusable)_tabs[index]) Focus.SetCursor(_tabs[index], false);
            string section = CodexModel.Sections[index];
            _sectionTitle.SetText(T(CodexModel.TabKey(section)));
            for (int i = _body.childCount - 1; i >= 0; i--) Object.Destroy(_body.GetChild(i).gameObject);

            var db = Ctx != null ? Ctx.Content : null;
            var names = Ctx != null ? Ctx.Names : null;
            var w = Ctx != null && Ctx.Session != null ? Ctx.Session.World : null;
            var p = w != null && w.Players != null && w.Players.Length > 0 && w.Players[0].Active ? w.Players[0] : null;
            var ch = p != null && p.Def != null ? p.Def : db != null ? db.BaselineCharacter : null;
            string charId = p != null ? p.CharId : Ids.Char.C1;
            var rows = CodexModel.Rows(section, names, db, charId, CodexModel.Args(db, ch, p, KeyName));

            float bw = _body.sizeDelta.x, maxH = _body.sizeDelta.y;
            float y = 0f;
            bool dense = rows.Count > 12;
            float rowH = rows.Count > 14 ? 32f : dense ? 40f : RowH;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Head == null)
                {
                    float ph = dense ? 32f : 40f;
                    if (y + ph > maxH) break;
                    UiKit.Label(Ctx, _body, "• " + r.Body, UiFontId.F16, 0f, y, bw, ph, UiTheme.Chalk, TextAnchor.UpperLeft, true);
                    y += ph;
                    continue;
                }
                float h = r.Body2 != null ? rowH + 24f : rowH;
                if (y + h > maxH) break;
                var line = UiKit.Node(_body, "Row", 0f, y, bw, h - 4f);
                UiKit.Rect(line, 0f, h - 5f, bw, 1f, UiTheme.WithAlpha(UiTheme.WoodLight, 0.8f), false, "Rule");
                float x = 0f;
                if (r.IconId != null && Ctx != null && Ctx.Art != null)
                {
                    var sp = Ctx.Art.Icon(r.IconId, 32);
                    if (sp != null) UiKit.Image(line, sp, 0f, (rowH - 4f - 32f) * 0.5f, 32f, 32f);
                    x = 44f;
                }
                UiKit.Label(Ctx, line, r.Head, UiFontId.F16, x, 0f, 196f, rowH - 4f, UiTheme.Chalk);
                float side = r.Side != null ? 260f : 0f;
                UiKit.Label(Ctx, line, r.Body ?? string.Empty, UiFontId.F16, x + 204f, 0f, bw - x - 204f - side, rowH - 4f, UiTheme.Chalk, TextAnchor.MiddleLeft, false);
                if (r.Body2 != null)
                    UiKit.Label(Ctx, line, r.Body2, UiFontId.F16, x + 204f, rowH - 8f, bw - x - 204f, 24f, UiTheme.SlateBlue);
                if (r.Side != null)
                    UiKit.Label(Ctx, line, r.Side, UiFontId.F16, bw - side, 0f, side, rowH - 4f, UiTheme.SlateBlue, TextAnchor.MiddleRight);
                y += h;
            }
        }
    }
}

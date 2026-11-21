using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>
    /// 키 재설정(ScreenId.KeyRebind, 08 12.3절, 02 3.1절). 설정의 조작 분류 오른쪽 (392,104) 1480×880 판.
    /// 배치 탭 셋([기본] [대체] [내 배치]), 기능 9줄 × 기본·보조 칸, 충돌 표시, 한글 입력기 안내, 지금 배치가 얹힌 키보드 그림.
    /// 키 받기 동안은 ctx.Screens.CaptureKeys = true로 메뉴 키를 막고 Keyboard.current의 물리 키를 직접 읽는다(Esc = 취소, 5초 = 저절로 취소).
    /// 일시정지 줄의 보조 칸은 Esc 고정(받을 수 없고 지울 수 없다). 겹친 채로는 [저장]이 꺼진다. 저장 → ctx.Settings.Save(입력원이 Changed로 다시 읽는다).
    /// K = 커서 칸 지우기(받기 전), 칸이 아니면 닫기. 규칙은 KeyRebindModel.
    /// </summary>
    public sealed class KeyRebindScreen : ScreenBase
    {
        const float PX = 392f, PY = 104f, PW = 1480f, PH = 880f, Pad = 24f;
        const float TabsY = 64f, TabW = 300f, TabH = 44f;
        const float HeadY = 120f, RowsY = 148f, RowH = 48f, CellH = 42f;
        const float NameX = 24f, NameW = 280f, PrimX = 320f, SecX = 580f, CellW = 240f, WarnX = 840f;
        const float StatusY = RowsY + 9f * RowH + 4f;
        const float KbY = StatusY + 32f, KbH = PH - KbY - 12f;

        KeyRebindModel _m;
        RectTransform _panel, _keyboard;
        UiLabel _status;
        UiButton _save;
        readonly UiButton[] _prim = new UiButton[KeyRebindModel.ActionCount];
        readonly UiButton[] _sec = new UiButton[KeyRebindModel.ActionCount];
        readonly UiLabel[] _warn = new UiLabel[KeyRebindModel.ActionCount];
        readonly Dictionary<IUiFocusable, (LogicalKey key, bool secondary)> _cellOf = new Dictionary<IUiFocusable, (LogicalKey, bool)>();
        readonly Dictionary<int, UnityEngine.UI.Image> _kbCaps = new Dictionary<int, UnityEngine.UI.Image>();
        bool _skipFrame;

        public override ScreenId Id => ScreenId.KeyRebind;
        public override ScreenLayer Layer => ScreenLayer.Panel;
        public override bool BlocksInput => true;

        protected override void BuildContent(RectTransform content)
        {
            _panel = UiKit.Panel(Ctx, content, UiPanelKind.Board, PX, PY, PW, PH).rectTransform;
            _panel.name = "KeyRebindPanel";
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, _panel, T(SettingsKeys.RebindTitle) + " · " + T(SettingsKeys.RebindContextCombat), UiFontId.F32, Pad, 12f, 800f, th,
                UiTheme.Chalk);
            _save = UiKit.Button(Ctx, _panel, T(SettingsKeys.CommonSave), PW - Pad - 2f * 200f - 16f, 12f, 200f, 44f, Save, UiButtonStyle.Primary,
                null, null, UiFontId.F16);
            var close = UiKit.Button(Ctx, _panel, T(SettingsKeys.CommonClose), PW - Pad - 200f, 12f, 200f, 44f, () => Close(),
                UiButtonStyle.Medium, null, null, UiFontId.F16);

            var labels = new List<string>();
            for (int i = 0; i < KeyRebindModel.Layouts.Length; i++) labels.Add(T(SettingsKeys.ValuePrefix + KeyRebindModel.Layouts[i]));
            Tabs = UiKit.Tabs(Ctx, _panel, labels, Pad, TabsY, TabW, TabH, i => SelectLayout(KeyRebindModel.Layouts[i]), 0, false, 8f);

            float lh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, _panel, T(SettingsKeys.RebindPrimary), UiFontId.F16, PrimX, HeadY, CellW, lh, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            UiKit.Label(Ctx, _panel, T(SettingsKeys.RebindSecondary), UiFontId.F16, SecX, HeadY, CellW, lh, UiTheme.SlateBlue, TextAnchor.MiddleCenter);

            for (int r = 0; r < KeyRebindModel.DisplayOrder.Length; r++)
            {
                var k = KeyRebindModel.DisplayOrder[r];
                float y = RowsY + r * RowH;
                UiKit.Label(Ctx, _panel, T(KeyRebindModel.ActionNameKey(k)), UiFontId.F16, NameX, y + (RowH - lh) * 0.5f, NameW, lh, UiTheme.Chalk);
                var pk = k;
                _prim[(int)k] = UiKit.Button(Ctx, _panel, string.Empty, PrimX, y + (RowH - CellH) * 0.5f, CellW, CellH, () => Begin(pk, false),
                    UiButtonStyle.Small, Focus, null, UiFontId.F16);
                _sec[(int)k] = UiKit.Button(Ctx, _panel, string.Empty, SecX, y + (RowH - CellH) * 0.5f, CellW, CellH, () => Begin(pk, true),
                    UiButtonStyle.Small, Focus, null, UiFontId.F16);
                _cellOf[_prim[(int)k]] = (k, false);
                _cellOf[_sec[(int)k]] = (k, true);
                _warn[(int)k] = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, WarnX, y + (RowH - lh) * 0.5f, PW - WarnX - Pad, lh, UiTheme.Amber);
            }
            Focus.Columns = 2;
            Focus.Add(_save);
            Focus.Add(close);

            _status = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, StatusY, PW - 2f * Pad, lh, UiTheme.SlateBlue);
            _keyboard = UiKit.Panel(Ctx, _panel, UiPanelKind.Recess, Pad, KbY, PW - 2f * Pad, KbH).rectTransform;
            _keyboard.name = "Keyboard";
            BuildKeyboard();
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _m = new KeyRebindModel(Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null);
            int tab = System.Array.IndexOf(KeyRebindModel.Layouts, _m.Layout);
            Tabs?.Select(tab < 0 ? 0 : tab, false);
            Refresh();
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            EndCapture();
            base.OnExit();
        }

        // ---------------------------------------------------------------- 조작

        void SelectLayout(string layout)
        {
            if (_m == null) return;
            _m.SelectLayout(layout);
            Refresh();
        }

        void Begin(LogicalKey k, bool secondary)
        {
            if (_m == null || !_m.BeginCapture(k, secondary)) return;
            if (Screens != null) Screens.CaptureKeys = true;
            // 칸을 누른 J·클릭이 같은 프레임에 그 칸의 키로 잡히지 않게 한 프레임 거른다.
            _skipFrame = true;
            Refresh();
        }

        void EndCapture()
        {
            if (Screens != null && Screens.CaptureKeys)
            {
                Screens.CaptureKeys = false;
                Manager?.Menu?.SwallowHeld();
            }
        }

        void Save()
        {
            if (_m == null) return;
            if (_m.Save(Ctx != null ? Ctx.Settings : null)) Close(true);
            else Refresh();
        }

        public override void Tick(float dt)
        {
            if (_m == null || _m.State != RebindState.Capturing) return;
            if (_skipFrame) { _skipFrame = false; return; }
            var end = RebindCaptureEnd.None;
            var kb = Keyboard.current;
            if (kb != null)
            {
                var keys = kb.allKeys;
                for (int i = 0; i < keys.Count; i++)
                {
                    KeyControl kc = keys[i];
                    if (kc == null || !kc.wasPressedThisFrame) continue;
                    // 물리 자리(Key)로 읽는다 — 한글 입력기 상태와 무관(TV-177).
                    end = _m.OnKey((int)kc.keyCode);
                    if (end != RebindCaptureEnd.None) break;
                }
            }
            if (end == RebindCaptureEnd.None) end = _m.Tick(dt);
            if (end != RebindCaptureEnd.None)
            {
                EndCapture();
                if (end == RebindCaptureEnd.Assigned)
                {
                    Tabs?.Select(System.Array.IndexOf(KeyRebindModel.Layouts, _m.Layout), false);
                }
                Refresh();
            }
        }

        public override bool OnBack()
        {
            // 받는 중엔 CaptureKeys로 Esc가 오지 않는다. 그 밖엔 관리자가 닫는다(저장하지 않은 바뀜은 버린다).
            return false;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel)
            {
                var cur = Focus.Current;
                if (_m != null && cur != null && _cellOf.TryGetValue(cur, out var cell))
                {
                    if (_m.Clear(cell.key, cell.secondary))
                    {
                        Tabs?.Select(System.Array.IndexOf(KeyRebindModel.Layouts, _m.Layout), false);
                        Refresh();
                    }
                    return true;
                }
                Close();
                return true;
            }
            return base.OnMenuKey(key);
        }

        // ---------------------------------------------------------------- 그리기

        string KeyText(int key) =>
            key == 0 ? T(KeyNoneName) : UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, (Key)key);

        const string KeyNoneName = "str.ui.key.none";

        void Refresh()
        {
            if (_m == null) return;
            var names = Ctx != null ? Ctx.Names : null;
            for (int r = 0; r < KeyRebindModel.DisplayOrder.Length; r++)
            {
                var k = KeyRebindModel.DisplayOrder[r];
                var p = _prim[(int)k];
                var s = _sec[(int)k];
                bool capP = _m.State == RebindState.Capturing && _m.CaptureAction == k && !_m.CaptureSecondary;
                bool capS = _m.State == RebindState.Capturing && _m.CaptureAction == k && _m.CaptureSecondary;
                p.SetText(capP ? T(SettingsKeys.RebindCapture) : KeyText(_m.DisplayKey(k, false)));
                s.SetText(capS ? T(SettingsKeys.RebindCapture) : KeyText(_m.DisplayKey(k, true)));
                p.Picked = capP;
                s.Picked = capS;
                if (KeyRebindModel.IsFixed(k, true)) s.SetDisabled(SettingsKeys.RebindPauseFixed);
                var other = _m.ConflictOf(k);
                _warn[(int)k].SetText(other.HasValue ? F(SettingsKeys.RebindConflict, "action", T(KeyRebindModel.ActionNameKey(other.Value))) : string.Empty);
            }
            if (_m.CanSave) _save.SetEnabled();
            else _save.SetDisabled(_m.State == RebindState.Capturing ? SettingsKeys.RebindCapture : SettingsKeys.RebindSaveBlocked);
            _status.SetText(_m.State == RebindState.Capturing ? T(SettingsKeys.RebindCapture)
                : T(SettingsKeys.RebindKoreanNote) + (names != null && names.Has(SettingsKeys.RebindHelp) ? "  " + T(SettingsKeys.RebindHelp) : string.Empty));
            RefreshKeyboard();
        }

        // 키보드 그림(1432×272 남짓): 줄마다 (Key, 너비 단위). 지금 배치에 쓰이는 키가 밝다.
        static readonly (Key key, float units)[][] KeyboardRows =
        {
            new[] { (Key.Escape, 1f), (Key.Digit1, 1f), (Key.Digit2, 1f), (Key.Digit3, 1f), (Key.Digit4, 1f), (Key.Digit5, 1f), (Key.Digit6, 1f),
                (Key.Digit7, 1f), (Key.Digit8, 1f), (Key.Digit9, 1f), (Key.Digit0, 1f), (Key.Minus, 1f), (Key.Equals, 1f), (Key.Backspace, 2f) },
            new[] { (Key.Tab, 1.5f), (Key.Q, 1f), (Key.W, 1f), (Key.E, 1f), (Key.R, 1f), (Key.T, 1f), (Key.Y, 1f), (Key.U, 1f), (Key.I, 1f),
                (Key.O, 1f), (Key.P, 1f), (Key.LeftBracket, 1f), (Key.RightBracket, 1f), (Key.Backslash, 1.5f) },
            new[] { (Key.CapsLock, 1.75f), (Key.A, 1f), (Key.S, 1f), (Key.D, 1f), (Key.F, 1f), (Key.G, 1f), (Key.H, 1f), (Key.J, 1f), (Key.K, 1f),
                (Key.L, 1f), (Key.Semicolon, 1f), (Key.Quote, 1f), (Key.Enter, 2.25f) },
            new[] { (Key.LeftShift, 2.25f), (Key.Z, 1f), (Key.X, 1f), (Key.C, 1f), (Key.V, 1f), (Key.B, 1f), (Key.N, 1f), (Key.M, 1f),
                (Key.Comma, 1f), (Key.Period, 1f), (Key.Slash, 1f), (Key.RightShift, 2.75f) },
            new[] { (Key.LeftCtrl, 1.5f), (Key.LeftAlt, 1.5f), (Key.Space, 6f), (Key.RightAlt, 1.5f), (Key.RightCtrl, 1.5f),
                (Key.LeftArrow, 1f), (Key.UpArrow, 1f), (Key.DownArrow, 1f), (Key.RightArrow, 1f) }
        };

        void BuildKeyboard()
        {
            const float Unit = 84f, Gap = 6f, KeyH = 44f;
            float y = 12f;
            for (int r = 0; r < KeyboardRows.Length; r++)
            {
                float x = 16f;
                var row = KeyboardRows[r];
                for (int i = 0; i < row.Length; i++)
                {
                    float w = row[i].units * Unit - Gap;
                    var cap = UiKit.Rect(_keyboard, x, y, w, KeyH, UiTheme.Wood, false, "Key." + row[i].key);
                    UiKit.Label(Ctx, cap.rectTransform, UiKeyLabels.Of(Ctx != null ? Ctx.Names : null, row[i].key), UiFontId.F12, 0f, 0f, w, KeyH,
                        UiTheme.SlateBlue, TextAnchor.MiddleCenter);
                    _kbCaps[(int)row[i].key] = cap;
                    x += row[i].units * Unit;
                }
                y += KeyH + Gap;
            }
        }

        void RefreshKeyboard()
        {
            var used = _m.UsedKeys();
            foreach (var kv in _kbCaps)
            {
                bool on = used.Contains(kv.Key);
                kv.Value.color = on ? UiTheme.Chalk : UiTheme.Wood;
                var label = kv.Value.GetComponentInChildren<UiLabel>();
                if (label != null) label.Color = on ? UiTheme.PaperText : UiTheme.SlateBlue;
            }
        }
    }
}

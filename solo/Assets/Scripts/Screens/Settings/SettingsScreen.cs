using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>
    /// 설정(ScreenId.Settings, 08 12.1·12.2절, UA 4.19). 왼쪽 분류 6개 (48,104) 320×880(세로 탭, Q/E로도) /
    /// 옵션 목록 (392,104) 1000×880 줄 높이 56 / 설명과 미리보기 (1416,104) 456×880 / 아래 [기본값으로] [닫기].
    /// 옵션 줄은 SettingsModel.Rows(= SettingsOptions.All을 분류별로)로 만든다. 바꾸면 SettingsOptions.Set → ISettingsStore.Save 즉시.
    /// 창·전체 화면과 해상도는 Screen.SetResolution으로 바로 적용한다(D-50). 계정(오프라인): 저장 파일 위치, [저장 파일 되돌리기].
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        const float Top = TopBarHeight;
        const float CatX = 48f, CatY = 104f - Top, CatW = 320f, CatTabH = 64f, CatGap = 8f;
        const float ListX = 392f, ListY = 104f - Top, ListW = 1000f, ListH = 880f, RowH = 56f;
        const float DescX = 1416f, DescY = 104f - Top, DescW = 456f, DescH = 880f, Pad = 16f;
        const float BottomY = 990f - Top, BottomH = 40f, BottomW = 240f;
        const float NameW = 460f, WidgetX = 500f, WidgetW = 460f, WidgetH = 40f;

        int _category;
        UiScrollList _list;
        UiLabel _descTitle, _descBody;
        SettingsPreviewView _preview;
        UiButton _defaults, _close;
        readonly Dictionary<IUiFocusable, SettingsRow> _rowOf = new Dictionary<IUiFocusable, SettingsRow>();
        readonly List<IUiFocusable> _rowWidgets = new List<IUiFocusable>();

        public override ScreenId Id => ScreenId.Settings;
        protected override string TitleKey => SettingsKeys.Title;

        string Category => SettingsModel.Categories[Mathf.Clamp(_category, 0, SettingsModel.Categories.Length - 1)];
        SettingsData Data => Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;

        protected override void BuildContent(RectTransform content)
        {
            var labels = new List<string>(SettingsModel.Categories.Length);
            for (int i = 0; i < SettingsModel.Categories.Length; i++) labels.Add(T(SettingsModel.CategoryNameKey(SettingsModel.Categories[i])));
            Tabs = UiKit.Tabs(Ctx, content, labels, CatX, CatY, CatW, CatTabH, SelectCategory, 0, true, CatGap);

            UiKit.Plate(Ctx, content, UiPanelKind.Board, ListX, ListY, ListW, ListH);
            _list = UiKit.ScrollList(Ctx, content, ListX + Pad, ListY + Pad, ListW - 2f * Pad, ListH - 2f * Pad, Focus, 0f);

            var desc = UiKit.Panel(Ctx, content, UiPanelKind.Recess, DescX, DescY, DescW, DescH).rectTransform;
            desc.name = "SettingsDescription";
            float lh = UiTheme.LineHeight(UiFontId.F24);
            _descTitle = UiKit.Label(Ctx, desc, string.Empty, UiFontId.F24, Pad, Pad, DescW - 2f * Pad, lh, UiTheme.Chalk);
            _descBody = UiKit.Label(Ctx, desc, string.Empty, UiFontId.F16, Pad, Pad + lh + 8f, DescW - 2f * Pad,
                DescH - SettingsPreviewView.H - 3f * Pad - lh - 8f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            _preview = new SettingsPreviewView(Ctx, desc, (DescW - SettingsPreviewView.W) * 0.5f, DescH - Pad - SettingsPreviewView.H);
            _preview.SetVisible(false);

            _defaults = UiKit.Button(Ctx, content, T(SettingsKeys.Defaults), ListX, BottomY, BottomW, BottomH, AskDefaults,
                UiButtonStyle.Medium, null, null, UiFontId.F16);
            _close = UiKit.Button(Ctx, content, T(SettingsKeys.CommonClose), ListX + BottomW + 16f, BottomY, BottomW, BottomH, () => Close(),
                UiButtonStyle.Medium, null, null, UiFontId.F16);
            Focus.CursorChanged = _ => RefreshDescription();
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _category = Tabs != null ? Tabs.Index : 0;
            Rebuild(0);
        }

        void SelectCategory(int i)
        {
            _category = i;
            Rebuild(0);
        }

        /// <summary>지금 분류의 줄을 다시 만든다(cursor = 커서를 둘 줄 순번).</summary>
        void Rebuild(int cursor)
        {
            Focus.Clear();
            _rowOf.Clear();
            _rowWidgets.Clear();
            MetaUi.ClearChildren(_list.Content);
            _list.Clear();
            var rows = SettingsModel.Rows(Category);
            for (int i = 0; i < rows.Count; i++) BuildRow(rows[i], i);
            Focus.Add(_defaults);
            Focus.Add(_close);
            if (SettingsModel.HasDefaults(Category)) _defaults.SetEnabled(); else _defaults.Enabled = false;
            if (_rowWidgets.Count > 0) Focus.SetCursor(_rowWidgets[Mathf.Clamp(cursor, 0, _rowWidgets.Count - 1)], false);
            else Focus.SetCursor(_close, false);
            RefreshDescription();
        }

        void BuildRow(SettingsRow row, int index)
        {
            var rt = _list.AddRow(RowH, "Row." + (row.OptionKey ?? row.Kind.ToString()));
            float lh = UiTheme.LineHeight(UiFontId.F16);
            float wy = (RowH - WidgetH) * 0.5f;
            var s = Data;
            IUiFocusable w = null;
            if (row.Kind == SettingsRowKind.Note)
            {
                UiKit.Label(Ctx, rt, T(row.NameKey), UiFontId.F16, 16f, (RowH - lh) * 0.5f, ListW - 2f * Pad - 32f, lh, UiTheme.SlateBlue);
                return;
            }
            UiKit.Label(Ctx, rt, T(row.NameKey), UiFontId.F16, 16f, (RowH - lh) * 0.5f, NameW, lh, UiTheme.Chalk);
            switch (row.Kind)
            {
                case SettingsRowKind.Option:
                    w = BuildOptionWidget(rt, row.Spec, s, wy);
                    break;
                case SettingsRowKind.Resolution:
                    w = BuildResolutionWidget(rt, s, wy);
                    break;
                case SettingsRowKind.KeyRebind:
                    w = UiKit.Button(Ctx, rt, T(SettingsKeys.OptKeyRebind), WidgetX, wy, WidgetW, WidgetH,
                        () => Screens?.Push(ScreenId.KeyRebind, ScreenArgs.From(ScreenId.Settings)), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
                    break;
                case SettingsRowKind.SavePath:
                {
                    string path = SettingsModel.SavePath(Ctx != null ? Ctx.Profiles : null);
                    var b = UiKit.Button(Ctx, rt, null, WidgetX, wy, WidgetW, WidgetH, null, UiButtonStyle.Plain, Focus, "");
                    var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                    UiKit.Label(Ctx, root, path, UiFontId.F12, 8f, 0f, WidgetW - 16f, WidgetH, UiTheme.Chalk, TextAnchor.MiddleLeft);
                    UiKit.AttachTooltip(b.gameObject, () => path);
                    w = b;
                    break;
                }
                case SettingsRowKind.ResetHints:
                    w = UiKit.Button(Ctx, rt, T(SettingsModel.ResetHintsKey), WidgetX, wy, WidgetW, WidgetH, () =>
                    {
                        global::Wizard.Solo.Screens.Hints.HintBook.ResetAll(Ctx != null ? Ctx.Profile : null, Ctx != null ? Ctx.Profiles : null);
                        Screens?.Toast(T(SettingsModel.ResetHintsDoneKey));
                    }, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
                    break;
                case SettingsRowKind.RestoreSave:
                    w = UiKit.Button(Ctx, rt, T(SettingsKeys.AccountRestoreSave), WidgetX, wy, WidgetW, WidgetH, AskRestore,
                        UiButtonStyle.Caution, Focus, null, UiFontId.F16);
                    break;
            }
            if (w != null)
            {
                _rowOf[w] = row;
                _rowWidgets.Add(w);
            }
        }

        IUiFocusable BuildOptionWidget(RectTransform rt, OptionSpec spec, SettingsData s, float wy)
        {
            switch (SettingsModel.WidgetOf(spec))
            {
                case SettingsWidget.Choice:
                    return ChoiceStepper.Create(Ctx, rt, WidgetX, wy, WidgetW, WidgetH, spec.Choices.Length, SettingsModel.ChoiceIndex(s, spec),
                        i => T(SettingsModel.ValueNameKey(spec.Choices[i])), i => OnChoice(spec, i), Focus);
                case SettingsWidget.Slider:
                    return UiKit.Slider(Ctx, rt, WidgetX, wy, WidgetW, WidgetH, SettingsOptions.Int(s, spec.Key), v => OnInt(spec, v),
                        spec.Min, spec.Max, 1, Focus, spec.Steps);
                default:
                    return UiKit.Toggle(Ctx, rt, null, WidgetX, wy, WidgetW, WidgetH, SettingsOptions.Bool(s, spec.Key), v => OnBool(spec, v), Focus);
            }
        }

        IUiFocusable BuildResolutionWidget(RectTransform rt, SettingsData s, float wy)
        {
            var dev = DeviceResolution();
            bool full = SettingsModel.IsFullscreen(s);
            var opts = full ? new List<Vector2Int> { dev } : SettingsModel.WindowedResolutions(dev.x, dev.y);
            if (opts.Count == 0) opts.Add(dev);
            var target = SettingsModel.TargetResolution(s, dev.x, dev.y);
            int idx = Mathf.Max(0, opts.IndexOf(target));
            var stepper = ChoiceStepper.Create(Ctx, rt, WidgetX, wy, WidgetW, WidgetH, opts.Count, idx,
                i => i >= 0 && i < opts.Count ? F(SettingsKeys.ResolutionValue, "w", opts[i].x, "h", opts[i].y) : string.Empty,
                i => { if (i >= 0 && i < opts.Count) OnResolution(opts[i]); }, Focus);
            // 전체 화면은 기기 해상도 그대로(D-50): 고를 것이 없다.
            if (full) stepper.SetDisabled(SettingsKeys.ResolutionFullscreenReason);
            return stepper;
        }

        // ---------------------------------------------------------------- 값 바꾸기

        void OnChoice(OptionSpec spec, int i)
        {
            if (!SettingsModel.SetChoice(Ctx != null ? Ctx.Settings : null, spec, i)) return;
            if (spec.Key == OptionKeys.WindowMode)
            {
                ApplyDisplay();
                Rebuild(Focus.Index);
                return;
            }
            RefreshDescription();
        }

        void OnInt(OptionSpec spec, int v)
        {
            SettingsModel.SetInt(Ctx != null ? Ctx.Settings : null, spec.Key, v);
            RefreshDescription();
        }

        void OnBool(OptionSpec spec, bool v)
        {
            SettingsModel.SetBool(Ctx != null ? Ctx.Settings : null, spec.Key, v);
            RefreshDescription();
        }

        void OnResolution(Vector2Int r)
        {
            if (SettingsModel.SetResolution(Ctx != null ? Ctx.Settings : null, r)) ApplyDisplay();
        }

        static Vector2Int DeviceResolution()
        {
            var r = Screen.currentResolution;
            return new Vector2Int(r.width, r.height);
        }

        /// <summary>창·전체 화면과 크기를 바로 적용한다(D-50: 창 = 1920×1080의 정수 배, 전체 화면 = 기기 해상도).</summary>
        void ApplyDisplay()
        {
            if (!Application.isPlaying) return;
            var s = Data;
            var dev = DeviceResolution();
            var t = SettingsModel.TargetResolution(s, dev.x, dev.y);
            if (t.x <= 0 || t.y <= 0) return;
            Screen.SetResolution(t.x, t.y, SettingsModel.IsFullscreen(s) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        void AskDefaults()
        {
            string cat = Category;
            if (!SettingsModel.HasDefaults(cat) || Screens == null) return;
            Screens.Confirm(SettingsKeys.Defaults, SettingsKeys.DefaultsConfirm, false, ok =>
            {
                if (!ok) return;
                SettingsModel.ResetCategory(Ctx != null ? Ctx.Settings : null, cat);
                if (cat == SettingsOptions.CatDisplay) ApplyDisplay();
                Rebuild(0);
            });
        }

        void AskRestore()
        {
            if (Screens == null) return;
            Screens.Confirm(SettingsKeys.AccountRestoreSave, SettingsKeys.AccountRestoreConfirm, true, ok =>
            {
                if (!ok) return;
                string key = SettingsModel.RestoreSave(Ctx != null ? Ctx.Profiles : null);
                Screens.Toast(T(key));
            });
        }

        // ---------------------------------------------------------------- 설명과 미리보기

        void RefreshDescription()
        {
            if (_descTitle == null) return;
            SettingsRow row = null;
            var cur = Focus.Current;
            if (cur != null) _rowOf.TryGetValue(cur, out row);
            if (row == null)
            {
                _descTitle.SetText(string.Empty);
                _descBody.SetText(string.Empty);
                _preview.SetVisible(false);
                return;
            }
            _descTitle.SetText(T(row.NameKey));
            bool hasDesc = row.DescKey != null && Ctx != null && Ctx.Names != null && Ctx.Names.Has(row.DescKey);
            _descBody.SetText(hasDesc ? T(row.DescKey) : string.Empty);
            bool show = row.Kind == SettingsRowKind.Option && SettingsPreviewModel.HasPreview(row.OptionKey);
            _preview.SetVisible(show);
            if (show) _preview.Apply(SettingsPreviewModel.Build(Data));
        }

        public override void Tick(float dt)
        {
            _preview?.Tick(dt);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}

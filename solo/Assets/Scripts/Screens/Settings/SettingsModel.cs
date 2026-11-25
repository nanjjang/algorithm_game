using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Settings
{
    /// <summary>설정 목록 한 줄의 종류.</summary>
    public enum SettingsRowKind
    {
        /// <summary>옵션 표(SettingsOptions.All)의 한 줄.</summary>
        Option = 0,
        /// <summary>화면: 해상도(창 모드일 때 1920×1080의 정수 배 중 고르기, 전체 화면은 기기 해상도 — D-50).</summary>
        Resolution = 1,
        /// <summary>조작: [키 재설정] → KeyRebind.</summary>
        KeyRebind = 2,
        /// <summary>안내 한 줄(대화와 공개: 흔적 공개는 강화 화면에서).</summary>
        Note = 3,
        /// <summary>계정(오프라인): 저장 파일 위치.</summary>
        SavePath = 4,
        /// <summary>계정(오프라인): [저장 파일 되돌리기].</summary>
        RestoreSave = 5,
        /// <summary>[힌트 다시 보기]: 본 문맥 힌트 표시를 지운다(Screens/Hints.HintBook.ResetAll).</summary>
        ResetHints = 6
    }

    /// <summary>옵션 줄의 위젯 꼴(OptionSpec의 꼴 그대로).</summary>
    public enum SettingsWidget { None = 0, Choice = 1, Slider = 2, Toggle = 3 }

    public sealed class SettingsRow
    {
        public SettingsRowKind Kind;
        /// <summary>Option 줄만.</summary>
        public OptionSpec Spec;
        /// <summary>줄 이름 열쇠.</summary>
        public string NameKey;
        /// <summary>설명 칸 열쇠(str.settings.desc.&lt;열쇠&gt;).</summary>
        public string DescKey;
        public SettingsWidget Widget;
        public string OptionKey => Spec != null ? Spec.Key : null;
    }

    /// <summary>
    /// 설정 화면 규칙(08 12.1·12.2절, 00 11절, D-50, 엔진 없음).
    /// 옵션 줄은 SettingsOptions.All을 분류별로 돌며 만든다(Hidden 제외 — 표에 줄을 더하면 화면에 저절로 나온다. 옵션을 다시 나열하지 않는다).
    /// 값은 SettingsOptions.Set으로만 넣고 바로 ISettingsStore.Save(Changed가 나서 표시 옵션·음량·입력원이 다시 읽는다).
    /// 줄일 수 없는 것(적 예고·적 탄·열 링·피격 표시·시야 화살표)과 솔로에서 숨기는 줄은 표에 애초에 없다(TV-169).
    /// </summary>
    public static class SettingsModel
    {
        public const string CatAccount = "account";
        public const string ResetHintsKey = "str.settings.opt.reset_hints";
        public const string ResetHintsDoneKey = "str.settings.reset_hints_done";

        /// <summary>왼쪽 분류 6개(순서 그대로).</summary>
        public static readonly string[] Categories =
        {
            SettingsOptions.CatDisplay, SettingsOptions.CatSound, SettingsOptions.CatControls, SettingsOptions.CatCombatView,
            SettingsOptions.CatChatPrivacy, CatAccount
        };

        public static string CategoryNameKey(string category) => SettingsKeys.CategoryPrefix + category;
        public static string OptionNameKey(string optionKey) => SettingsKeys.OptionPrefix + optionKey;
        public static string ValueNameKey(string value) => SettingsKeys.ValuePrefix + value;
        public static string DescKey(string key) => SettingsKeys.DescPrefix + key;

        public static SettingsWidget WidgetOf(OptionSpec spec)
        {
            if (spec == null) return SettingsWidget.None;
            if (spec.IsChoice) return SettingsWidget.Choice;
            if (spec.IsNumber) return SettingsWidget.Slider;
            return SettingsWidget.Toggle;
        }

        /// <summary>그 분류의 줄. 옵션 줄 = 표 순서, 해상도 줄은 창·전체 화면 줄 바로 뒤.</summary>
        public static List<SettingsRow> Rows(string category)
        {
            var rows = new List<SettingsRow>();
            var all = SettingsOptions.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s.Category != category || s.Hidden) continue;
                rows.Add(new SettingsRow
                {
                    Kind = SettingsRowKind.Option, Spec = s, NameKey = OptionNameKey(s.Key), DescKey = DescKey(s.Key), Widget = WidgetOf(s)
                });
                if (s.Key == OptionKeys.ContextHints)
                    rows.Add(new SettingsRow { Kind = SettingsRowKind.ResetHints, NameKey = ResetHintsKey, DescKey = DescKey("reset_hints") });
                if (s.Key == OptionKeys.WindowMode)
                    rows.Add(new SettingsRow
                    {
                        Kind = SettingsRowKind.Resolution, NameKey = SettingsKeys.OptResolution, DescKey = DescKey("resolution"),
                        Widget = SettingsWidget.Choice
                    });
            }
            if (category == SettingsOptions.CatControls)
                rows.Add(new SettingsRow { Kind = SettingsRowKind.KeyRebind, NameKey = SettingsKeys.OptKeyRebind, DescKey = DescKey("key_rebind") });
            if (category == SettingsOptions.CatChatPrivacy)
                rows.Add(new SettingsRow { Kind = SettingsRowKind.Note, NameKey = SettingsKeys.TraceNote });
            if (category == CatAccount)
            {
                rows.Add(new SettingsRow { Kind = SettingsRowKind.SavePath, NameKey = SettingsKeys.AccountSavePath, DescKey = DescKey("save_path") });
                rows.Add(new SettingsRow
                {
                    Kind = SettingsRowKind.RestoreSave, NameKey = SettingsKeys.AccountRestoreSave, DescKey = DescKey("restore_save")
                });
            }
            return rows;
        }

        /// <summary>[기본값으로]가 되는 분류(옵션이 있는 분류만. 계정은 없다).</summary>
        public static bool HasDefaults(string category) => category != CatAccount;

        // ---------------------------------------------------------------- 값 읽기·쓰기

        /// <summary>고르기 옵션의 지금 값 순번.</summary>
        public static int ChoiceIndex(SettingsData s, OptionSpec spec)
        {
            if (spec == null || spec.Choices == null) return 0;
            int i = Array.IndexOf(spec.Choices, SettingsOptions.Str(s, spec.Key));
            return i < 0 ? 0 : i;
        }

        /// <summary>순번 i에서 dir(±1)만큼 돈다(끝에서 반대쪽으로).</summary>
        public static int Cycle(int count, int i, int dir)
        {
            if (count <= 0) return 0;
            int n = (i + (dir < 0 ? -1 : 1)) % count;
            return n < 0 ? n + count : n;
        }

        /// <summary>값을 넣고 바뀌었으면 저장한다(Save 1번 → Changed 1번). 바뀌었으면 true.</summary>
        public static bool Set(ISettingsStore store, string key, JToken value)
        {
            if (store == null || store.Current == null) return false;
            var cur = store.Current;
            JToken before = cur.Options.TryGetValue(key, out var b) ? b.DeepClone() : null;
            SettingsOptions.Set(cur, key, value);
            JToken after = cur.Options.TryGetValue(key, out var a) ? a : null;
            if (JToken.DeepEquals(before, after)) return false;
            store.Save();
            return true;
        }

        public static bool SetChoice(ISettingsStore store, OptionSpec spec, int index)
        {
            if (spec == null || spec.Choices == null || index < 0 || index >= spec.Choices.Length) return false;
            return Set(store, spec.Key, spec.Choices[index]);
        }

        public static bool SetInt(ISettingsStore store, string key, int v) => Set(store, key, v);
        public static bool SetBool(ISettingsStore store, string key, bool v) => Set(store, key, v);

        /// <summary>그 분류만 기본값으로(조작이면 키 배치도 기본 표로 — KeyLayouts.Normalize). 저장 1번.</summary>
        public static void ResetCategory(ISettingsStore store, string category)
        {
            if (store == null || store.Current == null || !HasDefaults(category)) return;
            SettingsOptions.ResetCategory(store.Current, category);
            if (category == SettingsOptions.CatControls) KeyLayouts.Normalize(store.Current);
            store.Save();
        }

        // ---------------------------------------------------------------- 해상도 (D-50)

        /// <summary>창 모드 크기 후보(16:9). UI는 어느 크기에서도 창에 맞춰 늘고 준다(UiScale) — 정수 배일 필요가 없다.</summary>
        static readonly Vector2Int[] WindowedCandidates =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3200, 1800), new Vector2Int(3840, 2160),
        };

        /// <summary>창 모드의 크기 선택지: 후보 중 기기 화면에 들어가는 것(작은 것부터). 하나도 안 들어가면 기기 화면 크기 하나.</summary>
        public static List<Vector2Int> WindowedResolutions(int deviceW, int deviceH)
        {
            var list = new List<Vector2Int>();
            for (int i = 0; i < WindowedCandidates.Length; i++)
            {
                var c = WindowedCandidates[i];
                if (c.x <= deviceW && c.y <= deviceH) list.Add(c);
            }
            if (list.Count == 0 && deviceW > 0 && deviceH > 0) list.Add(new Vector2Int(deviceW, deviceH));
            return list;
        }

        public static bool IsFullscreen(SettingsData s) => SettingsOptions.Str(s, OptionKeys.WindowMode) == OptionValues.Fullscreen;

        /// <summary>적용할 크기: 전체 화면 = 기기 해상도, 창 = 저장한 크기가 선택지에 있으면 그것, 아니면 1920×1080 이하에서 가장 큰 선택지.</summary>
        public static Vector2Int TargetResolution(SettingsData s, int deviceW, int deviceH)
        {
            if (IsFullscreen(s)) return new Vector2Int(deviceW, deviceH);
            var opts = WindowedResolutions(deviceW, deviceH);
            int w = SettingsOptions.Int(s, OptionKeys.ResolutionW), h = SettingsOptions.Int(s, OptionKeys.ResolutionH);
            for (int i = 0; i < opts.Count; i++) if (opts[i].x == w && opts[i].y == h) return opts[i];
            if (opts.Count == 0) return new Vector2Int(deviceW, deviceH);
            var pick = opts[0];
            for (int i = 1; i < opts.Count; i++) if (opts[i].x <= UiScale.RefWidth && opts[i].y <= UiScale.RefHeight) pick = opts[i];
            return pick;
        }

        /// <summary>창 크기를 고른다(resolution_w/h 두 칸, 저장 1번).</summary>
        public static bool SetResolution(ISettingsStore store, Vector2Int r)
        {
            if (store == null || store.Current == null) return false;
            var cur = store.Current;
            int w0 = SettingsOptions.Int(cur, OptionKeys.ResolutionW), h0 = SettingsOptions.Int(cur, OptionKeys.ResolutionH);
            if (w0 == r.x && h0 == r.y) return false;
            SettingsOptions.Set(cur, OptionKeys.ResolutionW, r.x);
            SettingsOptions.Set(cur, OptionKeys.ResolutionH, r.y);
            store.Save();
            return true;
        }

        // ---------------------------------------------------------------- 계정(오프라인)

        /// <summary>저장 파일 되돌리기: RestorePrevious를 한 번 부르고 알림 열쇠(성공·실패)를 돌려준다.</summary>
        public static string RestoreSave(IProfileStore profiles)
        {
            bool ok = false;
            if (profiles != null)
            {
                try { ok = profiles.RestorePrevious(); }
                catch (Exception ex) { Debug.LogException(ex); ok = false; }
            }
            return ok ? SettingsKeys.AccountRestoreDone : SettingsKeys.AccountRestoreFailed;
        }

        public static string SavePath(IProfileStore profiles) => profiles != null ? profiles.Directory ?? string.Empty : string.Empty;
    }
}

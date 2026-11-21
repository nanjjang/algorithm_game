using System.Collections.Generic;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Settings
{
    public enum RebindState { Idle = 0, Capturing = 1 }

    /// <summary>키 받기 한 번의 끝.</summary>
    public enum RebindCaptureEnd { None = 0, Assigned = 1, CancelledByEsc = 2, TimedOut = 3 }

    /// <summary>
    /// 키 재설정 규칙(08 12.3절, 02 3.1절, D-74, 엔진 없음). 키는 Input System의 Key(물리 자리) 정수값 — 한글 입력기와 무관하다(TV-177).
    /// - 배치 탭 셋: 기본·대체를 고르면 KeyLayouts의 표를 그대로 넣는다(TV-171·172). 키를 하나라도 바꾸면 내 배치(custom).
    /// - 키 받기: Idle → Capturing(줄, 칸) → Esc = 취소(바꾸지 않음, TV-174) / 5초 입력 없음 = 저절로 취소(TV-173) / 다른 키 = 그 칸에.
    /// - 같은 문맥(전투) 안에서 다른 기능과 같은 키면 두 줄 모두 충돌 표시, 겹친 채로는 저장할 수 없다(TV-175).
    /// - 일시정지 줄: 보조 칸은 Esc 고정(받을 수 없고 지울 수 없다, U-24). 기본 칸 하나만 받는다. Esc는 어느 칸에도 넣을 수 없다(받기 취소 키).
    /// </summary>
    public sealed class KeyRebindModel
    {
        /// <summary>키 받기 저절로 취소까지(초). 08 12.3절 "5초(300 t) 입력 없음".</summary>
        public const float CaptureTimeoutSeconds = 5f;
        public const int ActionCount = KeyLayouts.KeyCount;
        /// <summary>메뉴 길의 고정 키(일시정지·받기 취소).</summary>
        public const int EscapeKey = (int)Key.Escape;

        /// <summary>기능 9줄의 그리는 순서(08 12.3절 표: 위 아래 왼쪽 오른쪽 발사 비기 자세 반전 자세 일시정지).</summary>
        public static readonly LogicalKey[] DisplayOrder =
        {
            LogicalKey.Up, LogicalKey.Down, LogicalKey.Left, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill,
            LogicalKey.Stance, LogicalKey.Reverse, LogicalKey.Pause
        };

        /// <summary>배치 탭 셋(str.settings.value.*).</summary>
        public static readonly string[] Layouts = { OptionValues.Default, OptionValues.Alternate, OptionValues.Custom };

        readonly int[] _primary = new int[ActionCount];
        readonly int[] _secondary = new int[ActionCount];

        public string Layout { get; private set; }
        public RebindState State { get; private set; }
        public LogicalKey CaptureAction { get; private set; }
        public bool CaptureSecondary { get; private set; }
        public float CaptureElapsed { get; private set; }
        /// <summary>설정에 저장한 것과 다른가.</summary>
        public bool Dirty { get; private set; }

        public KeyRebindModel(SettingsData s)
        {
            Load(s);
        }

        /// <summary>설정의 지금 배치를 읽는다(받기 상태·바뀜 표시도 처음으로).</summary>
        public void Load(SettingsData s)
        {
            Layout = SettingsOptions.Str(s, OptionKeys.KeyLayout);
            for (int i = 0; i < ActionCount; i++)
            {
                _primary[i] = KeyLayouts.PrimaryOf(s, (LogicalKey)i);
                _secondary[i] = KeyLayouts.SecondaryOf(s, (LogicalKey)i);
            }
            // default·alternate인데 저장된 줄이 비어 있으면(불러오기 전) 표로 채운다.
            var p = KeyLayouts.Primary(Layout);
            var q = KeyLayouts.Secondary(Layout);
            if (p != null && q != null && (s == null || s.KeyPrimary == null || s.KeyPrimary.Count < ActionCount))
                for (int i = 0; i < ActionCount; i++) { _primary[i] = p[i]; _secondary[i] = q[i]; }
            State = RebindState.Idle;
            CaptureElapsed = 0f;
            Dirty = false;
        }

        public int Primary(LogicalKey k) => _primary[(int)k];
        public int Secondary(LogicalKey k) => _secondary[(int)k];
        public int KeyAt(LogicalKey k, bool secondary) => secondary ? _secondary[(int)k] : _primary[(int)k];

        /// <summary>일시정지 줄의 보조 칸은 Esc 고정.</summary>
        public static bool IsFixed(LogicalKey k, bool secondary) => k == LogicalKey.Pause && secondary;

        /// <summary>화면에 그릴 키(고정 칸이면 Esc).</summary>
        public int DisplayKey(LogicalKey k, bool secondary) => IsFixed(k, secondary) ? EscapeKey : KeyAt(k, secondary);

        public bool CanCapture(LogicalKey k, bool secondary) => !IsFixed(k, secondary);

        // ---------------------------------------------------------------- 배치 탭

        /// <summary>배치를 고른다. 기본·대체 = 표의 값을 넣는다. 내 배치 = 지금 키를 그대로 두고 이름만 custom.</summary>
        public void SelectLayout(string layout)
        {
            CancelCapture();
            var p = KeyLayouts.Primary(layout);
            var q = KeyLayouts.Secondary(layout);
            if (p != null && q != null)
            {
                for (int i = 0; i < ActionCount; i++) { _primary[i] = p[i]; _secondary[i] = q[i]; }
                Layout = layout;
                Dirty = true;
                return;
            }
            if (layout == OptionValues.Custom && Layout != OptionValues.Custom) { Layout = OptionValues.Custom; Dirty = true; }
        }

        // ---------------------------------------------------------------- 키 받기

        public bool BeginCapture(LogicalKey k, bool secondary)
        {
            if (!CanCapture(k, secondary)) return false;
            State = RebindState.Capturing;
            CaptureAction = k;
            CaptureSecondary = secondary;
            CaptureElapsed = 0f;
            return true;
        }

        public void CancelCapture()
        {
            State = RebindState.Idle;
            CaptureElapsed = 0f;
        }

        /// <summary>받는 중 시간이 간다. 5초 동안 입력이 없으면 저절로 취소(TV-173).</summary>
        public RebindCaptureEnd Tick(float dt)
        {
            if (State != RebindState.Capturing) return RebindCaptureEnd.None;
            CaptureElapsed += dt;
            if (CaptureElapsed < CaptureTimeoutSeconds) return RebindCaptureEnd.None;
            CancelCapture();
            return RebindCaptureEnd.TimedOut;
        }

        /// <summary>받는 중 물리 키 하나가 눌렸다. Esc = 취소(바인딩 불변, TV-174). 그 밖 = 그 칸에 넣고 내 배치로.</summary>
        public RebindCaptureEnd OnKey(int key)
        {
            if (State != RebindState.Capturing || key == 0) return RebindCaptureEnd.None;
            if (key == EscapeKey)
            {
                CancelCapture();
                return RebindCaptureEnd.CancelledByEsc;
            }
            int i = (int)CaptureAction;
            if (CaptureSecondary) _secondary[i] = key; else _primary[i] = key;
            Layout = OptionValues.Custom;
            Dirty = true;
            CancelCapture();
            return RebindCaptureEnd.Assigned;
        }

        /// <summary>칸을 비운다(고정 칸은 못 지운다). 내 배치가 된다.</summary>
        public bool Clear(LogicalKey k, bool secondary)
        {
            if (IsFixed(k, secondary)) return false;
            int i = (int)k;
            if (secondary) { if (_secondary[i] == 0) return false; _secondary[i] = 0; }
            else { if (_primary[i] == 0) return false; _primary[i] = 0; }
            Layout = OptionValues.Custom;
            Dirty = true;
            return true;
        }

        // ---------------------------------------------------------------- 충돌 (같은 문맥 = 전투)

        /// <summary>그 기능과 같은 키를 쓰는 첫 다른 기능(없으면 null). 고정 Esc 칸은 Esc를 받을 수 없으므로 겹치지 않는다.</summary>
        public LogicalKey? ConflictOf(LogicalKey k)
        {
            int a = (int)k;
            for (int b = 0; b < ActionCount; b++)
            {
                if (b == a) continue;
                if (Shares(a, b)) return (LogicalKey)b;
            }
            return null;
        }

        bool Shares(int a, int b)
        {
            int pa = _primary[a], sa = _secondary[a], pb = _primary[b], sb = _secondary[b];
            return (pa != 0 && (pa == pb || pa == sb)) || (sa != 0 && (sa == pb || sa == sb));
        }

        /// <summary>충돌 표시가 붙는 기능들(DisplayOrder 순).</summary>
        public List<LogicalKey> Conflicts()
        {
            var list = new List<LogicalKey>();
            for (int i = 0; i < DisplayOrder.Length; i++) if (ConflictOf(DisplayOrder[i]).HasValue) list.Add(DisplayOrder[i]);
            return list;
        }

        /// <summary>경고 0개일 때만 저장된다(TV-175).</summary>
        public bool CanSave => State == RebindState.Idle && Conflicts().Count == 0;

        /// <summary>
        /// 저장: 키 줄을 설정에 넣고 key_layout(기본·대체 표와 같으면 그 이름, 아니면 custom)을 정해 ISettingsStore.Save
        /// (입력원이 Changed로 새 배치를 쓴다). 겹치면 저장하지 않고 false.
        /// </summary>
        public bool Save(ISettingsStore store)
        {
            if (store == null || store.Current == null || !CanSave) return false;
            var s = store.Current;
            string layout = Layout;
            if (layout != OptionValues.Custom && !MatchesTable(layout)) layout = OptionValues.Custom;
            if (layout == OptionValues.Custom)
            {
                s.KeyPrimary = new List<int>(_primary);
                s.KeySecondary = new List<int>(_secondary);
                SettingsOptions.Set(s, OptionKeys.KeyLayout, OptionValues.Custom);
            }
            else KeyLayouts.Apply(s, layout);
            Layout = layout;
            store.Save();
            Dirty = false;
            return true;
        }

        bool MatchesTable(string layout)
        {
            var p = KeyLayouts.Primary(layout);
            var q = KeyLayouts.Secondary(layout);
            if (p == null || q == null) return false;
            for (int i = 0; i < ActionCount; i++) if (p[i] != _primary[i] || q[i] != _secondary[i]) return false;
            return true;
        }

        /// <summary>키보드 그림에서 밝게 그릴 키(지금 배치에 쓰이는 물리 키 + 고정 Esc).</summary>
        public HashSet<int> UsedKeys()
        {
            var set = new HashSet<int> { EscapeKey };
            for (int i = 0; i < ActionCount; i++)
            {
                if (_primary[i] != 0) set.Add(_primary[i]);
                if (_secondary[i] != 0) set.Add(_secondary[i]);
            }
            return set;
        }

        /// <summary>기능 이름 열쇠(str.key_rebind.action.*).</summary>
        public static string ActionNameKey(LogicalKey k)
        {
            switch (k)
            {
                case LogicalKey.Up: return SettingsKeys.RebindActionPrefix + "move_up";
                case LogicalKey.Down: return SettingsKeys.RebindActionPrefix + "move_down";
                case LogicalKey.Left: return SettingsKeys.RebindActionPrefix + "move_left";
                case LogicalKey.Right: return SettingsKeys.RebindActionPrefix + "move_right";
                case LogicalKey.Fire: return SettingsKeys.RebindActionPrefix + "fire";
                case LogicalKey.Skill: return SettingsKeys.RebindActionPrefix + "art";
                case LogicalKey.Stance: return SettingsKeys.RebindActionPrefix + "stance";
                case LogicalKey.Reverse: return SettingsKeys.RebindActionPrefix + "reverse_stance";
                default: return SettingsKeys.RebindActionPrefix + "pause";
            }
        }
    }
}

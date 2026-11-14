using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 물리 키 읽개. 기본 구현은 Input System의 Keyboard.current[Key]를 직접 읽는다(UiKeyboardReader).
    /// 테스트는 가짜로 갈아 끼운다.
    /// </summary>
    public interface IUiKeyReader
    {
        /// <summary>그 물리 키가 지금 눌려 있는가. 키보드가 없으면 false.</summary>
        bool IsDown(Key key);
        /// <summary>이번 프레임에 어떤 키든 새로 눌렸는가(입력 장치 모드 전환용).</summary>
        bool AnyKeyPressedThisFrame { get; }
        /// <summary>이번 프레임의 마우스 이동(px). 마우스가 없으면 0.</summary>
        Vector2 MouseDelta { get; }
    }

    /// <summary>Keyboard.current / Mouse.current를 읽는다. 장치가 없으면(배치 모드) "아무것도 안 눌림".</summary>
    public sealed class UiKeyboardReader : IUiKeyReader
    {
        public bool IsDown(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            try { return kb[key].isPressed; }
            catch (System.ArgumentOutOfRangeException) { return false; }
        }

        public bool AnyKeyPressedThisFrame
        {
            get { var kb = Keyboard.current; return kb != null && kb.anyKey.wasPressedThisFrame; }
        }

        public Vector2 MouseDelta
        {
            get { var m = Mouse.current; return m != null ? m.delta.ReadValue() : Vector2.zero; }
        }
    }

    /// <summary>
    /// 메뉴 키 폴링(08 요약서 1.2절, 00 문서 D-35·4.1절). 물리 키 자리로 읽는다.
    /// - 방향: 방향키 + 설정의 이동 키 배치(SettingsData.KeyPrimary/KeySecondary의 Up·Left·Down·Right). 배치가 비어 있으면 W/A/S/D.
    /// - J·Z = Confirm, K·X = Cancel(뒤로/빼기), Esc = Back(메뉴 길의 고정 키 — 일시정지도 이 길로만 온다), Q = PrevTab, E = NextTab.
    /// - 재설정한 일시정지 키(LogicalKey.Pause)는 여기서 잡지 않는다(전투 입력원 → IGameSession.PauseRequested 길).
    /// - 키 반복: 방향 키와 Q/E는 누르고 있으면 RepeatDelay 뒤 RepeatInterval마다 다시 낸다. 결정·뒤로·Esc는 반복하지 않는다.
    /// </summary>
    public sealed class MenuInput
    {
        /// <summary>[임시] 키 반복 시작 지연(초). 요약서에 값이 없다(결과 보고에 올림).</summary>
        public const float DefaultRepeatDelay = 0.4f;
        /// <summary>[임시] 키 반복 간격(초).</summary>
        public const float DefaultRepeatInterval = 0.1f;

        static readonly MenuKey[] AllKeys =
        {
            MenuKey.Up, MenuKey.Down, MenuKey.Left, MenuKey.Right, MenuKey.Confirm, MenuKey.Cancel, MenuKey.Back,
            MenuKey.PrevTab, MenuKey.NextTab
        };

        readonly IUiKeyReader _reader;
        readonly List<Key>[] _bindings = new List<Key>[AllKeys.Length];
        readonly bool[] _down = new bool[AllKeys.Length];
        readonly float[] _held = new float[AllKeys.Length];
        readonly float[] _nextRepeat = new float[AllKeys.Length];

        public float RepeatDelay = DefaultRepeatDelay;
        public float RepeatInterval = DefaultRepeatInterval;

        public MenuInput(IUiKeyReader reader = null)
        {
            _reader = reader ?? new UiKeyboardReader();
            for (int i = 0; i < _bindings.Length; i++) _bindings[i] = new List<Key>();
            ApplySettings(null);
        }

        public IUiKeyReader Reader => _reader;

        /// <summary>이번 Update에 어떤 키든 새로 눌렸는가(관리자가 입력 장치 모드를 Keyboard로 바꾼다).</summary>
        public bool AnyKeyPressed { get; private set; }

        /// <summary>메뉴 키 하나에 묶인 물리 키(읽기 전용 사본).</summary>
        public IReadOnlyList<Key> KeysOf(MenuKey key) => _bindings[(int)key];

        /// <summary>설정의 이동 키 배치를 다시 읽는다(설정이 바뀔 때). null이면 기본 W/A/S/D.</summary>
        public void ApplySettings(SettingsData settings)
        {
            for (int i = 0; i < _bindings.Length; i++) _bindings[i].Clear();
            Bind(MenuKey.Up, Key.UpArrow);
            Bind(MenuKey.Down, Key.DownArrow);
            Bind(MenuKey.Left, Key.LeftArrow);
            Bind(MenuKey.Right, Key.RightArrow);
            bool any = false;
            any |= BindFromSettings(settings, LogicalKey.Up, MenuKey.Up);
            any |= BindFromSettings(settings, LogicalKey.Down, MenuKey.Down);
            any |= BindFromSettings(settings, LogicalKey.Left, MenuKey.Left);
            any |= BindFromSettings(settings, LogicalKey.Right, MenuKey.Right);
            if (!any)
            {
                Bind(MenuKey.Up, Key.W);
                Bind(MenuKey.Down, Key.S);
                Bind(MenuKey.Left, Key.A);
                Bind(MenuKey.Right, Key.D);
            }
            Bind(MenuKey.Confirm, Key.J); Bind(MenuKey.Confirm, Key.Z);
            Bind(MenuKey.Cancel, Key.K); Bind(MenuKey.Cancel, Key.X);
            Bind(MenuKey.Back, Key.Escape);
            Bind(MenuKey.PrevTab, Key.Q);
            Bind(MenuKey.NextTab, Key.E);
        }

        bool BindFromSettings(SettingsData s, LogicalKey logical, MenuKey menu)
        {
            if (s == null) return false;
            bool any = false;
            int i = (int)logical;
            if (s.KeyPrimary != null && i < s.KeyPrimary.Count && s.KeyPrimary[i] != 0) { Bind(menu, (Key)s.KeyPrimary[i]); any = true; }
            if (s.KeySecondary != null && i < s.KeySecondary.Count && s.KeySecondary[i] != 0) { Bind(menu, (Key)s.KeySecondary[i]); any = true; }
            return any;
        }

        void Bind(MenuKey menu, Key key)
        {
            // Esc는 Back 전용(고정 키). 이동 키 배치에 들어 있어도 방향으로 쓰지 않는다.
            if (key == Key.None || (key == Key.Escape && menu != MenuKey.Back)) return;
            var list = _bindings[(int)menu];
            if (!list.Contains(key)) list.Add(key);
        }

        static bool Repeats(MenuKey k) =>
            k == MenuKey.Up || k == MenuKey.Down || k == MenuKey.Left || k == MenuKey.Right || k == MenuKey.PrevTab || k == MenuKey.NextTab;

        /// <summary>
        /// 한 프레임. 새로 눌린 메뉴 키(와 반복)를 into에 더한다(순서: MenuKey 번호 순). dt는 화면 시간(unscaled) 초.
        /// </summary>
        public void Update(float dt, List<MenuKey> into)
        {
            AnyKeyPressed = false;
            bool anyFromReader = false;
            try { anyFromReader = _reader.AnyKeyPressedThisFrame; } catch (System.Exception) { anyFromReader = false; }
            for (int i = 0; i < AllKeys.Length; i++)
            {
                bool now = false;
                var list = _bindings[i];
                for (int j = 0; j < list.Count && !now; j++) now = _reader.IsDown(list[j]);
                var mk = AllKeys[i];
                if (now && !_down[i])
                {
                    into.Add(mk);
                    _held[i] = 0f;
                    _nextRepeat[i] = RepeatDelay;
                    AnyKeyPressed = true;
                }
                else if (now && Repeats(mk))
                {
                    _held[i] += dt;
                    // 한 프레임이 길어도 반복은 한 번만 낸다(밀린 반복을 몰아 내지 않는다).
                    if (_held[i] >= _nextRepeat[i])
                    {
                        into.Add(mk);
                        while (_nextRepeat[i] <= _held[i]) _nextRepeat[i] += RepeatInterval > 0f ? RepeatInterval : 1f;
                    }
                }
                _down[i] = now;
            }
            if (anyFromReader) AnyKeyPressed = true;
        }

        /// <summary>지금 눌린 것으로 알고 있는 상태를 버린다(눌린 채인 키는 다시 누를 때까지 새 누름으로 내지 않는다).</summary>
        public void SwallowHeld()
        {
            for (int i = 0; i < AllKeys.Length; i++)
            {
                bool now = false;
                var list = _bindings[i];
                for (int j = 0; j < list.Count && !now; j++) now = _reader.IsDown(list[j]);
                _down[i] = now;
                _held[i] = 0f;
                _nextRepeat[i] = float.MaxValue;
            }
        }

        public bool IsHeld(MenuKey key) => _down[(int)key];
    }
}

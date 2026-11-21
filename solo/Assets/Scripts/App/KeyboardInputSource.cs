using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;

namespace Wizard.Solo.App
{
    /// <summary>물리 키 읽개. 실물은 InputSystemKeyReader(Keyboard.current), 테스트는 가짜로 갈아 끼운다.</summary>
    public interface IKeyReader
    {
        /// <summary>그 물리 자리가 지금 눌려 있나. Key.None·키보드 없음이면 false.</summary>
        bool IsPressed(Key key);
    }

    /// <summary>Input System의 Keyboard.current[Key]를 직접 읽는다(물리 자리). 키보드가 없으면(배치 모드) 아무 키도 안 눌림.</summary>
    public sealed class InputSystemKeyReader : IKeyReader
    {
        public bool IsPressed(Key key)
        {
            if (key == Key.None) return false;
            var kb = Keyboard.current;
            if (kb == null) return false;
            try
            {
                var control = kb[key];
                return control != null && control.isPressed;
            }
            catch (Exception)
            {
                // 이 키보드 배치에 없는 키 번호(설정 파일이 손상된 경우 등)는 안 눌림으로 본다.
                return false;
            }
        }
    }

    /// <summary>
    /// 전투 입력원(02 요약서 3절, 00 문서 4.1절). 매 Poll에 물리 키를 읽어 논리 상태가 바뀐 키만 이벤트로 낸다.
    /// 한 논리 키에 물리 키 둘이면 OR. 같은 Poll에서 바뀐 키는 모두 같은 시각(simTimeU) — 04 요약서 1.2절.
    /// 배치는 설정에서 읽고 설정이 바뀌면(ISettingsStore.Changed) 다시 읽는다. Esc는 내지 않는다.
    /// </summary>
    public sealed class KeyboardInputSource : IInputSource, IDisposable
    {
        readonly IKeyReader _reader;
        readonly ISettingsStore _settings;
        readonly LogicalKeyTracker _tracker = new LogicalKeyTracker();
        KeyBindings _bindings;

        public KeyboardInputSource(ISettingsStore settings, IKeyReader reader = null)
        {
            _settings = settings;
            _reader = reader ?? new InputSystemKeyReader();
            ReloadBindings();
            if (_settings != null) _settings.Changed += ReloadBindings;
        }

        /// <summary>배치를 직접 준다(테스트, 키 재설정 미리보기).</summary>
        public KeyboardInputSource(KeyBindings bindings, IKeyReader reader)
        {
            _reader = reader ?? new InputSystemKeyReader();
            _bindings = bindings ?? KeyBindings.CreateDefault();
        }

        public KeyBindings Bindings => _bindings;

        public void ReloadBindings()
        {
            _bindings = KeyBindings.FromSettings(_settings != null ? _settings.Current : null);
        }

        public void Dispose()
        {
            if (_settings != null) _settings.Changed -= ReloadBindings;
        }

        bool Physical(LogicalKey k)
        {
            int i = (int)k;
            Key a = _bindings.Primary[i], b = _bindings.Secondary[i];
            // Esc는 KeyBindings가 이미 걸렀지만, 배치를 직접 준 경우를 위해 한 번 더 막는다.
            bool pa = a != Key.None && a != Key.Escape && _reader.IsPressed(a);
            bool pb = b != Key.None && b != Key.Escape && _reader.IsPressed(b);
            return pa || pb;
        }

        public void Poll(long simTimeU, List<InputEvent> into)
        {
            var order = LogicalKeyTracker.EmitOrder;
            for (int n = 0; n < order.Length; n++) _tracker.Apply(order[n], Physical(order[n]), simTimeU, into);
        }

        public bool IsDown(LogicalKey key) => _tracker.IsDown(key);

        public void Resync(long simTimeU, List<InputEvent> into, bool swallowHeldActions = false)
        {
            var order = LogicalKeyTracker.EmitOrder;
            for (int n = 0; n < order.Length; n++)
                _tracker.Resync(order[n], Physical(order[n]), simTimeU, into, swallowHeldActions);
        }

        public void ReleaseAll(long simTimeU, List<InputEvent> into) => _tracker.ReleaseAll(simTimeU, into);
    }
}

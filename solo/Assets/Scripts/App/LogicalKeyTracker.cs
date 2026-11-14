using System.Collections.Generic;

namespace Wizard.Solo.App
{
    /// <summary>
    /// 논리 키 상태 추적(순수). KeyboardInputSource와 BotInputSource가 같이 쓴다.
    /// - 논리 상태가 바뀔 때만 이벤트를 낸다(02 요약서 3.1절: 눌린 키의 누름, 안 눌린 키의 뗌은 무시).
    /// - 삼킴: Resync(swallowHeldActions)에서 새로 눌려 있던 동작 키(Fire·Skill·Reverse·Pause)는 물리적으로 뗐다 다시 누를 때까지
    ///   누름을 내지 않는다(00 문서 4.1절 — 메뉴·일시정지 중 누른 J·K가 발사·비기로 새지 않는다).
    /// - 한 번에 여러 키를 낼 때의 순서는 02 요약서 3.2절의 분류 순서: 자세·반전 → 방향 → J → K → Pause.
    /// </summary>
    public sealed class LogicalKeyTracker
    {
        /// <summary>이벤트를 내는 키 순서(분류 0 자세·반전, 1 방향, 2 J, 3 K, 4 Pause).</summary>
        public static readonly LogicalKey[] EmitOrder =
        {
            LogicalKey.Stance, LogicalKey.Reverse,
            LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right,
            LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Pause
        };

        readonly bool[] _down = new bool[SimConst.LogicalKeyCount];
        readonly bool[] _swallowed = new bool[SimConst.LogicalKeyCount];

        /// <summary>누르는 순간이 뜻인 키. 나머지(방향 넷, Stance)는 상태 키.</summary>
        public static bool IsAction(LogicalKey k) =>
            k == LogicalKey.Fire || k == LogicalKey.Skill || k == LogicalKey.Reverse || k == LogicalKey.Pause;

        public bool IsDown(LogicalKey k) => _down[(int)k];
        public bool IsSwallowed(LogicalKey k) => _swallowed[(int)k];

        /// <summary>한 키의 지금 물리 상태를 반영한다(보통 Poll).</summary>
        public void Apply(LogicalKey k, bool physical, long timeU, List<InputEvent> into)
        {
            int i = (int)k;
            if (_swallowed[i])
            {
                // 삼킨 키는 물리적으로 뗄 때까지 내부 상태가 "뗌"이다. 떼면 삼킴만 풀린다(이벤트 없음).
                if (!physical) _swallowed[i] = false;
                return;
            }
            if (physical == _down[i]) return;
            _down[i] = physical;
            into.Add(new InputEvent(k, physical, timeU));
        }

        /// <summary>다시 맞추기: 뗀 키는 뗌, 새로 눌린 상태 키는 누름, 새로 눌린 동작 키는 swallow면 삼킨다.</summary>
        public void Resync(LogicalKey k, bool physical, long timeU, List<InputEvent> into, bool swallowHeldActions)
        {
            int i = (int)k;
            if (_swallowed[i])
            {
                if (!physical) _swallowed[i] = false;
                return;
            }
            if (_down[i] && !physical)
            {
                _down[i] = false;
                into.Add(new InputEvent(k, false, timeU));
            }
            else if (!_down[i] && physical)
            {
                if (swallowHeldActions && IsAction(k)) { _swallowed[i] = true; return; }
                _down[i] = true;
                into.Add(new InputEvent(k, true, timeU));
            }
        }

        /// <summary>눌림으로 아는 키를 전부 뗌으로 내고 내부 상태(삼킴 포함)를 비운다.</summary>
        public void ReleaseAll(long timeU, List<InputEvent> into)
        {
            for (int n = 0; n < EmitOrder.Length; n++)
            {
                int i = (int)EmitOrder[n];
                _swallowed[i] = false;
                if (!_down[i]) continue;
                _down[i] = false;
                into.Add(new InputEvent(EmitOrder[n], false, timeU));
            }
        }
    }
}

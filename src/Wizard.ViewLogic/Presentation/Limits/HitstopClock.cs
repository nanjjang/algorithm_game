using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // spec 13 14.4 그림 정지 [확정 규칙], effects.md 0.2절.
    //  - 게임 시간을 멈추지 않는다. 맞은 대상과 맞힌 탄의 **그림만** 멈춘다. 이 시계는 표시 쪽 밀리초만 다루고
    //    세계·틱 시계(SimClock, 결투 시계)를 참조하지 않는다. 그래서 그림 정지 중에도 코어 틱은 그대로 간다(TV-192).
    //  - 길이 = 요청 × HitstopScale(Full 1 / Reduced 0.5 / Minimal 0, hitstop_enabled 끔이면 0).
    //  - 같은 대상에 겹치면 더 늦게 끝나는 쪽을 쓴다(늘이기만 한다).
    // 대상 열쇠는 표시 쪽이 정한다(개체 id, 탄 id 등). 강화 화면처럼 화면 전체를 멈춰도 되는 곳은 열쇠 하나를 화면에 쓴다.
    public sealed class HitstopClock
    {
        private readonly Dictionary<long, long> _frozenUntilMs = new Dictionary<long, long>();
        private readonly List<long> _expired = new List<long>();

        public int ActiveCount => _frozenUntilMs.Count;

        // 적용한 길이(ms)를 돌려준다. 0 이면 멈추지 않는다
        public int Request(long targetKey, int durationMs, long nowMs, in MotionSettings settings)
        {
            float scale = MotionPresets.HitstopScale(settings);
            int applied = durationMs > 0 ? (int)Math.Round(durationMs * (double)scale, MidpointRounding.AwayFromZero) : 0;
            if (applied <= 0)
            {
                return 0;
            }
            long until = nowMs + applied;
            if (!_frozenUntilMs.TryGetValue(targetKey, out long existing) || existing < until)
            {
                _frozenUntilMs[targetKey] = until;
            }
            return applied;
        }

        // 코어의 HitstopRequest(길이 = 틱, spec 01) → 밀리초(반올림)로 바꿔 요청한다. 틱은 1/60초(단위 환산)
        public int RequestTicks(long targetKey, int durationTicks, long nowMs, in MotionSettings settings)
        {
            long ms = ((long)durationTicks * UiTime.MsPerSecond + UiTime.UiTicksPerSecond / 2) / UiTime.UiTicksPerSecond;
            return Request(targetKey, (int)Math.Max(0, Math.Min(int.MaxValue, ms)), nowMs, settings);
        }

        public bool IsFrozen(long targetKey, long nowMs) =>
            _frozenUntilMs.TryGetValue(targetKey, out long until) && nowMs < until;

        // 그 대상의 그림이 이번 프레임에 나아갈 시간. 멈춰 있으면 0
        public float VisualDeltaSeconds(long targetKey, long nowMs, float frameDeltaSeconds) =>
            IsFrozen(targetKey, nowMs) ? 0f : frameDeltaSeconds;

        // 끝난 항목을 지운다(프레임마다 한 번)
        public void Prune(long nowMs)
        {
            _expired.Clear();
            foreach (KeyValuePair<long, long> kv in _frozenUntilMs)
            {
                if (kv.Value <= nowMs)
                {
                    _expired.Add(kv.Key);
                }
            }
            foreach (long k in _expired)
            {
                _frozenUntilMs.Remove(k);
            }
        }

        public void Clear() => _frozenUntilMs.Clear();
    }
}

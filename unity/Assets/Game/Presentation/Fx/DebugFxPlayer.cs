#nullable enable
using System;
using System.Globalization;
using Wizard.Netcode.Duel;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Fx
{
    // 에셋이 없는 지금의 그림 재생기(CONTRACTS 13, ARCHITECTURE 9 "지금은 Null 구현 + 디버그 표시", T-29).
    // 그림을 만들지 않고 요청마다 표식(식별자 + 위치 + 단계)을 하나 남긴다. 디버그 겹침 화면(Bus/PresentationDebugOverlay)이 그 표식을 찍는다.
    // 실제 재생기(V-13)는 IFxPlayer 를 구현해 PresentationBusSetup.Fx 에 꽂기만 하면 된다. 라우터와 버스는 바뀌지 않는다.
    //  - Phase = Retracted: 같은 식별자·같은 자리의 살아 있는 예측 표식을 0.1초(CONTRACTS 13 FxRequest) 동안 흐리다 지운다.
    //    그 표식이 없으면(이미 지워졌으면) 흐려지는 표식을 새로 남긴다.
    //  - 입자를 내지 않으므로 입자 예산(ParticleBudget, effects.md 9.3)에 요청하지 않는다. 예산은 실제 재생기가 따른다.
    // 시계는 표시 쪽 밀리초(부르는 쪽이 준다). 게임 시간·Time.timeScale 을 보지 않는다.
    public sealed class DebugFxPlayer : IFxPlayer
    {
        public const int Capacity = 64;                   // 자료구조 크기. 넘치면 가장 오래된 표식을 버린다
        public const int RetractFadeMs = 100;             // CONTRACTS 13 FxRequest.Phase: Retracted 면 0.1초 흐려지며 사라진다
        public const int MarkLifetimeMs = 1000;           // [임시] 디버그 표식이 남는 길이(표시 전용. 규칙이 아니다)

        private readonly Func<long> _nowMs;
        private readonly DebugFxMark[] _marks = new DebugFxMark[Capacity];
        private int _count;

        public DebugFxPlayer(Func<long> nowMs)
        {
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
        }

        public int PlayCount { get; private set; }

        public int RetractCount { get; private set; }

        // 표식이 바뀔 때마다 오른다(겹침 화면이 다시 그릴지 고른다)
        public int Version { get; private set; }

        public int Count => _count;

        public DebugFxMark Mark(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _marks[index];
        }

        // 0~1. 흐려지는 표식은 0.1초 동안 1 → 0
        public float Alpha(int index, long nowMs)
        {
            DebugFxMark m = Mark(index);
            if (!m.Fading)
            {
                return 1f;
            }
            long t = nowMs - m.FadeStartMs;
            if (t <= 0)
            {
                return 1f;
            }
            return t >= RetractFadeMs ? 0f : 1f - t / (float)RetractFadeMs;
        }

        public void Play(in FxRequest request)
        {
            long now = _nowMs();
            PlayCount++;
            Version++;
            if (request.Phase == EventPhase.Retracted)
            {
                RetractCount++;
                for (int i = _count - 1; i >= 0; i--)
                {
                    DebugFxMark m = _marks[i];
                    if (!m.Fading && m.Phase == EventPhase.Predicted && m.FxId == request.FxId && m.WorldPos == request.WorldPos)
                    {
                        _marks[i] = m.WithFade(now, EventPhase.Retracted);
                        return;
                    }
                }
                Append(new DebugFxMark(request.FxId, request.WorldPos, EventPhase.Retracted, LabelOf(request), now, now));
                return;
            }
            Append(new DebugFxMark(request.FxId, request.WorldPos, request.Phase, LabelOf(request), now, -1));
        }

        public void StopAll()
        {
            if (_count > 0)
            {
                Array.Clear(_marks, 0, _count);
                _count = 0;
                Version++;
            }
        }

        // 프레임마다 한 번(버스의 Render). 끝난 표식을 지운다
        public void Advance(long nowMs)
        {
            int w = 0;
            for (int r = 0; r < _count; r++)
            {
                DebugFxMark m = _marks[r];
                bool dead = m.Fading ? nowMs - m.FadeStartMs >= RetractFadeMs : nowMs - m.StartMs >= MarkLifetimeMs;
                if (!dead)
                {
                    _marks[w++] = m;
                }
            }
            if (w != _count)
            {
                Array.Clear(_marks, w, _count - w);
                _count = w;
                Version++;
            }
        }

        // "fx_hit_edge (1.50, -2.00)": 식별자와 표시 좌표(칸). 숫자는 문화권과 무관하게 찍는다
        public static string LabelOf(in FxRequest request) =>
            request.FxId + " (" + request.WorldPos.X.ToString("0.00", CultureInfo.InvariantCulture) + ", " +
            request.WorldPos.Y.ToString("0.00", CultureInfo.InvariantCulture) + ")";

        private void Append(DebugFxMark mark)
        {
            if (_count == Capacity)
            {
                Array.Copy(_marks, 1, _marks, 0, Capacity - 1);
                _count--;
            }
            _marks[_count++] = mark;
        }
    }
}

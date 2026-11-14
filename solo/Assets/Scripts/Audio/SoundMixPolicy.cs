using System;
using System.Collections.Generic;

namespace Wizard.Solo.Audio
{
    public enum SoundRejectReason { None = 0, RateLimit = 1, TelegraphLimit = 2, VoicesFull = 3 }

    /// <summary>섞는 규칙의 판단 하나: 낼 것인가, 어떤 음량(선형 배율)으로, 어느 보이스에.</summary>
    public struct SoundDecision
    {
        public bool Accepted;
        public SoundRejectReason Reason;
        public SoundCategory Category;
        /// <summary>요청 음량에 한도(+6dB)와 누르기(−6dB)를 적용한 선형 배율. 묶음 음량은 아직 곱하지 않았다.</summary>
        public float Gain;
        public int VoiceIndex;
        /// <summary>우선순위로 밀어낸 소리의 사건 id(없으면 null).</summary>
        public string StolenEventId;
        public bool Ducked;
    }

    /// <summary>
    /// 섞는 규칙(10 요약서 5.2절, 순수 클래스): 동시 32개, 적중 1초 8번·처치 1초 6번, 예고 동시 2개,
    /// 우선순위(내가 맞음 &gt; 예고 &gt; 스침·임계·잠금 &gt; 비기 &gt; 적중 &gt; 처치 &gt; 환경)로 보이스 밀어내기,
    /// 피격 뒤 다른 소리 −6dB 0.4초, 음량 한도 +6dB. 시각(초)은 부르는 쪽이 준다(테스트가 시계를 쥔다).
    /// 가득 찼을 때 같은 우선순위끼리는 밀어내지 않는다(새 소리를 버린다). 같은 묶음 안에서는 가장 오래된 것을 민다.
    /// </summary>
    public sealed class SoundMixPolicy
    {
        struct Voice
        {
            public bool Active;
            public string EventId;
            public SoundCategory Category;
            public double Start;
            public double End;
        }

        readonly SoundMixConfig _cfg;
        readonly Voice[] _voices;
        readonly Queue<double> _hitTimes = new Queue<double>();
        readonly Queue<double> _killTimes = new Queue<double>();
        double _duckUntil = double.NegativeInfinity;

        public SoundMixPolicy(SoundMixConfig config = null)
        {
            _cfg = config ?? SoundMixConfig.Spec();
            _voices = new Voice[Math.Max(1, _cfg.MaxVoices)];
        }

        public SoundMixConfig Config => _cfg;

        /// <summary>우선순위(클수록 높다). 화면 소리는 비기와 같다(솔로 임시).</summary>
        public static int Priority(SoundCategory c)
        {
            switch (c)
            {
                case SoundCategory.PlayerHit: return 6;
                case SoundCategory.Telegraph: return 5;
                case SoundCategory.GrazeHeat: return 4;
                case SoundCategory.Art: return 3;
                case SoundCategory.Interface: return 3;
                case SoundCategory.Hit: return 2;
                case SoundCategory.Kill: return 1;
                default: return 0;
            }
        }

        public static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);

        public SoundDecision Request(string eventId, float volume, double now, float durationSeconds = -1f) =>
            Request(eventId, SoundEventClassifier.Classify(eventId), volume, now, durationSeconds);

        public SoundDecision Request(string eventId, SoundCategory category, float volume, double now, float durationSeconds = -1f)
        {
            Prune(now);
            var d = new SoundDecision { Category = category, VoiceIndex = -1 };

            // 1) 사건 묶음별 초당 횟수
            Queue<double> rate = category == SoundCategory.Hit ? _hitTimes : category == SoundCategory.Kill ? _killTimes : null;
            int rateLimit = category == SoundCategory.Hit ? _cfg.HitPerSecond : _cfg.KillPerSecond;
            if (rate != null)
            {
                while (rate.Count > 0 && rate.Peek() <= now - _cfg.RateWindowSeconds) rate.Dequeue();
                if (rate.Count >= rateLimit) { d.Reason = SoundRejectReason.RateLimit; return d; }
            }

            // 2) 예고 음 동시 수
            if (category == SoundCategory.Telegraph && CountActive(SoundCategory.Telegraph) >= _cfg.TelegraphConcurrent)
            {
                d.Reason = SoundRejectReason.TelegraphLimit;
                return d;
            }

            // 3) 보이스: 빈 자리 → 없으면 더 낮은 우선순위 중 가장 낮고 오래된 것을 민다
            int slot = -1;
            for (int i = 0; i < _voices.Length; i++) if (!_voices[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                int myPri = Priority(category);
                int best = -1;
                for (int i = 0; i < _voices.Length; i++)
                {
                    int p = Priority(_voices[i].Category);
                    if (p >= myPri) continue;
                    if (best < 0) { best = i; continue; }
                    int bp = Priority(_voices[best].Category);
                    if (p < bp || (p == bp && _voices[i].Start < _voices[best].Start)) best = i;
                }
                if (best < 0) { d.Reason = SoundRejectReason.VoicesFull; return d; }
                d.StolenEventId = _voices[best].EventId;
                slot = best;
            }

            // 4) 음량: 한도 +6dB, 누르기 −6dB(피격 소리 자신은 누르지 않는다)
            float cap = DbToGain(_cfg.MaxBoostDb);
            float gain = volume < 0f ? 0f : volume > cap ? cap : volume;
            if (category != SoundCategory.PlayerHit && now < _duckUntil)
            {
                gain *= DbToGain(_cfg.DuckDb);
                d.Ducked = true;
            }
            if (category == SoundCategory.PlayerHit) _duckUntil = now + _cfg.DuckSeconds;

            double len = durationSeconds > 0f ? durationSeconds : _cfg.DefaultVoiceSeconds;
            _voices[slot] = new Voice { Active = true, EventId = eventId, Category = category, Start = now, End = now + len };
            if (rate != null) rate.Enqueue(now);

            d.Accepted = true;
            d.Gain = gain;
            d.VoiceIndex = slot;
            return d;
        }

        /// <summary>지금 누르기가 걸려 있나(피격 뒤 0.4초). 실제 믹서가 이미 울리는 소리에 걸 때 쓴다.</summary>
        public bool IsDucking(double now) => now < _duckUntil;

        /// <summary>지금 걸린 누르기 배율(1 = 없음).</summary>
        public float DuckGain(double now) => IsDucking(now) ? DbToGain(_cfg.DuckDb) : 1f;

        public int ActiveVoices(double now)
        {
            Prune(now);
            int n = 0;
            for (int i = 0; i < _voices.Length; i++) if (_voices[i].Active) n++;
            return n;
        }

        /// <summary>모든 소리를 끊는다(강화의 숨 멈춤, +13의 0.8초 — 10 요약서 5.2절 "침묵"). 초당 횟수 기록은 그대로.</summary>
        public void CutAll()
        {
            for (int i = 0; i < _voices.Length; i++) _voices[i] = default;
        }

        /// <summary>처음 상태로(판·화면을 새로 열 때).</summary>
        public void Reset()
        {
            CutAll();
            _hitTimes.Clear();
            _killTimes.Clear();
            _duckUntil = double.NegativeInfinity;
        }

        int CountActive(SoundCategory c)
        {
            int n = 0;
            for (int i = 0; i < _voices.Length; i++) if (_voices[i].Active && _voices[i].Category == c) n++;
            return n;
        }

        void Prune(double now)
        {
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i].Active && _voices[i].End <= now) _voices[i] = default;
        }
    }
}

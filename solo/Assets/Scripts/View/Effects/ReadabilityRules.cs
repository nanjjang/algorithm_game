using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.View.Enemies;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 가독성 정렬 띠(10 요약서 3.1절 위에 얹은 솔로 재정의 — "적 탄과 예고는 언제나 내 것 위").
    /// 아래에서 위로: 바닥 &lt; 내 장판 &lt; (예고 채움) &lt; 적 몸 &lt; 플레이어 &lt; 내 탄·이펙트(가산) &lt; 피해 숫자 &lt; 적 탄 &lt; 적 예고 테두리 &lt; 화면 밖 표식 &lt; HUD.
    /// SortBands(Core)의 띠 값은 그대로 두고 띠 안의 자리만 정한다(띠 안 0~99 자유, YOrder는 0~139).
    /// </summary>
    public static class FxLayers
    {
        /// <summary>플레이어 몸: 적 몸 띠(Bodies + YOrder 0~139) 바로 위 — 적 사이에 묻혀도 내 캐릭터가 보인다.</summary>
        public const int PlayerBody = SortBands.Bodies + 150;
        /// <summary>피해 숫자: 내 이펙트 위, 적 탄 아래(숫자가 적 탄을 가리지 않는다).</summary>
        public const int DamageNumbers = SortBands.EnemyShots - 40;
        public const int EnemyShots = SortBands.EnemyShots;
        /// <summary>적 예고 테두리(3겹 + 차오름 앞선): 적 탄 위.</summary>
        public const int TelegraphEdge = SortBands.EnemyShots + 80;
        /// <summary>화면 밖 표식: 전장의 모든 것 위(HUD 캔버스 아래).</summary>
        public const int Offscreen = SortBands.DamageNumbers + 300;
        public const int OffscreenTelegraph = Offscreen + 20;
    }

    /// <summary>시각 효과 우선순위(작을수록 앞선다). 프레임 상한이 차면 큰 번호부터 버린다. 적 예고·적 탄은 이 표에 들지 않는다(늘 그린다).</summary>
    public enum FxPriority
    {
        EnemyTelegraph = 0,
        PlayerHurt = 1,
        Graze = 2,
        Crit = 3,
        NormalHit = 4,
        Decor = 5
    }

    /// <summary>
    /// 프레임당 연출 상한과 군중 감쇠(순수 — 표시 시계·프레임은 부르는 쪽이 넘긴다). EffectsShared가 하나 가진다.
    /// - 입자: 프레임당 합계 FrameParticles. 우선순위마다 쓸 수 있는 몫의 끝(Share)이 있어, 합계가 그 끝에 닿으면 그 순위는 더 못 낸다
    ///   → 낮은 순위(장식)부터 버려지고 피격·스침은 끝까지 남는다.
    /// - 피해 숫자: 프레임당 새 숫자 FrameNumbers개(치명·큰 피해·합침은 셈 밖).
    /// - 흔들림: 짧은 창(ShakeWindow) 안에서는 이미 보낸 것보다 센 것만 보낸다(겹쳐 쌓이지 않는다). 세기 상한 ShakeMaxPx.
    /// - 그림 정지: 무게 비례 — 일반 타격·장식은 0, 치명·비기·보스는 값대로. 화면 위 적이 많을수록 치명·장식 정지를 줄인다. 한 번 상한 HitStopMax.
    /// - 군중: 화면 위 적 수가 CrowdFrom → CrowdTo로 늘면 장식 배율 1 → CrowdDecorMin, 일반 정지 배율 1 → CrowdHitStopMin.
    /// </summary>
    public sealed class FxGovernor
    {
        public int FrameParticles = 140;
        /// <summary>순위별 몫의 끝(합계에 대한 비율). 색인 = (int)FxPriority.</summary>
        public readonly float[] Share = { 1f, 1f, 0.95f, 0.85f, 0.7f, 0.45f };
        public int FrameNumbers = 6;
        public float ShakeWindow = 0.12f, ShakeMaxPx = 16f;
        public float HitStopMax = 0.16f;
        public int CrowdFrom = 20, CrowdTo = 70;
        public float CrowdDecorMin = 0.25f, CrowdHitStopMin = 0f;

        int _frameParticles, _frameNumbers;
        float _shakeSentPx, _shakeSentAt = -100f;
        int _onScreenEnemies;

        // 점검용
        public int DroppedParticles { get; private set; }
        public int DroppedNumbers { get; private set; }
        public int DroppedShakes { get; private set; }
        public int FrameParticleCount => _frameParticles;
        public int OnScreenEnemies => _onScreenEnemies;

        public static FxGovernor FromJuice(EffectsTuning j)
        {
            var g = new FxGovernor();
            if (j == null) return g;
            int I(string k, int d) { float v = j.F("readability", k); return v > 0f ? Mathf.RoundToInt(v) : d; }
            float F(string k, float d) { var t = j.Token("readability", k); return t != null ? j.F("readability", k) : d; }
            g.FrameParticles = I("frame_particles", g.FrameParticles);
            g.FrameNumbers = I("frame_numbers", g.FrameNumbers);
            g.ShakeWindow = F("shake_window_seconds", g.ShakeWindow);
            g.ShakeMaxPx = F("shake_max_px", g.ShakeMaxPx);
            g.HitStopMax = F("hitstop_max_seconds", g.HitStopMax);
            g.CrowdFrom = I("crowd_from", g.CrowdFrom);
            g.CrowdTo = I("crowd_to", g.CrowdTo);
            g.CrowdDecorMin = F("crowd_decor_min", g.CrowdDecorMin);
            g.CrowdHitStopMin = F("crowd_hitstop_min", g.CrowdHitStopMin);
            string[] names = { "telegraph", "hurt", "graze", "crit", "hit", "decor" };
            for (int i = 0; i < names.Length; i++)
                if (j.Token("readability", "priority_share", names[i]) != null) g.Share[i] = Mathf.Clamp01(j.F("readability", "priority_share", names[i]));
            return g;
        }

        /// <summary>입자 예산 분류 → 우선순위(적중·처치 = 일반 타격, 비기 = 치명급, 탄·대기·환경 = 장식).</summary>
        public static FxPriority PriorityOf(string category)
        {
            if (category == ParticleBudgetConfig.CatHit || category == ParticleBudgetConfig.CatKill) return FxPriority.NormalHit;
            if (category == ParticleBudgetConfig.CatArt) return FxPriority.Crit;
            return FxPriority.Decor;
        }

        /// <summary>프레임 시작(화면 위 적 수를 같이 받는다).</summary>
        public void BeginFrame(int onScreenEnemies)
        {
            _frameParticles = 0;
            _frameNumbers = 0;
            _onScreenEnemies = Mathf.Max(0, onScreenEnemies);
        }

        /// <summary>0 = 한산, 1 = 붐빔(CrowdTo 이상).</summary>
        public float Crowd
        {
            get
            {
                if (CrowdTo <= CrowdFrom) return _onScreenEnemies >= CrowdTo ? 1f : 0f;
                return Mathf.Clamp01((_onScreenEnemies - CrowdFrom) / (float)(CrowdTo - CrowdFrom));
            }
        }

        /// <summary>장식 입자 수 배율(붐빌수록 줄어든다).</summary>
        public float DecorScale => Mathf.Lerp(1f, CrowdDecorMin, Crowd);

        /// <summary>입자 수 배율: 장식·일반 타격만 군중에 줄어든다(일반 타격은 장식의 절반만큼).</summary>
        public float CountScale(FxPriority p)
        {
            if (p == FxPriority.Decor) return DecorScale;
            if (p == FxPriority.NormalHit) return Mathf.Lerp(1f, DecorScale, 0.5f);
            return 1f;
        }

        /// <summary>입자 n개를 이 프레임에 내도 되는가. 되면 셈에 더하고 true.</summary>
        public bool AllowParticles(FxPriority p, int n = 1)
        {
            if (n <= 0) return true;
            int i = Mathf.Clamp((int)p, 0, Share.Length - 1);
            if (p <= FxPriority.PlayerHurt) { _frameParticles += n; return true; } // 적 예고·내 피격은 버리지 않는다
            int end = Mathf.RoundToInt(FrameParticles * Share[i]);
            if (_frameParticles + n > end) { DroppedParticles += n; return false; }
            _frameParticles += n;
            return true;
        }

        /// <summary>새 피해 숫자. important(치명·약점·큰 피해·골드)는 늘 된다.</summary>
        public bool AllowNumber(bool important)
        {
            if (important) { _frameNumbers++; return true; }
            if (_frameNumbers >= FrameNumbers) { DroppedNumbers++; return false; }
            _frameNumbers++;
            return true;
        }

        /// <summary>흔들림 세기를 고른다(0 = 보내지 않음). now = 표시 초.</summary>
        public float Shake(float px, float now, FxPriority p = FxPriority.Crit)
        {
            if (px <= 0f) return 0f;
            if (p == FxPriority.Decor) px *= DecorScale;
            if (px > ShakeMaxPx) px = ShakeMaxPx;
            if (now - _shakeSentAt < ShakeWindow && px <= _shakeSentPx) { DroppedShakes++; return 0f; }
            if (now - _shakeSentAt >= ShakeWindow) _shakeSentPx = 0f;
            _shakeSentPx = Mathf.Max(_shakeSentPx, px);
            _shakeSentAt = now;
            return px;
        }

        /// <summary>그림 정지 길이(무게 비례). 일반 타격·장식·스침은 0(스침은 조작이 끊기면 안 된다 — 10 3.5절).</summary>
        public float HitStop(float seconds, FxPriority p, bool boss = false)
        {
            if (seconds <= 0f) return 0f;
            if (p == FxPriority.NormalHit || p == FxPriority.Decor || p == FxPriority.Graze) return 0f;
            if (!boss && p == FxPriority.Crit) seconds *= Mathf.Lerp(1f, CrowdHitStopMin, Crowd);
            return Mathf.Min(seconds, HitStopMax);
        }
    }

    /// <summary>플레이어 쪽 색이 위험색(색상각 330~15도, 채도·명도 70% 이상 — 적 공격·예고 전용)에 들면 한 번 경고한다.</summary>
    public sealed class PlayerColorGuard
    {
        readonly HashSet<string> _warned = new HashSet<string>();
        public int Warnings { get; private set; }

        public static bool IsDanger(Color c) => DangerColor.IsDanger((Color32)c);

        /// <summary>위험색이면 false(처음 보는 자리면 디버그 경고 하나).</summary>
        public bool Check(Color c, string where)
        {
            if (!IsDanger(c)) return true;
            Warnings++;
            if (where != null && _warned.Add(where))
                Debug.LogWarning("[readability] player-side color in danger range at " + where + ": #" + ColorUtility.ToHtmlStringRGB(c));
            return false;
        }
    }

    /// <summary>설정 '피해 숫자'(크게/작게/끄기)와 '내 이펙트 불투명도'·'섬광 줄이기'의 순수 도우미.</summary>
    public static class ReadabilityOptions
    {
        /// <summary>크기 배율. 끄기 = 0.</summary>
        public static float DamageNumberSizeScale(string size, float small, float large)
        {
            if (size == OptionValues.Off) return 0f;
            if (size == OptionValues.Large) return large > 0f ? large : 1f;
            return small > 0f ? small : 1f;
        }

        /// <summary>내 이펙트 알파 = 원래 알파 × 옵션(0~1). 누적 가림 상한은 이 뒤에 건다(10 3.2절 "옵션으로 못 올림").</summary>
        public static float MyFxAlpha(float alpha, float option01) => alpha * Mathf.Clamp01(option01);
    }

    /// <summary>
    /// 큰 고리 합치기(순수): 동시에 떠 있는 큰 고리는 MaxLive개까지. 앞 고리가 시작된 지 MergeWindow 안에 새 큰 고리가 오면
    /// 따로 띄우지 않고 그 고리를 키운다(보스 처치 + 웨이브 클리어 → 더 큰 고리 한 번). 자리가 꽉 차면 가장 약한(작은) 것과 합친다.
    /// </summary>
    public sealed class BigRingMerger
    {
        public struct Ring
        {
            public int Id;
            public Vector2 Pos;
            public float FromR, ToR, Life, Start;
            public Color Color;
            public int Merges;
        }

        public int MaxLive = 2;
        public float MergeWindow = 0.25f, MergeGrow = 1.2f, MaxRadius = 14f;
        /// <summary>새 고리가 앞 고리의 이 비율 이상이면 "비슷한 사건"으로 보고 키운다. 작으면 흡수만.</summary>
        public float ComparableRatio = 0.8f;
        readonly List<Ring> _live = new List<Ring>();
        int _nextId = 1;

        public IReadOnlyList<Ring> Live => _live;
        /// <summary>마지막 Request가 합친 고리를 키웠는가(false면 새 고리이거나 흡수만 했다).</summary>
        public bool LastGrew { get; private set; }
        public int Count => _live.Count;

        public void Prune(float now)
        {
            for (int i = _live.Count - 1; i >= 0; i--) if (now - _live[i].Start >= _live[i].Life) _live.RemoveAt(i);
        }

        /// <summary>새 큰 고리를 청한다. merged = 기존 고리에 합쳤다. 돌려준 Id의 고리를 그린다.</summary>
        public int Request(Vector2 pos, float fromR, float toR, float life, Color color, float now, out bool merged)
        {
            Prune(now);
            merged = false;
            LastGrew = false;
            int target = -1;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (now - _live[i].Start <= MergeWindow) { target = i; break; }
            if (target < 0 && _live.Count >= MaxLive)
            {
                // 꽉 찼으면 가장 작은 것과 합친다
                target = 0;
                for (int i = 1; i < _live.Count; i++) if (_live[i].ToR < _live[target].ToR) target = i;
            }
            if (target >= 0)
            {
                var r = _live[target];
                if (toR >= r.ToR * ComparableRatio)
                {
                    // 비슷한 크기의 큰 사건이 겹쳤다 → 하나로 합쳐 더 크게, 처음부터 다시 퍼진다
                    float big = Mathf.Max(r.ToR, toR);
                    r.ToR = Mathf.Min(MaxRadius, big * MergeGrow);
                    r.FromR = Mathf.Min(r.FromR, fromR);
                    r.Pos = Vector2.Lerp(r.Pos, pos, toR >= r.ToR ? 0.5f : 0.25f);
                    r.Life = Mathf.Max(r.Life, life) * 1.1f;
                    r.Start = now;
                    if (color.a > r.Color.a) r.Color = color;
                    r.Merges++;
                    LastGrew = true;
                }
                // 훨씬 작은 메아리 고리는 앞 고리에 흡수(따로 그리지 않는다)
                _live[target] = r;
                merged = true;
                return r.Id;
            }
            var n = new Ring { Id = _nextId++, Pos = pos, FromR = fromR, ToR = Mathf.Min(MaxRadius, toR), Life = life, Start = now, Color = color };
            _live.Add(n);
            return n.Id;
        }

        public bool TryGet(int id, out Ring ring)
        {
            for (int i = 0; i < _live.Count; i++) if (_live[i].Id == id) { ring = _live[i]; return true; }
            ring = default(Ring);
            return false;
        }

        public void Clear() => _live.Clear();
    }
}

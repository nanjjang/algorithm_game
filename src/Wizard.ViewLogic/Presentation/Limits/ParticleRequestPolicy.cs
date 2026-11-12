using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3 "지팡이 문서가 채우는 몫"과 그 아래 규칙: 효과가 ParticleBudget 에 요청하기 **전에** 요청량을 정한다.
    //  - 단계 구간별 요청 상한(다섯 지팡이 공통)으로 자른다. 지팡이마다 따로 주는 몫은 없다.
    //  - "지팡이 효과" 줄임 = 요청량 절반(올림), 최소 = 대기 0, 총구 0, 흘리는 입자 0, 적중 최소형(입자 2개), 꼬리 띠 없음.
    //  - 내 탄 수 문턱(100 / 200): 흘리는 입자 끔, 꼬리 띠 절반·끔, 적중 장식 절반·최소형.
    //  - 결투 상대: 꼬리 띠 없음, 흘리는 입자 0, 적중 최소형, 총구 0, 대기는 요청량의 절반(올림) 30까지(DECISIONS 2.7).
    //  - 광장: 나는 요청량 전부(60까지), 가까운 순 8명은 절반(올림) 30까지, 나머지 0. "남의 지팡이 효과" 줄임·끔이면 8명도 0.
    // 줄이는 규칙이 둘 이상 걸리면 [임시] 겹쳐 곱하지 않고 가장 강한 하나를 쓴다(예: 지팡이 효과 줄임 + 탄 150발 → 적중 절반 한 번).
    public sealed class ParticleRequestPolicy
    {
        private readonly ParticleBudgetTable _table;

        public ParticleRequestPolicy(ParticleBudgetTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public ParticleBudgetTable Table => _table;

        public int MaxLifetimeMs(WandParticleKind kind) => _table.Lifetimes.Of(kind);

        public static ParticleCategory CategoryOf(WandParticleKind kind)
        {
            switch (kind)
            {
                case WandParticleKind.Idle: return ParticleCategory.WandIdle;
                case WandParticleKind.Muzzle:
                case WandParticleKind.Shed: return ParticleCategory.Shot;
                case WandParticleKind.Hit: return ParticleCategory.Hit;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        // 손에 든 지팡이 한 자루의 대기 입자(동시)
        public int Idle(int tier, int requested, WandFxLevel level)
        {
            int n = Clamp(requested, _table.TierCap(tier).Idle);
            return ApplyLevel(n, level);
        }

        // 결투 상대 지팡이의 대기 입자: 요청량의 절반(올림), 30까지. 본체 모양은 실제 단계 그대로(입자가 아니다)
        public int DuelOpponentIdle(int tier, int requested, WandFxLevel level)
        {
            int n = Math.Min(HalfUp(Clamp(requested, _table.TierCap(tier).Idle)), _table.Shares.DuelOpponentIdleCap);
            return ApplyLevel(n, level);
        }

        // 광장의 지팡이 대기. nearRank = 나와의 거리 순위(0 = 나, 1 = 가장 가까운 사람)
        public int PlazaIdle(int nearRank, int tier, int requested, WandFxLevel ownLevel, OthersWandFx others)
        {
            int n = Clamp(requested, _table.TierCap(tier).Idle);
            if (nearRank <= 0)
            {
                return ApplyLevel(Math.Min(n, _table.Shares.PlazaSelfIdleCap), ownLevel);
            }
            if (nearRank > _table.Shares.PlazaNearCount || others != OthersWandFx.All)
            {
                return 0;
            }
            return Math.Min(HalfUp(n), _table.Shares.PlazaNearIdleCap);
        }

        // 발사 1회의 총구 입자(분열로 여러 발이어도 한 묶음)
        public int Muzzle(int tier, int requested, WandFxLevel level, bool duelOpponent)
        {
            if (duelOpponent)
            {
                return 0;
            }
            return ApplyLevel(Clamp(requested, _table.TierCap(tier).Muzzle), level);
        }

        // 날아가는 탄 1발 뒤에 동시에 살아 있는 입자
        public int ShedPerBullet(int tier, int requested, WandFxLevel level, int myBulletsOnScreen, bool duelOpponent)
        {
            if (duelOpponent || !_table.BulletLoad(myBulletsOnScreen).ShedEnabled)
            {
                return 0;
            }
            return ApplyLevel(Clamp(requested, _table.TierCap(tier).ShedPerBullet), level);
        }

        // 적중 1회. 덧붙는 것(임계 +4, 약점 +2, 저항 −2)은 지팡이 요청량 밖이라 상한으로 자른 뒤에 더한다
        public int Hit(int tier, int requested, WandFxLevel level, int myBulletsOnScreen, bool crit, bool weak, bool resist, bool duelOpponent)
        {
            HitDecorRules r = _table.Hit;
            int n = Clamp(requested, _table.TierCap(tier).Hit);
            if (crit)
            {
                n += r.ExtraCrit;
            }
            if (weak)
            {
                n += r.ExtraWeak;
            }
            if (resist)
            {
                n += r.ExtraResist;
            }
            n = Math.Max(0, n);
            HitDecorMode mode = Strongest(ModeOf(level), _table.BulletLoad(myBulletsOnScreen).HitDecor);
            if (duelOpponent)
            {
                mode = HitDecorMode.Minimal;
            }
            switch (mode)
            {
                case HitDecorMode.Full: return n;
                case HitDecorMode.Half: return HalfUp(n);
                case HitDecorMode.Minimal: return Math.Min(n, r.MinimalParticles);
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        // 같은 적이 짧게 여러 번 맞아 큰 불꽃 하나로 합칠 때의 입자 수(9.3: 큰 불꽃 하나 = 입자 8)
        public int MergedHit => _table.Hit.MergedParticles;

        // 꼬리 띠 길이(px). 입자가 아니지만 9.3의 길이 한도와 내 탄 수 문턱을 따른다
        public int TrailLengthPx(int tier, int requestedPx, WandFxLevel level, int myBulletsOnScreen, bool duelOpponent)
        {
            if (duelOpponent || level == WandFxLevel.Minimal)
            {
                return 0;
            }
            int px = Clamp(requestedPx, Math.Min(_table.TierCap(tier).TrailLengthPx, _table.Trail.MaxLengthPx));
            int percent = _table.BulletLoad(myBulletsOnScreen).TrailLengthPercent;
            int byLoad = px * percent / ParticleBudgetMath.PercentScale;
            int byLevel = level == WandFxLevel.Reduced ? HalfUp(px) : px;
            return Math.Min(byLoad, byLevel);
        }

        // 처치 조각: 한 프레임에 5마리 넘게 죽으면 조각을 절반(올림)으로
        public int KillShards(int requested, int killsThisFrame)
        {
            int n = Math.Max(0, requested);
            return killsThisFrame > _table.Hit.KillHalveOverPerFrame ? HalfUp(n) : n;
        }

        public static int HalfUp(int n) => n <= 0 ? 0 : (n + 1) / 2;

        private static int Clamp(int requested, int cap) => Math.Max(0, Math.Min(requested, cap));

        private static int ApplyLevel(int n, WandFxLevel level)
        {
            switch (level)
            {
                case WandFxLevel.All: return n;
                case WandFxLevel.Reduced: return HalfUp(n);
                case WandFxLevel.Minimal: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        private static HitDecorMode ModeOf(WandFxLevel level)
        {
            switch (level)
            {
                case WandFxLevel.All: return HitDecorMode.Full;
                case WandFxLevel.Reduced: return HitDecorMode.Half;
                case WandFxLevel.Minimal: return HitDecorMode.Minimal;
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        private static HitDecorMode Strongest(HitDecorMode a, HitDecorMode b) => (byte)a >= (byte)b ? a : b;
    }
}

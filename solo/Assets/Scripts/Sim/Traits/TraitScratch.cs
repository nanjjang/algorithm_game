using System.Collections.Generic;

namespace Wizard.Solo.Sim.Traits
{
    /// <summary>탐식이 깎은 열 한 건(최근 60틱 합 상한용). 02 요약서 9.1절.</summary>
    public struct GluttonyEntry
    {
        public int Tick;
        public int Hu;
    }

    /// <summary>대상 고르기 후보(거리 제곱, 적).</summary>
    public struct TraitTarget
    {
        public float SqrDistance;
        public EnemyState Enemy;
    }

    /// <summary>
    /// PlayerState.TraitScratch에 두는 Traits 전용 상태(플레이어마다 하나). 모듈 객체에는 판 상태를 두지 않는다
    /// (세계 둘이 같은 모듈 묶음을 써도 서로 닿지 않게).
    /// </summary>
    public sealed class TraitScratch
    {
        public const int NoTick = int.MinValue;

        // ---- 불안정 핵: 다음 J 발사분을 미리 굴려 둔 결과
        /// <summary>다음 발사분이 굴려져 있다.</summary>
        public bool HasUnstableRoll;
        /// <summary>굴려 둔 결과: 다음 발사가 자폭이다.</summary>
        public bool UnstableExplodes;
        /// <summary>굴려 둔 결과를 RollUnstableCore가 돌려준 틱(같은 틱의 되물음은 같은 값). 아직 안 썼으면 NoTick.</summary>
        public int UnstableConsumedTick = NoTick;
        /// <summary>UnstableCoreWarn을 낼(다시 낼) 틱. 없으면 NoTick.</summary>
        public int NextWarnTick = NoTick;
        /// <summary>폭발 반경 배율(불안정 핵 2, 없으면 1). ExplosionRadiusMul이 세계 없이 읽는다.</summary>
        public float ExplosionRadiusMul = 1f;

        // ---- 탐식: 최근 창 안에 깎은 열
        public readonly List<GluttonyEntry> GluttonyWindow = new List<GluttonyEntry>();

        // ---- 유리 지팡이: 이번 웨이브에 내려간 횟수
        public int GlassDropsThisWave;

        // ---- 대상 고르기 임시 목록(끓어넘침, 피뢰침). 호출 안에서만 뜻이 있다.
        public readonly List<TraitTarget> Targets = new List<TraitTarget>();

        public int GluttonySumHu(int nowTick, int windowTicks)
        {
            int sum = 0;
            for (int i = 0; i < GluttonyWindow.Count; i++)
                if (GluttonyWindow[i].Tick > nowTick - windowTicks) sum += GluttonyWindow[i].Hu;
            return sum;
        }

        public void PruneGluttony(int nowTick, int windowTicks)
        {
            int keepFrom = 0;
            while (keepFrom < GluttonyWindow.Count && GluttonyWindow[keepFrom].Tick <= nowTick - windowTicks) keepFrom++;
            if (keepFrom > 0) GluttonyWindow.RemoveRange(0, keepFrom);
        }
    }
}

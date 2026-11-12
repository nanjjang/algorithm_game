using System;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // 한 화면의 한 분류 몫(effects.md 9.3 표의 한 줄). 몫은 연출 줄이기 단계별(Full, Reduced, Minimal)
    public sealed class ParticlePoolDef
    {
        private readonly int[] _quota;

        public ParticlePoolDef(ParticleCategory category, int priority, ParticleRecurrence recurrence, int[] quotaByPreset, bool outsideTotal)
        {
            if (quotaByPreset == null || quotaByPreset.Length != ParticleBudgetTable.PresetCount)
            {
                throw new ArgumentException("quota needs one value per MotionPreset", nameof(quotaByPreset));
            }
            Category = category;
            Priority = priority;
            Recurrence = recurrence;
            _quota = (int[])quotaByPreset.Clone();
            OutsideTotal = outsideTotal;
        }

        public ParticleCategory Category { get; }

        // 작을수록 중요하다. 합계가 넘치면 큰 번호부터 줄인다(9.3 "넘쳤을 때" 4번: 6 → 5 → 4 → 3 → 2, 1은 마지막)
        public int Priority { get; }

        public ParticleRecurrence Recurrence { get; }

        // 화면 합계에 세지 않는 몫(광장의 연습 허수아비: "분류 2와 4를 각 60씩 따로 준다")
        public bool OutsideTotal { get; }

        public int Quota(MotionPreset preset) => _quota[ParticleBudgetTable.PresetIndex(preset)];
    }
}

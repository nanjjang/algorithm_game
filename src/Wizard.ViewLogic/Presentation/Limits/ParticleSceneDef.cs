using System;
using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // 한 화면의 입자 예산: 합계(단계별)와 분류 몫
    public sealed class ParticleSceneDef
    {
        private readonly int[] _total;
        private readonly ParticlePoolDef[] _pools;

        public ParticleSceneDef(ParticleScene scene, int[] totalByPreset, IReadOnlyList<ParticlePoolDef> pools)
        {
            if (totalByPreset == null || totalByPreset.Length != ParticleBudgetTable.PresetCount)
            {
                throw new ArgumentException("total needs one value per MotionPreset", nameof(totalByPreset));
            }
            if (pools == null)
            {
                throw new ArgumentNullException(nameof(pools));
            }
            Scene = scene;
            _total = (int[])totalByPreset.Clone();
            _pools = new ParticlePoolDef[pools.Count];
            for (int i = 0; i < pools.Count; i++)
            {
                _pools[i] = pools[i] ?? throw new ArgumentNullException(nameof(pools));
            }
        }

        public ParticleScene Scene { get; }

        public IReadOnlyList<ParticlePoolDef> Pools => _pools;

        public int Total(MotionPreset preset) => _total[ParticleBudgetTable.PresetIndex(preset)];

        public ParticlePoolDef? Pool(ParticleCategory category)
        {
            foreach (ParticlePoolDef p in _pools)
            {
                if (p.Category == category)
                {
                    return p;
                }
            }
            return null;
        }

        public int Quota(ParticleCategory category, MotionPreset preset)
        {
            ParticlePoolDef? p = Pool(category);
            return p == null ? 0 : p.Quota(preset);
        }

        // 합계에 세는 몫의 합
        public int SumOfQuotas(MotionPreset preset)
        {
            int sum = 0;
            foreach (ParticlePoolDef p in _pools)
            {
                if (!p.OutsideTotal)
                {
                    sum += p.Quota(preset);
                }
            }
            return sum;
        }
    }
}

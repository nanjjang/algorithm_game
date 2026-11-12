using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wizard.ViewLogic.Presentation.Limits
{
    // effects.md 9.3절(입자 예산의 유일한 기준, DECISIONS 2.8)의 표 전체. 값은 data/presentation/particle_budget.json 에서 온다
    // (코드에 수치를 두지 않는다). 읽는 쪽(테스트, Unity 표시 계층)이 JSON 을 풀어 이 생성자에 넣는다(공유 코드에 JSON 파서를 들이지 않는다).
    // 생성자가 표의 모양을 검사하고 틀리면 ArgumentException 이다.
    public sealed class ParticleBudgetTable
    {
        public const int PresetCount = 3;   // MotionPreset 의 값 수(자료구조 크기)

        private readonly ParticleSceneDef[] _scenes;
        private readonly WandTierCap[] _tiers;
        private readonly BulletLoadStep[] _bulletLoad;

        public ParticleBudgetTable(IReadOnlyList<ParticleSceneDef> scenes, IReadOnlyList<WandTierCap> wandTiers, WandLifetimes lifetimes,
                                   HitDecorRules hit, IReadOnlyList<BulletLoadStep> bulletLoad, TrailLimits trail, ParticleShareRules shares)
        {
            _scenes = ToArray(scenes, nameof(scenes));
            _tiers = ToArray(wandTiers, nameof(wandTiers));
            _bulletLoad = ToArray(bulletLoad, nameof(bulletLoad));
            Lifetimes = lifetimes;
            Hit = hit;
            Trail = trail;
            Shares = shares;
            List<string> problems = Check();
            if (problems.Count > 0)
            {
                throw new ArgumentException("particle budget table is invalid: " + string.Join("; ", problems));
            }
        }

        public IReadOnlyList<ParticleSceneDef> Scenes => _scenes;

        public IReadOnlyList<WandTierCap> WandTiers => _tiers;

        public IReadOnlyList<BulletLoadStep> BulletLoadSteps => _bulletLoad;

        public WandLifetimes Lifetimes { get; }

        public HitDecorRules Hit { get; }

        public TrailLimits Trail { get; }

        public ParticleShareRules Shares { get; }

        public int MaxTier => _tiers[_tiers.Length - 1].MaxTier;

        public static int PresetIndex(MotionPreset preset)
        {
            switch (preset)
            {
                case MotionPreset.Full: return 0;
                case MotionPreset.Reduced: return 1;
                case MotionPreset.Minimal: return 2;
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        public ParticleSceneDef Scene(ParticleScene scene)
        {
            foreach (ParticleSceneDef s in _scenes)
            {
                if (s.Scene == scene)
                {
                    return s;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(scene), scene, "scene missing from particle budget table");
        }

        public int SceneTotal(ParticleScene scene, MotionPreset preset) => Scene(scene).Total(preset);

        public int Quota(ParticleScene scene, ParticleCategory category, MotionPreset preset) => Scene(scene).Quota(category, preset);

        // 지팡이 단계(+0 ~ +MaxTier)의 요청 상한. 범위 밖 단계는 가까운 끝으로 자른다
        public WandTierCap TierCap(int tier)
        {
            int t = Math.Max(0, Math.Min(MaxTier, tier));
            foreach (WandTierCap c in _tiers)
            {
                if (t >= c.MinTier && t <= c.MaxTier)
                {
                    return c;
                }
            }
            throw new InvalidOperationException("wand tier table has a gap");   // 생성자가 막는다
        }

        // 화면의 내 탄 수 → 줄이기 단계(MinBullets 이하인 가장 높은 단계)
        public BulletLoadStep BulletLoad(int myBullets)
        {
            BulletLoadStep pick = _bulletLoad[0];
            foreach (BulletLoadStep s in _bulletLoad)
            {
                if (myBullets >= s.MinBullets)
                {
                    pick = s;
                }
            }
            return pick;
        }

        private List<string> Check()
        {
            var p = new List<string>();
            var seenScenes = new List<ParticleScene>();
            foreach (ParticleSceneDef s in _scenes)
            {
                if (seenScenes.Contains(s.Scene))
                {
                    p.Add("scene " + s.Scene + " twice");
                }
                seenScenes.Add(s.Scene);
                var seenCategories = new List<ParticleCategory>();
                var seenPriorities = new List<int>();
                foreach (ParticlePoolDef pool in s.Pools)
                {
                    if (pool.Category == ParticleCategory.None)
                    {
                        p.Add(s.Scene + ": pool without category");
                    }
                    if (seenCategories.Contains(pool.Category))
                    {
                        p.Add(s.Scene + ": category " + pool.Category + " twice");
                    }
                    seenCategories.Add(pool.Category);
                    if (seenPriorities.Contains(pool.Priority))
                    {
                        p.Add(s.Scene + ": priority " + Num(pool.Priority) + " twice (eviction order must be total)");
                    }
                    seenPriorities.Add(pool.Priority);
                    for (int i = 0; i < PresetCount; i++)
                    {
                        if (pool.Quota((MotionPreset)i) < 0)
                        {
                            p.Add(s.Scene + "/" + pool.Category + ": negative quota");
                        }
                    }
                }
                for (int i = 0; i < PresetCount; i++)
                {
                    var preset = (MotionPreset)i;
                    if (s.Total(preset) < 0)
                    {
                        p.Add(s.Scene + ": negative total");
                    }
                    if (s.Total(preset) > s.SumOfQuotas(preset))
                    {
                        p.Add(s.Scene + "/" + preset + ": total " + Num(s.Total(preset)) + " exceeds the sum of quotas " + Num(s.SumOfQuotas(preset)));
                    }
                }
            }
            if (_tiers.Length == 0)
            {
                p.Add("no wand tiers");
            }
            else
            {
                int expect = 0;
                foreach (WandTierCap c in _tiers)
                {
                    if (c.MinTier != expect || c.MaxTier < c.MinTier)
                    {
                        p.Add("wand tiers must be contiguous from +0 (at +" + Num(c.MinTier) + ")");
                    }
                    if (c.Idle < 0 || c.Muzzle < 0 || c.ShedPerBullet < 0 || c.Hit < 0 || c.TrailLengthPx < 0)
                    {
                        p.Add("wand tier +" + Num(c.MinTier) + ": negative cap");
                    }
                    expect = c.MaxTier + 1;
                }
            }
            if (_bulletLoad.Length == 0 || _bulletLoad[0].MinBullets != 0)
            {
                p.Add("bullet load steps must start at 0 bullets");
            }
            for (int i = 1; i < _bulletLoad.Length; i++)
            {
                if (_bulletLoad[i].MinBullets <= _bulletLoad[i - 1].MinBullets)
                {
                    p.Add("bullet load steps must be ascending");
                }
            }
            return p;
        }

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static T[] ToArray<T>(IReadOnlyList<T> list, string name)
        {
            if (list == null)
            {
                throw new ArgumentNullException(name);
            }
            var a = new T[list.Count];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = list[i];
            }
            return a;
        }
    }
}

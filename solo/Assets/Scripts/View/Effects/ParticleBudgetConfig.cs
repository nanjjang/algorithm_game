using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.View.Effects
{
    /// <summary>입자 분류 하나(particle_budget.json scenes[].pools[]).</summary>
    public sealed class ParticlePool
    {
        public string Category;
        /// <summary>작을수록 먼저 지킨다(합계가 넘치면 큰 번호부터 줄인다 — 10 요약서 3.3절).</summary>
        public int Priority;
        /// <summary>true = one_shot(터짐: 몫이 차면 가장 오래된 것이 흐려지며 자리를 내줌) / false = recurring(몫이 차면 새로 안 만듦).</summary>
        public bool OneShot;
        /// <summary>Full / Reduced / Minimal.</summary>
        public int[] Quota = new int[3];
    }

    /// <summary>지팡이 단계 구간별 입자 수(wand_tiers).</summary>
    public sealed class WandTier
    {
        public int MinTier, MaxTier;
        public int Idle, Muzzle, ShedPerBullet, Hit, TrailLengthPx;
    }

    /// <summary>화면의 내 탄 수 단계(bullet_load).</summary>
    public sealed class BulletLoadStep
    {
        public int MinBullets;
        public bool Shed;
        public int TrailLengthPercent;
        /// <summary>full / half / minimal.</summary>
        public string HitDecor;
    }

    /// <summary>
    /// data/presentation/particle_budget.json(08 요약서 15.3절, 10 요약서 3.3절)의 전투 장면 값. 세 칸 배열 = Full / Reduced / Minimal.
    /// </summary>
    public sealed class ParticleBudgetConfig
    {
        public const string Battle = "battle";
        public const string HitDecorFull = "full", HitDecorHalf = "half", HitDecorMinimal = "minimal";
        public const string CatArt = "art", CatHit = "hit", CatKill = "kill", CatShot = "shot", CatWandIdle = "wand_idle", CatEnvironment = "environment";

        public int[] Total = new int[3];
        public readonly List<ParticlePool> Pools = new List<ParticlePool>();
        public readonly List<WandTier> WandTiers = new List<WandTier>();
        public readonly List<BulletLoadStep> BulletLoad = new List<BulletLoadStep>();
        public float LifeIdle, LifeMuzzle, LifeShed, LifeHit;
        public int HitExtraCrit, HitExtraWeak, HitExtraResist, HitMinimalParticles, HitMergeOverHits, HitMergedParticles, KillHalveOverPerFrame;
        public float HitMergeWindowSeconds;
        public int TrailMaxLengthPx, TrailMaxFrames, TrailMaxOpacityPercent;
        public int GroundMarksPerWand, GroundMarkMaxDiameterPx, GroundMarkMaxOpacityPercent;
        public float GroundMarkMaxLifeSeconds;
        public int DamageNumbersMax;
        public float DamageMergeWindowSeconds, OneShotFadeSeconds;

        public ParticlePool Pool(string category)
        {
            for (int i = 0; i < Pools.Count; i++) if (Pools[i].Category == category) return Pools[i];
            return null;
        }

        /// <summary>지팡이 실효 단계 → 구간(없으면 첫 구간).</summary>
        public WandTier TierFor(int stage)
        {
            for (int i = 0; i < WandTiers.Count; i++)
                if (stage >= WandTiers[i].MinTier && stage <= WandTiers[i].MaxTier) return WandTiers[i];
            if (WandTiers.Count == 0) return new WandTier();
            return stage < WandTiers[0].MinTier ? WandTiers[0] : WandTiers[WandTiers.Count - 1];
        }

        /// <summary>화면의 내 탄 수 → 단계(min_bullets 이하로 가장 큰 것).</summary>
        public BulletLoadStep LoadFor(int bullets)
        {
            BulletLoadStep best = null;
            for (int i = 0; i < BulletLoad.Count; i++)
                if (bullets >= BulletLoad[i].MinBullets && (best == null || BulletLoad[i].MinBullets >= best.MinBullets)) best = BulletLoad[i];
            return best ?? new BulletLoadStep { Shed = true, TrailLengthPercent = 100, HitDecor = HitDecorFull };
        }

        public static ParticleBudgetConfig Parse(string json, string sceneId = Battle)
        {
            var c = new ParticleBudgetConfig();
            if (string.IsNullOrEmpty(json)) return c;
            var root = JObject.Parse(json);
            var scenes = root["scenes"] as JArray;
            if (scenes != null)
            {
                foreach (var st in scenes)
                {
                    var s = st as JObject;
                    if (s == null || s.Value<string>("id") != sceneId) continue;
                    c.Total = Three(s["total"]);
                    var pools = s["pools"] as JArray;
                    if (pools != null)
                        foreach (var pt in pools)
                        {
                            var p = pt as JObject;
                            if (p == null) continue;
                            c.Pools.Add(new ParticlePool
                            {
                                Category = p.Value<string>("category"),
                                Priority = p.Value<int?>("priority") ?? 0,
                                OneShot = p.Value<string>("kind") == "one_shot",
                                Quota = Three(p["quota"])
                            });
                        }
                }
            }
            var tiers = root["wand_tiers"] as JArray;
            if (tiers != null)
                foreach (var tt in tiers)
                {
                    var t = tt as JObject;
                    if (t == null) continue;
                    c.WandTiers.Add(new WandTier
                    {
                        MinTier = t.Value<int?>("min_tier") ?? 0, MaxTier = t.Value<int?>("max_tier") ?? 0,
                        Idle = t.Value<int?>("idle") ?? 0, Muzzle = t.Value<int?>("muzzle") ?? 0,
                        ShedPerBullet = t.Value<int?>("shed_per_bullet") ?? 0, Hit = t.Value<int?>("hit") ?? 0,
                        TrailLengthPx = t.Value<int?>("trail_length_px") ?? 0
                    });
                }
            var life = root["wand_lifetimes_seconds"] as JObject;
            if (life != null)
            {
                c.LifeIdle = life.Value<float?>("idle") ?? 0f;
                c.LifeMuzzle = life.Value<float?>("muzzle") ?? 0f;
                c.LifeShed = life.Value<float?>("shed") ?? 0f;
                c.LifeHit = life.Value<float?>("hit") ?? 0f;
            }
            var hit = root["hit"] as JObject;
            if (hit != null)
            {
                c.HitExtraCrit = hit.Value<int?>("extra_crit") ?? 0;
                c.HitExtraWeak = hit.Value<int?>("extra_weak") ?? 0;
                c.HitExtraResist = hit.Value<int?>("extra_resist") ?? 0;
                c.HitMinimalParticles = hit.Value<int?>("minimal_particles") ?? 0;
                c.HitMergeWindowSeconds = hit.Value<float?>("merge_window_seconds") ?? 0f;
                c.HitMergeOverHits = hit.Value<int?>("merge_over_hits") ?? 0;
                c.HitMergedParticles = hit.Value<int?>("merged_particles") ?? 0;
                c.KillHalveOverPerFrame = hit.Value<int?>("kill_halve_over_per_frame") ?? 0;
            }
            var load = root["bullet_load"] as JArray;
            if (load != null)
                foreach (var lt in load)
                {
                    var l = lt as JObject;
                    if (l == null) continue;
                    c.BulletLoad.Add(new BulletLoadStep
                    {
                        MinBullets = l.Value<int?>("min_bullets") ?? 0,
                        Shed = l.Value<bool?>("shed") ?? false,
                        TrailLengthPercent = l.Value<int?>("trail_length_percent") ?? 0,
                        HitDecor = l.Value<string>("hit_decor") ?? HitDecorFull
                    });
                }
            var trail = root["trail"] as JObject;
            if (trail != null)
            {
                c.TrailMaxLengthPx = trail.Value<int?>("max_length_px") ?? 0;
                c.TrailMaxFrames = trail.Value<int?>("max_frames") ?? 0;
                c.TrailMaxOpacityPercent = trail.Value<int?>("max_opacity_percent") ?? 0;
                c.GroundMarksPerWand = trail.Value<int?>("ground_marks_per_wand") ?? 0;
                c.GroundMarkMaxDiameterPx = trail.Value<int?>("ground_mark_max_diameter_px") ?? 0;
                c.GroundMarkMaxOpacityPercent = trail.Value<int?>("ground_mark_max_opacity_percent") ?? 0;
                c.GroundMarkMaxLifeSeconds = trail.Value<float?>("ground_mark_max_life_seconds") ?? 0f;
            }
            var shares = root["shares"] as JObject;
            if (shares != null)
            {
                c.DamageNumbersMax = shares.Value<int?>("damage_numbers_max") ?? 0;
                c.DamageMergeWindowSeconds = shares.Value<float?>("damage_merge_window_seconds") ?? 0f;
                c.OneShotFadeSeconds = shares.Value<float?>("one_shot_fade_seconds") ?? 0f;
            }
            return c;
        }

        static int[] Three(JToken t)
        {
            var r = new int[3];
            var a = t as JArray;
            if (a == null) return r;
            for (int i = 0; i < r.Length && i < a.Count; i++) r[i] = a[i].Value<int>();
            return r;
        }

        public static int Level(int effectsLevel) => effectsLevel < 0 ? 0 : effectsLevel > 2 ? 2 : effectsLevel;
    }
}

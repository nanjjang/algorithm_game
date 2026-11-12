using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Wizard.TestKit;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // 테스트 쪽 읽기: data/presentation/particle_budget.json → ParticleBudgetTable.
    // 공유 코드에는 JSON 파서를 들이지 않으므로(ARCHITECTURE 8절) 테스트가 System.Text.Json 으로 풀어 생성자에 넣는다.
    // 초는 밀리초로 바꾸고 정수가 아니면 실패한다(십진 그대로 적은 값이 정확히 바뀌어야 한다).
    internal static class ParticleBudgetJson
    {
        public const string RepoPath = "data/presentation/particle_budget.json";

        private static readonly Lazy<ParticleBudgetTable> Cached = new Lazy<ParticleBudgetTable>(() => Load(TestRepo.Path(RepoPath)));

        public static ParticleBudgetTable Table => Cached.Value;

        public static ParticleBudgetTable Load(string path)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = doc.RootElement;

            var scenes = new List<ParticleSceneDef>();
            foreach (JsonElement s in root.GetProperty("scenes").EnumerateArray())
            {
                if (!ParticleBudgetIds.TryScene(s.GetProperty("id").GetString()!, out ParticleScene scene))
                {
                    throw new InvalidDataException("unknown scene " + s.GetProperty("id").GetString());
                }
                var pools = new List<ParticlePoolDef>();
                foreach (JsonElement p in s.GetProperty("pools").EnumerateArray())
                {
                    if (!ParticleBudgetIds.TryCategory(p.GetProperty("category").GetString()!, out ParticleCategory category))
                    {
                        throw new InvalidDataException("unknown category " + p.GetProperty("category").GetString());
                    }
                    if (!ParticleBudgetIds.TryRecurrence(p.GetProperty("kind").GetString()!, out ParticleRecurrence kind))
                    {
                        throw new InvalidDataException("unknown kind " + p.GetProperty("kind").GetString());
                    }
                    bool outside = p.TryGetProperty("outside_total", out JsonElement o) && o.GetBoolean();
                    pools.Add(new ParticlePoolDef(category, p.GetProperty("priority").GetInt32(), kind, Ints(p.GetProperty("quota")), outside));
                }
                scenes.Add(new ParticleSceneDef(scene, Ints(s.GetProperty("total")), pools));
            }

            var tiers = new List<WandTierCap>();
            foreach (JsonElement t in root.GetProperty("wand_tiers").EnumerateArray())
            {
                tiers.Add(new WandTierCap(
                    t.GetProperty("min_tier").GetInt32(), t.GetProperty("max_tier").GetInt32(),
                    t.GetProperty("idle").GetInt32(), t.GetProperty("muzzle").GetInt32(), t.GetProperty("shed_per_bullet").GetInt32(),
                    t.GetProperty("hit").GetInt32(), t.GetProperty("trail_length_px").GetInt32()));
            }

            JsonElement life = root.GetProperty("wand_lifetimes_seconds");
            var lifetimes = new WandLifetimes(Ms(life, "idle"), Ms(life, "muzzle"), Ms(life, "shed"), Ms(life, "hit"));

            JsonElement h = root.GetProperty("hit");
            var hit = new HitDecorRules(
                h.GetProperty("extra_crit").GetInt32(), h.GetProperty("extra_weak").GetInt32(), h.GetProperty("extra_resist").GetInt32(),
                h.GetProperty("minimal_particles").GetInt32(), Ms(h, "merge_window_seconds"), h.GetProperty("merge_over_hits").GetInt32(),
                h.GetProperty("merged_particles").GetInt32(), h.GetProperty("kill_halve_over_per_frame").GetInt32());

            var load = new List<BulletLoadStep>();
            foreach (JsonElement b in root.GetProperty("bullet_load").EnumerateArray())
            {
                if (!ParticleBudgetIds.TryHitDecor(b.GetProperty("hit_decor").GetString()!, out HitDecorMode mode))
                {
                    throw new InvalidDataException("unknown hit_decor " + b.GetProperty("hit_decor").GetString());
                }
                load.Add(new BulletLoadStep(b.GetProperty("min_bullets").GetInt32(), b.GetProperty("shed").GetBoolean(),
                    ExactInt(b.GetProperty("trail_length_percent").GetDecimal()), mode));
            }

            JsonElement tr = root.GetProperty("trail");
            var trail = new TrailLimits(
                tr.GetProperty("max_length_px").GetInt32(), tr.GetProperty("max_frames").GetInt32(),
                ExactInt(tr.GetProperty("max_opacity_percent").GetDecimal()), tr.GetProperty("ground_marks_per_wand").GetInt32(),
                tr.GetProperty("ground_mark_max_diameter_px").GetInt32(), ExactInt(tr.GetProperty("ground_mark_max_opacity_percent").GetDecimal()),
                Ms(tr, "ground_mark_max_life_seconds"));

            JsonElement sh = root.GetProperty("shares");
            var shares = new ParticleShareRules(
                sh.GetProperty("duel_opponent_idle_cap").GetInt32(), sh.GetProperty("plaza_self_idle_cap").GetInt32(),
                sh.GetProperty("plaza_near_count").GetInt32(), sh.GetProperty("plaza_near_idle_cap").GetInt32(),
                sh.GetProperty("damage_numbers_max").GetInt32(), Ms(sh, "damage_merge_window_seconds"), Ms(sh, "one_shot_fade_seconds"));

            return new ParticleBudgetTable(scenes, tiers, lifetimes, hit, load, trail, shares);
        }

        private static int[] Ints(JsonElement array)
        {
            var list = new List<int>();
            foreach (JsonElement e in array.EnumerateArray())
            {
                list.Add(e.GetInt32());
            }
            return list.ToArray();
        }

        private static int Ms(JsonElement obj, string name) => ExactInt(obj.GetProperty(name).GetDecimal() * ParticleBudgetMath.MsPerSecond);

        private static int ExactInt(decimal v)
        {
            if (decimal.Truncate(v) != v)
            {
                throw new InvalidDataException("not an exact integer after unit conversion: " + v);
            }
            return (int)v;
        }
    }
}

using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Waves
{
    /// <summary>
    /// 떼 출현(솔로 조정, G09.json "horde" 절): 명세 구성(WavePlanner)을 만든 뒤 잡몹 엔트리를 배율만큼 불린다.
    /// 명세 구성은 그대로 두고 그 위에 덧붙이기만 한다 — 예산·엘리트·종 상한·보스 웨이브는 바뀌지 않는다.
    /// 난수를 쓰지 않는다(누적 내림 분배): 같은 구성이면 언제나 같은 결과, 야영 미리보기와 실제 웨이브가 같다.
    /// 늘어난 엔트리는 OutsideBudget = true, Elite = false로 표시한다(명세 구성에서 엘리트가 아닌 엔트리는 OutsideBudget이 늘 false라 겹치지 않는다).
    /// </summary>
    public static class WaveHorde
    {
        /// <summary>배율의 정수 단위(천분율).</summary>
        const int Permille = 1000;

        /// <summary>잡몹인가: 역할이 horde.normal_roles에 있고, 종 상한이 없고, 졸개가 아니다.</summary>
        public static bool IsNormalSpecies(IContentDb db, WaveTuning t, EnemyDef def, IList<string> whims)
        {
            if (db == null || t == null || def == null || def.Minion || def.Cost <= 0) return false;
            if (def.Role == null || !t.HordeNormalRoles.Contains(def.Role)) return false;
            return WavePlanner.SpeciesCap(db, def.Id, whims) <= 0;
        }

        /// <summary>떼 출현이 늘린 엔트리인가.</summary>
        public static bool IsExtra(in WaveEntry entry) => entry.OutsideBudget && !entry.Elite;

        /// <summary>
        /// 구성을 제자리에서 불린다. 보스 웨이브·꺼짐이면 그대로. 잡몹 엔트리의 배율 m = 웨이브 곡선 값(역할 상한 적용).
        /// 같은 m을 쓰는 잡몹끼리 구성 순서로 센 i번째의 사본 수 = floor((i+1)·m) − floor(i·m) → 그 무리 N마리면 합계 floor(N·m)마리.
        /// 사본은 원본 바로 뒤, 같은 묶음. 미리보기 종 목록은 바뀌지 않는다(같은 종만 는다). 늘린 수를 돌려준다.
        /// </summary>
        public static int Expand(IContentDb db, WaveTuning t, WavePlan plan)
        {
            if (db == null || t == null || plan == null || plan.BossId != null || !t.HordeMultiplies) return 0;
            var src = new List<WaveEntry>(plan.Entries);
            plan.Entries.Clear();
            // 배율(천분율)마다 따로 센 색인 — 같은 배율의 잡몹끼리 누적 내림 분배
            var index = new Dictionary<int, long>();
            int added = 0;
            for (int i = 0; i < src.Count; i++)
            {
                var e = src[i];
                plan.Entries.Add(e);
                if (e.Elite || e.OutsideBudget) continue;
                var def = db.Enemy(e.Species);
                if (!IsNormalSpecies(db, t, def, plan.Whims)) continue;
                int mul = (int)Math.Round(t.HordeMultiplier(plan.Wave, def.Role) * Permille, MidpointRounding.AwayFromZero);
                if (mul <= Permille) continue;
                long k0;
                index.TryGetValue(mul, out k0);
                long copies = (k0 + 1) * mul / Permille - k0 * mul / Permille;
                index[mul] = k0 + 1;
                for (long k = 1; k < copies; k++)
                {
                    plan.Entries.Add(new WaveEntry { Species = e.Species, Elite = false, OutsideBudget = true, Bundle = e.Bundle });
                    added++;
                }
            }
            return added;
        }

        /// <summary>그 구성에서 종 하나의 명세 잡몹 수(원본)와 떼 출현이 늘린 수. 보상 나누기(split)와 빠른 처리 목표가 쓴다.</summary>
        public static void CountSpecies(WavePlan plan, string species, out int originals, out int extras)
        {
            originals = extras = 0;
            if (plan == null) return;
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var e = plan.Entries[i];
                if (e.Species != species || e.Elite) continue;
                if (e.OutsideBudget) extras++; else originals++;
            }
        }

        /// <summary>동시 생존 상한(명세 max_alive × 배율, 반올림. 명세 값 아래로는 내려가지 않는다).</summary>
        public static int MaxAlive(WaveTuning t, int specMaxAlive)
        {
            if (t == null || !t.HordeEnabled || t.HordeMaxAliveMultiplier <= 1f) return specMaxAlive;
            return Math.Max(specMaxAlive, (int)Math.Round(specMaxAlive * (double)t.HordeMaxAliveMultiplier, MidpointRounding.AwayFromZero));
        }
    }
}

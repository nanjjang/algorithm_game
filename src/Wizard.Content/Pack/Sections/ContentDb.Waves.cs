using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 7 waves(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/WavesConverter.cs. 원본: data/sim/waves.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻은 WaveCurveDef):
    //   i32 day_length
    //   u16 n, (i32 from, i32 to, u8 tod) × n                    tod_ranges. from 오름차순, 겹침 없음
    //   i32 hp_linear_until  i64 hp_linear_step_micro  i64 hp_exp_base_micro  i32 hp_exp_rate_pct
    //   i32 elite_hp_mul  i32 boss_hp_ref_wave
    //   i32 budget_linear_base  i32 budget_linear_per_wave  i32 budget_linear_until
    //   i32 budget_late_base  i32 budget_late_per_wave  i32 budget_cap
    //   i32 bundle_base  i32 bundle_grow_after  i32 bundle_grow_every  i32 bundle_max
    //   i32 bundle_interval_ticks  i32 bundle_early_release_alive  i32 max_alive
    //   구간 표 telegraph_cap: u16 n, (i32 from, i32 to, i32 value) × n    to = int.MaxValue 는 끝 없음. from 오름차순, 겹침 없음
    //   구간 표 elite_count
    //   i32 elite_cost_mul
    //   u16 n, (u16 species, i32 cap) × n                        species_cap. species 오름차순, 겹침 없음
    //   i32 stall_ticks  i32 stall_kill_score_pct
    //   구간 표 modifier_count
    //   i32 contact  i32 telegraph_plus  i32 elite_boss_plus_from  i32 all_plus_from  i32 attack_cap
    //   u16 fill_species
    //   u16 n, (i32 wave, 틀 하나) × n                            script. wave 오름차순, 겹침 없음. 틀 하나의 꼴은 ContentDb.Frames.cs
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 0/1 이 아닌 bool)는 ContentException.
    public sealed partial class ContentDb
    {
        private WaveCurveDef? _waveCurve;

        partial void LoadWaves(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            var d = new WaveCurveDef();
            d.DayLength = WavesI32(ref r);
            ushort tods = WavesU16(ref r);
            d.TodRangeFrom = new int[tods];
            d.TodRangeTo = new int[tods];
            d.TodRangeTod = new Wizard.Content.TimeOfDay[tods];
            int lastTo = 0;
            for (int i = 0; i < tods; i++)
            {
                int from = WavesI32(ref r);
                int to = WavesI32(ref r);
                if (from <= lastTo || to < from)
                {
                    throw WavesBroken("tod ranges not ascending or overlapping");
                }
                lastTo = to;
                if (!r.TryU8(out byte tod))
                {
                    throw WavesBroken("truncated entry");
                }
                if (tod == (byte)Wizard.Content.TimeOfDay.None || !Enum.IsDefined(typeof(Wizard.Content.TimeOfDay), tod))
                {
                    throw WavesBroken("unknown time of day");
                }
                d.TodRangeFrom[i] = from;
                d.TodRangeTo[i] = to;
                d.TodRangeTod[i] = (Wizard.Content.TimeOfDay)tod;
            }
            d.HpLinearUntil = WavesI32(ref r);
            d.HpLinearStepMicro = WavesI64(ref r);
            d.HpExpBaseMicro = WavesI64(ref r);
            d.HpExpRatePct = WavesI32(ref r);
            d.EliteHpMul = WavesI32(ref r);
            d.BossHpRefWave = WavesI32(ref r);
            d.BudgetLinearBase = WavesI32(ref r);
            d.BudgetLinearPerWave = WavesI32(ref r);
            d.BudgetLinearUntil = WavesI32(ref r);
            d.BudgetLateBase = WavesI32(ref r);
            d.BudgetLatePerWave = WavesI32(ref r);
            d.BudgetCap = WavesI32(ref r);
            d.BundleBase = WavesI32(ref r);
            d.BundleGrowAfter = WavesI32(ref r);
            d.BundleGrowEvery = WavesI32(ref r);
            d.BundleMax = WavesI32(ref r);
            d.BundleIntervalTicks = WavesI32(ref r);
            d.BundleEarlyReleaseAlive = WavesI32(ref r);
            d.MaxAlive = WavesI32(ref r);
            WavesSteps(ref r, out d.TelegraphCapFrom, out d.TelegraphCapTo, out d.TelegraphCap);
            WavesSteps(ref r, out d.EliteCountFrom, out d.EliteCountTo, out d.EliteCount);
            d.EliteCostMul = WavesI32(ref r);
            ushort caps = WavesU16(ref r);
            d.SpeciesCapSpecies = new EnemyTypeId[caps];
            d.SpeciesCap = new int[caps];
            int previous = 0;
            for (int i = 0; i < caps; i++)
            {
                d.SpeciesCapSpecies[i] = FramesSpecies(ref r, "waves", ref previous);
                d.SpeciesCap[i] = WavesI32(ref r);
            }
            d.StallTicks = WavesI32(ref r);
            d.StallKillScorePct = WavesI32(ref r);
            WavesSteps(ref r, out d.ModifierCountFrom, out d.ModifierCountTo, out d.ModifierCount);
            d.ContactDamage = WavesI32(ref r);
            d.TelegraphDamagePlus = WavesI32(ref r);
            d.EliteBossPlusFromWave = WavesI32(ref r);
            d.AllPlusFromWave = WavesI32(ref r);
            d.AttackDamageCap = WavesI32(ref r);
            previous = 0;
            d.FillSpecies = FramesSpecies(ref r, "waves", ref previous);
            ushort script = WavesU16(ref r);
            d.ScriptWaves = new int[script];
            d.ScriptPlans = new FrameDef[script];
            int lastWave = 0;
            for (int i = 0; i < script; i++)
            {
                int wave = WavesI32(ref r);
                if (wave <= lastWave)
                {
                    throw WavesBroken("script waves not in ascending order");
                }
                lastWave = wave;
                d.ScriptWaves[i] = wave;
                d.ScriptPlans[i] = FramePlan(ref r, "waves");
            }
            if (r.Remaining != 0)
            {
                throw WavesBroken("trailing bytes");
            }
            _waveCurve = d;
        }

        partial void FindWaveCurve(ref WaveCurveDef? def)
        {
            if (_waveCurve != null)
            {
                def = _waveCurve;
            }
        }

        // 구간 표 하나. from 오름차순, from ≤ to, 앞 줄의 to 보다 뒤에서 시작(겹침 없음)
        private static void WavesSteps(ref ByteReader r, out int[] from, out int[] to, out int[] value)
        {
            ushort n = WavesU16(ref r);
            from = new int[n];
            to = new int[n];
            value = new int[n];
            int lastTo = 0;
            for (int i = 0; i < n; i++)
            {
                from[i] = WavesI32(ref r);
                to[i] = WavesI32(ref r);
                value[i] = WavesI32(ref r);
                if (from[i] <= lastTo || to[i] < from[i])
                {
                    throw WavesBroken("wave ranges not ascending or overlapping");
                }
                lastTo = to[i];
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static ushort WavesU16(ref ByteReader r)
        {
            if (!r.TryU16(out ushort v))
            {
                throw WavesBroken("truncated entry");
            }
            return v;
        }

        private static int WavesI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw WavesBroken("truncated entry");
            }
            return v;
        }

        private static long WavesI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw WavesBroken("truncated entry");
            }
            return v;
        }

        private static ContentException WavesBroken(string what) => new ContentException("content section 'waves': " + what);
    }
}

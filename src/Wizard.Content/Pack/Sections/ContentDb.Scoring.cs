using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 11 scoring(CONTRACTS 11.2)을 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ScoringConverter.cs. 원본: data/sim/scoring.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻은 ScoringDef):
    //   i32 wave_coef_tenths_at_wave_1  i32 wave_coef_tenths_per_wave
    //   i32 kill_per_cost  i32 boss_kill  i32 wave_clear
    //   i32 fast_per_second  i32 fast_target_ticks  i32 fast_target_ticks_boss
    //   i32 no_hit  i32 graze_per  i32 graze_cap  i32 graze_cap_boss
    //   i32 phantom_first
    // 깨진 바이트(모자람, 남음)는 ContentException.
    public sealed partial class ContentDb
    {
        private ScoringDef? _scoring;

        partial void LoadScoring(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            var d = new ScoringDef();
            d.WaveCoefTenthsAtWave1 = ScoringI32(ref r);
            d.WaveCoefTenthsPerWave = ScoringI32(ref r);
            d.KillPerCost = ScoringI32(ref r);
            d.BossKill = ScoringI32(ref r);
            d.WaveClear = ScoringI32(ref r);
            d.FastPerSecond = ScoringI32(ref r);
            d.FastTargetTicks = ScoringI32(ref r);
            d.FastTargetTicksBoss = ScoringI32(ref r);
            d.NoHit = ScoringI32(ref r);
            d.GrazePer = ScoringI32(ref r);
            d.GrazeCap = ScoringI32(ref r);
            d.GrazeCapBoss = ScoringI32(ref r);
            d.PhantomFirst = ScoringI32(ref r);
            if (r.Remaining != 0)
            {
                throw ScoringBroken("trailing bytes");
            }
            _scoring = d;
        }

        partial void FindScoring(ref ScoringDef? def)
        {
            if (_scoring != null)
            {
                def = _scoring;
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static int ScoringI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw ScoringBroken("truncated entry");
            }
            return v;
        }

        private static ContentException ScoringBroken(string what) => new ContentException("content section 'scoring': " + what);
    }
}

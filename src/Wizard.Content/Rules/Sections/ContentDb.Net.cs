using System;
using Wizard.Content.Rules;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 34 net(CONTRACTS 11.2, 해시 밖)을 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/NetConverter.cs. 원본: data/rules/net.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 표 줄 수 N                                      1 이상
    //   (i32 rtt_up_to_ms, i32 delay_ticks) × N             경계 엄격히 오름차순, D 비감소, 둘 다 1 이상
    //   i32 rollback_limit_ticks
    //   i32 disconnect_silence_ticks
    //   i32 disconnect_grace_ticks
    //   i32 full_state_gap_ticks
    //   i32 catchup_ticks_per_advance
    //   i32 rtt_hold_cap_ms
    //   i32 queue_rtt_limit_ms
    //   i32 order_violation_forfeit
    //   i32 spectator_delay_ticks
    //   i32 spectator_keyframe_ticks
    //   i32 clock_sync_samples
    //   i32 clock_resync_ticks
    //   i32 clock_slew_max_ppm
    //   i32 hash_mismatch_flag_over                         위 열네 개는 모두 1 이상(마지막 값은 T-62)
    // 깨진 바이트(모자람, 남음, 표 순서, 1 미만)는 ContentException.
    public sealed partial class ContentDb
    {
        private NetRuleSet? _net;

        partial void LoadNet(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw NetBroken("truncated entry");
            }
            if (count == 0)
            {
                throw NetBroken("empty input delay table");
            }
            var table = new InputDelayStep[count];
            for (int i = 0; i < count; i++)
            {
                int rtt = NetI32(ref r);
                int delay = NetI32(ref r);
                if (rtt < 1 || delay < 1)
                {
                    throw NetBroken("non-positive input delay step");
                }
                if (i > 0 && (rtt <= table[i - 1].RttUpToMs || delay < table[i - 1].DelayTicks))
                {
                    throw NetBroken("input delay table not ascending");
                }
                table[i] = new InputDelayStep(rtt, delay);
            }
            var d = new NetRuleSet();
            d.InputDelayTable = table;
            d.RollbackLimitTicks = NetPositive(ref r);
            d.DisconnectSilenceTicks = NetPositive(ref r);
            d.DisconnectGraceTicks = NetPositive(ref r);
            d.FullStateGapTicks = NetPositive(ref r);
            d.CatchupTicksPerAdvance = NetPositive(ref r);
            d.RttHoldCapMs = NetPositive(ref r);
            d.QueueRttLimitMs = NetPositive(ref r);
            d.OrderViolationForfeit = NetPositive(ref r);
            d.SpectatorDelayTicks = NetPositive(ref r);
            d.SpectatorKeyframeTicks = NetPositive(ref r);
            d.ClockSyncSamples = NetPositive(ref r);
            d.ClockResyncTicks = NetPositive(ref r);
            d.ClockSlewMaxPpm = NetPositive(ref r);
            d.HashMismatchFlagOver = NetPositive(ref r);
            if (r.Remaining != 0)
            {
                throw NetBroken("trailing bytes");
            }
            _net = d;
        }

        partial void FindNet(ref NetRuleSet? def)
        {
            def = _net;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static int NetI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw NetBroken("truncated entry");
            }
            return v;
        }

        private static int NetPositive(ref ByteReader r)
        {
            int v = NetI32(ref r);
            if (v < 1)
            {
                throw NetBroken("non-positive value");
            }
            return v;
        }

        private static ContentException NetBroken(string what) => new ContentException("content section 'net': " + what);
    }
}

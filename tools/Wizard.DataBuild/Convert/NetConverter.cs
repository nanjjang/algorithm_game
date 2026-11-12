using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다(ArenaConverter 와 같은 이유)
namespace Wizard.DataBuild
{
    // data/rules/net.json → 팩 절 34 net(해시 밖). 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Rules/Sections/ContentDb.Net.cs.
    // 스키마: data/schema/net.schema.json. 근거: spec 10 14.5·14.7·14.8·14.9·3.7, online 4.12, CONTRACTS 2.2 NetRuleSet.
    // 단위: 초 → 틱(× 60, 가장 가까운 정수, 같으면 짝수). 나머지는 원본이 이미 틱·ms·개수라 정수 그대로.
    // 교차 검사: 입력 지연 표는 경계가 엄격히 오름차순이고 D 가 줄지 않는다(왕복이 클수록 D 가 작아질 이유가 없다).
    //
    // 절 바이트(LE, 모든 수는 정수):
    //   u16 표 줄 수 N
    //   (i32 rtt_up_to_ms, i32 delay_ticks) × N          경계 오름차순
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
    public sealed class NetConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Net;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            JsonElement root = file.Root;
            int before = diagnostics.Count;

            var rttBounds = new List<int>();
            var delays = new List<int>();
            int row = 0;
            foreach (JsonElement step in root.GetProperty("input_delay_table").EnumerateArray())
            {
                string at = "/input_delay_table/" + row.ToString(CultureInfo.InvariantCulture);
                int rtt = Int(step.GetProperty("rtt_up_to_ms"), at + "/rtt_up_to_ms", file, diagnostics);
                int delay = Int(step.GetProperty("delay_ticks"), at + "/delay_ticks", file, diagnostics);
                if (rttBounds.Count > 0)
                {
                    if (rtt <= rttBounds[rttBounds.Count - 1])
                    {
                        diagnostics.Error(file.RepoPath, at + "/rtt_up_to_ms", "경계가 앞 줄보다 크지 않다(표는 경계 오름차순, 겹침 없음)");
                    }
                    if (delay < delays[delays.Count - 1])
                    {
                        diagnostics.Error(file.RepoPath, at + "/delay_ticks", "D 가 앞 줄보다 작다(왕복이 클수록 D 는 줄지 않는다)");
                    }
                }
                rttBounds.Add(rtt);
                delays.Add(delay);
                row++;
            }

            int rollback = Int(root.GetProperty("rollback_limit_ticks"), "/rollback_limit_ticks", file, diagnostics);
            int silence = Units.SecondsToTicks(root.GetProperty("disconnect_silence_seconds").GetDecimal());
            int grace = Units.SecondsToTicks(root.GetProperty("disconnect_grace_seconds").GetDecimal());
            int fullStateGap = Int(root.GetProperty("full_state_gap_ticks"), "/full_state_gap_ticks", file, diagnostics);
            int catchup = Int(root.GetProperty("catchup_ticks_per_advance"), "/catchup_ticks_per_advance", file, diagnostics);
            int rttHold = Int(root.GetProperty("rtt_hold_cap_ms"), "/rtt_hold_cap_ms", file, diagnostics);
            int queueRtt = Int(root.GetProperty("queue_rtt_limit_ms"), "/queue_rtt_limit_ms", file, diagnostics);
            int forfeit = Int(root.GetProperty("order_violation_forfeit"), "/order_violation_forfeit", file, diagnostics);
            int spectatorDelay = Units.SecondsToTicks(root.GetProperty("spectator_delay_seconds").GetDecimal());
            int spectatorKeyframe = Int(root.GetProperty("spectator_keyframe_ticks"), "/spectator_keyframe_ticks", file, diagnostics);

            // 초를 틱으로 바꾼 값이 0 이 되면(0.001초) 끊김·유예·관전 지연이 사라진다
            RequirePositive(silence, "/disconnect_silence_seconds", file, diagnostics);
            RequirePositive(grace, "/disconnect_grace_seconds", file, diagnostics);
            RequirePositive(spectatorDelay, "/spectator_delay_seconds", file, diagnostics);

            if (diagnostics.Count != before)
            {
                return;
            }

            output.U16((ushort)rttBounds.Count);      // 스키마가 줄 수를 64 이하로 막는다
            for (int i = 0; i < rttBounds.Count; i++)
            {
                output.I32(rttBounds[i]);
                output.I32(delays[i]);
            }
            output.I32(rollback);
            output.I32(silence);
            output.I32(grace);
            output.I32(fullStateGap);
            output.I32(catchup);
            output.I32(rttHold);
            output.I32(queueRtt);
            output.I32(forfeit);
            output.I32(spectatorDelay);
            output.I32(spectatorKeyframe);
        }

        // 스키마의 "integer" 는 6.0 도 받으므로 decimal 로 읽어 정수인지 본다(다른 변환기의 Int 도우미와 같다)
        private static int Int(JsonElement e, string at, DataFile file, Diagnostics diagnostics)
        {
            decimal v = e.GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                diagnostics.Error(file.RepoPath, at, "정수가 아니거나 i32 범위 밖이다");
                return 0;
            }
            return (int)v;
        }

        private static void RequirePositive(int ticks, string at, DataFile file, Diagnostics diagnostics)
        {
            if (ticks < 1)
            {
                diagnostics.Error(file.RepoPath, at, "틱으로 바꾸면 0 이다(1틱 = 1/60초 이상이어야 한다)");
            }
        }
    }
}

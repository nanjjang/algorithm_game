using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.NetSim;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Netcode.Tests.Matrix
{
    // M2-NET-06 수락 조건: 왕복 60/90/120ms × 지터 10/20/30ms × 유실 0/1/3% 전 조합(27)에서
    //   두 클라이언트의 확정 해시 열 = 호스트(WZDR 을 다시 돌린 틱마다의 해시), 되감기 깊이 ≤ 8, 해시 불일치·전체 상태 요청 0, 몰수 계수 0,
    //   Retracted 뒤 같은 열쇠의 Confirmed 없음, 받아들여진 이벤트의 시각은 누른 시각 그대로.
    // 두 사람이 같은 조건이고, 키는 틱마다 1/4 의 확률로 1~2개를 틱 안 균등한 위치에 누르거나 뗀다. 600틱을 누르고 60틱을 쉰 뒤 판정한다
    public sealed class NetcodeMatrixTests
    {
        private const uint PlayTicks = 600;
        private const uint QuietTicks = 60;
        private const int KeyRate = 4;

        private readonly ITestOutputHelper _out;

        public NetcodeMatrixTests(ITestOutputHelper output)
        {
            _out = output;
        }

        public static IEnumerable<object[]> Combos()
        {
            foreach (int rtt in new[] { 60, 90, 120 })
            {
                foreach (int jitter in new[] { 10, 20, 30 })
                {
                    foreach (int lossPpm in new[] { 0, 10000, 30000 })
                    {
                        yield return new object[] { rtt, jitter, lossPpm };
                    }
                }
            }
        }

        private static NetSimMatch Play(int rttMs, int jitterMs, int lossPpm)
        {
            ulong seed = 0x6D61_7472UL ^ ((ulong)rttMs << 32) ^ ((ulong)jitterMs << 16) ^ (ulong)lossPpm;
            NetSimMatch m = NetSimMatch.Symmetric(seed, rttMs, jitterMs, lossPpm);
            m.Keys = (slot, n, rng) => n < PlayTicks ? NetSimMatch.RandomKeys(rng, KeyRate) : null;
            m.Run(PlayTicks + QuietTicks);
            return m;
        }

        [Theory]
        [MemberData(nameof(Combos))]
        public void ConfirmedHashesMatchHostInEveryCombination(int rttMs, int jitterMs, int lossPpm)
        {
            NetSimMatch m = Play(rttMs, jitterMs, lossPpm);

            MatrixChecks.All(m, (int)PlayTicks);
            for (int s = 0; s < 2; s++)
            {
                DuelNetStats st = m.Host.StatsFor(new PlayerSlot((byte)s));
                int stalls = m.Trace[s].Advances.Count(a => a.advance.Stalled);
                _out.WriteLine($"rtt {rttMs} jitter {jitterMs} loss {lossPpm / 10000}% slot {s}: D {m.Trace[s].Delay}, pressed {m.Trace[s].Pressed.Count}, " +
                               $"dropped presses {MatrixChecks.DroppedPresses(m, s)}, late {st.EventsLate}, lost seqs {st.SeqsLost}, early {st.EventsEarly}, " +
                               $"max rollback {MatrixChecks.MaxRollback(m, s)}, stalled advances {stalls}, up lost {m.Up[s].Lost}/{m.Up[s].Sent}");
            }
        }

        // 수락 조건 "Retracted 가 난 이벤트는 그 뒤 같은 틱에서 Confirmed 로 나오지 않는다"(열쇠 (Tick, Type, Slot, Ordinal), CONTRACTS 6.4)
        [Theory]
        [MemberData(nameof(Combos))]
        public void RetractedEventIsNeverConfirmedLaterInEveryCombination(int rttMs, int jitterMs, int lossPpm)
        {
            MatrixChecks.RetractedNeverConfirmedLater(Play(rttMs, jitterMs, lossPpm));
        }
    }
}

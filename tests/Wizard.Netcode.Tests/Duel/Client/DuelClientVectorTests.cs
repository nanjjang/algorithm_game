using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // spec 10 20.14의 TV-N21~TV-N22(M2-NET-05의 벡터)와 TV-N23(전체 상태를 청하고 덮어쓴다, 4번째 불일치에 표시. CCR-1, T-62).
    // 기대값은 명세에서 가져온다. 표시의 한도(spec 10 2.8 hash_mismatch_flag "3번 넘게")는 팩에서 읽는다(코드와 시험에 3이 없다).
    public sealed class DuelClientVectorTests
    {

        // TV-N21: 상대 예측 = 직전 키 유지. 확정에서 상대가 틱 T에 J 누름 → T로 되감아 다시 돌림
        [Fact]
        public void S10_TV_N21__OpponentFireInConfirmRollsBackToThatTick()
        {
            const uint t = 3;
            var rig = new ClientRig(me: 0);
            for (uint n = 0; n <= 5; n++)
            {
                rig.Client.Advance(n);
            }
            Assert.Equal(6u, rig.Client.View.Tick);
            Assert.Equal(KeyMask.None, rig.Client.View.Player(new PlayerSlot(1)).Keys);   // 예측: 상대는 새 이벤트 없음

            var confirmed = new List<ConfirmedTick>();
            for (uint k = 0; k < t; k++)
            {
                confirmed.Add(rig.StepServer());
            }
            confirmed.Add(rig.StepServer(slot1: new[] { ClientRig.Ev(t, LogicalKey.Fire, true, 1) }));
            rig.Deliver(rig.ServerTick(confirmed));

            DuelAdvance a = rig.Client.Advance(6);
            Assert.False(a.Stalled);
            Assert.Equal(t + 1, a.ConfirmedTick);
            // 확정 세계가 T 를 실제 입력으로 지났고, 예측 세계는 그 스냅숏에서 T+1..5 를 다시 돌린 뒤 6 으로 나아갔다
            Assert.Equal(2, a.RollbackDepth);
            Assert.Equal(1, a.TicksSimulated);
            Assert.Equal(7u, a.PredictedTick);
            Assert.Equal(confirmed[(int)t].Hash, rig.Client.ConfirmedHash(t));

            // 다시 돌린 예측 = T 의 J 누름을 넣고 그 뒤로 "누른 채 유지"로 돌린 세계
            SimWorld reference = ClientRig.NewWorld();
            for (uint k = 0; k <= 6; k++)
            {
                PlayerInput[] inputs = k == t
                    ? new[] { new PlayerInput(new PlayerSlot(1), ClientRig.Ev(t, LogicalKey.Fire, true, 1)) }
                    : System.Array.Empty<PlayerInput>();
                reference.Step(inputs, System.ReadOnlySpan<RunCommand>.Empty, NullSink.Instance);
            }
            Assert.Equal(reference.View.Tick, rig.Client.View.Tick);
            Assert.Equal(reference.View.Player(new PlayerSlot(1)).Keys, rig.Client.View.Player(new PlayerSlot(1)).Keys);
            Assert.NotEqual(KeyMask.None, rig.Client.View.Player(new PlayerSlot(1)).Keys & (KeyMask)(1 << (int)LogicalKey.Fire));
        }

        // TV-N21의 반대편: 확정이 예측과 같으면 되감지 않는다
        [Fact]
        public void S10_TV_N21__ConfirmEqualToPredictionDoesNotRollBack()
        {
            var rig = new ClientRig(me: 0);
            for (uint n = 0; n <= 5; n++)
            {
                rig.Client.Advance(n);
            }
            rig.ConfirmEmptyThrough(3);
            DuelAdvance a = rig.Client.Advance(6);
            Assert.Equal(0, a.RollbackDepth);
            Assert.Equal(1, a.TicksSimulated);
            Assert.Equal(4u, a.ConfirmedTick);
        }

        // TV-N22: 지금 틱 − 마지막 확정 틱 = 9 → 시뮬레이션 멈추고 기다림(전체 상태는 청하지 않는다)
        [Fact]
        public void S10_TV_N22__StallsWhenNowMinusLastConfirmedIsNine()
        {
            NetRules net = ClientRig.Net;
            Assert.Equal(8, net.RollbackLimit);   // spec 10 2.8 rollback_max 8틱. TV-N22의 "9"는 이 값 + 1이다
            var rig = new ClientRig(me: 0);
            for (uint n = 0; n <= 9; n++)
            {
                rig.Client.Advance(n);
            }
            rig.ConfirmEmptyThrough(9);
            const uint lastConfirmed = 9;

            for (uint n = lastConfirmed + 1; n <= lastConfirmed + 8; n++)
            {
                DuelAdvance ok = rig.Client.Advance(n);
                Assert.False(ok.Stalled);
                Assert.Equal(1, ok.TicksSimulated);
            }

            DuelAdvance a = rig.Client.Advance(lastConfirmed + 9);
            Assert.True(a.Stalled);
            Assert.Equal(0, a.TicksSimulated);
            Assert.Equal(lastConfirmed + 9, a.PredictedTick);       // 틱 18 을 처리하지 않았다
            Assert.Equal(lastConfirmed + 1, a.ConfirmedTick);
            Assert.False(rig.Client.NeedsFullState);
        }

        // TV-N23: 해시 불일치 → 상태 전체 요청·덮어쓰기. 4번째 불일치에 표시.
        // spec 10 14.7 "한 경기에 3번 넘게 해시가 어긋난 클라이언트는 기록해 표시한다". 한도는 저장소 팩의 NetRuleSet 에서 읽는다
        [Fact]
        public void S10_TV_N23__HashMismatchRequestsFullStateOverwritesAndFlagsOnFourth()
        {
            int flagOver = ClientRig.Db.Net.HashMismatchFlagOver;
            Assert.Equal(3, flagOver);      // spec 10 2.8 hash_mismatch_flag "한 경기 3번 넘게, 4회째 표시"
            var rig = new ClientRig(me: 0);
            uint n = 0;
            Assert.False(rig.Client.HashMismatchFlagged);
            for (int round = 1; round <= flagOver + 1; round++)
            {
                MismatchAndRecover(rig, ref n, round);
                // 1~3번째는 표시하지 않고, 4번째에 선다. 덮어쓴 뒤 해시가 맞아도 표시는 남는다(한 경기의 셈)
                Assert.Equal(round > 3, rig.Client.HashMismatchFlagged);
            }
            Assert.Equal(4, rig.Client.HashMismatchCount);
            Assert.True(rig.Client.HashMismatchFlagged);
        }

        // 한도가 다른 규칙(net.json 의 hash_mismatch_flag_over 만 바꾼 팩)에서도 그 값 + 1번째에 선다: 코어에 3이 박혀 있지 않다
        [Fact]
        public void S10_TV_N23__FlagFollowsTheLimitInThePack()
        {
            const int otherLimit = 5;
            ContentDb db = ClientRig.DbWithNet("\"hash_mismatch_flag_over\": 3", "\"hash_mismatch_flag_over\": " + otherLimit);
            Assert.Equal(otherLimit, db.Net.HashMismatchFlagOver);
            var rig = new ClientRig(me: 0, db: db);
            uint n = 0;
            for (int round = 1; round <= otherLimit + 1; round++)
            {
                MismatchAndRecover(rig, ref n, round);
                Assert.Equal(round == otherLimit + 1, rig.Client.HashMismatchFlagged);
            }
        }

        // 확정 공백으로 청한 전체 상태는 불일치로 세지 않는다: 몇 번을 청해도 표시가 서지 않는다
        [Fact]
        public void S10_TV_N23__GapFullStateRequestsAreNotCounted()
        {
            var rig = new ClientRig(me: 0);
            int gap = ClientRig.Net.GapLimit;
            int flagOver = ClientRig.Db.Net.HashMismatchFlagOver;
            uint n = 0;
            for (int round = 1; round <= flagOver + 1; round++)
            {
                // 서버 세계를 공백 한도 + 1틱 앞서 돌리고, 그 다음 틱부터만 보낸다(확정의 첫 틱이 내 확정 틱보다 한도 넘게 앞선다)
                for (int i = 0; i <= gap; i++)
                {
                    rig.Client.Advance(n++);
                    rig.StepServer();
                }
                rig.Client.Advance(n);
                ConfirmedTick ahead = rig.StepServer();
                rig.Deliver(rig.ServerTick(new[] { ahead }));
                Assert.True(rig.Client.NeedsFullState);
                Assert.Equal(0, rig.Client.HashMismatchCount);
                Assert.False(rig.Client.HashMismatchFlagged);

                var w = new ByteWriter();
                rig.Server.WriteState(w);
                rig.Client.OnFullState(rig.Server.Tick, w.Written);
                Assert.False(rig.Client.NeedsFullState);
            }
            Assert.Equal(0, rig.Client.HashMismatchCount);
            Assert.False(rig.Client.HashMismatchFlagged);
        }

        // 한 번의 불일치: 30틱을 확정하고 마지막 틱의 해시를 틀리게 보낸다(서버 세계와 내 확정 세계가 갈라진 것과 같다) →
        // 전체 상태를 청하고, 청하는 동안 확정 세계는 멈추고, 받은 상태로 덮어써 서버와 같은 해시로 이어 간다
        private static void MismatchAndRecover(ClientRig rig, ref uint n, int expectedCount)
        {
            var ticks = new List<ConfirmedTick>();
            for (int i = 0; i < 30; i++)
            {
                rig.Client.Advance(n++);
                ticks.Add(rig.StepServer());
            }
            ConfirmedTick last = ticks[ticks.Count - 1];
            rig.Deliver(rig.ServerTick(ticks.GetRange(0, ticks.Count - 1)));
            rig.Deliver(rig.ServerTick(new[] { last }, hashOf: last, hashValue: last.Hash ^ 1UL));

            Assert.True(rig.Client.NeedsFullState);
            Assert.Equal(expectedCount, rig.Client.HashMismatchCount);

            // 청하는 동안 확정 세계는 멈춘다: 뒤의 확정은 받지 않는다
            uint confirmedBefore = rig.Client.Advance(n).ConfirmedTick;
            ConfirmedTick more = rig.StepServer();
            rig.Deliver(rig.ServerTick(new[] { more }));
            Assert.Equal(confirmedBefore, rig.Client.Advance(n).ConfirmedTick);

            // 덮어쓰기: 서버 상태 전체 → 호스트와 같은 해시로 이어 간다
            var w = new ByteWriter();
            rig.Server.WriteState(w);
            rig.Client.OnFullState(rig.Server.Tick, w.Written);
            Assert.False(rig.Client.NeedsFullState);
            Assert.Equal(rig.Server.ComputeStateHash(), rig.Client.ConfirmedHash(rig.Server.Tick - 1));

            var after = new List<ConfirmedTick>();
            for (int i = 0; i < 5; i++)
            {
                rig.Client.Advance(n++);
                after.Add(rig.StepServer());
            }
            rig.Deliver(rig.ServerTick(after, hashOf: after[after.Count - 1]));
            Assert.False(rig.Client.NeedsFullState);
            Assert.Equal(expectedCount, rig.Client.HashMismatchCount);
            Assert.Equal(after[after.Count - 1].Hash, rig.Client.ConfirmedHash(after[after.Count - 1].Tick));
        }
    }
}

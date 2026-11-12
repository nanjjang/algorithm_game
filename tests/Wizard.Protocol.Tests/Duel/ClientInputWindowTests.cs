using System;
using System.Collections.Generic;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Protocol.Tests.Duel
{
    // CONTRACTS 10.2: 이벤트 창 = 유효 틱이 sealed_through_tick − 7 이상(아직 봉인하지 않은 다음 틱의 것도 싣는다). base_seq 의 뜻.
    public sealed class ClientInputWindowTests
    {
        [Fact]
        public void WindowStartsSevenTicksBeforeSealed()
        {
            Assert.Equal(93U, ClientInputWindow.FirstTick(100));
            Assert.True(ClientInputWindow.Contains(100, 93));
            Assert.False(ClientInputWindow.Contains(100, 92));
            Assert.True(ClientInputWindow.Contains(100, 100));
            Assert.True(ClientInputWindow.Contains(100, 101));    // 다음 틱(n + D), 아직 봉인 전
            Assert.Equal(0U, ClientInputWindow.FirstTick(0));
            Assert.Equal(0U, ClientInputWindow.FirstTick(6));
            Assert.Equal(0U, ClientInputWindow.FirstTick(7));
            Assert.Equal(1U, ClientInputWindow.FirstTick(8));
            Assert.Equal(uint.MaxValue - 7U, ClientInputWindow.FirstTick(uint.MaxValue));
        }

        [Fact]
        public void FillCarriesWindowEventsIncludingTheNextUnsealedTick()
        {
            // 입력 시계 n = 98, D = 3 → sealed_through_tick = n + D − 1 = 100, 새 키의 유효 틱 = 101
            var history = new List<InputEvent>
            {
                new InputEvent(91, 0, LogicalKey.Up, true, 1),
                new InputEvent(92, 10, LogicalKey.Up, false, 2),     // 창 밖(92 < 93)
                new InputEvent(93, 0, LogicalKey.Fire, true, 3),     // 창의 첫 틱
                new InputEvent(97, 5, LogicalKey.Fire, false, 4),
                new InputEvent(101, 200, LogicalKey.Skill, true, 5), // 다음 틱
            };
            var p = new ClientInputPacket { SealedThroughTick = 100 };
            ClientInputWindow.Fill(p, history.ToArray(), 5);
            Assert.Equal(3, p.EventCount);
            Assert.Equal(3U, p.Events[0].Seq);
            Assert.Equal(101U, p.Events[2].Tick);
            Assert.Equal(2U, p.BaseSeq);                         // 첫 이벤트 바로 앞 순번

            // 다음 틱 패킷(sealed 101): 93 이 빠지고 94 부터
            p.SealedThroughTick = 101;
            ClientInputWindow.Fill(p, history.ToArray(), 5);
            Assert.Equal(2, p.EventCount);
            Assert.Equal(4U, p.Events[0].Seq);
            Assert.Equal(3U, p.BaseSeq);
        }

        [Fact]
        public void FillWithoutEventsUsesLastAssignedSeq()
        {
            var history = new[] { new InputEvent(10, 0, LogicalKey.Up, true, 41), new InputEvent(11, 0, LogicalKey.Up, false, 42) };
            var p = new ClientInputPacket { SealedThroughTick = 100 };
            ClientInputWindow.Fill(p, history, 42);
            Assert.Equal(0, p.EventCount);
            Assert.Equal(42U, p.BaseSeq);
            ClientInputWindow.Fill(p, ReadOnlySpan<InputEvent>.Empty, 0);
            Assert.Equal(0U, p.BaseSeq);
        }

        // 창 안에 64개가 넘으면(8틱 × 8개 + 다음 틱) 가장 오래된 것부터 뺀다. base_seq 는 실은 첫 이벤트의 앞
        [Fact]
        public void FillKeepsTheNewest64()
        {
            var history = new List<InputEvent>();
            uint seq = 0;
            for (uint t = 93; t <= 101; t++)
            {
                for (int i = 0; i < 8; i++)
                {
                    history.Add(new InputEvent(t, (byte)(i * 16), LogicalKey.Fire, (i & 1) == 0, ++seq));
                }
            }
            var p = new ClientInputPacket { SealedThroughTick = 100 };
            ClientInputWindow.Fill(p, history.ToArray(), seq);
            Assert.Equal(DuelWire.MaxClientEvents, p.EventCount);
            Assert.Equal(9U, p.Events[0].Seq);
            Assert.Equal(72U, p.Events[63].Seq);
            Assert.Equal(8U, p.BaseSeq);
            Assert.Equal(DuelCodecError.None, p.Measure(out int size));
            Assert.True(DuelPacketFrame.SealedLength(size) <= DuelWire.MaxPacketBytes);
        }
    }
}

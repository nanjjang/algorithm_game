using System;
using System.Collections.Generic;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // WZDR(CONTRACTS 11.1)을 시험 쪽에서 직접 읽고 다시 돌린다. 머리말과 꼬리말의 검사는 Wizard.Sim 의 공개 코덱(DuelRecordCodec)에 맡기고,
    // 본문의 0x05 레코드는 형식 그대로 읽는다(그 읽기 도구는 Wizard.Sim 의 내부 타입이다)
    internal static class Wzdr
    {
        private const byte TagDuelInput = 0x05;
        private const byte TagFooter = 0xFF;
        private const int InputDelayBytes = 2;

        public static readonly ReplayLimits Generous = new ReplayLimits(10_000_000U, 10_000_000L);

        public sealed class File
        {
            public DuelSetup Setup = null!;
            public DuelRecordBody Body = null!;
            public readonly List<PlayerInput> Inputs = new List<PlayerInput>();
        }

        public static File Read(ReadOnlyMemory<byte> bytes)
        {
            byte[] f = bytes.ToArray();
            Assert.True(DuelRecordCodec.TryRead(f, Generous, out DuelSetup? setup, out DuelRecordBody? body, out RecordError e), e.ToString());
            var file = new File { Setup = setup!, Body = body! };
            var r = new ByteReader(f.AsSpan(body!.HeaderLength + InputDelayBytes));
            uint tick = 0;
            while (true)
            {
                Assert.True(r.TryU8(out byte tag));
                if (tag == TagFooter)
                {
                    break;
                }
                Assert.Equal(TagDuelInput, tag);
                tick += (uint)ReadVar(ref r);
                Assert.True(r.TryU8(out byte slot));
                Assert.True(r.TryU8(out byte offset));
                Assert.True(r.TryU8(out byte packed));
                uint seq = (uint)ReadVar(ref r);
                Assert.True(InputEvent.TryUnpackKey(packed, out LogicalKey key, out bool down));
                file.Inputs.Add(new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, seq)));
            }
            Assert.Equal(body.InputCount, file.Inputs.Count);
            return file;
        }

        private static ulong ReadVar(ref ByteReader r)
        {
            ulong v = 0;
            int shift = 0;
            while (true)
            {
                Assert.True(r.TryU8(out byte b));
                v |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return v;
                }
                shift += 7;
            }
        }

        // 기록을 처음부터 다시 돌린다. 돌려주는 것: 틱 t 를 Step 한 뒤의 해시(색인 t), 마지막 해시
        public static (List<ulong> afterStep, ulong final) Replay(File file)
        {
            SimWorld world = SimWorld.CreateDuel(HostKit.Db, SimConfig.ForDuel(HostKit.Db, file.Setup.Options), file.Setup);
            var sink = new NullSink();
            var hashes = new List<ulong>();
            int at = 0;
            var buf = new PlayerInput[16];
            while (world.Tick < file.Body.EndTick)
            {
                int n = 0;
                while (at < file.Inputs.Count && file.Inputs[at].Event.Tick == world.Tick)
                {
                    buf[n++] = file.Inputs[at++];
                }
                world.Step(buf.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, sink);
                hashes.Add(world.ComputeStateHash());
            }
            Assert.Equal(file.Inputs.Count, at);
            return (hashes, world.ComputeStateHash());
        }

        private sealed class NullSink : ISimEventSink
        {
            public void Emit(in SimEvent e)
            {
            }
        }
    }
}

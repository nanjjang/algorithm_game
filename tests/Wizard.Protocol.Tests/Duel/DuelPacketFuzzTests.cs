using System;
using Wizard.Core;
using Wizard.Protocol.Duel;
using Wizard.TestKit;
using Wizard.TestKit.Shared.Fuzz;
using Xunit;
using Xunit.Abstractions;

namespace Wizard.Protocol.Tests.Duel
{
    // 무작위 바이트 100만 건: 겉 형식과 일곱 본문 코덱 어디에서도 예외가 나지 않고, 읽기에 성공한 바이트는 다시 써도 같은 바이트다.
    // 무작위 바이트는 거의 다 거절되므로 픽스처를 비튼 입력(바이트 바꾸기·끼우기·빼기·자르기)으로 받아들이는 길도 같이 두드린다.
    public sealed class DuelPacketFuzzTests
    {
        private const int RandomCases = 1_000_000;
        private const int MutationCases = 200_000;

        private static readonly byte[] Key = new byte[DuelWire.KeyBytes];

        private readonly ITestOutputHelper _output;

        public DuelPacketFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RandomBytesNeverThrowAndAcceptedBytesRoundTrip()
        {
            var harness = new Harness();
            FuzzAssert.BytesHold(_output, new FuzzOptions(0x6475656c5f726e64UL, RandomCases), 0, DuelWire.MaxPacketBytes + 40, harness.Check);
            _output.WriteLine("accepted bodies: " + harness.AcceptedBodies + ", opened frames: " + harness.OpenedFrames);
        }

        [Fact]
        public void MutatedFixturesNeverThrowAndAcceptedBytesRoundTrip()
        {
            var harness = new Harness();
            byte[][] seeds = Seeds();
            FuzzGen<byte[]> gen = (ref Pcg32 rng) => Mutate(ref rng, seeds[rng.NextInt(seeds.Length)]);
            FuzzAssert.Holds(_output, new FuzzOptions(0x6475656c5f6d7574UL, MutationCases), gen, harness.Check, FuzzShrinkers.Bytes, FuzzFormat.Hex);
            _output.WriteLine("accepted bodies: " + harness.AcceptedBodies + ", opened frames: " + harness.OpenedFrames);
            Assert.True(harness.AcceptedBodies > MutationCases / 10, "mutation fuzz should reach the accepting paths");
        }

        // 본문 픽스처 각각에 머리(+0 태그)를 붙인 패킷. AcceptAllCipher 로 열리므로 본문 해석까지 닿는다
        private static byte[][] Seeds()
        {
            var list = new System.Collections.Generic.List<byte[]>();
            var cipher = new AcceptAllCipher();
            ulong number = 1;
            foreach (PacketFixtures.Body b in PacketFixtures.Bodies.Values)
            {
                var packet = new byte[DuelWire.MaxPacketBytes];
                var h = new DuelPacketHeader((DuelPacketKind)b.Kind, 0x01020304U, number++);
                int n = DuelPacketFrame.Seal(cipher, Key, DuelDirection.ClientToServer, h, b.Bytes, packet, out _);
                list.Add(packet.AsSpan(0, n).ToArray());
            }
            return list.ToArray();
        }

        private static byte[] Mutate(ref Pcg32 rng, byte[] seed)
        {
            var b = new System.Collections.Generic.List<byte>(seed);
            int edits = 1 + rng.NextInt(4);
            for (int i = 0; i < edits; i++)
            {
                int op = rng.NextInt(5);
                // 머리 13바이트 뒤(본문)를 주로 건드린다. 가끔 머리도
                int lo = rng.NextInt(8) == 0 ? 0 : Math.Min(DuelWire.HeaderBytes, b.Count);
                int at = b.Count > lo ? lo + rng.NextInt(b.Count - lo) : 0;
                switch (op)
                {
                    case 0 when b.Count > 0:
                        b[at] = (byte)rng.NextInt(256);
                        break;
                    case 1 when b.Count > 0:
                        b[at] ^= (byte)(1 << rng.NextInt(8));
                        break;
                    case 2:
                        b.Insert(Math.Min(at, b.Count), (byte)rng.NextInt(256));
                        break;
                    case 3 when b.Count > 0:
                        b.RemoveAt(at);
                        break;
                    case 4 when b.Count > 0:
                        b.RemoveRange(at, b.Count - at);
                        break;
                }
            }
            return b.ToArray();
        }

        private sealed class Harness
        {
            private readonly AcceptAllCipher _cipher = new AcceptAllCipher();
            private readonly BodyRoundTrip _rt = new BodyRoundTrip();
            private readonly byte[] _body = new byte[DuelWire.MaxPacketBytes + 64];
            private readonly byte[] _resealed = new byte[DuelWire.MaxPacketBytes];

            public int AcceptedBodies { get; private set; }

            public int OpenedFrames { get; private set; }

            public string? Check(byte[] input)
            {
                // (1) 겉 형식: 머리를 읽고 연다. 열렸으면 그 종류의 본문으로 읽는다
                if (DuelPacketFrame.TryOpen(_cipher, Key, DuelDirection.ClientToServer, input, _body, out DuelPacketHeader h, out int len,
                        out DuelCodecError fe))
                {
                    OpenedFrames++;
                    if (input.Length < DuelWire.OverheadBytes || input.Length > DuelWire.MaxPacketBytes)
                    {
                        return "opened a frame of length " + input.Length;
                    }
                    DuelCodecError be = _rt.Read(h.Kind, _body.AsSpan(0, len));
                    if (be == DuelCodecError.None)
                    {
                        AcceptedBodies++;
                        if (!_rt.RewrittenSpan.SequenceEqual(_body.AsSpan(0, len)))
                        {
                            return "body rewrite differs for kind " + h.Kind;
                        }
                        int n = DuelPacketFrame.Seal(_cipher, Key, DuelDirection.ClientToServer, h, _rt.RewrittenSpan, _resealed, out _);
                        // 가짜 암호의 태그는 0 이므로 원래 태그가 0일 때만 같다. 머리와 본문 부분만 비교한다
                        if (n != input.Length || !_resealed.AsSpan(0, n - DuelWire.TagBytes).SequenceEqual(input.AsSpan(0, n - DuelWire.TagBytes)))
                        {
                            return "frame rewrite differs";
                        }
                    }
                }
                else if (fe == DuelCodecError.None)
                {
                    return "TryOpen failed without an error";
                }

                // (2) 같은 바이트를 일곱 본문 코덱 각각에 그대로 넣는다
                for (int k = 1; k <= 7; k++)
                {
                    DuelCodecError e = _rt.Read((DuelPacketKind)k, input);
                    if (e == DuelCodecError.None)
                    {
                        AcceptedBodies++;
                        if (!_rt.RewrittenSpan.SequenceEqual(input))
                        {
                            return "body rewrite differs for kind " + k;
                        }
                    }
                }
                return null;
            }
        }
    }
}

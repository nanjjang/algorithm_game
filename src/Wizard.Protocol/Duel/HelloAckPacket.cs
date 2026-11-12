using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.4, 10.5. 서버 → 클라. 16바이트.
    // D 는 서버가 자기 표본으로 정해 여기로만 내려 준다(DuelSetup 에 없다). rtt_ms_measured 는 서버가 잰 왕복(표시와 기록용)
    public readonly struct HelloAckPacket
    {
        public const int BodyBytes = 16;

        public readonly uint StartTick;
        public readonly ulong MatchEpochServerUs;
        public readonly byte InputDelaySelf;
        public readonly byte InputDelayOpponent;
        public readonly ushort RttMsMeasured;

        public HelloAckPacket(uint startTick, ulong matchEpochServerUs, byte inputDelaySelf, byte inputDelayOpponent, ushort rttMsMeasured)
        {
            StartTick = startTick;
            MatchEpochServerUs = matchEpochServerUs;
            InputDelaySelf = inputDelaySelf;
            InputDelayOpponent = inputDelayOpponent;
            RttMsMeasured = rttMsMeasured;
        }

        public void WriteTo(ByteWriter w)
        {
            w.U32(StartTick);
            w.U64(MatchEpochServerUs);
            w.U8(InputDelaySelf);
            w.U8(InputDelayOpponent);
            w.U16(RttMsMeasured);
        }

        public static DuelCodecError TryRead(ReadOnlySpan<byte> body, out HelloAckPacket packet)
        {
            packet = default;
            if (body.Length > BodyBytes)
            {
                return DuelCodecError.TrailingBytes;
            }
            var r = new ByteReader(body);
            if (!r.TryU32(out uint start) || !r.TryU64(out ulong epoch) || !r.TryU8(out byte dSelf) || !r.TryU8(out byte dOpp)
                || !r.TryU16(out ushort rtt))
            {
                return DuelCodecError.Truncated;
            }
            packet = new HelloAckPacket(start, epoch, dSelf, dOpp, rtt);
            return DuelCodecError.None;
        }
    }
}

using System;
using Wizard.Core;

namespace Wizard.Protocol.Duel
{
    // CONTRACTS 10.4. 클라 → 서버. 5바이트. 왕복 칸은 없다(10.5: 왕복과 D는 서버가 잰다)
    public readonly struct HelloPacket
    {
        public const int BodyBytes = 5;

        public readonly PlayerSlot Slot;      // 결투는 0·1
        public readonly uint ClientBuild;

        public HelloPacket(PlayerSlot slot, uint clientBuild)
        {
            Slot = slot;
            ClientBuild = clientBuild;
        }

        public DuelCodecError Validate() => Slot.Value < DuelWire.DuelSlots ? DuelCodecError.None : DuelCodecError.OutOfRange;

        public DuelCodecError TryWrite(ByteWriter w)
        {
            DuelCodecError e = Validate();
            if (e != DuelCodecError.None)
            {
                return e;
            }
            w.U8(Slot.Value);
            w.U32(ClientBuild);
            return DuelCodecError.None;
        }

        public static DuelCodecError TryRead(ReadOnlySpan<byte> body, out HelloPacket packet)
        {
            packet = default;
            if (body.Length > BodyBytes)
            {
                return DuelCodecError.TrailingBytes;
            }
            var r = new ByteReader(body);
            if (!r.TryU8(out byte slot) || !r.TryU32(out uint build))
            {
                return DuelCodecError.Truncated;
            }
            var p = new HelloPacket(new PlayerSlot(slot), build);
            DuelCodecError e = p.Validate();
            if (e != DuelCodecError.None)
            {
                return e;
            }
            packet = p;
            return DuelCodecError.None;
        }
    }
}

using System;
using System.Security.Cryptography;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 11.1. 입력 기록(.wzrc)과 결투 확정 기록(WZDR) 본문의 꼬리표·꼬리말 상수와 봉인(SHA-256).
    // 쓰는 쪽(InputRecorder, DuelRecordWriter)과 읽는 쪽(InputRecordCodec, DuelRecordCodec)이 같은 상수와 같은 봉인 함수를 쓴다.
    // 틱 밖에서만 쓴다(Step 과 무관). 가변 정적 필드가 없다.
    internal static class InputRecordFormat
    {
        public const byte TagInput = 0x01;          // varuint Δtick, u8 offset, u8 key_down
        public const byte TagCommand = 0x02;        // varuint Δtick, u8 op, i32 a, i32 b
        public const byte TagPause = 0x03;          // varuint Δtick, u32 wall_ms
        public const byte TagCheckpoint = 0x04;     // u16 wave, u32 tick, u64 state_hash
        public const byte TagDuelInput = 0x05;      // varuint Δtick, u8 slot, u8 offset, u8 key_down, varuint seq (WZDR 만)
        public const byte TagFooter = 0xFF;         // u32 end_tick, u64 final_state_hash, u8 flags, [32] record_sha256

        public const byte FlagDebugUsed = 0x01;     // 비트 0 = debug_used
        public const byte KnownFlags = FlagDebugUsed;

        public const int ShaBytes = 32;
        public const int FooterBytes = 1 + 4 + 8 + 1 + ShaBytes;
        public const int DuelInputDelayCount = 2;   // WZDR 머리말 뒤의 input_delay u8 × 2(슬롯 순)
        public const int DuelSlots = 2;             // 결투는 두 사람(슬롯 0, 1)
        public const byte FirstDebugOp = (byte)RunCommandOp.DebugSkipWave;   // 200~255 는 디버그 전용(CONTRACTS 3.1)

        // op 이 RunCommandOp 에 정의된 값인가(CONTRACTS 3.1). 정의되지 않은 번호는 구조 오류다(3.8절)
        public static bool IsKnownOp(byte op)
        {
            switch ((RunCommandOp)op)
            {
                case RunCommandOp.PickCard:
                case RunCommandOp.RerollCards:
                case RunCommandOp.DiscardBlock:
                case RunCommandOp.SetSlot:
                case RunCommandOp.SwapSlots:
                case RunCommandOp.Heal:
                case RunCommandOp.UnlockArt:
                case RunCommandOp.Depart:
                case RunCommandOp.AbandonRun:
                case RunCommandOp.CloseCampScreen:
                case RunCommandOp.EnterPractice:
                case RunCommandOp.ExitPractice:
                case RunCommandOp.DebugSkipWave:
                case RunCommandOp.DebugInvulnerable:
                case RunCommandOp.DebugGrant:
                case RunCommandOp.DebugSetStage:
                    return true;
                default:
                    return false;
            }
        }

        // record_sha256 = 그 앞의 모든 바이트의 SHA-256(11.1절, T-49): 머리말 ‖ (WZDR 은 input_delay 두 칸) ‖ 본문 ‖ 꼬리말의 칸(0xFF 부터 flags 까지).
        // prefix = 머리말(과 input_delay), rest = 본문 ‖ 꼬리말의 칸
        public static void Sha256(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> rest, Span<byte> destination)
        {
            using (IncrementalHash h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                h.AppendData(prefix);
                h.AppendData(rest);
                if (!h.TryGetHashAndReset(destination, out int written) || written != ShaBytes)
                {
                    throw new InvalidOperationException("SHA-256 did not produce 32 bytes");
                }
            }
        }

        // prefix ‖ 본문 ‖ 꼬리말을 한 배열로 만든다
        public static byte[] Seal(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> body, uint endTick, ulong finalStateHash, byte flags)
        {
            var w = new ByteWriter(prefix.Length + body.Length + FooterBytes);
            w.Bytes(prefix);
            w.Bytes(body);
            w.U8(TagFooter);
            w.U32(endTick);
            w.U64(finalStateHash);
            w.U8(flags);
            Span<byte> sha = stackalloc byte[ShaBytes];
            Sha256(ReadOnlySpan<byte>.Empty, w.Written, sha);
            w.Bytes(sha);
            return w.ToArray();
        }

        // afterPrefix = 본문 ‖ 꼬리말, footerStart = 그 안에서 꼬리표 0xFF 의 자리.
        // 순서: 길이(모자람, 남음) → record_sha256(prefix ‖ afterPrefix 의 sha 앞까지) → flags. 무결성을 먼저 보고 칸을 읽는다
        public static RecordError ReadFooter(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> afterPrefix, int footerStart, out RecordFooter footer)
        {
            footer = default;
            int footerLength = afterPrefix.Length - footerStart;
            if (footerLength < FooterBytes)
            {
                return RecordError.Truncated;
            }
            if (footerLength > FooterBytes)
            {
                return RecordError.TrailingBytes;
            }
            int shaStart = afterPrefix.Length - ShaBytes;
            Span<byte> actual = stackalloc byte[ShaBytes];
            Sha256(prefix, afterPrefix.Slice(0, shaStart), actual);
            if (!actual.SequenceEqual(afterPrefix.Slice(shaStart)))
            {
                return RecordError.ShaMismatch;
            }
            var r = new ByteReader(afterPrefix.Slice(footerStart + 1, FooterBytes - 1 - ShaBytes));
            if (!r.TryU32(out uint endTick) || !r.TryU64(out ulong finalHash) || !r.TryU8(out byte flags))
            {
                return RecordError.Truncated;
            }
            if ((flags & ~KnownFlags) != 0)
            {
                return RecordError.BadFlags;
            }
            footer = new RecordFooter(endTick, finalHash, flags);
            return RecordError.None;
        }

        // varuint Δtick 를 읽어 기준 틱에 더한다. 끝에서 모자라면 Truncated, uint 를 넘으면 TickOverflow
        public static RecordError ReadTick(ref ByteReader r, uint baseTick, out uint tick)
        {
            tick = 0;
            if (!r.TryVarUInt(out ulong delta))
            {
                return VarUIntFailure(ref r);
            }
            ulong t = baseTick + delta;
            if (delta > uint.MaxValue || t > uint.MaxValue)
            {
                return RecordError.TickOverflow;
            }
            tick = (uint)t;
            return RecordError.None;
        }

        // TryVarUInt 가 실패한 까닭: 끝까지 이어짐 비트만 있으면 잘린 것, 아니면 64비트를 넘친 것
        public static RecordError VarUIntFailure(ref ByteReader r)
        {
            ByteReader probe = r;
            while (probe.TryU8(out byte b))
            {
                if ((b & 0x80) == 0)
                {
                    return RecordError.TickOverflow;
                }
            }
            return RecordError.Truncated;
        }
    }
}

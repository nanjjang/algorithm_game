using System;
using System.Diagnostics.CodeAnalysis;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1, DECISIONS-TECH T-48. 결투 확정 기록(WZDR)을 읽는 유일한 문. 예외를 던지지 않는다.
    //   머리말(= DuelSetup 의 정규 바이트, magic "WZDR") ‖ input_delay u8 × 2 ‖ 0x05 레코드의 열 ‖ 꼬리말
    // record_sha256 은 그 앞의 모든 바이트의 SHA-256 이다(머리말과 input_delay 포함).
    // 머리말은 DuelSetup.TryReadCanonicalPrefix 로, 본문은 WzdrBodyReader 하나로 읽는다(ReplayRunner.RunDuel 도 같은 판독기를 쓴다).
    public static class DuelRecordCodec
    {
        private const int MagicBytes = 4;
        private const int FormatVersionBytes = 2;
        private const byte Magic0 = (byte)'W';
        private const byte Magic1 = (byte)'Z';
        private const byte Magic2 = (byte)'D';
        private const byte Magic3 = (byte)'R';

        // 구조를 전부 확인한 뒤에만 true. 실패는 전부 RecordCorrupt 다(error 는 까닭). limits 는 MaxTicks 만 쓴다(end_tick 의 상한)
        public static bool TryRead(ReadOnlySpan<byte> file, ReplayLimits limits, [NotNullWhen(true)] out DuelSetup? setup,
                                   [NotNullWhen(true)] out DuelRecordBody? body, out RecordError error)
        {
            body = null;
            if (!DuelSetup.TryReadCanonicalPrefix(file, out setup, out int headerLength))
            {
                setup = null;
                error = HeaderError(file);
                return false;
            }
            ReadOnlySpan<byte> rest = file.Slice(headerLength);
            if (rest.Length < InputRecordFormat.DuelInputDelayCount)
            {
                setup = null;
                error = RecordError.Truncated;
                return false;
            }
            int prefixLength = headerLength + InputRecordFormat.DuelInputDelayCount;
            ReadOnlySpan<byte> recordBody = file.Slice(prefixLength);
            var reader = new WzdrBodyReader(recordBody);
            int inputCount = 0;
            while (reader.Next(out _, out error))
            {
                inputCount++;
            }
            if (error == RecordError.None)
            {
                error = InputRecordFormat.ReadFooter(file.Slice(0, prefixLength), recordBody, reader.FooterStart, out RecordFooter footer);
                if (error == RecordError.None)
                {
                    // 결투에는 디버그 op 이 없다. 꼬리말의 debug_used 는 이 형식에서 쓰지 않는 비트다
                    error = footer.Flags != 0 ? RecordError.BadFlags : reader.CheckAgainstEnd(footer.EndTick, limits.MaxTicks);
                }
                if (error == RecordError.None)
                {
                    body = new DuelRecordBody
                    {
                        HeaderLength = headerLength,
                        InputDelayTicks0 = rest[0],
                        InputDelayTicks1 = rest[1],
                        EndTick = footer.EndTick,
                        FinalStateHash = footer.FinalStateHash,
                        InputCount = inputCount,
                    };
                    return true;
                }
            }
            setup = null;
            return false;
        }

        // 머리말을 읽지 못한 까닭: magic, format_version, 그 밖(모자란 길이나 정규가 아닌 칸)
        private static RecordError HeaderError(ReadOnlySpan<byte> file)
        {
            if (file.Length < MagicBytes)
            {
                return RecordError.Truncated;
            }
            if (file[0] != Magic0 || file[1] != Magic1 || file[2] != Magic2 || file[3] != Magic3)
            {
                return RecordError.BadMagic;
            }
            if (file.Length < MagicBytes + FormatVersionBytes)
            {
                return RecordError.Truncated;
            }
            int version = file[MagicBytes] | (file[MagicBytes + 1] << 8);
            if (version != DuelSetup.CanonicalFormatVersion)
            {
                return RecordError.BadVersion;
            }
            return RecordError.BadHeader;
        }
    }
}

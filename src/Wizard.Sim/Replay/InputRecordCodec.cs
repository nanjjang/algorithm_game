using System;
using System.Diagnostics.CodeAnalysis;
using Wizard.Core;
using Wizard.Sim.Waves;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1. 신뢰할 수 없는 .wzrc 바이트를 읽는 유일한 문. 예외를 던지지 않는다(WriteHeader 는 쓰는 쪽이라 던진다).
    // 머리말은 직접 풀지 않고 EndlessSetup.TryReadCanonicalPrefix 를 쓴다(T-46의 따라오는 일 1). 본문은 WzrcBodyReader 하나로 읽는다.
    public static class InputRecordCodec
    {
        private const int MagicBytes = 4;
        private const int FormatVersionBytes = 2;
        private const byte Magic0 = (byte)'W';
        private const byte Magic1 = (byte)'Z';
        private const byte Magic2 = (byte)'R';
        private const byte Magic3 = (byte)'C';

        // 구조를 전부 확인한 뒤에만 true. 실패는 전부 RecordCorrupt 다(error 는 까닭). limits 는 MaxTicks 만 쓴다(end_tick 의 상한, T-49).
        // body 의 값은 클라이언트가 적은 것이다. 구조만 확인된 것이고 재실행으로 확인된 것이 아니다.
        public static bool TryRead(ReadOnlySpan<byte> file, ReplayLimits limits, [NotNullWhen(true)] out InputRecordHeader? header,
                                   [NotNullWhen(true)] out InputRecordBody? body, out RecordError error)
        {
            header = null;
            body = null;
            if (!EndlessSetup.TryReadCanonicalPrefix(file, out EndlessSetup? setup, out int headerLength))
            {
                error = HeaderError(file);
                return false;
            }
            ReadOnlySpan<byte> recordBody = file.Slice(headerLength);
            if (!TryReadBody(file.Slice(0, headerLength), recordBody, limits.MaxTicks, out RecordFooter footer, out WzrcBodySummary summary, out error))
            {
                return false;
            }
            header = new InputRecordHeader { FormatVersion = EndlessSetup.CanonicalFormatVersion, Setup = setup };
            body = new InputRecordBody
            {
                HeaderLength = headerLength,
                EndTick = footer.EndTick,
                FinalStateHash = footer.FinalStateHash,
                DebugUsed = footer.DebugUsed,
                HasDebugOps = summary.HasDebugOps,
                CommandCount = summary.CommandCount,
                InputCount = summary.InputCount,
                PauseCount = summary.PauseCount,
                PauseTotalMs = summary.PauseTotalMs,
                Checkpoints = ReadCheckpoints(recordBody, summary.CheckpointCount),
            };
            return true;
        }

        // 머리말을 쓰는 코드는 여기 한 곳이다(= header.Setup.WriteCanonical). 형식에 담기지 않으면 아무것도 쓰지 않고 InvalidOperationException
        public static void WriteHeader(ByteWriter w, InputRecordHeader header)
        {
            if (w == null)
            {
                throw new ArgumentNullException(nameof(w));
            }
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (header.FormatVersion != EndlessSetup.CanonicalFormatVersion)
            {
                throw new InvalidOperationException("InputRecordHeader.FormatVersion is not the canonical format version");
            }
            if (header.Setup == null)
            {
                throw new InvalidOperationException("InputRecordHeader.Setup is null");
            }
            header.Setup.WriteCanonical(w);
        }

        // 머리말 뒤의 바이트(본문 ‖ 꼬리말)를 확인한다. record_sha256 은 headerBytes ‖ 본문 ‖ 꼬리말의 칸으로 대조한다.
        // ReplayRunner 는 파일의 머리말 대신 믿는 원천(run.setup_canonical)의 바이트를 headerBytes 로 준다.
        // 관리 힙에 할당하지 않는다(무작위 바이트를 싸게 거른다). 중간 지점 배열은 통과한 뒤에 TryRead 가 만든다
        internal static bool TryReadBody(ReadOnlySpan<byte> headerBytes, ReadOnlySpan<byte> recordBody, uint maxTicks,
                                         out RecordFooter footer, out WzrcBodySummary summary, out RecordError error)
        {
            footer = default;
            summary = default;
            var reader = new WzrcBodyReader(recordBody);
            while (reader.Next(out RecordEntry entry, out error))
            {
                switch (entry.Kind)
                {
                    case RecordKind.Command:
                        summary.CommandCount++;
                        if ((byte)entry.Op >= InputRecordFormat.FirstDebugOp)
                        {
                            summary.HasDebugOps = true;
                        }
                        break;
                    case RecordKind.Input:
                        summary.InputCount++;
                        break;
                    case RecordKind.Pause:
                        summary.PauseCount++;
                        summary.PauseTotalMs += entry.WallMs;   // 레코드 하나가 6바이트 이상이라 long 을 넘지 못한다
                        break;
                    default:
                        summary.CheckpointCount++;
                        break;
                }
            }
            if (error != RecordError.None)
            {
                return false;
            }
            error = InputRecordFormat.ReadFooter(headerBytes, recordBody, reader.FooterStart, out footer);
            if (error != RecordError.None)
            {
                return false;
            }
            error = reader.CheckAgainstEnd(footer.EndTick, maxTicks);
            return error == RecordError.None;
        }

        // 이미 확인한 본문에서 0x04 레코드를 파일 순서대로 모은다
        private static RunCheckpoint[] ReadCheckpoints(ReadOnlySpan<byte> recordBody, int count)
        {
            if (count == 0)
            {
                return Array.Empty<RunCheckpoint>();
            }
            var checkpoints = new RunCheckpoint[count];
            var reader = new WzrcBodyReader(recordBody);
            int n = 0;
            while (n < count && reader.Next(out RecordEntry entry, out _))
            {
                if (entry.Kind == RecordKind.Checkpoint)
                {
                    checkpoints[n++] = new RunCheckpoint(entry.Wave, entry.Tick, entry.StateHash);
                }
            }
            return checkpoints;
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
            if (version != EndlessSetup.CanonicalFormatVersion)
            {
                return RecordError.BadVersion;
            }
            return RecordError.BadHeader;   // 모자란 길이, 또는 정규가 아닌 칸(RunKind 밖의 kind, 0·1이 아닌 참거짓, int 를 넘는 data_version)
        }
    }
}

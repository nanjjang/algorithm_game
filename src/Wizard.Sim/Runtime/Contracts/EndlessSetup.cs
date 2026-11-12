using System;
using System.Diagnostics.CodeAnalysis;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim
{
    // CONTRACTS 3.2, 11.1, DECISIONS-TECH T-46. 무한 웨이브 한 판의 시작 조건.
    // 정규 바이트 = .wzrc 머리말 그대로(magic 부터 unlocked_arts 까지). 서버 프로필에서는 서버가 StartRun 때 만들어
    // run.setup_canonical 에 저장하고 RunStartTicket.setup 으로 내려 준다. 클라이언트는 그 바이트를 읽어 쓰고 다시 조립하지 않는다.
    public sealed class EndlessSetup
    {
        public const ushort CanonicalFormatVersion = 1;   // .wzrc 의 format_version(11.1절)

        private const byte Magic0 = (byte)'W';
        private const byte Magic1 = (byte)'Z';
        private const byte Magic2 = (byte)'R';
        private const byte Magic3 = (byte)'C';
        private const int CoreVersionBytes = 16;                       // core_version [16]
        private const int CoreVersionChars = CoreVersionBytes * 2;     // hex 32자
        private const string HexDigits = "0123456789abcdef";
        private const int HexLetterBase = 10;                          // 'a' 의 값
        private const int NibbleBits = 4;
        private const int NibbleMask = 0xF;
        private const RunKind LastRunKind = RunKind.Weekly;            // RunKind 에 값을 더하면 같이 올린다(시험이 열거형과 대조한다)

        public Guid RunId;
        public ulong Seed;
        public RunKind Kind;
        public int StartWave = 1;                         // 1, 11, 21, …
        public bool PhantomFirst;
        public WandSnapshot Wand = new WandSnapshot();
        public ArtMask UnlockedArts;
        public bool SeasonTagOn;                          // 시즌 주화·한정 드랍 기회를 기록할지

        // 판의 봉투 값(T-46). 이 판을 돌릴 코어와 팩이다. 세계는 읽지 않는다(상태와 해시에 들어가지 않는다).
        // 맞는 코어·팩으로 돌리는지는 호출자가 본다(클라이언트는 출발 때, 워커는 대기열 조건과 팩 저장소에서).
        // 서버는 지금 링크한 코어의 상수가 아니라 출발 요청의 core_version 을 넣는다(T-18). 그래서 상수가 아니라 칸이다.
        public string CoreVersion = string.Empty;         // hex 32자(소문자). SimVersion.CoreVersion 과 같은 꼴
        public int DataVersion;                           // ContentDb.DataVersion. 0 이상

        // w 의 끝에 덧붙인다. 칸이 형식에 담기지 않으면 아무것도 쓰지 않고 InvalidOperationException 을 던진다.
        // 규칙상의 "안 됨"이 아니라 설정을 만든 코드의 결함이다(틱 밖에서만 부른다).
        public void WriteCanonical(ByteWriter w)
        {
            if (w == null)
            {
                throw new ArgumentNullException(nameof(w));
            }
            string? problem = FindUnwritable();
            if (problem != null)
            {
                throw new InvalidOperationException("EndlessSetup has no canonical bytes: " + problem);
            }

            w.U8(Magic0);
            w.U8(Magic1);
            w.U8(Magic2);
            w.U8(Magic3);
            w.U16(CanonicalFormatVersion);
            for (int i = 0; i < CoreVersionBytes; i++)
            {
                int hi = HexValue(CoreVersion[i * 2]);
                int lo = HexValue(CoreVersion[i * 2 + 1]);
                w.U8((byte)((hi << NibbleBits) | lo));
            }
            w.U32((uint)DataVersion);

            Span<byte> runId = stackalloc byte[UuidBytes.Size];
            UuidBytes.Write(RunId, runId);                // RFC 4122 네트워크 순서(0.3절). Guid.ToByteArray 를 쓰지 않는다
            w.Bytes(runId);
            w.U64(Seed);
            w.U8((byte)Kind);
            w.U16((ushort)StartWave);
            w.U8(PhantomFirst ? (byte)1 : (byte)0);
            w.U8(SeasonTagOn ? (byte)1 : (byte)0);

            w.U8((byte)Wand.Character);
            w.U8((byte)Wand.Stage);
            w.U8((byte)Wand.SlotsExtra);
            w.U8((byte)Wand.Mutations.Length);
            for (int i = 0; i < Wand.Mutations.Length; i++)
            {
                w.U8((byte)Wand.Mutations[i]);
            }
            w.U8((byte)Wand.Core);
            w.U8((byte)Wand.Links.Length);
            for (int i = 0; i < Wand.Links.Length; i++)
            {
                w.U8((byte)Wand.Links[i]);
            }
            w.U16((ushort)UnlockedArts);
        }

        // 신뢰할 수 없는 바이트를 읽는다. 예외 없음. bytes 전체가 정규 바이트일 때만 true(뒤에 바이트가 남으면 false).
        // true 인 입력은 WriteCanonical 이 다시 쓰면 같은 바이트가 된다(한 설정에 바이트 열 하나).
        // 캐릭터·변이·블록 id 가 그 팩에 있는지, 시작 웨이브가 규칙에 맞는지는 보지 않는다(팩과 규칙을 모른다).
        public static bool TryReadCanonical(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out EndlessSetup? setup)
        {
            if (TryReadCanonicalPrefix(bytes, out setup, out int length) && length == bytes.Length)
            {
                return true;
            }
            setup = null;
            return false;
        }

        // 기록 파일(.wzrc)의 앞머리에서 읽는다. length = 머리말의 길이(본문이 시작하는 자리). 뒤에 남는 바이트는 보지 않는다.
        public static bool TryReadCanonicalPrefix(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out EndlessSetup? setup, out int length)
        {
            setup = null;
            length = 0;
            var r = new ByteReader(bytes);

            if (!r.TryU8(out byte m0) || !r.TryU8(out byte m1) || !r.TryU8(out byte m2) || !r.TryU8(out byte m3)
                || m0 != Magic0 || m1 != Magic1 || m2 != Magic2 || m3 != Magic3)
            {
                return false;
            }
            if (!r.TryU16(out ushort formatVersion) || formatVersion != CanonicalFormatVersion)
            {
                return false;
            }
            if (!r.TryBytes(CoreVersionBytes, out ReadOnlySpan<byte> coreVersion))
            {
                return false;
            }
            if (!r.TryU32(out uint dataVersion) || dataVersion > int.MaxValue)
            {
                return false;
            }
            if (!r.TryBytes(UuidBytes.Size, out ReadOnlySpan<byte> runId) || !r.TryU64(out ulong seed))
            {
                return false;
            }
            if (!r.TryU8(out byte kind) || kind > (byte)LastRunKind)
            {
                return false;
            }
            if (!r.TryU16(out ushort startWave))
            {
                return false;
            }
            if (!TryBool(ref r, out bool phantomFirst) || !TryBool(ref r, out bool seasonTagOn))
            {
                return false;
            }
            if (!r.TryU8(out byte character) || !r.TryU8(out byte stage) || !r.TryU8(out byte slotsExtra))
            {
                return false;
            }
            if (!r.TryU8(out byte mutationCount) || !r.TryBytes(mutationCount, out ReadOnlySpan<byte> mutations))
            {
                return false;
            }
            if (!r.TryU8(out byte core))
            {
                return false;
            }
            if (!r.TryU8(out byte linkCount) || !r.TryBytes(linkCount, out ReadOnlySpan<byte> links))
            {
                return false;
            }
            if (!r.TryU16(out ushort unlockedArts))
            {
                return false;
            }

            // 구조가 전부 맞은 뒤에만 만든다(배열은 길어야 255칸이다)
            var wand = new WandSnapshot
            {
                Character = (CharacterId)character,
                Stage = stage,
                SlotsExtra = slotsExtra,
                Mutations = new MutationId[mutations.Length],
                Core = (BlockId)core,
                Links = new BlockId[links.Length],
            };
            for (int i = 0; i < mutations.Length; i++)
            {
                wand.Mutations[i] = (MutationId)mutations[i];
            }
            for (int i = 0; i < links.Length; i++)
            {
                wand.Links[i] = (BlockId)links[i];
            }

            char[] hex = new char[CoreVersionChars];
            for (int i = 0; i < CoreVersionBytes; i++)
            {
                hex[i * 2] = HexDigits[coreVersion[i] >> NibbleBits];
                hex[i * 2 + 1] = HexDigits[coreVersion[i] & NibbleMask];
            }

            setup = new EndlessSetup
            {
                RunId = UuidBytes.Read(runId),
                Seed = seed,
                Kind = (RunKind)kind,
                StartWave = startWave,
                PhantomFirst = phantomFirst,
                Wand = wand,
                UnlockedArts = (ArtMask)unlockedArts,
                SeasonTagOn = seasonTagOn,
                CoreVersion = new string(hex),
                DataVersion = (int)dataVersion,
            };
            length = r.Position;
            return true;
        }

        // 형식에 담기지 않는 칸의 이름(개발용 글자). 다 담기면 null
        private string? FindUnwritable()
        {
            if (CoreVersion == null || CoreVersion.Length != CoreVersionChars)
            {
                return "CoreVersion must be 32 lower-case hex digits";
            }
            for (int i = 0; i < CoreVersionChars; i++)
            {
                if (HexValue(CoreVersion[i]) < 0)
                {
                    return "CoreVersion must be 32 lower-case hex digits";
                }
            }
            if (DataVersion < 0)
            {
                return "DataVersion is negative";
            }
            if (Kind > LastRunKind)
            {
                return "Kind is not a RunKind value";
            }
            if (StartWave < 0 || StartWave > ushort.MaxValue)
            {
                return "StartWave does not fit u16";
            }
            if (Wand == null)
            {
                return "Wand is null";
            }
            if (Wand.Stage < 0 || Wand.Stage > byte.MaxValue)
            {
                return "Wand.Stage does not fit u8";
            }
            if (Wand.SlotsExtra < 0 || Wand.SlotsExtra > byte.MaxValue)
            {
                return "Wand.SlotsExtra does not fit u8";
            }
            if (Wand.Mutations == null || Wand.Mutations.Length > byte.MaxValue)
            {
                return "Wand.Mutations is null or longer than 255";
            }
            if (Wand.Links == null || Wand.Links.Length > byte.MaxValue)
            {
                return "Wand.Links is null or longer than 255";
            }
            return null;
        }

        // 소문자 hex 한 글자의 값. 아니면 −1(대문자도 아니다: 한 버전에 글자 열 하나)
        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + HexLetterBase;
            }
            return -1;
        }

        // 참거짓은 0 과 1 만 정규다
        private static bool TryBool(ref ByteReader r, out bool value)
        {
            value = false;
            if (!r.TryU8(out byte b) || b > 1)
            {
                return false;
            }
            value = b == 1;
            return true;
        }
    }
}

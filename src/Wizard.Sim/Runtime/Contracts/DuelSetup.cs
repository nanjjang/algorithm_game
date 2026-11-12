using System;
using System.Diagnostics.CodeAnalysis;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;

namespace Wizard.Sim
{
    // CONTRACTS 3.2, 11.1, DECISIONS-TECH T-48. 결투 한 경기의 시작 조건.
    // 정규 바이트 = WZDR 머리말 그대로(magic 부터 두 번째 플레이어의 unlocked_arts 까지). 입력 지연 D는 여기에 없다
    // (시뮬레이션은 D를 모른다. WZDR 은 머리말 뒤의 별도 칸에 싣는다).
    // 규칙은 EndlessSetup 과 같다(T-46): 한 설정에 바이트 열 하나, 읽기는 예외 없이, 쓰기는 담기지 않으면 던진다.
    public sealed class DuelSetup
    {
        public const ushort CanonicalFormatVersion = 1;   // WZDR 의 format_version(11.1절)

        private const int DuelPlayers = 2;
        private const byte Magic0 = (byte)'W';
        private const byte Magic1 = (byte)'Z';
        private const byte Magic2 = (byte)'D';
        private const byte Magic3 = (byte)'R';
        private const int CoreVersionBytes = 16;                       // core_version [16]
        private const int CoreVersionChars = CoreVersionBytes * 2;     // hex 32자
        private const string HexDigits = "0123456789abcdef";
        private const int HexLetterBase = 10;                          // 'a' 의 값
        private const int NibbleBits = 4;
        private const int NibbleMask = 0xF;

        public Guid MatchId;
        public ulong Seed;
        public DuelRoomOptions Options = new DuelRoomOptions();
        public DuelPlayerSetup[] Players = Array.Empty<DuelPlayerSetup>();    // 길이 2. 리그 제한을 적용한 뒤의 값

        // 경기의 봉투 값(T-46 과 같다). 이 경기를 돌릴 코어와 팩이다. 세계는 읽지 않는다(상태와 해시에 들어가지 않는다).
        // 맞는 코어·팩으로 돌리는지는 호출자가 본다. 새 설정은 CoreVersion 이 비어 있어 채우기 전에는 쓰지 못한다.
        public string CoreVersion = string.Empty;         // hex 32자(소문자). SimVersion.CoreVersion 과 같은 꼴
        public int DataVersion;                           // ContentDb.DataVersion. 0 이상

        // w 의 끝에 덧붙인다. 칸이 형식에 담기지 않으면 아무것도 쓰지 않고 InvalidOperationException 을 던진다
        // (설정을 만든 코드의 결함이다. 틱 밖에서만 부른다).
        public void WriteCanonical(ByteWriter w)
        {
            if (w == null)
            {
                throw new ArgumentNullException(nameof(w));
            }
            string? problem = FindUnwritable();
            if (problem != null)
            {
                throw new InvalidOperationException("DuelSetup has no canonical bytes: " + problem);
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

            Span<byte> matchId = stackalloc byte[UuidBytes.Size];
            UuidBytes.Write(MatchId, matchId);            // RFC 4122 네트워크 순서(0.3절)
            w.Bytes(matchId);
            w.U64(Seed);

            DuelRoomOptions o = Options;
            w.U8((byte)o.Rounds);
            w.U16((ushort)o.RoundSeconds);
            w.U16((ushort)o.HpMultPermille);
            w.U8((byte)o.EnhanceCap);
            w.I16((short)o.IlCap);
            w.U8(o.AllowMutations ? (byte)1 : (byte)0);
            w.I16((short)o.HeroBlockCap);
            w.U8(o.Equalize ? (byte)1 : (byte)0);
            w.U8(o.WildIntrusion ? (byte)1 : (byte)0);
            w.U8(o.ThornsOff ? (byte)1 : (byte)0);
            w.U8((byte)o.FloorColor);
            w.U8(o.Spectate ? (byte)1 : (byte)0);

            for (int i = 0; i < DuelPlayers; i++)
            {
                DuelPlayerSetup p = Players[i];
                WandSnapshot wand = p.AppliedWand;
                w.U8(p.Slot.Value);
                w.U8((byte)wand.Character);
                w.U8((byte)wand.Stage);
                w.U8((byte)wand.SlotsExtra);
                w.U8((byte)wand.Mutations.Length);
                for (int k = 0; k < wand.Mutations.Length; k++)
                {
                    w.U8((byte)wand.Mutations[k]);
                }
                w.U8((byte)wand.Core);
                w.U8((byte)wand.Links.Length);
                for (int k = 0; k < wand.Links.Length; k++)
                {
                    w.U8((byte)wand.Links[k]);
                }
                w.U8((byte)p.ActualStage);
                w.U32((uint)p.MaxHpPoints);
                w.U16((ushort)p.UnlockedArts);
            }
        }

        // 신뢰할 수 없는 바이트를 읽는다. 예외 없음. bytes 전체가 정규 바이트일 때만 true(뒤에 바이트가 남으면 false).
        // true 인 입력은 WriteCanonical 이 다시 쓰면 같은 바이트가 된다.
        // 옵션 값이 선택지 안인지, id 가 그 팩에 있는지는 보지 않는다(규칙과 팩을 모른다).
        public static bool TryReadCanonical(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out DuelSetup? setup)
        {
            if (TryReadCanonicalPrefix(bytes, out setup, out int length) && length == bytes.Length)
            {
                return true;
            }
            setup = null;
            return false;
        }

        // 기록 파일(WZDR)의 앞머리에서 읽는다. length = 머리말의 길이(input_delay 두 칸이 시작하는 자리). 뒤에 남는 바이트는 보지 않는다.
        public static bool TryReadCanonicalPrefix(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out DuelSetup? setup, out int length)
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
            if (!r.TryBytes(UuidBytes.Size, out ReadOnlySpan<byte> matchId) || !r.TryU64(out ulong seed))
            {
                return false;
            }
            if (!TryReadOptions(ref r, out DuelRoomOptions? options))
            {
                return false;
            }
            var players = new DuelPlayerSetup[DuelPlayers];
            for (int i = 0; i < DuelPlayers; i++)
            {
                if (!TryReadPlayer(ref r, i, out DuelPlayerSetup? player))
                {
                    return false;
                }
                players[i] = player;
            }

            char[] hex = new char[CoreVersionChars];
            for (int i = 0; i < CoreVersionBytes; i++)
            {
                hex[i * 2] = HexDigits[coreVersion[i] >> NibbleBits];
                hex[i * 2 + 1] = HexDigits[coreVersion[i] & NibbleMask];
            }

            setup = new DuelSetup
            {
                MatchId = UuidBytes.Read(matchId),
                Seed = seed,
                Options = options,
                Players = players,
                CoreVersion = new string(hex),
                DataVersion = (int)dataVersion,
            };
            length = r.Position;
            return true;
        }

        // options: 명세 표(spec 10 9.2)의 순서. 상한 둘은 i16 이고 −1 이 "없음"이다
        private static bool TryReadOptions(ref ByteReader r, [NotNullWhen(true)] out DuelRoomOptions? options)
        {
            options = null;
            if (!r.TryU8(out byte rounds) || !r.TryU16(out ushort roundSeconds) || !r.TryU16(out ushort hpMultPermille)
                || !r.TryU8(out byte enhanceCap))
            {
                return false;
            }
            if (!r.TryI16(out short ilCap) || ilCap < DuelRoomOptions.NoCap)
            {
                return false;
            }
            if (!TryBool(ref r, out bool allowMutations))
            {
                return false;
            }
            if (!r.TryI16(out short heroBlockCap) || heroBlockCap < DuelRoomOptions.NoCap)
            {
                return false;
            }
            if (!TryBool(ref r, out bool equalize) || !TryBool(ref r, out bool wildIntrusion) || !TryBool(ref r, out bool thornsOff))
            {
                return false;
            }
            if (!r.TryU8(out byte floorColor) || !TryBool(ref r, out bool spectate))
            {
                return false;
            }
            options = new DuelRoomOptions
            {
                Rounds = rounds,
                RoundSeconds = roundSeconds,
                HpMultPermille = hpMultPermille,
                EnhanceCap = enhanceCap,
                IlCap = ilCap,
                AllowMutations = allowMutations,
                HeroBlockCap = heroBlockCap,
                Equalize = equalize,
                WildIntrusion = wildIntrusion,
                ThornsOff = thornsOff,
                FloorColor = (TimeOfDay)floorColor,
                Spectate = spectate,
            };
            return true;
        }

        // player: slot u8(= 자리 번호), 적용된 지팡이(.wzrc 의 wand_snapshot 과 같은 배치), actual_stage u8, max_hp_points u32, unlocked_arts u16
        private static bool TryReadPlayer(ref ByteReader r, int index, [NotNullWhen(true)] out DuelPlayerSetup? player)
        {
            player = null;
            if (!r.TryU8(out byte slot) || slot != index)
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
            if (!r.TryU8(out byte actualStage))
            {
                return false;
            }
            if (!r.TryU32(out uint maxHpPoints) || maxHpPoints > int.MaxValue)
            {
                return false;
            }
            if (!r.TryU16(out ushort unlockedArts))
            {
                return false;
            }

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
            player = new DuelPlayerSetup
            {
                Slot = new PlayerSlot(slot),
                AppliedWand = wand,
                ActualStage = actualStage,
                MaxHpPoints = (int)maxHpPoints,
                UnlockedArts = (ArtMask)unlockedArts,
            };
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
            DuelRoomOptions o = Options;
            if (o == null)
            {
                return "Options is null";
            }
            if (o.Rounds < 0 || o.Rounds > byte.MaxValue)
            {
                return "Options.Rounds does not fit u8";
            }
            if (o.RoundSeconds < 0 || o.RoundSeconds > ushort.MaxValue)
            {
                return "Options.RoundSeconds does not fit u16";
            }
            if (o.HpMultPermille < 0 || o.HpMultPermille > ushort.MaxValue)
            {
                return "Options.HpMultPermille does not fit u16";
            }
            if (o.EnhanceCap < 0 || o.EnhanceCap > byte.MaxValue)
            {
                return "Options.EnhanceCap does not fit u8";
            }
            if (o.IlCap < DuelRoomOptions.NoCap || o.IlCap > short.MaxValue)
            {
                return "Options.IlCap is neither NoCap nor a value that fits i16";
            }
            if (o.HeroBlockCap < DuelRoomOptions.NoCap || o.HeroBlockCap > short.MaxValue)
            {
                return "Options.HeroBlockCap is neither NoCap nor a value that fits i16";
            }
            if (Players == null || Players.Length != DuelPlayers)
            {
                return "Players must have exactly two entries";
            }
            for (int i = 0; i < DuelPlayers; i++)
            {
                string? problem = FindUnwritablePlayer(Players[i], i);
                if (problem != null)
                {
                    return problem;
                }
            }
            return null;
        }

        private static string? FindUnwritablePlayer(DuelPlayerSetup? p, int index)
        {
            if (p == null)
            {
                return "Players has a null entry";
            }
            if (p.Slot.Value != index)
            {
                return "Players[i].Slot must be i";
            }
            WandSnapshot wand = p.AppliedWand;
            if (wand == null)
            {
                return "AppliedWand is null";
            }
            if (wand.Stage < 0 || wand.Stage > byte.MaxValue)
            {
                return "AppliedWand.Stage does not fit u8";
            }
            if (wand.SlotsExtra < 0 || wand.SlotsExtra > byte.MaxValue)
            {
                return "AppliedWand.SlotsExtra does not fit u8";
            }
            if (wand.Mutations == null || wand.Mutations.Length > byte.MaxValue)
            {
                return "AppliedWand.Mutations is null or longer than 255";
            }
            if (wand.Links == null || wand.Links.Length > byte.MaxValue)
            {
                return "AppliedWand.Links is null or longer than 255";
            }
            if (p.ActualStage < 0 || p.ActualStage > byte.MaxValue)
            {
                return "ActualStage does not fit u8";
            }
            if (p.MaxHpPoints < 0)
            {
                return "MaxHpPoints is negative";
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

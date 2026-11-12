using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Wizard.Core;

namespace Wizard.Text
{
    // CONTRACTS 5절, spec 14 2.2절. DataBuild 가 data/names/names.<locale>.json 을 검사해 만든 이진 절(names.<locale>.wznm)을 읽는다.
    // 바이트의 꼴은 tools/Wizard.DataBuild/Names/NameTableWriter.cs 가 정본이다:
    //   magic "WZNM"(4), version u16, locale(varuint 바이트 수 + UTF-8), count u32,
    //   항목 × count: 열쇠(varuint + UTF-8), 값(varuint + UTF-8) — 열쇠의 서수 오름차순, 중복 없음,
    //   sha256[32](앞의 모든 바이트). 수는 리틀 엔디언.
    // 공유 코드에 JSON 파서를 들이지 않는다(Wizard.Text 는 Core 에만 기댄다. ARCHITECTURE 8.3).
    // 값은 원문 그대로다(틀이면 {…} 가 남아 있다). 틀 해석은 TextResolver 가 한다.
    // 이진 절은 빌드에 들어가는 파일이라 깨졌으면 빌드가 틀린 것이다: Load 는 InvalidDataException 을 던진다(CONTRACTS 0.2 "예외는 프로그래머 오류에만").
    public sealed class NameTable
    {
        public const ushort FormatVersion = 1;    // NameTableWriter.FormatVersion 과 같아야 한다
        private const int MagicLength = 4;
        private const int ShaLength = 32;
        private const int MinEntryBytes = 2;      // 빈 열쇠·빈 값이어도 길이 varuint 가 하나씩 있다

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly Dictionary<string, string> _entries;

        private NameTable(string locale, int version, Dictionary<string, string> entries)
        {
            Locale = locale;
            Version = version;
            _entries = entries;
        }

        public string Locale { get; }

        public int Version { get; }

        public int Count => _entries.Count;

        public static NameTable Load(ReadOnlySpan<byte> pack)
        {
            if (pack.Length < MagicLength + 2 + 1 + 4 + ShaLength)
            {
                throw new InvalidDataException("name table too short");
            }
            if (pack[0] != (byte)'W' || pack[1] != (byte)'Z' || pack[2] != (byte)'N' || pack[3] != (byte)'M')
            {
                throw new InvalidDataException("name table magic mismatch");
            }
            int bodyLength = pack.Length - ShaLength;
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(pack.Slice(0, bodyLength).ToArray());
            }
            if (!pack.Slice(bodyLength, ShaLength).SequenceEqual(digest))
            {
                throw new InvalidDataException("name table sha256 mismatch");
            }

            var r = new ByteReader(pack.Slice(MagicLength, bodyLength - MagicLength));
            if (!r.TryU16(out ushort version))
            {
                throw new InvalidDataException("name table header truncated");
            }
            if (version != FormatVersion)
            {
                throw new InvalidDataException("name table version " + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is not supported");
            }
            string locale = ReadString(ref r, "locale");
            if (locale.Length == 0)
            {
                throw new InvalidDataException("name table locale is empty");
            }
            if (!r.TryU32(out uint count))
            {
                throw new InvalidDataException("name table header truncated");
            }
            if (count > (uint)(r.Remaining / MinEntryBytes))
            {
                throw new InvalidDataException("name table entry count exceeds the data");
            }
            var entries = new Dictionary<string, string>((int)count, StringComparer.Ordinal);
            string? previous = null;
            for (uint i = 0; i < count; i++)
            {
                string key = ReadString(ref r, "key");
                string value = ReadString(ref r, "value");
                if (key.Length == 0)
                {
                    throw new InvalidDataException("name table has an empty key");
                }
                // DataBuild 가 서수 오름차순으로 쓴다(CONTRACTS 5절). 어긋나면 다른 도구가 만든 바이트다
                if (previous != null && string.CompareOrdinal(previous, key) >= 0)
                {
                    throw new InvalidDataException("name table keys are not in ascending ordinal order at '" + key + "'");
                }
                entries.Add(key, value);
                previous = key;
            }
            if (r.Remaining != 0)
            {
                throw new InvalidDataException("name table has trailing bytes before sha256");
            }
            return new NameTable(locale, version, entries);
        }

        // 별칭(spec 14 1.2: master → npc.master, field/plaza/arena → place.*)을 푼 뒤 찾는다. 값은 원문(틀이면 {…} 그대로)
        public bool TryGet(string nameKey, out string value)
        {
            if (nameKey == null)
            {
                throw new ArgumentNullException(nameof(nameKey));
            }
            if (_entries.TryGetValue(NameAliases.Resolve(nameKey), out string? v))
            {
                value = v;
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static string ReadString(ref ByteReader r, string what)
        {
            if (!r.TryVarUInt(out ulong length) || length > (ulong)r.Remaining || !r.TryBytes((int)length, out ReadOnlySpan<byte> bytes))
            {
                throw new InvalidDataException("name table " + what + " truncated");
            }
            try
            {
                return StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("name table " + what + " is not valid UTF-8");
            }
        }
    }
}

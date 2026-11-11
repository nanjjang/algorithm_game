using System;
using System.Security.Cryptography;
using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.3, 11.2. 콘텐츠 팩 .wzpack 의 머리말과 절 목차를 읽고 해시를 확인한다.
    //   magic "WZPK"(4)  schema u16  data_version u32  core_compat [16]
    //   section_count u16, 목차: { section_id u16, offset u32, length u32 } …
    //   섹션들(id 오름차순)
    //   sha256 [32] (앞의 모든 바이트)
    // 절의 내용을 정의 타입으로 바꾸는 것은 ContentDb.<절>.cs 가 한다. 모르는 절은 건너뛴다.
    public sealed class ContentPack
    {
        public const int ShaLength = 32;
        private const int CoreCompatLength = 16;
        private const int MagicLength = 4;

        private readonly byte[] _bytes;
        private readonly ushort[] _sectionIds;
        private readonly uint[] _offsets;
        private readonly uint[] _lengths;

        private ContentPack(byte[] bytes, int schema, int dataVersion, string coreCompat, ushort[] ids, uint[] offsets, uint[] lengths)
        {
            _bytes = bytes;
            SchemaVersion = schema;
            DataVersion = dataVersion;
            CoreCompat = coreCompat;
            _sectionIds = ids;
            _offsets = offsets;
            _lengths = lengths;
        }

        public int SchemaVersion { get; }

        public int DataVersion { get; }       // 판마다 기록하는 데이터 판 번호

        public string CoreCompat { get; }     // 이 팩이 맞는 코어 버전(SimVersion.CoreVersion, hex 32자)

        public ReadOnlyMemory<byte> Sha256 => new ReadOnlyMemory<byte>(_bytes, _bytes.Length - ShaLength, ShaLength);

        public int SectionCount => _sectionIds.Length;

        public bool TryGetSection(ushort sectionId, out ReadOnlyMemory<byte> section)
        {
            for (int i = 0; i < _sectionIds.Length; i++)
            {
                if (_sectionIds[i] == sectionId)
                {
                    section = new ReadOnlyMemory<byte>(_bytes, (int)_offsets[i], (int)_lengths[i]);
                    return true;
                }
            }
            section = default;
            return false;
        }

        // 형식·해시가 틀리면 ContentException
        public static ContentPack Load(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < MagicLength + 2 + 4 + CoreCompatLength + 2 + ShaLength)
            {
                throw new ContentException("pack too short");
            }
            if (bytes[0] != (byte)'W' || bytes[1] != (byte)'Z' || bytes[2] != (byte)'P' || bytes[3] != (byte)'K')
            {
                throw new ContentException("pack magic mismatch");
            }
            int bodyLength = bytes.Length - ShaLength;
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(bytes.Slice(0, bodyLength).ToArray());
            }
            if (!bytes.Slice(bodyLength, ShaLength).SequenceEqual(digest))
            {
                throw new ContentException("pack sha256 mismatch");
            }
            var r = new ByteReader(bytes.Slice(MagicLength, bodyLength - MagicLength));
            if (!r.TryU16(out ushort schema) || !r.TryU32(out uint dataVersion) || !r.TryBytes(CoreCompatLength, out ReadOnlySpan<byte> compat)
                || !r.TryU16(out ushort count))
            {
                throw new ContentException("pack header truncated");
            }
            var ids = new ushort[count];
            var offsets = new uint[count];
            var lengths = new uint[count];
            for (int i = 0; i < count; i++)
            {
                if (!r.TryU16(out ids[i]) || !r.TryU32(out offsets[i]) || !r.TryU32(out lengths[i]))
                {
                    throw new ContentException("pack table of contents truncated");
                }
                if (i > 0 && ids[i] <= ids[i - 1])
                {
                    throw new ContentException("pack sections not in ascending id order");
                }
                if ((ulong)offsets[i] + lengths[i] > (ulong)bodyLength)
                {
                    throw new ContentException("pack section out of range");
                }
            }
            if (dataVersion > int.MaxValue)
            {
                throw new ContentException("pack data_version out of range");
            }
            return new ContentPack(bytes.ToArray(), schema, (int)dataVersion, Hex(compat), ids, offsets, lengths);
        }

        private static string Hex(ReadOnlySpan<byte> b)
        {
            const string digits = "0123456789abcdef";
            var chars = new char[b.Length * 2];
            for (int i = 0; i < b.Length; i++)
            {
                chars[2 * i] = digits[b[i] >> 4];
                chars[2 * i + 1] = digits[b[i] & 0xF];
            }
            return new string(chars);
        }
    }
}

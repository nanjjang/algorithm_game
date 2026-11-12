using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Wizard.Core;

namespace Wizard.DataBuild.Core
{
    // CONTRACTS 11.2 의 .wzpack 쓰기(읽기는 Wizard.Content.ContentPack.Load). 모든 수는 리틀 엔디언(ByteWriter).
    //   magic "WZPK"(4)  schema u16  data_version u32  core_compat [16]
    //   section_count u16, 목차: { section_id u16, offset u32, length u32 } × section_count   (offset 은 파일 처음부터)
    //   섹션들(id 오름차순으로 빈틈 없이 이어 붙인다)
    //   sha256 [32] (앞의 모든 바이트)
    public static class PackWriter
    {
        public const ushort SchemaVersion = 1;          // 팩 형식 판. 형식을 바꾸면 올린다(계약 변경)
        public const int CoreCompatLength = 16;
        public const int ShaLength = 32;
        public const int HeaderLength = 4 + 2 + 4 + CoreCompatLength + 2;
        public const int TocEntryLength = 2 + 4 + 4;

        public static byte[] Write(ushort schema, uint dataVersion, ReadOnlySpan<byte> coreCompat, IReadOnlyList<PackSection> sections)
        {
            if (coreCompat.Length != CoreCompatLength)
            {
                throw new ArgumentException("core_compat must be 16 bytes", nameof(coreCompat));
            }
            if (sections.Count > ushort.MaxValue)
            {
                throw new ArgumentException("too many sections", nameof(sections));
            }
            for (int i = 1; i < sections.Count; i++)
            {
                if (sections[i].Id <= sections[i - 1].Id)
                {
                    throw new ArgumentException("sections must be in strictly ascending id order", nameof(sections));
                }
            }
            var w = new ByteWriter(HeaderLength + sections.Count * TocEntryLength + ShaLength);
            w.U8((byte)'W');
            w.U8((byte)'Z');
            w.U8((byte)'P');
            w.U8((byte)'K');
            w.U16(schema);
            w.U32(dataVersion);
            w.Bytes(coreCompat);
            w.U16((ushort)sections.Count);
            long offset = HeaderLength + (long)sections.Count * TocEntryLength;
            foreach (PackSection s in sections)
            {
                if (offset + s.Body.Length > uint.MaxValue)
                {
                    throw new ArgumentException("pack larger than 4 GiB", nameof(sections));
                }
                w.U16(s.Id);
                w.U32((uint)offset);
                w.U32((uint)s.Body.Length);
                offset += s.Body.Length;
            }
            foreach (PackSection s in sections)
            {
                w.Bytes(s.Body);
            }
            byte[] sha = SHA256.HashData(w.Written);
            w.Bytes(sha);
            return w.ToArray();
        }

        // SimVersion.CoreVersion(hex 32자) → 16바이트
        public static byte[] CoreCompatFromHex(string hex)
        {
            if (hex.Length != CoreCompatLength * 2)
            {
                throw new ArgumentException("core version must be 32 hex digits", nameof(hex));
            }
            var b = new byte[CoreCompatLength];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (byte)((Nibble(hex[2 * i]) << 4) | Nibble(hex[2 * i + 1]));
            }
            return b;
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            throw new ArgumentException("core version must be lowercase hex: '" + c + "'");
        }
    }
}

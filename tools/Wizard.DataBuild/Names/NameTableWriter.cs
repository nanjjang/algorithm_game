using System.Security.Cryptography;
using System.Text;
using Wizard.Core;

namespace Wizard.DataBuild.Names
{
    // 이름 표의 이진 절 names.<locale>.wznm(CONTRACTS 5절. 읽는 쪽은 Wizard.Text.NameTable.Load, ByteReader 로 읽는다).
    // CONTRACTS 5절은 "magic, version u16, locale, count u32, (열쇠 UTF-8, 값 UTF-8)…, sha256" 까지만 정했다. 바이트의 꼴은 이렇다:
    //   magic "WZNM"(4)
    //   version u16                     형식 판(FormatVersion). [임시] 표 내용의 판 번호 자리가 따로 정해지지 않아 형식 판을 싣는다
    //   locale  : varuint 바이트 수 + UTF-8("ko")
    //   count u32
    //   항목 × count : 열쇠(varuint 바이트 수 + UTF-8), 값(varuint 바이트 수 + UTF-8)   ← 열쇠의 서수 오름차순, 표지 "~end" 는 싣지 않는다
    //   sha256 [32] (앞의 모든 바이트)
    // 수는 리틀 엔디언, varuint 는 LEB128(ByteWriter.VarUInt). 문자열은 정규화하지 않고 원본의 글자 그대로다.
    public static class NameTableWriter
    {
        public const ushort FormatVersion = 1;
        public const string FileExtension = ".wznm";

        public static string FileName(string locale) => "names." + locale + FileExtension;

        public static byte[] Write(NamesFile names) => Write(names, NamesFile.Locale);

        public static byte[] Write(NamesFile names, string locale)
        {
            var w = new ByteWriter(4096);
            w.U8((byte)'W');
            w.U8((byte)'Z');
            w.U8((byte)'N');
            w.U8((byte)'M');
            w.U16(FormatVersion);
            Str(w, locale);
            w.U32((uint)names.Entries.Count);
            foreach (NameEntry e in names.Entries)
            {
                Str(w, e.Key);
                Str(w, e.Value);
            }
            byte[] sha = SHA256.HashData(w.Written);
            w.Bytes(sha);
            return w.ToArray();
        }

        private static void Str(ByteWriter w, string s)
        {
            byte[] b = new UTF8Encoding(false, true).GetBytes(s);
            w.VarUInt((ulong)b.Length);
            w.Bytes(b);
        }
    }
}

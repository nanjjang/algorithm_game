using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 프로필 파일의 꼴(07 요약서 9.3절, C 11.3): UTF-8(BOM 없음) JSON 본문 + 줄바꿈 + 마지막 줄에 본문의 SHA-256(16진 소문자).
    /// 깨짐 감지용이고 변조 방지가 아니다(고치기는 막지 않는다).
    /// </summary>
    public static class ProfileFile
    {
        static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Sha256Hex(string body)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8NoBom.GetBytes(body));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>본문 → 파일 글자(본문, 줄바꿈, 해시, 줄바꿈).</summary>
        public static string Compose(string body) => body + "\n" + Sha256Hex(body) + "\n";

        /// <summary>파일 글자에서 본문을 꺼낸다. 마지막 줄의 해시가 본문과 맞을 때만 true.</summary>
        public static bool TrySplit(string text, out string body)
        {
            body = null;
            if (string.IsNullOrEmpty(text)) return false;
            if (text[0] == '﻿') text = text.Substring(1);
            string t = text.TrimEnd('\r', '\n', ' ', '\t');
            int nl = t.LastIndexOf('\n');
            if (nl <= 0) return false;
            string hash = t.Substring(nl + 1).Trim();
            string b = t.Substring(0, nl);
            if (b.EndsWith("\r")) b = b.Substring(0, b.Length - 1);
            if (!string.Equals(hash, Sha256Hex(b), StringComparison.OrdinalIgnoreCase)) return false;
            body = b;
            return true;
        }

        public static string ReadText(string path) => Utf8NoBom.GetString(File.ReadAllBytes(path));

        /// <summary>
        /// 원자적 쓰기의 앞 절반: 임시 파일에 다 쓰고 디스크까지 내린다. 이름 바꾸기는 <see cref="ReplaceWith"/>.
        /// </summary>
        public static void WriteTemp(string tempPath, string text)
        {
            byte[] bytes = Utf8NoBom.GetBytes(text);
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }

        /// <summary>임시 파일을 dest 자리로 옮긴다(dest가 있으면 갈아 끼운다). 끝나면 임시 파일은 없다.</summary>
        public static void ReplaceWith(string tempPath, string destPath)
        {
            if (File.Exists(destPath))
            {
                try
                {
                    File.Replace(tempPath, destPath, null);
                    return;
                }
                catch (Exception e) when (e is PlatformNotSupportedException || e is IOException || e is UnauthorizedAccessException)
                {
                    // File.Replace가 안 되는 파일 체계: 지우고 옮긴다. 그 사이에 꺼지면 .1 세대(이미 복사됨)가 남는다.
                    if (!File.Exists(tempPath)) throw;
                    File.Delete(destPath);
                }
            }
            File.Move(tempPath, destPath);
        }

        /// <summary>src를 dest로 옮긴다(dest가 있으면 먼저 지운다).</summary>
        public static void MoveOver(string srcPath, string destPath)
        {
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(srcPath, destPath);
        }
    }
}

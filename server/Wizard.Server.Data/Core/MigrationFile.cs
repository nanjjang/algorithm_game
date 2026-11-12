using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.Server.Data
{
    // server/migrations/ 의 파일 하나: NNNN_<이름>.sql (번호 네 자리, 이름은 소문자·숫자·밑줄. DECISIONS-TECH T-24).
    // 체크섬 = UTF-8 BOM 을 떼고 CRLF 를 LF 로 바꾼 바이트의 SHA-256. 체크아웃의 줄 끝 설정만 다른 파일은 같은 파일로 본다.
    public sealed class MigrationFile
    {
        private static readonly Regex FileName = new Regex("^([0-9]{4})_([a-z0-9_]+)\\.sql$", RegexOptions.CultureInvariant);

        private MigrationFile(int version, string name, string sql, byte[] checksum)
        {
            Version = version;
            Name = name;
            Sql = sql;
            Checksum = checksum;
        }

        public int Version { get; }

        public string Name { get; }      // 파일 이름 전체

        public string Sql { get; }

        public byte[] Checksum { get; }

        public static MigrationFile FromBytes(string fileName, byte[] content)
        {
            Match m = FileName.Match(fileName);
            if (!m.Success)
            {
                throw new MigrationException("migration file name must be NNNN_name.sql: " + fileName);
            }
            byte[] normalized = Normalize(content);
            int version = int.Parse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            return new MigrationFile(version, fileName, new UTF8Encoding(false, true).GetString(normalized), SHA256.HashData(normalized));
        }

        // 폴더의 *.sql 전부를 번호순으로. 꼴이 틀린 이름이나 겹치는 번호가 있으면 하나도 적용하지 않고 거절한다
        public static IReadOnlyList<MigrationFile> LoadDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new MigrationException("migration directory not found: " + directory);
            }
            var files = new List<MigrationFile>();
            foreach (string path in Directory.GetFiles(directory, "*.sql", SearchOption.TopDirectoryOnly))
            {
                files.Add(FromBytes(Path.GetFileName(path), File.ReadAllBytes(path)));
            }
            files.Sort((a, b) => a.Version.CompareTo(b.Version));
            for (int i = 1; i < files.Count; i++)
            {
                if (files[i].Version == files[i - 1].Version)
                {
                    throw new MigrationException("duplicate migration number: " + files[i - 1].Name + ", " + files[i].Name);
                }
            }
            return files;
        }

        private static byte[] Normalize(byte[] content)
        {
            int start = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
            var output = new List<byte>(content.Length);
            for (int i = start; i < content.Length; i++)
            {
                if (content[i] == (byte)'\r' && i + 1 < content.Length && content[i + 1] == (byte)'\n')
                {
                    continue;
                }
                output.Add(content[i]);
            }
            return output.ToArray();
        }
    }
}

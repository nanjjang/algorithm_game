using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wizard.AssetCheck.Report
{
    // 검수 승인 장부 art/production/approved.json(PIPELINE 6.4절 ⑨, 6.5절): (자리 id, 파일 SHA-256).
    // 파일이 바뀌면 해시가 달라져 승인이 풀린다("검수됨" → "반입됨"). 파일이 없으면 빈 장부다.
    // 쓰는 것은 assetcheck approve 뿐이고 줄은 자리 id 의 서수 순, 줄바꿈 LF 다(같은 내용이면 같은 바이트).
    public sealed class ApprovalLedger
    {
        public const string RepoPath = "art/production/approved.json";

        private readonly SortedDictionary<string, ApprovalEntry> _entries = new SortedDictionary<string, ApprovalEntry>(StringComparer.Ordinal);

        public IEnumerable<ApprovalEntry> Entries => _entries.Values;

        public int Count => _entries.Count;

        public bool TryGet(string slotId, out ApprovalEntry entry)
        {
            if (_entries.TryGetValue(slotId, out ApprovalEntry? e))
            {
                entry = e;
                return true;
            }
            entry = null!;
            return false;
        }

        public void Set(ApprovalEntry entry) => _entries[entry.SlotId] = entry;

        public static ApprovalLedger Load(string path)
        {
            var ledger = new ApprovalLedger();
            if (!File.Exists(path))
            {
                return ledger;
            }
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, new UTF8Encoding(false))))
            {
                if (!doc.RootElement.TryGetProperty("approved", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException(RepoPath + ": 'approved' must be an array");
                }
                foreach (JsonElement e in list.EnumerateArray())
                {
                    string id = e.GetProperty("id").GetString() ?? "";
                    string file = e.GetProperty("file").GetString() ?? "";
                    string sha = e.GetProperty("sha256").GetString() ?? "";
                    if (id.Length == 0 || sha.Length != Sha256HexLength)
                    {
                        throw new InvalidDataException(RepoPath + ": bad entry '" + id + "'");
                    }
                    ledger.Set(new ApprovalEntry(id, file, sha));
                }
            }
            return ledger;
        }

        public void Save(string path)
        {
            var options = new JsonWriterOptions { Indented = true, NewLine = "\n" };
            using (var ms = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms, options))
                {
                    w.WriteStartObject();
                    w.WriteString("_note", "assetcheck approve <slot id> writes this file (PIPELINE 6.4 step 9). An approval is bound to the SHA-256 of the file; a changed file is no longer approved.");
                    w.WriteStartArray("approved");
                    foreach (ApprovalEntry e in _entries.Values)
                    {
                        w.WriteStartObject();
                        w.WriteString("id", e.SlotId);
                        w.WriteString("file", e.File);
                        w.WriteString("sha256", e.Sha256);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                ms.WriteByte((byte)'\n');
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        private const int Sha256HexLength = 64;

        public static string Sha256Of(string fullPath)
        {
            using (FileStream fs = File.OpenRead(fullPath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(fs);
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h)
                {
                    sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }
    }
}

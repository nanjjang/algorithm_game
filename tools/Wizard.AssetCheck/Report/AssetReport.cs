using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Slots;
using Wizard.ViewLogic.Assets;

namespace Wizard.AssetCheck.Report
{
    // assetcheck report(PIPELINE 6.4절 ①, 6.5절): 자리마다 없음/반입됨/검수됨/실패를 artifacts/asset-report.json 으로 낸다.
    // 자리 = 매니페스트의 기록(열쇠, 크기가 여럿이면 크기마다) + 콘텐츠 id 가 요구하는데 없는 그림 + asset_slots.json 의 없는 자리.
    // 출력은 결정적이다(자리 id 의 서수 순, 시각 없음, 줄바꿈 LF).
    public sealed class AssetReport
    {
        public const string DefaultOutput = "artifacts/asset-report.json";
        public const int Schema = 1;

        private AssetReport(IReadOnlyList<SlotReport> slots, IReadOnlyList<Finding> findings, int slotTableCount)
        {
            Slots = slots;
            Findings = findings;
            SlotTableCount = slotTableCount;
        }

        public IReadOnlyList<SlotReport> Slots { get; }

        public IReadOnlyList<Finding> Findings { get; }

        // asset_slots.json 의 장수 합(TV-196: 반응 부품 71)
        public int SlotTableCount { get; }

        public int CountOf(SlotStatus s) => Slots.Count(x => x.Status == s);

        public SlotReport? Find(string slotId) => Slots.FirstOrDefault(s => string.Equals(s.SlotId, slotId, StringComparison.Ordinal));

        public static AssetReport Build(ImportCheckResult result, ApprovalLedger ledger)
        {
            AssetCheckInput input = result.Input;
            AssetRegistry reg = input.Registry;
            var errorsByFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var errorsBySlot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Finding f in result.Errors)
            {
                Add(f.Slot != null ? errorsBySlot : errorsByFile, f.Slot ?? f.File, "[" + f.Check + "] " + f.Message);
            }

            var slots = new List<SlotReport>();
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (AssetRecord rec in reg.Records)
            {
                string id = SlotIds.Of(rec, reg);
                var problems = new List<string>();
                if (errorsBySlot.TryGetValue(id, out List<string>? own))
                {
                    problems.AddRange(own);
                }
                foreach (string file in ImportChecker.TextureFiles(rec).Select(ImportChecker.RepoPath).Append(ImportChecker.RepoPath(ImportChecker.ManifestOf(rec.Key.Pack))))
                {
                    if (errorsByFile.TryGetValue(file, out List<string>? fileErrors))
                    {
                        problems.AddRange(fileErrors.Select(e => file + ": " + e));
                    }
                }
                string? sha = null;
                if (input.Images.Exists(rec.File))
                {
                    if (!hashes.TryGetValue(rec.File, out string? h))
                    {
                        h = ApprovalLedger.Sha256Of(input.Images.FullPath(rec.File));
                        hashes.Add(rec.File, h);
                    }
                    sha = h;
                }
                SlotStatus status;
                bool stale = false;
                if (problems.Count > 0)
                {
                    status = SlotStatus.Failed;
                }
                else if (ledger.TryGet(id, out ApprovalEntry a) && sha != null)
                {
                    bool same = string.Equals(a.Sha256, sha, StringComparison.Ordinal) && string.Equals(a.File, rec.File, StringComparison.Ordinal);
                    status = same ? SlotStatus.Approved : SlotStatus.Imported;
                    stale = !same;
                }
                else
                {
                    status = SlotStatus.Imported;
                }
                slots.Add(new SlotReport(id, rec.Key, status, rec.File, sha, rec.FrameSize, rec.FrameCount, stale, problems, null));
            }
            foreach (MissingSlot m in result.Missing)
            {
                slots.Add(new SlotReport(m.SlotId, m.Key, SlotStatus.Missing, null, null, m.FrameSize, m.Count, false, new string[0], m.Reason));
            }
            slots.Sort((a, b) => string.CompareOrdinal(a.SlotId, b.SlotId));
            return new AssetReport(slots, result.Findings, input.Slots.TotalCount);
        }

        private static void Add(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out List<string>? list))
            {
                list = new List<string>();
                map.Add(key, list);
            }
            list.Add(value);
        }

        public static string StatusName(SlotStatus s)
        {
            switch (s)
            {
                case SlotStatus.Missing: return "missing";
                case SlotStatus.Imported: return "imported";
                case SlotStatus.Approved: return "approved";
                default: return "failed";
            }
        }

        public byte[] ToJson()
        {
            var options = new JsonWriterOptions { Indented = true, NewLine = "\n" };
            using (var ms = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms, options))
                {
                    w.WriteStartObject();
                    w.WriteNumber("schema", Schema);
                    w.WriteStartObject("summary");
                    w.WriteNumber("slots", Slots.Count);
                    w.WriteNumber("missing", CountOf(SlotStatus.Missing));
                    w.WriteNumber("imported", CountOf(SlotStatus.Imported));
                    w.WriteNumber("approved", CountOf(SlotStatus.Approved));
                    w.WriteNumber("failed", CountOf(SlotStatus.Failed));
                    w.WriteNumber("errors", Findings.Count(f => f.IsError));
                    w.WriteNumber("warnings", Findings.Count(f => !f.IsError));
                    w.WriteNumber("slot_table_count", SlotTableCount);
                    w.WriteEndObject();
                    w.WriteStartArray("slots");
                    foreach (SlotReport s in Slots)
                    {
                        w.WriteStartObject();
                        w.WriteString("id", s.SlotId);
                        w.WriteString("pack", AssetSlotTable.PackName(s.Key.Pack));
                        w.WriteString("status", StatusName(s.Status));
                        if (s.File != null)
                        {
                            w.WriteString("file", ImportChecker.RepoPath(s.File));
                        }
                        if (s.Sha256 != null)
                        {
                            w.WriteString("sha256", s.Sha256);
                        }
                        if (s.FrameSize.HasValue)
                        {
                            w.WriteStartArray("frame_size");
                            w.WriteNumberValue(s.FrameSize.Value.Width);
                            w.WriteNumberValue(s.FrameSize.Value.Height);
                            w.WriteEndArray();
                        }
                        if (s.Frames > 0)
                        {
                            w.WriteNumber(s.Status == SlotStatus.Missing ? "count" : "frames", s.Frames);
                        }
                        if (s.ApprovalStale)
                        {
                            w.WriteBoolean("approval_stale", true);
                        }
                        if (s.Reason != null)
                        {
                            w.WriteString("reason", s.Reason);
                        }
                        if (s.Problems.Count > 0)
                        {
                            w.WriteStartArray("problems");
                            foreach (string p in s.Problems)
                            {
                                w.WriteStringValue(p);
                            }
                            w.WriteEndArray();
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteStartArray("findings");
                    foreach (Finding f in Findings)
                    {
                        w.WriteStartObject();
                        w.WriteString("severity", f.IsError ? "error" : "warning");
                        w.WriteString("check", f.Check);
                        w.WriteString("file", f.File);
                        if (f.Slot != null)
                        {
                            w.WriteString("slot", f.Slot);
                        }
                        w.WriteString("message", f.Message);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                ms.WriteByte((byte)'\n');
                return ms.ToArray();
            }
        }

        public string SummaryLine() =>
            "assetcheck report: " + Slots.Count.ToString(CultureInfo.InvariantCulture) + " slot(s): "
            + CountOf(SlotStatus.Missing).ToString(CultureInfo.InvariantCulture) + " missing, "
            + CountOf(SlotStatus.Imported).ToString(CultureInfo.InvariantCulture) + " imported, "
            + CountOf(SlotStatus.Approved).ToString(CultureInfo.InvariantCulture) + " approved, "
            + CountOf(SlotStatus.Failed).ToString(CultureInfo.InvariantCulture) + " failed";
    }
}

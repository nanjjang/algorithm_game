using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Slots
{
    // data/presentation/asset_slots.json(DECISIONS-TECH T-58 5번): 매니페스트에 없는 그림 자리의 표.
    // 줄 순서는 파일 순서 그대로다. 같은 열쇠가 두 번이면 문제로 적는다
    public sealed class AssetSlotTable
    {
        public const string RepoPath = "data/presentation/asset_slots.json";

        private AssetSlotTable(IReadOnlyList<AssetSlotEntry> entries, IReadOnlyList<string> problems)
        {
            Entries = entries;
            Problems = problems;
        }

        public IReadOnlyList<AssetSlotEntry> Entries { get; }

        public IReadOnlyList<string> Problems { get; }

        public int TotalCount
        {
            get
            {
                int n = 0;
                foreach (AssetSlotEntry e in Entries)
                {
                    n += e.Count;
                }
                return n;
            }
        }

        public static AssetSlotTable Empty() => new AssetSlotTable(new AssetSlotEntry[0], new string[0]);

        public static AssetSlotTable Parse(string json)
        {
            var entries = new List<AssetSlotEntry>();
            var problems = new List<string>();
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                problems.Add("not JSON: " + e.Message);
                return new AssetSlotTable(entries, problems);
            }
            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("slots", out JsonElement slots)
                    || slots.ValueKind != JsonValueKind.Array)
                {
                    problems.Add("'slots' must be an array");
                    return new AssetSlotTable(entries, problems);
                }
                var seen = new HashSet<AssetKey>(AssetKeyComparer.Instance);
                int index = 0;
                foreach (JsonElement s in slots.EnumerateArray())
                {
                    string at = "/slots/" + index.ToString(CultureInfo.InvariantCulture);
                    index++;
                    if (!TryEntry(s, out AssetSlotEntry? entry, out string why))
                    {
                        problems.Add(at + ": " + why);
                        continue;
                    }
                    if (!seen.Add(entry!.Key))
                    {
                        problems.Add(at + ": duplicate slot " + AssetKeyComparer.Format(entry.Key));
                        continue;
                    }
                    entries.Add(entry);
                }
            }
            return new AssetSlotTable(entries, problems);
        }

        private static bool TryEntry(JsonElement s, out AssetSlotEntry? entry, out string why)
        {
            entry = null;
            why = "";
            if (s.ValueKind != JsonValueKind.Object)
            {
                why = "slot must be an object";
                return false;
            }
            if (!TryString(s, "pack", out string packName) || !TryPack(packName, out AssetPack pack))
            {
                why = "'pack' must be one of ui, character, wand, monster, fx, world, background";
                return false;
            }
            if (!TryString(s, "id", out string id) || id.Length == 0)
            {
                why = "'id' must be a non-empty string";
                return false;
            }
            if (!TryString(s, "variant", out string variant) || variant.Length == 0)
            {
                why = "'variant' must be a non-empty string";
                return false;
            }
            if (!s.TryGetProperty("frame_size", out JsonElement size) || size.ValueKind != JsonValueKind.Array || size.GetArrayLength() != 2
                || !size[0].TryGetInt32(out int w) || !size[1].TryGetInt32(out int h) || w <= 0 || h <= 0)
            {
                why = "'frame_size' must be [width, height] with positive integers";
                return false;
            }
            if (!s.TryGetProperty("count", out JsonElement countEl) || !countEl.TryGetInt32(out int count) || count <= 0)
            {
                why = "'count' must be a positive integer";
                return false;
            }
            TryString(s, "spec", out string spec);
            entry = new AssetSlotEntry(new AssetKey(pack, id, variant), new PixelSize(w, h), count, spec);
            return true;
        }

        private static bool TryString(JsonElement o, string name, out string value)
        {
            value = "";
            if (o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String)
            {
                value = e.GetString() ?? "";
                return true;
            }
            return false;
        }

        public static bool TryPack(string name, out AssetPack pack)
        {
            foreach (AssetPack p in (AssetPack[])Enum.GetValues(typeof(AssetPack)))
            {
                if (string.Equals(PackName(p), name, StringComparison.Ordinal))
                {
                    pack = p;
                    return true;
                }
            }
            pack = default;
            return false;
        }

        public static string PackName(AssetPack pack) => pack.ToString().ToLowerInvariant();
    }
}

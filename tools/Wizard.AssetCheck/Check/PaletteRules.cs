using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Wizard.AssetCheck.Check
{
    // asset_rules.json 의 char5_palette: 그믐(char.5) 팔레트와 지팡이 +0 머리 색 검사의 기준(DECISIONS 2.2)
    public sealed class PaletteRules
    {
        public string Character { get; private set; } = "";

        public string Wand { get; private set; } = "";

        // 저장소 기준 경로
        public string Reference { get; private set; } = "";

        public int MaxColors { get; private set; }

        public IReadOnlyList<int> Palette { get; private set; } = new int[0];

        public IReadOnlyList<string> ExcludedVariants { get; private set; } = new string[0];

        public int HeadStage { get; private set; }

        public string HeadFacing { get; private set; } = "";

        public string HeadPose { get; private set; } = "";

        public double OutlineMaxValue { get; private set; }

        public double PurpleHueFrom { get; private set; }

        public double PurpleHueTo { get; private set; }

        public double PurpleMinSaturation { get; private set; }

        public bool InPalette(int rgb)
        {
            foreach (int c in Palette)
            {
                if (c == rgb)
                {
                    return true;
                }
            }
            return false;
        }

        public static PaletteRules Parse(JsonElement r)
        {
            var rules = new PaletteRules
            {
                Character = r.GetProperty("character").GetString() ?? "",
                Wand = r.GetProperty("wand").GetString() ?? "",
                Reference = r.GetProperty("reference").GetString() ?? "",
                MaxColors = r.GetProperty("max_colors").GetInt32(),
            };
            var palette = new List<int>();
            foreach (JsonElement c in r.GetProperty("palette").EnumerateArray())
            {
                palette.Add(ParseHex(c.GetString() ?? ""));
            }
            rules.Palette = palette;
            var excluded = new List<string>();
            foreach (JsonElement v in r.GetProperty("checked_variants_exclude").EnumerateArray())
            {
                excluded.Add(v.GetString() ?? "");
            }
            rules.ExcludedVariants = excluded;
            JsonElement head = r.GetProperty("wand_head");
            rules.HeadStage = head.GetProperty("stage").GetInt32();
            rules.HeadFacing = head.GetProperty("facing").GetString() ?? "";
            rules.HeadPose = head.GetProperty("pose").GetString() ?? "";
            rules.OutlineMaxValue = head.GetProperty("outline_max_value_percent").GetDouble();
            rules.PurpleHueFrom = head.GetProperty("purple_hue_from").GetDouble();
            rules.PurpleHueTo = head.GetProperty("purple_hue_to").GetDouble();
            rules.PurpleMinSaturation = head.GetProperty("purple_min_saturation_percent").GetDouble();
            return rules;
        }

        // "#RRGGBB" → 0xRRGGBB
        public static int ParseHex(string hex)
        {
            if (hex.Length != 7 || hex[0] != '#'
                || !int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
            {
                throw new FormatException("bad color '" + hex + "' (expected #RRGGBB)");
            }
            return v;
        }
    }
}

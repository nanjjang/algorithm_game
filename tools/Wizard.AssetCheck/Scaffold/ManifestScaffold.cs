using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Wizard.AssetCheck.Png;
using Wizard.ViewLogic.Assets;

namespace Wizard.AssetCheck.Scaffold
{
    // assetcheck scaffold <png>(PIPELINE 6.4절 ④): 파일 이름 규칙(spec 14 4.4절, PIPELINE 6.3절)에서 매니페스트 항목 초안을 만들어
    // 보여 준다. **아무 파일도 쓰지 않는다.** 사람이 확인하고 manifest.json 에 넣는다.
    // 매니페스트가 정본이고 게임은 파일 이름을 해석하지 않는다(spec 14 4.4절). 이름 해석은 초안을 만들 때만 쓴다.
    public static class ManifestScaffold
    {
        public const string FillMarker = "<fill>";

        private static readonly Regex UiName = new Regex(
            "^(?<id>[a-z]-[a-z0-9]+)_(?<name>[a-z0-9_]+?)_(?<w>[0-9]+)x(?<h>[0-9]+)(?<rest>(_[a-z0-9_]+)?)\\.png$", RegexOptions.CultureInvariant);

        private static readonly Regex FrameSuffix = new Regex("_f(?<n>[0-9]+)$", RegexOptions.CultureInvariant);

        private static readonly Regex FxName = new Regex(
            "^(?<key>[a-z0-9_]+)_(?<w>[0-9]+)x(?<h>[0-9]+)_f(?<n>[0-9]+)\\.png$", RegexOptions.CultureInvariant);

        private static readonly Regex MonsterName = new Regex("^(?<id>(enemy|elite|boss)\\.[a-z0-9_.]+)\\.png$", RegexOptions.CultureInvariant);

        // artPath: art/production 기준 경로('/' 구분). image: 그 PNG(크기 대조용)
        public static ScaffoldResult Draft(string artPath, PngImage image, AssetRegistry? registry)
        {
            var notes = new List<string>();
            if (registry != null)
            {
                foreach (AssetRecord r in registry.Records)
                {
                    if (string.Equals(r.File, artPath, StringComparison.Ordinal))
                    {
                        notes.Add("already in the manifest as " + AssetKeyComparer.Format(r.Key));
                        break;
                    }
                }
            }
            string[] parts = artPath.Split('/');
            string file = parts[parts.Length - 1];
            switch (parts[0])
            {
                case "ui":
                    return Ui(artPath, file, image, notes);
                case "effects":
                    return Fx(parts, file, image, notes);
                case "monsters":
                    return Monster(file, image, notes);
                case "wands":
                    return Wand(parts, file, image, notes);
                case "players":
                    return Player(parts, file, image, notes);
                default:
                    return ScaffoldResult.Fail("no naming rule for art/production/" + parts[0] + "/ (PIPELINE 6.3: ui, players, wands, monsters, effects)");
            }
        }

        private static ScaffoldResult Ui(string artPath, string file, PngImage image, List<string> notes)
        {
            Match m = UiName.Match(file);
            if (!m.Success)
            {
                return ScaffoldResult.Fail("UI file name must be <id>_<name>_<w>x<h>[_<state>][_f<N>].png (spec 14 4.4): " + file);
            }
            int w = Int(m, "w"), h = Int(m, "h");
            string rest = m.Groups["rest"].Value;
            int frames = 1;
            Match f = FrameSuffix.Match(rest);
            if (f.Success)
            {
                frames = Int(f, "n");
            }
            string id = m.Groups["id"].Value.ToUpperInvariant();
            if (UiAssetName.TrySplit(id, file, w, h, out string variant, out string state))
            {
                notes.Add("registry key: Ui " + id + " / " + variant + " # " + state);
            }
            CheckStrip(image, w, h, frames, notes);
            var sb = new StringBuilder();
            sb.Append("{ \"id\": \"").Append(id).Append("\", \"file\": \"").Append(artPath.Substring("ui/".Length))
              .Append("\", \"width\": ").Append(N(w)).Append(", \"height\": ").Append(N(h)).Append(", \"frames\": ").Append(N(frames));
            if (frames > 1)
            {
                sb.Append(", \"frame_ms\": \"").Append(FillMarker).Append('"');
                notes.Add("frame_ms is not in the file name: replace " + FillMarker);
            }
            sb.Append(" }");
            notes.Add("add to \"assets\"; atlas packing is the bake script's job (spec 14 4.4; 1920x1080 backgrounds are not packed)");
            return ScaffoldResult.Ok("art/production/ui/manifest.json", sb.ToString(), notes);
        }

        private static ScaffoldResult Fx(string[] parts, string file, PngImage image, List<string> notes)
        {
            if (parts.Length != 3)
            {
                return ScaffoldResult.Fail("effects pictures live in effects/<group>/<key>_<w>x<h>_f<N>.png");
            }
            Match m = FxName.Match(file);
            if (!m.Success)
            {
                return ScaffoldResult.Fail("effects file name must be <key>_<w>x<h>_f<N>.png (spec 14 5.6, _f1 for one frame): " + file);
            }
            int w = Int(m, "w"), h = Int(m, "h"), n = Int(m, "n");
            CheckStrip(image, w, h, n, notes);
            var sb = new StringBuilder();
            sb.Append("\"").Append(m.Groups["key"].Value).Append("\": { \"path\": \"").Append(parts[1]).Append('/').Append(file)
              .Append("\", \"size\": [").Append(N(w)).Append(", ").Append(N(h)).Append("], \"frames\": [");
            for (int i = 0; i < n; i++)
            {
                sb.Append(i > 0 ? ", " : "").Append('[').Append(N(i * w)).Append(", 0, ").Append(N(w)).Append(", ").Append(N(h)).Append(']');
            }
            sb.Append("], \"fps\": \"").Append(FillMarker).Append("\", \"loop\": \"").Append(FillMarker).Append("\" }");
            notes.Add("add under \"" + parts[1] + "\"; fps and loop are not in the file name: replace " + FillMarker);
            return ScaffoldResult.Ok("art/production/effects/manifest.json", sb.ToString(), notes);
        }

        private static ScaffoldResult Monster(string file, PngImage image, List<string> notes)
        {
            Match m = MonsterName.Match(file);
            if (!m.Success)
            {
                return ScaffoldResult.Fail("monster file name must be <content id>.png (enemy.*, elite.*, boss.*; PIPELINE 6.3): " + file);
            }
            string id = m.Groups["id"].Value;
            var sb = new StringBuilder();
            sb.Append("\"").Append(id).Append("\": { \"path\": \"").Append(file).Append("\", \"size\": [\"").Append(FillMarker).Append("\", \"")
              .Append(FillMarker).Append("\"], \"grid\": [\"").Append(FillMarker).Append("\", \"").Append(FillMarker).Append("\"], \"origin\": [\"")
              .Append(FillMarker).Append("\", \"").Append(FillMarker).Append("\"], \"clips\": {} }");
            notes.Add("sheet is " + N(image.Width) + "x" + N(image.Height) + "; frame size, grid, origin and clips are not in the file name (spec 14 4.8 lists the canvas and clips per monster)");
            return ScaffoldResult.Ok("art/production/monsters/manifest.json", sb.ToString(), notes);
        }

        private static ScaffoldResult Wand(string[] parts, string file, PngImage image, List<string> notes)
        {
            if (parts.Length != 3)
            {
                return ScaffoldResult.Fail("wand sheets live in wands/<folder>/{hand,inspection,icon}.png");
            }
            string folder = parts[1];
            int cw, ch, columns, rows;
            string extra;
            switch (file)
            {
                case "hand.png":
                    cw = 32; ch = 64; columns = 14; rows = 6;   // spec 14 4.1, 5.4: 32×64, 14단계 × 6행(방향 3 × 자세 2)
                    extra = ", \"rows\": [\"down_hold\", \"down_aim\", \"right_hold\", \"right_aim\", \"up_hold\", \"up_aim\"]";
                    break;
                case "inspection.png":
                    cw = 192; ch = 192; columns = 7; rows = 2;  // 14단계를 7×2
                    extra = ", \"rows\": 2";
                    break;
                case "icon.png":
                    cw = 64; ch = 64; columns = 14; rows = 1;
                    extra = "";
                    break;
                default:
                    return ScaffoldResult.Fail("wand sheet must be hand.png, inspection.png or icon.png (spec 14 5.1): " + file);
            }
            if (image.Width != cw * columns || image.Height != ch * rows)
            {
                notes.Add("PNG is " + N(image.Width) + "x" + N(image.Height) + ", spec 14 5.4 expects " + N(cw * columns) + "x" + N(ch * rows));
            }
            string sheet = Path.GetFileNameWithoutExtension(file);
            string json = "\"" + sheet + "\": { \"file\": \"" + folder + "/" + file + "\", \"cell\": [" + N(cw) + ", " + N(ch) + "], \"columns\": " + N(columns) + extra + " }";
            notes.Add("put it in sprites." + folder + "; stage points (head, mutations, expansion) are per stage and not in the file name");
            return ScaffoldResult.Ok("art/production/wands/manifest.json", json, notes);
        }

        private static ScaffoldResult Player(string[] parts, string file, PngImage image, List<string> notes)
        {
            if (parts.Length != 3)
            {
                return ScaffoldResult.Fail("character pictures live in players/<folder>/<file>.png");
            }
            string folder = parts[1];
            string field;
            switch (file)
            {
                case "body.png": field = "body"; break;
                case "portraits_64x64_f4.png": field = "portrait"; break;
                case "selection_192x192.png": field = "selection"; break;
                case "silhouette_32x32.png": field = "silhouette"; break;
                default:
                    return ScaffoldResult.Fail("character file must be body.png, portraits_64x64_f4.png, selection_192x192.png or silhouette_32x32.png (spec 14 5.1): " + file);
            }
            if (field == "body")
            {
                notes.Add("body.png is " + N(image.Width) + "x" + N(image.Height) + "; clips and per-frame points (feet, hand, head, shoulder) are not in the file name");
            }
            return ScaffoldResult.Ok("art/production/players/manifest.json",
                "\"" + field + "\": \"" + folder + "/" + file + "\"", notes);
        }

        private static void CheckStrip(PngImage image, int w, int h, int frames, List<string> notes)
        {
            if (image.Width != w * frames || image.Height != h)
            {
                notes.Add("PNG is " + N(image.Width) + "x" + N(image.Height) + " but the name says " + N(w) + "x" + N(h) + " x " + N(frames)
                    + " frame(s) = " + N(w * frames) + "x" + N(h) + " (the import check will fail)");
            }
        }

        private static int Int(Match m, string group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

        private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);
    }
}

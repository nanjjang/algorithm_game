using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // art/production/wands/manifest.json (spec 14 5.4절). 폴더 열쇠 → wand.N 은 asset_aliases.json wand_folder.
    // 열쇠 = (Wand, wand.N, "hand"|"inspection"|"icon"), 프레임은 시트의 칸(행 우선). 칸 고르기와 단계별 점은 WandSheet.
    // 떨어져 뜨는 조각(detachedEffects)은 wand_detached 가 가리키는 (Wand, wand.N, 변형) 이고 기준점은 anchor
    internal static class WandManifestLoader
    {
        public const string Dir = "wands";
        public const string ManifestPath = "wands/manifest.json";
        public const string HandVariant = "hand";
        public const string InspectionVariant = "inspection";
        public const string IconVariant = "icon";
        private const string PointFilter = "point";
        private const int PointLength = 2;

        public static void Load(ManifestJson root, AssetAliases aliases, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(root))
            {
                return;
            }
            bool point = root.TryGet("filter", out ManifestJson f) && f.TryGetString(out string fs) && fs == PointFilter;
            reg.SetSettings(AssetPack.Wand, new PackTextureSettings(
                point, sink.OptionalBool(root, "mipmaps", false), sink.OptionalBool(root, "texture_compression", false), root.Has("texture_compression")));
            if (!sink.TryInts(root, "handCanvas", PointLength, out int[] canvas) || !sink.TryInts(root, "grip", PointLength, out int[] grip))
            {
                return;
            }
            ManifestJson? sprites = root.Get("sprites");
            if (sprites == null || !sink.Object(sprites))
            {
                sink.Add(root, "'sprites' must be an object");
                return;
            }
            foreach (KeyValuePair<string, ManifestJson> w in sprites.Properties)
            {
                if (!aliases.TryWand(w.Key, out string wandId))
                {
                    sink.Add(w.Value, "wand folder '" + w.Key + "' has no wand id (asset_aliases.json wand_folder)");
                    continue;
                }
                LoadWand(wandId, w.Value, reg, sink, new PixelSize(canvas[0], canvas[1]), new PixelPoint(grip[0], grip[1]));
            }

            ManifestJson? detached = root.Get("detachedEffects");
            if (detached != null && sink.Object(detached))
            {
                foreach (KeyValuePair<string, ManifestJson> d in detached.Properties)
                {
                    LoadDetached(d.Key, d.Value, aliases, reg, sink);
                }
            }
        }

        private static void LoadWand(string wandId, ManifestJson w, AssetRegistry reg, ProblemSink sink, PixelSize canvas, PixelPoint grip)
        {
            if (!sink.Object(w))
            {
                return;
            }
            ManifestJson? handNode = w.Get(HandVariant);
            if (handNode == null || !sink.Object(handNode))
            {
                sink.Add(w, "missing 'hand'");
                return;
            }
            var rowNames = new List<string>();
            if (!handNode.TryGet("rows", out ManifestJson rows) || rows.Kind != ManifestJsonKind.Array || rows.Count == 0)
            {
                sink.Add(handNode, "'hand.rows' must be a non-empty array of row names");
                return;
            }
            foreach (ManifestJson r in rows.Items)
            {
                if (!r.TryGetString(out string name) || name.Length == 0)
                {
                    sink.Add(r, "row name must be a non-empty string");
                    return;
                }
                rowNames.Add(name);
            }
            if (!TryGrid(handNode, rowNames.Count, sink, out SheetGrid? hand)
                || !TryGrid(w.Get(InspectionVariant), null, sink, out SheetGrid? inspection)
                || !TryGrid(w.Get(IconVariant), null, sink, out SheetGrid? icon))
            {
                return;
            }
            if (hand!.Cell.Width != canvas.Width || hand.Cell.Height != canvas.Height)
            {
                sink.Add(handNode, "hand cell is not handCanvas " + canvas);
                return;
            }

            if (!w.TryGet("stages", out ManifestJson stagesNode) || stagesNode.Kind != ManifestJsonKind.Array)
            {
                sink.Add(w, "'stages' must be an array");
                return;
            }
            var stages = new List<WandStagePoints>();
            foreach (ManifestJson s in stagesNode.Items)
            {
                if (!sink.Object(s)
                    || !sink.TryInt(s, "stage", out int stage)
                    || !sink.TryInts(s, "head", PointLength, out int[] head)
                    || !sink.TryInts(s, "expansion", PointLength, out int[] expansion))
                {
                    return;
                }
                if (stage != stages.Count)
                {
                    sink.Add(s, "stages must be listed 0, 1, 2, … in order");
                    return;
                }
                if (!s.TryGet("mutations", out ManifestJson muts) || muts.Kind != ManifestJsonKind.Array)
                {
                    sink.Add(s, "'mutations' must be an array of points");
                    return;
                }
                var points = new List<PixelPoint>();
                foreach (ManifestJson m in muts.Items)
                {
                    if (!sink.TryIntsOf(m, "mutations[]", PointLength, out int[] mp))
                    {
                        return;
                    }
                    points.Add(new PixelPoint(mp[0], mp[1]));
                }
                stages.Add(new WandStagePoints(stage, new PixelPoint(head[0], head[1]), points, new PixelPoint(expansion[0], expansion[1])));
            }
            if (stages.Count != hand.Columns || inspection!.CellCount < hand.Columns || icon!.CellCount < hand.Columns)
            {
                sink.Add(w, "stage count " + Inv.Int(stages.Count) + " does not match the sheets (hand columns " + Inv.Int(hand.Columns) + ")");
                return;
            }

            reg.AddWand(new WandSheet(wandId, grip, hand, rowNames, inspection, icon, stages));
            AddSheet(reg, sink, w, wandId, HandVariant, hand, grip);
            AddSheet(reg, sink, w, wandId, InspectionVariant, inspection, null);
            AddSheet(reg, sink, w, wandId, IconVariant, icon, null);
        }

        // rows 가 정수면 그 행 수, 이름 배열이면 rowCount(손 그림), 없으면 1
        private static bool TryGrid(ManifestJson? node, int? rowCount, ProblemSink sink, out SheetGrid? grid)
        {
            grid = null;
            if (node == null || !sink.Object(node))
            {
                sink.Add("", "missing wand sheet");
                return false;
            }
            if (!sink.TryString(node, "file", out string file) || !sink.TryInts(node, "cell", PointLength, out int[] cell) || !sink.TryPositive(node, "columns", out int columns))
            {
                return false;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(node, "bad sheet path '" + file + "'");
                return false;
            }
            int rows = 1;
            if (rowCount.HasValue)
            {
                rows = rowCount.Value;
            }
            else if (node.TryGet("rows", out ManifestJson r) && (!r.TryGetInt(out rows) || rows <= 0))
            {
                sink.Add(r, "'rows' must be a positive integer");
                return false;
            }
            grid = new SheetGrid(path, new PixelSize(cell[0], cell[1]), columns, rows);
            return true;
        }

        private static void AddSheet(AssetRegistry reg, ProblemSink sink, ManifestJson at, string wandId, string variant, SheetGrid grid, PixelPoint? pivot)
        {
            var rects = new PixelRect[grid.CellCount];
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = grid.CellRect(i);
            }
            var rec = new AssetRecord(new AssetKey(AssetPack.Wand, wandId, variant, null), grid.File, grid.Cell, rects);
            rec.Pivot = pivot;
            rec.Source = at;
            if (!reg.Add(rec))
            {
                sink.Add(at, "duplicate " + AssetKeyComparer.Format(rec.Key));
            }
        }

        private static void LoadDetached(string key, ManifestJson d, AssetAliases aliases, AssetRegistry reg, ProblemSink sink)
        {
            if (!aliases.TryWandDetached(key, out WandDetachedAlias alias))
            {
                sink.Add(d, "detached effect '" + key + "' has no wand (asset_aliases.json wand_detached)");
                return;
            }
            if (!sink.Object(d)
                || !sink.TryString(d, "file", out string file)
                || !sink.TryInts(d, "cell", PointLength, out int[] cell)
                || !sink.TryPositive(d, "frames", out int frames)
                || !sink.TryInts(d, "anchor", PointLength, out int[] anchor))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(d, "bad detached effect path '" + file + "'");
                return;
            }
            var rec = new AssetRecord(new AssetKey(AssetPack.Wand, alias.WandId, alias.Variant, null), path, new PixelSize(cell[0], cell[1]),
                StripFrames.Horizontal(0, 0, cell[0], cell[1], frames));
            rec.Pivot = new PixelPoint(anchor[0], anchor[1]);
            rec.Source = d;
            if (!reg.Add(rec))
            {
                sink.Add(d, "duplicate " + AssetKeyComparer.Format(rec.Key));
            }
        }
    }
}

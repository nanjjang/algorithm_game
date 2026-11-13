using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // art/production/players/manifest.json (spec 14 5.3절). 폴더 열쇠 → char.N 은 asset_aliases.json character_folder.
    // 열쇠 = (Character, char.N, 클립 이름). 프레임별 기준점은 feet, 붙는 점은 feet·hand·head·shoulder.
    // 초상·큰 그림·실루엣은 (Character, char.N, "portrait"|"selection"|"silhouette") 이고 규격은 character_sheets [임시].
    // frames(147)는 클립 프레임의 합과 같아야 한다(TV-191)
    internal static class PlayerManifestLoader
    {
        public const string Dir = "players";
        public const string ManifestPath = "players/manifest.json";
        private const string PointFilter = "point";
        private const int RectLength = 4;
        private const int PointLength = 2;

        public static void Load(ManifestJson root, AssetAliases aliases, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(root))
            {
                return;
            }
            bool point = root.TryGet("filter", out ManifestJson f) && f.TryGetString(out string fs) && fs == PointFilter;
            reg.SetSettings(AssetPack.Character, new PackTextureSettings(
                point, sink.OptionalBool(root, "mipmaps", false), sink.OptionalBool(root, "texture_compression", false), root.Has("texture_compression")));
            if (!sink.TryInts(root, "canvas", PointLength, out int[] canvas) || !sink.TryInts(root, "pivotPx", PointLength, out int[] pivot))
            {
                return;
            }
            ManifestJson? characters = root.Get("characters");
            if (characters == null || !sink.Object(characters))
            {
                sink.Add(root, "'characters' must be an object");
                return;
            }
            foreach (KeyValuePair<string, ManifestJson> c in characters.Properties)
            {
                if (!aliases.TryCharacter(c.Key, out string charId))
                {
                    sink.Add(c.Value, "character folder '" + c.Key + "' has no char id (asset_aliases.json character_folder)");
                    continue;
                }
                LoadCharacter(charId, c.Value, aliases, reg, sink, new PixelSize(canvas[0], canvas[1]), new PixelPoint(pivot[0], pivot[1]));
            }
        }

        private static void LoadCharacter(string charId, ManifestJson c, AssetAliases aliases, AssetRegistry reg, ProblemSink sink, PixelSize canvas, PixelPoint pivot)
        {
            if (!sink.Object(c) || !sink.TryString(c, "body", out string body) || !sink.TryPositive(c, "frames", out int total))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, body, out string bodyPath))
            {
                sink.Add(c, "bad body path '" + body + "'");
                return;
            }
            ManifestJson? clips = c.Get("clips");
            if (clips == null || !sink.Object(clips))
            {
                sink.Add(c, "'clips' must be an object");
                return;
            }
            int sum = 0;
            foreach (KeyValuePair<string, ManifestJson> clip in clips.Properties)
            {
                sum += LoadClip(charId, clip.Key, clip.Value, bodyPath, reg, sink, canvas, pivot);
            }
            if (sum != total)
            {
                sink.Add(c, "frames " + Inv.Int(total) + " is not the sum of clip frames " + Inv.Int(sum));
            }

            foreach (KeyValuePair<string, SheetStrip> sheet in aliases.CharacterSheets)
            {
                if (!sink.TryString(c, sheet.Key, out string file))
                {
                    continue;
                }
                if (!AssetPaths.TryCombine(Dir, file, out string path))
                {
                    sink.Add(c, "bad " + sheet.Key + " path '" + file + "'");
                    continue;
                }
                SheetStrip s = sheet.Value;
                var rec = new AssetRecord(new AssetKey(AssetPack.Character, charId, sheet.Key, null), path, s.Cell,
                    StripFrames.Horizontal(0, 0, s.Cell.Width, s.Cell.Height, s.Frames));
                rec.Source = c;
                if (!reg.Add(rec))
                {
                    sink.Add(c, "duplicate " + AssetKeyComparer.Format(rec.Key));
                }
            }
        }

        // 프레임 수를 돌려준다(문제가 있으면 0)
        private static int LoadClip(string charId, string name, ManifestJson clip, string bodyPath, AssetRegistry reg, ProblemSink sink, PixelSize canvas, PixelPoint pivot)
        {
            if (!sink.Object(clip) || !sink.TryPositive(clip, "fps", out int fps))
            {
                return 0;
            }
            bool loop = sink.OptionalBool(clip, "loop", false);
            if (!clip.TryGet("frames", out ManifestJson frames) || frames.Kind != ManifestJsonKind.Array || frames.Count == 0)
            {
                sink.Add(clip, "'frames' must be a non-empty array");
                return 0;
            }
            var rects = new PixelRect[frames.Count];
            var origins = new PixelPoint[frames.Count];
            var points = new CharacterFramePoints[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                ManifestJson fr = frames[i];
                if (!sink.Object(fr)
                    || !sink.TryInts(fr, "rect", RectLength, out int[] r)
                    || !sink.TryInts(fr, "feet", PointLength, out int[] feet)
                    || !sink.TryInts(fr, "hand", PointLength, out int[] hand)
                    || !sink.TryInts(fr, "head", PointLength, out int[] head)
                    || !sink.TryInts(fr, "shoulder", PointLength, out int[] shoulder))
                {
                    return 0;
                }
                if (r[2] != canvas.Width || r[3] != canvas.Height)
                {
                    sink.Add(fr, "frame rect is not the canvas size " + canvas);
                    return 0;
                }
                rects[i] = new PixelRect(r[0], r[1], r[2], r[3]);
                origins[i] = new PixelPoint(feet[0], feet[1]);
                points[i] = new CharacterFramePoints(origins[i], new PixelPoint(hand[0], hand[1]), new PixelPoint(head[0], head[1]), new PixelPoint(shoulder[0], shoulder[1]));
            }
            var rec = new AssetRecord(new AssetKey(AssetPack.Character, charId, name, null), bodyPath, canvas, rects);
            rec.Pivot = pivot;
            rec.FrameOrigins = origins;
            rec.CharacterPoints = points;
            rec.Fps = fps;
            rec.Loop = loop;
            rec.Source = clip;
            if (!reg.Add(rec))
            {
                sink.Add(clip, "duplicate " + AssetKeyComparer.Format(rec.Key));
                return 0;
            }
            return frames.Count;
        }
    }
}

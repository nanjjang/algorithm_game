using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // art/production/effects/manifest.json (spec 14 5.6절). 열쇠 = (Fx, "<묶음>/<열쇠>"). 최상위의 notes 밖의 속성이 묶음이다
    // (player, special, ritual, enemy, loot, wand, status). 원점은 왼쪽 위(notes.origin)라 기준점이 없다.
    // 필터·밉맵은 notes 에 있다
    internal static class FxManifestLoader
    {
        public const string Dir = "effects";
        public const string ManifestPath = "effects/manifest.json";
        public const string NotesKey = "notes";
        public const char GroupSeparator = '/';
        private const string PointFilter = "point";
        private const int RectLength = 4;
        private const int PointLength = 2;

        public static void Load(ManifestJson root, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(root))
            {
                return;
            }
            ManifestJson? notes = root.Get(NotesKey);
            bool point = false;
            bool mipmaps = false;
            bool compression = false;
            bool compressionDeclared = false;
            if (notes != null && sink.Object(notes))
            {
                point = notes.TryGet("filter", out ManifestJson f) && f.TryGetString(out string fs) && fs == PointFilter;
                mipmaps = sink.OptionalBool(notes, "mipmaps", false);
                compression = sink.OptionalBool(notes, "texture_compression", false);
                compressionDeclared = notes.Has("texture_compression");
            }
            reg.SetSettings(AssetPack.Fx, new PackTextureSettings(point, mipmaps, compression, compressionDeclared));

            foreach (KeyValuePair<string, ManifestJson> group in root.Properties)
            {
                if (group.Key == NotesKey)
                {
                    continue;
                }
                if (!sink.Object(group.Value))
                {
                    continue;
                }
                foreach (KeyValuePair<string, ManifestJson> e in group.Value.Properties)
                {
                    LoadEntry(group.Key + GroupSeparator + e.Key, e.Value, reg, sink);
                }
            }
        }

        private static void LoadEntry(string id, ManifestJson e, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(e)
                || !sink.TryString(e, "path", out string file)
                || !sink.TryInts(e, "size", PointLength, out int[] size)
                || !sink.TryPositive(e, "fps", out int fps))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(e, "bad path '" + file + "'");
                return;
            }
            if (!e.TryGet("frames", out ManifestJson frames) || frames.Kind != ManifestJsonKind.Array || frames.Count == 0)
            {
                sink.Add(e, "'frames' must be a non-empty array of rects");
                return;
            }
            var frameSize = new PixelSize(size[0], size[1]);
            var rects = new PixelRect[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                if (!sink.TryIntsOf(frames[i], "frames[]", RectLength, out int[] r))
                {
                    return;
                }
                if (r[2] != frameSize.Width || r[3] != frameSize.Height)
                {
                    sink.Add(frames[i], "frame rect is not the entry size " + frameSize);
                    return;
                }
                rects[i] = new PixelRect(r[0], r[1], r[2], r[3]);
            }
            var rec = new AssetRecord(new AssetKey(AssetPack.Fx, id, null, null), path, frameSize, rects);
            rec.Fps = fps;
            rec.Loop = sink.OptionalBool(e, "loop", false);
            rec.Source = e;
            if (!reg.Add(rec))
            {
                sink.Add(e, "duplicate " + AssetKeyComparer.Format(rec.Key));
            }
        }
    }
}

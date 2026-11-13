using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // art/production/monsters/manifest.json (spec 14 5.5절). 열쇠 = (Monster, 에셋 id, 클립 이름).
    // 프레임별 origin 을 쓴다(22개 프레임이 기본값과 다르다, TV-217). 기본 기준점은 항목의 origin.
    // 프레임 시간은 매니페스트에 없다(애니메이션 속도는 게임이 정한다, RD-mo). missingOrProvisional 은 선택 항목(C-07)
    internal static class MonsterManifestLoader
    {
        public const string Dir = "monsters";
        public const string ManifestPath = "monsters/manifest.json";
        private const string NearestScale = "nearest";
        private const int RectLength = 4;
        private const int PointLength = 2;

        public static void Load(ManifestJson root, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(root))
            {
                return;
            }
            bool point = root.TryGet("scaleMode", out ManifestJson sm) && sm.TryGetString(out string s) && s == NearestScale;
            reg.SetSettings(AssetPack.Monster, new PackTextureSettings(
                point, sink.OptionalBool(root, "mipmaps", false), sink.OptionalBool(root, "texture_compression", false), root.Has("texture_compression")));
            ManifestJson? entries = root.Get("entries");
            if (entries == null || !sink.Object(entries))
            {
                sink.Add(root, "'entries' must be an object");
                return;
            }
            foreach (KeyValuePair<string, ManifestJson> e in entries.Properties)
            {
                LoadEntry(e.Key, e.Value, reg, sink);
            }
        }

        private static void LoadEntry(string id, ManifestJson e, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(e)
                || !sink.TryString(e, "path", out string file)
                || !sink.TryInts(e, "size", PointLength, out int[] size)
                || !sink.TryInts(e, "origin", PointLength, out int[] origin))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(e, "bad path '" + file + "'");
                return;
            }
            ManifestJson? clips = e.Get("clips");
            if (clips == null || !sink.Object(clips))
            {
                sink.Add(e, "'clips' must be an object");
                return;
            }
            var frameSize = new PixelSize(size[0], size[1]);
            foreach (KeyValuePair<string, ManifestJson> clip in clips.Properties)
            {
                ManifestJson frames = clip.Value;
                if (frames.Kind != ManifestJsonKind.Array || frames.Count == 0)
                {
                    sink.Add(frames, "clip must be a non-empty array of frames");
                    continue;
                }
                var rects = new PixelRect[frames.Count];
                var origins = new PixelPoint[frames.Count];
                bool ok = true;
                for (int i = 0; i < frames.Count && ok; i++)
                {
                    ManifestJson fr = frames[i];
                    if (!sink.Object(fr) || !sink.TryInts(fr, "rect", RectLength, out int[] r) || !sink.TryInts(fr, "origin", PointLength, out int[] o))
                    {
                        ok = false;
                        break;
                    }
                    if (r[2] != frameSize.Width || r[3] != frameSize.Height)
                    {
                        sink.Add(fr, "frame rect is not the entry size " + frameSize);
                        ok = false;
                        break;
                    }
                    rects[i] = new PixelRect(r[0], r[1], r[2], r[3]);
                    origins[i] = new PixelPoint(o[0], o[1]);
                }
                if (!ok)
                {
                    continue;
                }
                var rec = new AssetRecord(new AssetKey(AssetPack.Monster, id, clip.Key, null), path, frameSize, rects);
                rec.Pivot = new PixelPoint(origin[0], origin[1]);
                rec.FrameOrigins = origins;
                rec.Source = e;
                if (!reg.Add(rec))
                {
                    sink.Add(frames, "duplicate " + AssetKeyComparer.Format(rec.Key));
                }
            }
        }
    }
}

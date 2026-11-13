using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // art/production/ui/manifest.json (spec 14 5.2절). 열쇠 = (Ui, id, 변형, 상태), 변형·상태는 UiAssetName.TrySplit.
    // 아틀라스가 있으면 프레임 사각형은 페이지 안 좌표다(atlas.rect = 띠 전체 영역). external 은 파일이 있으면
    // (Ui, id, "external") 로 넣고 없으면 없다고 표시한다(S-2405·S-2407, C-13)
    internal static class UiManifestLoader
    {
        public const string Dir = "ui";
        public const string ManifestPath = "ui/manifest.json";
        public const string ExternalVariant = "external";
        private const string NearestSampling = "nearest";
        private const int RectLength = 4;
        private const int PointLength = 2;

        public static void Load(ManifestJson root, AssetAliases aliases, AssetRegistry reg, IAssetSource source, ProblemSink sink)
        {
            if (!sink.Object(root))
            {
                return;
            }
            bool point = root.TryGet("sampling", out ManifestJson sampling) && sampling.TryGetString(out string s) && s == NearestSampling;
            bool compressionDeclared = root.Has("texture_compression");
            reg.SetSettings(AssetPack.Ui, new PackTextureSettings(
                point, sink.OptionalBool(root, "mipmaps", false), sink.OptionalBool(root, "texture_compression", false), compressionDeclared));

            if (!root.TryGet("assets", out ManifestJson assets) || assets.Kind != ManifestJsonKind.Array)
            {
                sink.Add(root, "'assets' must be an array");
            }
            else
            {
                foreach (ManifestJson e in assets.Items)
                {
                    LoadEntry(e, aliases, reg, sink);
                }
            }

            ManifestJson? external = root.Get("external");
            if (external != null && sink.Object(external))
            {
                foreach (KeyValuePair<string, ManifestJson> p in external.Properties)
                {
                    LoadExternal(p.Key, p.Value, aliases, reg, source, sink);
                }
            }
        }

        private static void LoadEntry(ManifestJson e, AssetAliases aliases, AssetRegistry reg, ProblemSink sink)
        {
            if (!sink.Object(e)
                || !sink.TryString(e, "id", out string id)
                || !sink.TryString(e, "file", out string file)
                || !sink.TryPositive(e, "width", out int w)
                || !sink.TryPositive(e, "height", out int h)
                || !sink.TryPositive(e, "frames", out int frames))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(e, "bad file path '" + file + "'");
                return;
            }
            if (!UiAssetName.TrySplit(id, AssetPaths.FileName(file), w, h, out string variant, out string state))
            {
                sink.Add(e, "file name '" + file + "' does not give a variant for " + id + " (expected <id>_<name>_<w>x<h>[_<state>][_f<N>].png)");
                return;
            }

            PixelRect[] rects;
            string? atlasPage = null;
            ManifestJson? atlas = e.Get("atlas");
            if (atlas != null)
            {
                if (!sink.Object(atlas) || !sink.TryString(atlas, "page", out string page) || !sink.TryInts(atlas, "rect", RectLength, out int[] r))
                {
                    return;
                }
                if (r[2] != w * frames || r[3] != h)
                {
                    sink.Add(atlas, "atlas rect " + Inv.Size(r[2], r[3]) + " is not (width * frames) x height");
                    return;
                }
                if (!AssetPaths.TryCombine(Dir, page, out string pagePath))
                {
                    sink.Add(atlas, "bad atlas page '" + page + "'");
                    return;
                }
                atlasPage = pagePath;
                rects = StripFrames.Horizontal(r[0], r[1], w, h, frames);
            }
            else
            {
                rects = StripFrames.Horizontal(0, 0, w, h, frames);
            }

            var rec = new AssetRecord(new AssetKey(AssetPack.Ui, id, variant, state), path, new PixelSize(w, h), rects);
            rec.AtlasPage = atlasPage;
            rec.Source = e;

            ManifestJson? nine = e.Get("nine_slice_ltrb");
            if (nine != null)
            {
                if (!sink.TryIntsOf(nine, "nine_slice_ltrb", RectLength, out int[] n))
                {
                    return;
                }
                rec.NineSlice = new NineSlice(n[0], n[1], n[2], n[3]);
            }
            ManifestJson? frameMs = e.Get("frame_ms");
            if (frameMs != null)
            {
                if (!frameMs.TryGetInt(out int ms) || ms <= 0)
                {
                    sink.Add(frameMs, "'frame_ms' must be a positive integer");
                    return;
                }
                rec.FrameMs = ms;
            }
            if (frames > 1)
            {
                // [임시] UI 의 움직이는 그림(선택 괄호 맥동, 불러오기 모닥불, 승급 화살 …)은 반복한다. 매니페스트에 반복 필드가 없다
                rec.Loop = true;
            }
            ManifestJson? origin = e.Get("sprite_origin");
            if (origin != null)
            {
                if (!sink.TryIntsOf(origin, "sprite_origin", PointLength, out int[] o))
                {
                    return;
                }
                rec.Pivot = new PixelPoint(o[0], o[1]);
            }
            ManifestJson? element = e.Get("element");
            if (element != null)
            {
                if (!element.TryGetString(out string el) || !aliases.TryElement(el, out string canonical))
                {
                    sink.Add(element, "unknown element " + element + " (asset_aliases.json element)");
                    return;
                }
                rec.Element = canonical;
            }
            ManifestJson? kind = e.Get("block_kind");
            if (kind != null)
            {
                if (!kind.TryGetString(out string k))
                {
                    sink.Add(kind, "'block_kind' must be a string");
                    return;
                }
                rec.BlockKind = k;
            }

            if (!reg.Add(rec))
            {
                sink.Add(e, "duplicate " + AssetKeyComparer.Format(rec.Key) + " at " + Inv.Size(w, h));
            }
        }

        private static void LoadExternal(string id, ManifestJson ext, AssetAliases aliases, AssetRegistry reg, IAssetSource source, ProblemSink sink)
        {
            if (!sink.Object(ext) || !sink.TryString(ext, "file", out string file))
            {
                return;
            }
            if (!AssetPaths.TryCombine(Dir, file, out string path))
            {
                sink.Add(ext, "bad external file path '" + file + "'");
                return;
            }
            var key = new AssetKey(AssetPack.Ui, id, ExternalVariant, null);
            if (!source.Exists(path))
            {
                reg.MarkMissing(key);
                return;
            }
            if (!aliases.TryUiExternalSize(id, out PixelSize size))
            {
                sink.Add(ext, "size of external " + id + " is unknown (add it to asset_aliases.json ui_external_size)");
                return;
            }
            var rec = new AssetRecord(key, path, size, StripFrames.One(size.Width, size.Height));
            rec.Source = ext;
            if (!reg.Add(rec))
            {
                sink.Add(ext, "duplicate " + AssetKeyComparer.Format(key));
            }
        }
    }
}

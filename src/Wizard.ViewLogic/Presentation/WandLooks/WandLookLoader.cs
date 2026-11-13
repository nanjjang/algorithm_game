using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation.Limits;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // data/presentation/wand_looks/wand_N.json 다섯 개 → WandLookTable. 형식은 data/schema/wand_looks.schema.json.
    // 공유 코드라 System.Text.Json 대신 ManifestJsonReader 로 읽는다(ARCHITECTURE 3.3).
    // budget 을 주면 등록 검사(WandLookChecks)를 같이 돌리고, 검사에서 거부된 덧씌움은 표에 넣지 않는다(spec 04 12.3절 "자료 등록 시 자동 검사")
    public static class WandLookLoader
    {
        public const string RepoDir = "data/presentation/wand_looks";
        public const string FilePrefix = "wand_";
        public const string FileSuffix = ".json";

        public static WandLookLoadResult LoadDirectory(string directory, ParticleBudgetTable? budget)
        {
            var files = new List<KeyValuePair<string, string>>();
            string[] paths = Directory.GetFiles(directory, FilePrefix + "*" + FileSuffix);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string p in paths)
            {
                files.Add(new KeyValuePair<string, string>(Path.GetFileName(p), File.ReadAllText(p)));
            }
            return Load(files, budget);
        }

        // files: (진단용 이름, JSON 글) 목록
        public static WandLookLoadResult Load(IReadOnlyList<KeyValuePair<string, string>> files, ParticleBudgetTable? budget)
        {
            var problems = new List<string>();
            var sets = new List<WandLookSet>();
            foreach (KeyValuePair<string, string> f in files)
            {
                ManifestJson root;
                try
                {
                    root = ManifestJsonReader.Parse(f.Value);
                }
                catch (ManifestJsonException e)
                {
                    problems.Add(f.Key + ": not JSON: " + e.Message);
                    continue;
                }
                var r = new Reader(f.Key, problems);
                WandLookSet? set = r.ReadSet(root);
                if (set == null)
                {
                    continue;
                }
                bool duplicate = false;
                foreach (WandLookSet s in sets)
                {
                    duplicate |= s.WandKind == set.WandKind;
                }
                if (duplicate)
                {
                    problems.Add(f.Key + ": duplicate " + set.WandId);
                    continue;
                }
                if (budget != null)
                {
                    set = Register(f.Key, set, budget, problems);
                }
                sets.Add(set);
            }
            return new WandLookLoadResult(new WandLookTable(sets), problems);
        }

        // 기본 자료의 검사 + 덧씌움마다 등록 검사. 거부된 덧씌움을 뺀 묶음을 돌려준다
        private static WandLookSet Register(string file, WandLookSet set, ParticleBudgetTable budget, List<string> problems)
        {
            foreach (string p in WandLookChecks.CheckBase(set, budget))
            {
                problems.Add(file + ": " + p);
            }
            var accepted = new List<WandSkin>();
            foreach (WandSkin skin in set.Skins)
            {
                IReadOnlyList<string> found = WandLookChecks.CheckSkin(set, skin, budget);
                if (found.Count == 0)
                {
                    accepted.Add(skin);
                    continue;
                }
                foreach (string p in found)
                {
                    problems.Add(file + ": " + p);
                }
            }
            if (accepted.Count == set.Skins.Count)
            {
                return set;
            }
            return new WandLookSet(set.WandKind, set.Element, set.GrowthAxis, set.FootFx13, set.SlotMarker, set.Sheet, set.Decor,
                set.Detached, set.FootnoteDotsMax, set.FootStars, set.Stages, accepted);
        }

        private sealed class Reader
        {
            private const int MinMutationSites = 2;
            private const int MaxMutationSites = 3;
            private const int RgbHexDigits = 6;

            private readonly string _file;
            private readonly List<string> _problems;
            private bool _failed;

            public Reader(string file, List<string> problems)
            {
                _file = file;
                _problems = problems;
            }

            public WandLookSet? ReadSet(ManifestJson root)
            {
                string wandId = Str(root, "wand");
                int kind = ParseWandKind(root, wandId);
                string element = Str(root, "element");
                string growth = Str(root, "growth_axis");
                string foot = Str(root, "foot_fx_13");
                string slot = Str(root, "slot_marker");
                WandLookSheet? sheet = ReadSheet(Req(root, "sheet"));
                WandDecor? decor = ReadDecor(Req(root, "decor"));
                var detached = new List<WandDetachedRange>();
                foreach (ManifestJson d in Arr(root, "detached"))
                {
                    detached.Add(new WandDetachedRange(Str(d, "variant"), Int(d, "min_stage"), Int(d, "max_stage")));
                }
                int dots = 0;
                ManifestJson? dotsJson = root.Get("footnote_dots_max");
                if (dotsJson != null)
                {
                    dots = AsInt(dotsJson);
                }
                WandFootStars? stars = null;
                ManifestJson? starsJson = root.Get("foot_stars");
                if (starsJson != null)
                {
                    stars = new WandFootStars(Int(starsJson, "stage"), Int(starsJson, "max_stars"), Int(starsJson, "max_debt_rings"));
                }
                var stages = new List<WandStageLook>();
                foreach (ManifestJson s in Arr(root, "stages"))
                {
                    WandStageLook? look = ReadStage(s, stages);
                    if (look != null)
                    {
                        stages.Add(look);
                    }
                }
                if (stages.Count != WandLookSheet.StageCount)
                {
                    Fail(root, "needs " + WandLookSheet.StageCount.ToString(CultureInfo.InvariantCulture) + " stages in order");
                }
                var skins = new List<WandSkin>();
                foreach (ManifestJson s in Arr(root, "skins"))
                {
                    WandSkin? skin = ReadSkin(s, kind);
                    if (skin != null)
                    {
                        skins.Add(skin);
                    }
                }
                if (_failed || sheet == null || decor == null)
                {
                    return null;
                }
                return new WandLookSet(kind, element, growth, foot, slot, sheet, decor, detached, dots, stars, stages, skins);
            }

            private int ParseWandKind(ManifestJson root, string wandId)
            {
                if (wandId.StartsWith(WandLookSet.WandIdPrefix, StringComparison.Ordinal)
                    && int.TryParse(wandId.Substring(WandLookSet.WandIdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                    && n >= 1)
                {
                    return n;
                }
                Fail(root, "wand must be wand.<N>");
                return 0;
            }

            private WandLookSheet? ReadSheet(ManifestJson? s)
            {
                if (s == null)
                {
                    return null;
                }
                var rows = new List<string>();
                foreach (ManifestJson r in Arr(s, "hand_rows"))
                {
                    rows.Add(AsStr(r));
                }
                return new WandLookSheet(Point(s, "grip"), Size(s, "hand_cell"), rows, Size(s, "inspection_cell"), Int(s, "inspection_columns"),
                    Size(s, "icon_cell"));
            }

            private WandDecor? ReadDecor(ManifestJson? d)
            {
                if (d == null)
                {
                    return null;
                }
                return new WandDecor(Str(d, "trail"), Str(d, "muzzle"), Str(d, "hit"), Str(d, "foot"));
            }

            private WandStageLook? ReadStage(ManifestJson s, List<WandStageLook> before)
            {
                int stage = Int(s, "stage");
                if (stage != before.Count)
                {
                    Fail(s, "stage out of order");
                    return null;
                }
                var mutations = new List<PixelPoint>();
                foreach (ManifestJson m in Arr(s, "mutations"))
                {
                    mutations.Add(AsPoint(m));
                }
                if (mutations.Count < MinMutationSites || mutations.Count > MaxMutationSites)
                {
                    Fail(s, "mutations must have 2 or 3 points");
                }
                WandParticleRequest particles = ReadParticles(Req(s, "particles"), before.Count == 0 ? (int?)null : before[before.Count - 1].Particles.TrailPx);
                ManifestJson? dd = Req(s, "duel_decor");
                var duel = new WandSimplestDecor(dd == null ? "" : Str(dd, "muzzle"), dd == null ? "" : Str(dd, "hit"));
                ManifestJson? e = Req(s, "enhance");
                WandEnhanceVariant enhance = default;
                if (e != null)
                {
                    enhance = new WandEnhanceVariant(OptInt(e, "intensity"), Scene(e), OptInt(e, "transition_frames"));
                }
                return new WandStageLook(stage, Point(s, "head"), mutations, Point(s, "expansion"), particles, duel, enhance);
            }

            // 꼬리 길이가 null(명세 미기재)이면 바로 아래 단계의 값을 물려 쓴다. +0 이 null 이면 0
            private WandParticleRequest ReadParticles(ManifestJson? p, int? inheritedTrail)
            {
                if (p == null)
                {
                    return default;
                }
                int? trail = OptInt(p, "trail_px");
                return new WandParticleRequest(Int(p, "idle"), Int(p, "muzzle"), Int(p, "shed"), Int(p, "hit"),
                    trail ?? inheritedTrail ?? 0, trail.HasValue);
            }

            private WandEnhanceScene Scene(ManifestJson e)
            {
                ManifestJson? v = e.Get("scene");
                if (v == null)
                {
                    return WandEnhanceScene.None;
                }
                switch (AsStr(v))
                {
                    case "milestone": return WandEnhanceScene.Milestone;
                    case "stage13": return WandEnhanceScene.Stage13;
                    default:
                        Fail(v, "unknown scene");
                        return WandEnhanceScene.None;
                }
            }

            private WandSkin? ReadSkin(ManifestJson s, int kind)
            {
                WandSkinSource source;
                switch (Str(s, "source"))
                {
                    case "default": source = WandSkinSource.Default; break;
                    case "record": source = WandSkinSource.Record; break;
                    case "season": source = WandSkinSource.Season; break;
                    case "trade": source = WandSkinSource.Trade; break;
                    case "shop": source = WandSkinSource.Shop; break;
                    default:
                        Fail(s, "unknown skin source");
                        return null;
                }
                bool bound = false;
                ManifestJson? b = Req(s, "account_bound");
                if (b != null && !b.TryGetBool(out bound))
                {
                    Fail(b, "account_bound must be a boolean");
                }
                var patches = new List<WandStagePatch>();
                foreach (ManifestJson p in Arr(s, "per_stage"))
                {
                    ManifestJson? pp = p.Get("particles");
                    WandParticleRequest? particles = pp == null ? (WandParticleRequest?)null : ReadParticles(pp, 0);
                    patches.Add(new WandStagePatch(Int(p, "stage"), Int(p, "silhouette_stage"), Palette(p), OptStr(p, "ornament"), particles));
                }
                return new WandSkin(Int(s, "skin_id"), kind, source, bound, Palette(s), OptStr(s, "ornament"), OptStr(s, "trail_override"),
                    OptStr(s, "enhance_fx_override"), OptStr(s, "sound_timbre"), patches);
            }

            private List<int> Palette(ManifestJson o)
            {
                var colors = new List<int>();
                ManifestJson? p = o.Get("palette");
                if (p == null)
                {
                    return colors;
                }
                foreach (ManifestJson c in p.Items)
                {
                    string hex = AsStr(c);
                    if (hex.Length == RgbHexDigits + 1 && hex[0] == '#'
                        && int.TryParse(hex.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
                    {
                        colors.Add(rgb);
                    }
                    else
                    {
                        Fail(c, "palette color must be #RRGGBB");
                    }
                }
                return colors;
            }

            private ManifestJson? Req(ManifestJson o, string name)
            {
                ManifestJson? v = o.Get(name);
                if (v == null)
                {
                    Fail(o, "missing " + name);
                }
                return v;
            }

            private IReadOnlyList<ManifestJson> Arr(ManifestJson o, string name)
            {
                ManifestJson? v = Req(o, name);
                if (v == null)
                {
                    return new ManifestJson[0];
                }
                if (v.Kind != ManifestJsonKind.Array)
                {
                    Fail(v, "must be an array");
                }
                return v.Items;
            }

            private string Str(ManifestJson o, string name)
            {
                ManifestJson? v = Req(o, name);
                return v == null ? "" : AsStr(v);
            }

            private string? OptStr(ManifestJson o, string name)
            {
                ManifestJson? v = o.Get(name);
                return v == null ? null : AsStr(v);
            }

            private string AsStr(ManifestJson v)
            {
                if (!v.TryGetString(out string s))
                {
                    Fail(v, "must be a string");
                }
                return s;
            }

            private int Int(ManifestJson o, string name)
            {
                ManifestJson? v = Req(o, name);
                return v == null ? 0 : AsInt(v);
            }

            private int? OptInt(ManifestJson o, string name)
            {
                ManifestJson? v = o.Get(name);
                return v == null ? (int?)null : AsInt(v);
            }

            private int AsInt(ManifestJson v)
            {
                if (!v.TryGetInt(out int n))
                {
                    Fail(v, "must be an integer");
                }
                return n;
            }

            private PixelPoint Point(ManifestJson o, string name)
            {
                ManifestJson? v = Req(o, name);
                return v == null ? default : AsPoint(v);
            }

            private PixelPoint AsPoint(ManifestJson v)
            {
                if (!v.TryGetIntArray(2, out int[] xy))
                {
                    Fail(v, "must be [x, y]");
                    return default;
                }
                return new PixelPoint(xy[0], xy[1]);
            }

            private PixelSize Size(ManifestJson o, string name)
            {
                ManifestJson? v = Req(o, name);
                if (v == null)
                {
                    return default;
                }
                if (!v.TryGetIntArray(2, out int[] wh))
                {
                    Fail(v, "must be [w, h]");
                    return default;
                }
                return new PixelSize(wh[0], wh[1]);
            }

            private void Fail(ManifestJson at, string message)
            {
                _failed = true;
                _problems.Add(_file + ":" + at.Pointer + ": " + message);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Wizard.AssetCheck.Color;
using Wizard.AssetCheck.Png;
using Wizard.AssetCheck.Slots;
using Wizard.Content;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Check
{
    // 반입 검사(spec 14 5.7.1절, PIPELINE 6.4절 ⑤):
    //   1. 모든 파일이 있고 PNG 크기 = (width × frames, height) 또는 매니페스트 사각형을 담는다
    //   2. 필터 점, 밉맵 없음, 압축 없음
    //   3. 플레이어 쪽 그림에 위험색 픽셀 0개(치장 포함, TV-238). 적 몸은 CheckEnemyBody(spec 05 1.4)
    //   4. 1920×1080 배경은 아틀라스에 넣지 않는다
    //   5. 콘텐츠 id 마다 필요한 그림이 있는지(없으면 대체 사용, 경고)
    //   + char.5 그림의 고유 색 16 이하·원본 팔레트의 부분집합, 지팡이 +0 머리가 보라 계열(DECISIONS 2.2)
    // 결과는 결정적이다(진단은 파일, 검사, 자리, 문구의 서수 순).
    public static class ImportChecker
    {
        public const string ArtPrefix = "art/production/";
        private const int BackgroundWidth = 1920;
        private const int BackgroundHeight = 1080;
        private const int SampleColors = 8;

        public static ImportCheckResult Run(AssetCheckInput input)
        {
            var findings = new List<Finding>();
            AssetRegistry reg = input.Registry;
            var images = input.Images;

            foreach (AssetLoadProblem p in input.LoadProblems)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Manifest, PathOfLoadProblem(p.File),
                    null, (p.Pointer.Length > 0 ? p.Pointer + ": " : "") + p.Message));
            }
            foreach (string p in input.Slots.Problems)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Slots, AssetSlotTable.RepoPath, null, p));
            }

            CheckSettings(reg, findings);
            var checkedDanger = new HashSet<string>(StringComparer.Ordinal);
            var checkedEnemy = new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetRecord rec in reg.Records)
            {
                string slot = SlotIds.Of(rec, reg);
                CheckFiles(rec, slot, images, findings);
                CheckAtlas(rec, slot, findings);
                AssetSide side = AssetSides.Of(rec.Key);
                foreach (string file in TextureFiles(rec))
                {
                    if (side == AssetSide.PlayerSide)
                    {
                        if (checkedDanger.Add(file))
                        {
                            CheckDanger(file, rec, images, findings);
                        }
                    }
                    else if (rec.Key.Pack == AssetPack.Monster && checkedEnemy.Add(file))
                    {
                        CheckEnemyBody(file, images, findings);
                    }
                }
            }
            CheckPalette(input, findings);

            var missing = new List<MissingSlot>();
            CollectContentRequirements(input, missing);
            foreach (AssetKey k in reg.MarkedMissing)
            {
                missing.Add(new MissingSlot(k, null, 0, "manifest external file is missing (spec 14 C-13)"));
            }
            foreach (AssetSlotEntry e in input.Slots.Entries)
            {
                if (!HasVariant(reg, e.Key))
                {
                    missing.Add(new MissingSlot(e.Key, e.FrameSize, e.Count, "asset_slots.json (spec " + e.Spec + ")"));
                }
            }
            missing = Dedupe(missing).Where(m => !input.Rules.IsNotRequired(m.SlotId)).ToList();
            foreach (MissingSlot m in missing)
            {
                findings.Add(new Finding(FindingSeverity.Warning, CheckNames.Content, "", m.SlotId, "no picture, placeholder is used — " + m.Reason));
            }

            findings.Sort(CompareFindings);
            return new ImportCheckResult(input, findings, missing);
        }

        // 텍스처를 쥐는 파일(낱장, 아틀라스 페이지). art/production 기준
        public static IEnumerable<string> TextureFiles(AssetRecord rec)
        {
            yield return rec.File;
            if (rec.AtlasPage != null)
            {
                yield return rec.AtlasPage;
            }
        }

        public static string RepoPath(string artPath) => ArtPrefix + artPath;

        private static string PathOfLoadProblem(string file) =>
            file.StartsWith("data/", StringComparison.Ordinal) ? file : RepoPath(file);

        private static void CheckSettings(AssetRegistry reg, List<Finding> findings)
        {
            var packs = new SortedSet<AssetPack>();
            foreach (AssetRecord r in reg.Records)
            {
                packs.Add(r.Key.Pack);
            }
            foreach (AssetPack pack in packs)
            {
                string where = RepoPath(ManifestOf(pack));
                if (!reg.TryGetSettings(pack, out PackTextureSettings s))
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Settings, where, null, "the manifest does not declare texture settings"));
                    continue;
                }
                if (!s.PointFilter)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Settings, where, null, "filter is not point/nearest"));
                }
                if (s.Mipmaps)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Settings, where, null, "mipmaps are on"));
                }
                if (s.Compression)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Settings, where, null, "texture compression is on"));
                }
            }
        }

        // 묶음의 매니페스트(art/production 기준). 설정 검사의 진단이 이 파일에 붙는다
        public static string ManifestOf(AssetPack pack)
        {
            switch (pack)
            {
                case AssetPack.Ui: return "ui/manifest.json";
                case AssetPack.Character: return "players/manifest.json";
                case AssetPack.Wand: return "wands/manifest.json";
                case AssetPack.Monster: return "monsters/manifest.json";
                case AssetPack.Fx: return "effects/manifest.json";
                default: return pack.ToString().ToLowerInvariant() + "/manifest.json";
            }
        }

        private static void CheckFiles(AssetRecord rec, string slot, ImageCache images, List<Finding> findings)
        {
            string file = RepoPath(rec.File);
            if (!images.TryGet(rec.File, out PngImage loose, out string error))
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.File, file, slot, error));
                return;
            }
            if (rec.Key.Pack == AssetPack.Ui)
            {
                // UI 는 낱장이 (폭 × 프레임, 높이) 띠다(spec 14 5.2절). 아틀라스가 있으면 페이지도 사각형을 담아야 한다
                int w = rec.FrameSize.Width * rec.FrameCount;
                int h = rec.FrameSize.Height;
                if (loose.Width != w || loose.Height != h)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Size, file, slot,
                        "PNG is " + Size(loose.Width, loose.Height) + ", manifest says " + Size(w, h) + " (width x frames, height)"));
                }
                if (rec.AtlasPage != null)
                {
                    string page = RepoPath(rec.AtlasPage);
                    if (!images.TryGet(rec.AtlasPage, out PngImage pageImage, out string pageError))
                    {
                        findings.Add(new Finding(FindingSeverity.Error, CheckNames.File, page, slot, pageError));
                        return;
                    }
                    CheckRects(rec, pageImage, page, slot, findings);
                }
                return;
            }
            CheckRects(rec, loose, file, slot, findings);
        }

        private static void CheckRects(AssetRecord rec, PngImage image, string file, string slot, List<Finding> findings)
        {
            for (int i = 0; i < rec.FrameCount; i++)
            {
                PixelRect r = rec.FrameRect(i);
                if (r.X < 0 || r.Y < 0 || r.Width <= 0 || r.Height <= 0 || r.Right > image.Width || r.Bottom > image.Height)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Size, file, slot,
                        "frame " + i.ToString(CultureInfo.InvariantCulture) + " rect " + r + " is outside the " + Size(image.Width, image.Height) + " PNG"));
                    return;
                }
            }
        }

        private static void CheckAtlas(AssetRecord rec, string slot, List<Finding> findings)
        {
            if (rec.AtlasPage != null && rec.FrameSize.Width >= BackgroundWidth && rec.FrameSize.Height >= BackgroundHeight)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Atlas, RepoPath(rec.File), slot,
                    "a " + rec.FrameSize + " background is packed into atlas " + rec.AtlasPage + " (spec 14 4.4: not packed)"));
            }
        }

        private static void CheckDanger(string artPath, AssetRecord rec, ImageCache images, List<Finding> findings)
        {
            if (!images.TryGet(artPath, out PngImage image, out _))
            {
                return;   // CheckFiles 가 이미 적었다
            }
            int n = ColorRules.CountDangerPixels(image.Rgba);
            if (n == 0)
            {
                return;
            }
            string kind = AssetSides.IsCosmetic(rec.Key) ? "cosmetic picture" : "player-side picture";
            findings.Add(new Finding(FindingSeverity.Error, CheckNames.Danger, RepoPath(artPath), null,
                kind + " has " + n.ToString(CultureInfo.InvariantCulture) + " danger-color pixel(s) (hue 330-15, S>=70, V>=70; DECISIONS 2.2), e.g. "
                + SampleDanger(image)));
        }

        private static string SampleDanger(PngImage image)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.AlphaAt(x, y) > 0 && ColorRules.InDangerRange(image.RgbAt(x, y)))
                    {
                        return ColorRules.Hex(image.RgbAt(x, y)) + " at (" + x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                }
            }
            return "";
        }

        private static void CheckEnemyBody(string artPath, ImageCache images, List<Finding> findings)
        {
            if (!images.TryGet(artPath, out PngImage image, out _))
            {
                return;
            }
            var byVerdict = new SortedDictionary<EnemyColorVerdict, SortedSet<int>>();
            foreach (int c in UniqueColors(image))
            {
                EnemyColorVerdict v = ColorRules.CheckEnemyColor(c);
                if (v == EnemyColorVerdict.Ok)
                {
                    continue;
                }
                if (!byVerdict.TryGetValue(v, out SortedSet<int>? set))
                {
                    set = new SortedSet<int>();
                    byVerdict.Add(v, set);
                }
                set.Add(c);
            }
            foreach (KeyValuePair<EnemyColorVerdict, SortedSet<int>> p in byVerdict)
            {
                FindingSeverity sev = ColorRules.IsEnemyError(p.Key) ? FindingSeverity.Error : FindingSeverity.Warning;
                findings.Add(new Finding(sev, CheckNames.EnemyColor, RepoPath(artPath), null,
                    EnemyRule(p.Key) + ": " + p.Value.Count.ToString(CultureInfo.InvariantCulture) + " color(s) " + Sample(p.Value)));
            }
        }

        private static string EnemyRule(EnemyColorVerdict v)
        {
            switch (v)
            {
                case EnemyColorVerdict.Danger: return "danger color on an enemy body (only telegraphs and attacks)";
                case EnemyColorVerdict.Teal: return "COL-5 teal 150-180 deg is arcane-only";
                case EnemyColorVerdict.ForbiddenHue: return "COL-4 hue 280-15 deg";
                case EnemyColorVerdict.ForbiddenHueEdge: return "COL-4 boundary hue (spec 05 C-06, check by hand)";
                case EnemyColorVerdict.OverSaturated: return "COL-2 saturation above 50%";
                case EnemyColorVerdict.BrightSky: return "COL-5 bright sky 195-210 deg, V>=90";
                case EnemyColorVerdict.Gold: return "COL-6 gold #FFD24A";
                case EnemyColorVerdict.WeaponTint: return "COL-8 #FF2E88 must be on the weapon part only";
                default: return v.ToString();
            }
        }

        private static void CheckPalette(AssetCheckInput input, List<Finding> findings)
        {
            PaletteRules rules = input.Rules.Palette;
            AssetRegistry reg = input.Registry;
            IReadOnlyList<AssetRecord> charRecords = reg.RecordsOf(AssetPack.Character, rules.Character);
            bool haveWand = reg.TryGetWand(rules.Wand, out WandSheet wand);
            if (charRecords.Count == 0 && !haveWand)
            {
                return;   // 그림이 없으면 대조 보고서가 "없음"으로 낸다
            }
            if (!input.ReferenceExists)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, rules.Reference, null,
                    "the original reference picture is missing; the palette check cannot run"));
            }

            var files = new SortedSet<string>(StringComparer.Ordinal);
            foreach (AssetRecord r in charRecords)
            {
                if (r.Key.Variant != null && rules.ExcludedVariants.Contains(r.Key.Variant))
                {
                    continue;
                }
                files.Add(r.File);
            }
            foreach (string file in files)
            {
                if (!input.Images.TryGet(file, out PngImage image, out _))
                {
                    continue;
                }
                SortedSet<int> colors = UniqueColors(image);
                if (colors.Count > rules.MaxColors)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, RepoPath(file), null,
                        rules.Character + " picture has " + colors.Count.ToString(CultureInfo.InvariantCulture) + " colors (at most "
                        + rules.MaxColors.ToString(CultureInfo.InvariantCulture) + ", DECISIONS 2.2)"));
                }
                var outside = new SortedSet<int>(colors.Where(c => !rules.InPalette(c)));
                if (outside.Count > 0)
                {
                    findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, RepoPath(file), null,
                        outside.Count.ToString(CultureInfo.InvariantCulture) + " color(s) are not in the original palette of "
                        + rules.Reference + " (" + AssetRules.RepoPath + "): " + Sample(outside)));
                }
            }
            foreach (string v in rules.ExcludedVariants)
            {
                foreach (AssetRecord r in charRecords)
                {
                    if (r.Key.Variant == v)
                    {
                        findings.Add(new Finding(FindingSeverity.Warning, CheckNames.Palette, RepoPath(r.File), null,
                            "not checked against the 16-color palette (" + v + " is excluded in " + AssetRules.RepoPath + ")"));
                    }
                }
            }

            if (haveWand)
            {
                CheckWandHead(wand, rules, input.Images, findings);
            }
        }

        private static void CheckWandHead(WandSheet wand, PaletteRules rules, ImageCache images, List<Finding> findings)
        {
            string file = RepoPath(wand.Hand.File);
            if (!images.TryGet(wand.Hand.File, out PngImage image, out _))
            {
                return;
            }
            SpriteFacing facing = (SpriteFacing)Enum.Parse(typeof(SpriteFacing), rules.HeadFacing, true);
            WandPose pose = (WandPose)Enum.Parse(typeof(WandPose), rules.HeadPose, true);
            PixelRect cell;
            PixelPoint head;
            try
            {
                cell = wand.HandRect(rules.HeadStage, facing, pose);
                head = wand.PointsOf(rules.HeadStage).Head;
            }
            catch (InvalidOperationException e)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, file, null, "cannot find the +0 head: " + e.Message));
                return;
            }
            catch (ArgumentOutOfRangeException e)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, file, null, "cannot find the +0 head: " + e.Message));
                return;
            }
            if (cell.Right > image.Width || cell.Bottom > image.Height)
            {
                return;   // 크기 검사가 적었다
            }
            int purple = 0, other = 0;
            var counts = new SortedDictionary<int, int>();
            for (int y = cell.Y; y < cell.Y + Math.Min(head.Y, cell.Height); y++)
            {
                for (int x = cell.X; x < cell.Right; x++)
                {
                    if (image.AlphaAt(x, y) == 0)
                    {
                        continue;
                    }
                    int rgb = image.RgbAt(x, y);
                    Hsv c = Hsv.FromRgb(rgb);
                    if (c.V < rules.OutlineMaxValue)
                    {
                        continue;
                    }
                    counts[rgb] = counts.TryGetValue(rgb, out int n) ? n + 1 : 1;
                    if (IsPurple(c, rules))
                    {
                        purple++;
                    }
                    else
                    {
                        other++;
                    }
                }
            }
            if (purple <= other)
            {
                findings.Add(new Finding(FindingSeverity.Error, CheckNames.Palette, file, null,
                    rules.Wand + " stage " + rules.HeadStage.ToString(CultureInfo.InvariantCulture) + " head is not purple (DECISIONS 2.2): "
                    + purple.ToString(CultureInfo.InvariantCulture) + " purple vs " + other.ToString(CultureInfo.InvariantCulture)
                    + " other pixel(s) above the head point; colors " + Sample(new SortedSet<int>(counts.Keys))));
            }
        }

        public static bool IsPurple(Hsv c, PaletteRules rules) =>
            c.S >= rules.PurpleMinSaturation && ColorRules.HueIn(c, rules.PurpleHueFrom, rules.PurpleHueTo);

        public static SortedSet<int> UniqueColors(PngImage image)
        {
            var set = new SortedSet<int>();
            byte[] p = image.Rgba;
            for (int i = 0; i + 3 < p.Length; i += 4)
            {
                if (p[i + 3] > 0)
                {
                    set.Add((p[i] << 16) | (p[i + 1] << 8) | p[i + 2]);
                }
            }
            return set;
        }

        // 콘텐츠 id → 있어야 하는 그림(spec 14 5.7.1절 5번, 3절 표 → 5.2절 대응표, asset_aliases.json)
        private static void CollectContentRequirements(AssetCheckInput input, List<MissingSlot> missing)
        {
            AssetRegistry reg = input.Registry;
            AssetAliases aliases = input.Aliases;
            foreach (string id in AllContentIds())
            {
                if (aliases.TryUiIcon(id, out UiIconRule rule, out string variant))
                {
                    foreach (string icon in rule.Icons)
                    {
                        var key = new AssetKey(AssetPack.Ui, icon, variant);
                        if (!HasVariant(reg, key))
                        {
                            missing.Add(new MissingSlot(key, null, 0, "icon for " + id + " (spec 14 5.2)"));
                        }
                    }
                }
                if (id.StartsWith("char.", StringComparison.Ordinal))
                {
                    if (reg.RecordsOf(AssetPack.Character, id).Count == 0)
                    {
                        missing.Add(new MissingSlot(new AssetKey(AssetPack.Character, id), null, 0, "character " + id));
                    }
                    string wandId = "wand." + id.Substring("char.".Length);
                    if (!reg.TryGetWand(wandId, out _))
                    {
                        missing.Add(new MissingSlot(new AssetKey(AssetPack.Wand, wandId), null, 0, "wand of " + id));
                    }
                }
                if ((id.StartsWith("enemy.", StringComparison.Ordinal) || id.StartsWith("boss.", StringComparison.Ordinal))
                    && reg.RecordsOf(AssetPack.Monster, id).Count == 0)
                {
                    missing.Add(new MissingSlot(new AssetKey(AssetPack.Monster, id), null, 0, "monster sheet " + id));
                }
                if (aliases.TryBlockProjectileFx(id, out string fx) && !reg.Contains(new AssetKey(AssetPack.Fx, fx)))
                {
                    missing.Add(new MissingSlot(new AssetKey(AssetPack.Fx, fx), null, 0, "projectile of " + id));
                }
            }
        }

        // Wizard.Content.ContentIds 의 ToId(열거형) 전부. 열거형마다 모든 값을 돌고 id 가 없는 값은 뺀다. 서수 순
        public static IReadOnlyList<string> AllContentIds()
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (MethodInfo m in typeof(ContentIds).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (m.Name != "ToId" || ps.Length != 1 || !ps[0].ParameterType.IsEnum || m.ReturnType != typeof(string))
                {
                    continue;
                }
                foreach (object v in Enum.GetValues(ps[0].ParameterType))
                {
                    try
                    {
                        if (m.Invoke(null, new[] { v }) is string id)
                        {
                            ids.Add(id);
                        }
                    }
                    catch (TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException)
                    {
                        // id 가 없는 값(개수 표지 등)
                    }
                }
            }
            return ids.ToList();
        }

        public static bool HasVariant(AssetRegistry reg, AssetKey key)
        {
            if (key.Variant == null)
            {
                return reg.RecordsOf(key.Pack, key.Id).Count > 0;
            }
            foreach (AssetRecord r in reg.RecordsOf(key.Pack, key.Id))
            {
                if (string.Equals(r.Key.Variant, key.Variant, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<MissingSlot> Dedupe(List<MissingSlot> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<MissingSlot>();
            foreach (MissingSlot m in list.OrderBy(m => m.SlotId, StringComparer.Ordinal))
            {
                if (seen.Add(m.SlotId))
                {
                    result.Add(m);
                }
            }
            return result;
        }

        private static string Sample(IEnumerable<int> colors)
        {
            var parts = colors.Take(SampleColors).Select(ColorRules.Hex).ToList();
            int total = colors.Count();
            return string.Join(", ", parts) + (total > SampleColors ? ", …" : "");
        }

        private static string Size(int w, int h) => w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture);

        private static int CompareFindings(Finding a, Finding b)
        {
            int c = string.CompareOrdinal(a.File, b.File);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Check, b.Check);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Slot ?? "", b.Slot ?? "");
            if (c != 0) return c;
            return string.CompareOrdinal(a.Message, b.Message);
        }
    }
}

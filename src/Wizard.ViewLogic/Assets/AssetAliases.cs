using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Assets.Loaders;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Assets
{
    // data/presentation/asset_aliases.json: 콘텐츠 id 와 그림 열쇠가 다른 곳을 한 곳에 적은 표(PIPELINE 6.3, spec 14 3.1·5.3·5.4·5.6절).
    //   element              매니페스트의 속성 키 → 정식 키(frost → ice, shadow → dark. spec 14 3.1절 ElementAlias)
    //   character_folder     MAN-pl 폴더 열쇠 → char.N (spec 14 5.3절, C-05)
    //   wand_folder          MAN-wd 폴더 열쇠 → wand.N
    //   wand_detached        MAN-wd detachedEffects 열쇠 → (wand.N, 레지스트리 변형)
    //   block_projectile_fx  block.<k> → 탄 그림 Fx 열쇠("player/mana_bolt", spec 14 5.6절 C-02). 탄 그림이 없는 블록은 줄이 없다
    //   ui_icons             콘텐츠 id → UI 아이콘 변형 규칙(spec 14 5.2절 대응표의 일부)
    //   character_sheets     MAN-pl 이 칸 크기·프레임 수를 적지 않은 그림(초상, 큰 그림, 실루엣)의 규격(spec 14 4.6절 표) [임시]
    //   ui_external_size     MAN-ui external 이 크기를 적지 않은 그림의 크기(S-0201 1920×1080, spec 14 TV-183) [임시]
    //   wand_up_split        위 방향 겹침 예외(spec 14 4.6절) [임시]
    // 작업용 이름의 로마자 표기는 코드에 쓰지 않고 이 파일에만 둔다(CONTRACTS 2.1, names-scan 의 예외).
    public sealed class AssetAliases
    {
        public const string RepoPath = "data/presentation/asset_aliases.json";

        private readonly Dictionary<string, string> _element = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _characterFolders = new List<KeyValuePair<string, string>>();
        private readonly List<KeyValuePair<string, string>> _wandFolders = new List<KeyValuePair<string, string>>();
        private readonly Dictionary<string, WandDetachedAlias> _wandDetached = new Dictionary<string, WandDetachedAlias>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _blockFx = new List<KeyValuePair<string, string>>();
        private readonly List<UiIconRule> _uiIcons = new List<UiIconRule>();
        private readonly List<KeyValuePair<string, SheetStrip>> _characterSheets = new List<KeyValuePair<string, SheetStrip>>();
        private readonly Dictionary<string, PixelSize> _uiExternalSize = new Dictionary<string, PixelSize>(StringComparer.Ordinal);
        private readonly Dictionary<string, WandUpSplit> _wandUpSplit = new Dictionary<string, WandUpSplit>(StringComparer.Ordinal);

        private AssetAliases()
        {
        }

        public IReadOnlyList<KeyValuePair<string, string>> CharacterFolders => _characterFolders;

        public IReadOnlyList<KeyValuePair<string, string>> WandFolders => _wandFolders;

        public IReadOnlyList<KeyValuePair<string, string>> BlockProjectileFx => _blockFx;

        public IReadOnlyList<UiIconRule> UiIcons => _uiIcons;

        public IReadOnlyList<KeyValuePair<string, SheetStrip>> CharacterSheets => _characterSheets;

        // 매니페스트의 속성 키 → 정식 키. 표에 없으면 false
        public bool TryElement(string manifestKey, out string canonical)
        {
            if (_element.TryGetValue(manifestKey, out string? v))
            {
                canonical = v;
                return true;
            }
            canonical = "";
            return false;
        }

        public bool TryCharacter(string folder, out string charId) => TryFind(_characterFolders, folder, out charId);

        public bool TryWand(string folder, out string wandId) => TryFind(_wandFolders, folder, out wandId);

        public bool TryWandDetached(string manifestKey, out WandDetachedAlias alias) => _wandDetached.TryGetValue(manifestKey, out alias);

        public bool TryBlockProjectileFx(string blockId, out string fxId) => TryFind(_blockFx, blockId, out fxId);

        public bool TryUiExternalSize(string uiId, out PixelSize size) => _uiExternalSize.TryGetValue(uiId, out size);

        public bool TryWandUpSplit(string wandId, out WandUpSplit split) => _wandUpSplit.TryGetValue(wandId, out split);

        // 콘텐츠 id 에 맞는 아이콘 규칙과 변형("block.fireball" → I-B1·I-B2, "block_fireball")
        public bool TryUiIcon(string contentId, out UiIconRule rule, out string variant)
        {
            foreach (UiIconRule r in _uiIcons)
            {
                if (r.Matches(contentId))
                {
                    rule = r;
                    variant = r.VariantFor(contentId);
                    return true;
                }
            }
            rule = null!;
            variant = "";
            return false;
        }

        public static AssetAliases Parse(string json, List<AssetLoadProblem> problems)
        {
            var sink = new ProblemSink(problems, RepoPath);
            var a = new AssetAliases();
            ManifestJson root;
            try
            {
                root = ManifestJsonReader.Parse(json);
            }
            catch (ManifestJsonException e)
            {
                sink.Add("", "not JSON: " + e.Message);
                return a;
            }
            if (!sink.Object(root))
            {
                return a;
            }
            ReadStringMap(sink, root, "element", (k, v) => a._element.Add(k, v));
            ReadStringMap(sink, root, "character_folder", (k, v) => a._characterFolders.Add(new KeyValuePair<string, string>(k, v)));
            ReadStringMap(sink, root, "wand_folder", (k, v) => a._wandFolders.Add(new KeyValuePair<string, string>(k, v)));
            ReadStringMap(sink, root, "block_projectile_fx", (k, v) => a._blockFx.Add(new KeyValuePair<string, string>(k, v)));

            ManifestJson? detached = Section(sink, root, "wand_detached");
            if (detached != null)
            {
                foreach (KeyValuePair<string, ManifestJson> p in detached.Properties)
                {
                    if (IsNote(p.Key))
                    {
                        continue;
                    }
                    if (sink.Object(p.Value) && sink.TryString(p.Value, "wand", out string wand) && sink.TryString(p.Value, "variant", out string variant))
                    {
                        a._wandDetached.Add(p.Key, new WandDetachedAlias(wand, variant));
                    }
                }
            }

            ManifestJson? sheets = Section(sink, root, "character_sheets");
            if (sheets != null)
            {
                foreach (KeyValuePair<string, ManifestJson> p in sheets.Properties)
                {
                    if (IsNote(p.Key))
                    {
                        continue;
                    }
                    if (sink.Object(p.Value) && sink.TryInts(p.Value, "cell", 2, out int[] cell) && sink.TryPositive(p.Value, "frames", out int frames))
                    {
                        a._characterSheets.Add(new KeyValuePair<string, SheetStrip>(p.Key, new SheetStrip(new PixelSize(cell[0], cell[1]), frames)));
                    }
                }
            }

            ManifestJson? ext = Section(sink, root, "ui_external_size");
            if (ext != null)
            {
                foreach (KeyValuePair<string, ManifestJson> p in ext.Properties)
                {
                    if (IsNote(p.Key))
                    {
                        continue;
                    }
                    if (sink.TryIntsOf(p.Value, p.Key, 2, out int[] wh))
                    {
                        a._uiExternalSize.Add(p.Key, new PixelSize(wh[0], wh[1]));
                    }
                }
            }

            ManifestJson? split = Section(sink, root, "wand_up_split");
            if (split != null)
            {
                foreach (KeyValuePair<string, ManifestJson> p in split.Properties)
                {
                    if (IsNote(p.Key))
                    {
                        continue;
                    }
                    if (sink.Object(p.Value) && sink.TryInt(p.Value, "split_y", out int y) && sink.TryInt(p.Value, "offset_x", out int x))
                    {
                        a._wandUpSplit.Add(p.Key, new WandUpSplit(y, x));
                    }
                }
            }

            if (root.TryGet("ui_icons", out ManifestJson icons))
            {
                if (icons.Kind != ManifestJsonKind.Array)
                {
                    sink.Add(icons, "'ui_icons' must be an array");
                }
                else
                {
                    foreach (ManifestJson rule in icons.Items)
                    {
                        ReadIconRule(sink, rule, a._uiIcons);
                    }
                }
            }
            else
            {
                sink.Add(root, "missing 'ui_icons'");
            }
            return a;
        }

        private static void ReadIconRule(ProblemSink sink, ManifestJson rule, List<UiIconRule> into)
        {
            if (!sink.Object(rule) || !sink.TryString(rule, "content_prefix", out string prefix))
            {
                return;
            }
            string variantPrefix = "";
            if (rule.TryGet("variant_prefix", out ManifestJson vp) && !vp.TryGetString(out variantPrefix))
            {
                sink.Add(vp, "'variant_prefix' must be a string");
                return;
            }
            var ids = new List<string>();
            if (!rule.TryGet("icons", out ManifestJson iconList) || iconList.Kind != ManifestJsonKind.Array || iconList.Count == 0)
            {
                sink.Add(rule, "'icons' must be a non-empty array of UI ids");
                return;
            }
            foreach (ManifestJson id in iconList.Items)
            {
                if (!id.TryGetString(out string s) || s.Length == 0)
                {
                    sink.Add(id, "icon id must be a non-empty string");
                    return;
                }
                ids.Add(s);
            }
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            ManifestJson? ko = rule.Get("key_overrides");
            if (ko != null)
            {
                ReadStringMap(sink, ko, (k, v) => overrides.Add(k, v));
            }
            into.Add(new UiIconRule(prefix, ids, variantPrefix, overrides));
        }

        private static ManifestJson? Section(ProblemSink sink, ManifestJson root, string name)
        {
            if (!root.TryGet(name, out ManifestJson v))
            {
                sink.Add(root, "missing '" + name + "'");
                return null;
            }
            return sink.Object(v) ? v : null;
        }

        private static void ReadStringMap(ProblemSink sink, ManifestJson root, string name, Action<string, string> add)
        {
            ManifestJson? section = Section(sink, root, name);
            if (section != null)
            {
                ReadStringMap(sink, section, add);
            }
        }

        private static void ReadStringMap(ProblemSink sink, ManifestJson section, Action<string, string> add)
        {
            if (!sink.Object(section))
            {
                return;
            }
            foreach (KeyValuePair<string, ManifestJson> p in section.Properties)
            {
                if (IsNote(p.Key))
                {
                    continue;
                }
                if (p.Value.TryGetString(out string v) && v.Length > 0)
                {
                    add(p.Key, v);
                }
                else
                {
                    sink.Add(p.Value, "value must be a non-empty string");
                }
            }
        }

        // 데이터 주석 "_note"(와 '_'로 시작하는 열쇠)는 항목이 아니다(data/schema/_common.schema.json note)
        private static bool IsNote(string key) => key.Length > 0 && key[0] == '_';

        private static bool TryFind(List<KeyValuePair<string, string>> list, string key, out string value)
        {
            foreach (KeyValuePair<string, string> p in list)
            {
                if (string.Equals(p.Key, key, StringComparison.Ordinal))
                {
                    value = p.Value;
                    return true;
                }
            }
            value = "";
            return false;
        }
    }
}

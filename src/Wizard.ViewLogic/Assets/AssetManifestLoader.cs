using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;
using Wizard.ViewLogic.Assets.Loaders;
using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // spec 14 5.7절 LoadAll: 매니페스트 다섯 개(ui, players, wands, monsters, effects)를 읽어 통합 레지스트리 하나로 모은다.
    // 별칭은 data/presentation/asset_aliases.json 의 글을 받는다(파일을 어디서 읽을지는 부르는 쪽이 정한다).
    // 다섯 형식을 읽은 뒤 별칭 표가 가리키는 열쇠(블록 탄 그림, 아이콘 규칙)가 레지스트리에 있는지 본다.
    // 그림 파일 자체의 검사(크기, 위험색, 필터)는 반입 검사(5.7.1, M9-AST-02)의 몫이다.
    public static class AssetManifestLoader
    {
        public static AssetLoadResult LoadAll(IAssetSource source, string aliasesJson, IAssetLog? log = null)
        {
            var problems = new List<AssetLoadProblem>();
            AssetAliases aliases = AssetAliases.Parse(aliasesJson, problems);
            var reg = new AssetRegistry(log);

            ManifestJson? ui = Read(source, UiManifestLoader.ManifestPath, problems);
            if (ui != null)
            {
                UiManifestLoader.Load(ui, aliases, reg, source, new ProblemSink(problems, UiManifestLoader.ManifestPath));
            }
            ManifestJson? players = Read(source, PlayerManifestLoader.ManifestPath, problems);
            if (players != null)
            {
                PlayerManifestLoader.Load(players, aliases, reg, new ProblemSink(problems, PlayerManifestLoader.ManifestPath));
            }
            ManifestJson? wands = Read(source, WandManifestLoader.ManifestPath, problems);
            if (wands != null)
            {
                WandManifestLoader.Load(wands, aliases, reg, new ProblemSink(problems, WandManifestLoader.ManifestPath));
            }
            ManifestJson? monsters = Read(source, MonsterManifestLoader.ManifestPath, problems);
            if (monsters != null)
            {
                MonsterManifestLoader.Load(monsters, reg, new ProblemSink(problems, MonsterManifestLoader.ManifestPath));
            }
            ManifestJson? fx = Read(source, FxManifestLoader.ManifestPath, problems);
            if (fx != null)
            {
                FxManifestLoader.Load(fx, reg, new ProblemSink(problems, FxManifestLoader.ManifestPath));
            }

            CheckAliasTargets(aliases, reg, new ProblemSink(problems, AssetAliases.RepoPath));
            return new AssetLoadResult(reg, aliases, problems);
        }

        private static ManifestJson? Read(IAssetSource source, string path, List<AssetLoadProblem> problems)
        {
            if (!source.TryReadText(path, out string text))
            {
                problems.Add(new AssetLoadProblem(path, "", "manifest not found"));
                return null;
            }
            try
            {
                return ManifestJsonReader.Parse(text);
            }
            catch (ManifestJsonException e)
            {
                problems.Add(new AssetLoadProblem(path, "", "not JSON: " + e.Message));
                return null;
            }
        }

        // 별칭이 가리키는 그림이 있어야 한다(없으면 표가 낡은 것이다)
        private static void CheckAliasTargets(AssetAliases aliases, AssetRegistry reg, ProblemSink sink)
        {
            foreach (KeyValuePair<string, string> p in aliases.BlockProjectileFx)
            {
                if (!reg.Contains(new AssetKey(AssetPack.Fx, p.Value)))
                {
                    sink.Add("/block_projectile_fx/" + p.Key, "fx " + p.Value + " is not in the effects manifest");
                }
            }
            foreach (UiIconRule rule in aliases.UiIcons)
            {
                foreach (string icon in rule.Icons)
                {
                    if (reg.RecordsOf(AssetPack.Ui, icon).Count == 0)
                    {
                        sink.Add("/ui_icons", "icon " + icon + " is not in the UI manifest");
                    }
                }
            }
            CheckDistinct(aliases.CharacterFolders, "/character_folder", sink);
            CheckDistinct(aliases.WandFolders, "/wand_folder", sink);
        }

        private static void CheckDistinct(IReadOnlyList<KeyValuePair<string, string>> map, string pointer, ProblemSink sink)
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> p in map)
            {
                if (!seen.Add(p.Value))
                {
                    sink.Add(pointer + "/" + p.Key, "two folders map to " + p.Value);
                }
            }
        }
    }
}

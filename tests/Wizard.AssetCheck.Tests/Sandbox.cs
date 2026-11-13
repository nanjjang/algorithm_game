using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Slots;
using Wizard.TestKit;

namespace Wizard.AssetCheck.Tests
{
    // 임시 저장소: Wizard.sln + data/presentation/asset_aliases.json(저장소의 것) + asset_slots.json + 규칙 표(저장소의 것)
    // + art/production 의 다섯 매니페스트(비어 있거나 시험이 쓴 것). 끝나면 지운다
    internal sealed class Sandbox : IDisposable
    {
        public const string EmptyUi = "{ \"sampling\": \"nearest\", \"mipmaps\": false, \"texture_compression\": false, \"assets\": [] }";
        public const string EmptyPlayers = "{ \"filter\": \"point\", \"mipmaps\": false, \"canvas\": [64, 64], \"pivotPx\": [30, 60], \"mirrorRightForLeft\": true, \"characters\": {} }";
        public const string EmptyWands = "{ \"handCanvas\": [32, 64], \"grip\": [16, 48], \"filter\": \"point\", \"mipmaps\": false, \"sprites\": {} }";
        public const string EmptyMonsters = "{ \"schema\": 1, \"scaleMode\": \"nearest\", \"entries\": {} }";
        public const string EmptyFx = "{ \"notes\": { \"origin\": \"top-left\", \"filter\": \"point\", \"mipmaps\": false } }";

        public Sandbox()
        {
            Root = Path.Combine(Path.GetTempPath(), "wizard-assetcheck-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            WriteText("Wizard.sln", "");
            WriteText("data/presentation/asset_aliases.json", EmptyPackAliases());
            WriteText(AssetSlotTable.RepoPath, "{ \"slots\": [] }");
            WriteText(AssetRules.RepoPath, File.ReadAllText(TestRepo.Path(AssetRules.RepoPath)));
            WriteText("art/production/ui/manifest.json", EmptyUi);
            WriteText("art/production/players/manifest.json", EmptyPlayers);
            WriteText("art/production/wands/manifest.json", EmptyWands);
            WriteText("art/production/monsters/manifest.json", EmptyMonsters);
            WriteText("art/production/effects/manifest.json", EmptyFx);
            CopyFromRepo("art/player/dark_wizard/idle-reference-details.png");
        }

        public string Root { get; }

        // 저장소의 별칭 표에서 "그림이 있어야 하는 별칭"(탄 그림, 아이콘 규칙)만 비운다. 빈 매니페스트로도 로더가 문제 없이 읽게 한다
        private static string EmptyPackAliases()
        {
            JsonNode root = JsonNode.Parse(File.ReadAllText(TestRepo.Path("data/presentation/asset_aliases.json")))!;
            root["block_projectile_fx"] = new JsonObject();
            root["ui_icons"] = new JsonArray();
            return root.ToJsonString();
        }

        public string Art => Path.Combine(Root, "art", "production");

        public string PathOf(string repoPath) => Path.Combine(Root, repoPath.Replace('/', Path.DirectorySeparatorChar));

        public void WriteText(string repoPath, string text)
        {
            string p = PathOf(repoPath);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, text, new UTF8Encoding(false));
        }

        public void WritePng(string artPath, byte[] png) => TestPng.Write(PathOf("art/production/" + artPath), png);

        public void CopyFromRepo(string repoPath)
        {
            string p = PathOf(repoPath);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.Copy(TestRepo.Path(repoPath), p, true);
        }

        public AssetCheckInput Load() => AssetCheckRepo.Load(Root);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}

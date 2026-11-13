using System;
using System.IO;
using System.Text;
using Wizard.AssetCheck.Check;
using Wizard.AssetCheck.Slots;
using Wizard.ViewLogic.Assets;

namespace Wizard.AssetCheck
{
    // 저장소에서 반입 검사의 입력을 읽는다. 뿌리는 --root 이거나, 없으면 실행 파일의 위치에서 위로 Wizard.sln 을 찾는다
    // (현재 폴더에 기대지 않는다).
    public static class AssetCheckRepo
    {
        public const string ArtRoot = "art/production";
        public const string SolutionFile = "Wizard.sln";

        public static string FindRoot(string? explicitRoot)
        {
            if (!string.IsNullOrEmpty(explicitRoot))
            {
                string full = Path.GetFullPath(explicitRoot!);
                if (!File.Exists(Path.Combine(full, SolutionFile)))
                {
                    throw new DirectoryNotFoundException("--root " + full + " has no " + SolutionFile);
                }
                return full;
            }
            string? d = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(d))
            {
                if (File.Exists(Path.Combine(d, SolutionFile)))
                {
                    return d;
                }
                d = Path.GetDirectoryName(d.TrimEnd(Path.DirectorySeparatorChar));
            }
            throw new DirectoryNotFoundException("repository root (" + SolutionFile + ") not found above " + AppContext.BaseDirectory + "; pass --root");
        }

        public static string Resolve(string root, string repoPath) => Path.Combine(root, repoPath.Replace('/', Path.DirectorySeparatorChar));

        public static AssetCheckInput Load(string root)
        {
            string aliases = ReadRequired(root, AssetAliases.RepoPath);
            AssetLoadResult load = AssetManifestLoader.LoadAll(new DirectoryAssetSource(Resolve(root, ArtRoot)), aliases);
            string slotsPath = Resolve(root, AssetSlotTable.RepoPath);
            AssetSlotTable slots = File.Exists(slotsPath)
                ? AssetSlotTable.Parse(File.ReadAllText(slotsPath, new UTF8Encoding(false)))
                : AssetSlotTable.Parse("{}");
            AssetRules rules = AssetRules.Parse(ReadRequired(root, AssetRules.RepoPath));
            bool referenceExists = File.Exists(Resolve(root, rules.Palette.Reference));
            return new AssetCheckInput(load.Registry, load.Aliases, load.Problems, slots, rules,
                new ImageCache(Resolve(root, ArtRoot)), referenceExists);
        }

        private static string ReadRequired(string root, string repoPath)
        {
            string full = Resolve(root, repoPath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException(repoPath + " is missing", full);
            }
            return File.ReadAllText(full, new UTF8Encoding(false));
        }
    }
}

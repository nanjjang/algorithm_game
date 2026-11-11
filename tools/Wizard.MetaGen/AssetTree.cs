using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.MetaGen
{
    // src/ 와 unity/Assets/ 아래를 훑어 .meta 를 가져야 하는 대상(폴더·파일)과 이미 있는 .meta 를 모은다.
    // Unity 가 가져오지 않는 이름(숨은 자산)은 대상이 아니고, 그 안으로 내려가지도 않는다.
    // 결과는 경로 오름차순(서수 비교)이다. 같은 나무면 같은 순서다.
    public sealed class AssetTree
    {
        public const string MetaSuffix = ".meta";

        // CONTRACTS 11.5: .meta 를 두는 범위. 범위의 뿌리 자신은 대상이 아니다(Assets.meta, src.meta 는 없다).
        public static readonly string[] Scopes = { "src", "unity/Assets" };

        // src/ 의 바로 아래 폴더는 UPM 패키지의 뿌리다(ARCHITECTURE 4.2). Unity 에게 패키지 뿌리는 Assets/ 와 같은 자리라
        // 뿌리 폴더 자신의 .meta(src/Wizard.Core.meta)는 Unity 가 만들지도 읽지도 않는다(6000.3.20f1 에서 확인).
        // 그래서 src/ 에서는 패키지 안의 폴더·파일만 대상이다. src/ 바로 아래의 파일도 어느 패키지에도 들지 않아 대상이 아니다.
        // 단, src/ 바로 아래에 놓인 .meta 는 모아서 주인 없는 .meta 로 잡는다.
        public const string PackageRootsScope = "src";

        // 이 범위의 .meta 는 GUID 가 경로의 MD5 와 같아야 한다(T-34). 그 밖은 이미 있는 값을 그대로 둔다.
        public static readonly string[] StrictGuidScopes = { "src", "unity/Assets/Game" };

        public AssetTree(List<AssetEntry> assets, List<string> metas, List<string> scopesFound)
        {
            Assets = assets;
            Metas = metas;
            ScopesFound = scopesFound;
        }

        // .meta 를 가져야 하는 폴더·파일. 저장소 기준 상대 경로.
        public IReadOnlyList<AssetEntry> Assets { get; }

        // 이미 있는 .meta 파일. 저장소 기준 상대 경로.
        public IReadOnlyList<string> Metas { get; }

        public IReadOnlyList<string> ScopesFound { get; }

        public static AssetTree Scan(string root)
        {
            var assets = new List<AssetEntry>();
            var metas = new List<string>();
            var found = new List<string>();
            foreach (string scope in Scopes)
            {
                string dir = Path.Combine(root, scope);
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                found.Add(scope);
                if (scope == PackageRootsScope)
                {
                    WalkPackageRoots(root, dir, assets, metas);
                }
                else
                {
                    Walk(root, dir, assets, metas);
                }
            }
            assets.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            metas.Sort(string.CompareOrdinal);
            return new AssetTree(assets, metas, found);
        }

        // Unity 의 숨은 자산 규칙(Unity 설명서 "Special folder names"/숨은 자산):
        // '.' 로 시작하는 것, '~' 로 끝나는 것, 이름이 cvs 인 폴더, 확장자가 .tmp 인 파일.
        // 이 기기의 Unity 6000.3.20f1 로 확인했다(결과 보고 참고).
        public static bool IsHidden(string name, bool isDirectory)
        {
            if (name.Length == 0 || name[0] == '.' || name[name.Length - 1] == '~')
            {
                return true;
            }
            if (isDirectory)
            {
                return string.Equals(name, "cvs", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(Path.GetExtension(name), ".tmp", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMetaFileName(string name)
        {
            return name.EndsWith(MetaSuffix, StringComparison.Ordinal) && name.Length > MetaSuffix.Length;
        }

        private static void WalkPackageRoots(string root, string dir, List<AssetEntry> assets, List<string> metas)
        {
            var info = new DirectoryInfo(dir);
            foreach (FileSystemInfo entry in info.EnumerateFileSystemInfos())
            {
                bool isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                if (!isDirectory)
                {
                    if (IsMetaFileName(entry.Name) && entry.Name[0] != '.')
                    {
                        metas.Add(RepoPath.Relative(root, entry.FullName));
                    }
                    continue;
                }
                if (IsHidden(entry.Name, true) || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
                Walk(root, entry.FullName, assets, metas);
            }
        }

        private static void Walk(string root, string dir, List<AssetEntry> assets, List<string> metas)
        {
            var info = new DirectoryInfo(dir);
            foreach (FileSystemInfo entry in info.EnumerateFileSystemInfos())
            {
                bool isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                string name = entry.Name;
                if (!isDirectory && IsMetaFileName(name))
                {
                    // '.' 으로 시작하는 .meta 는 Unity 가 보지 않는 파일이다(.DS_Store.meta 같은 것). 대상에서 뺀다.
                    if (name[0] != '.')
                    {
                        metas.Add(RepoPath.Relative(root, entry.FullName));
                    }
                    continue;
                }
                if (IsHidden(name, isDirectory))
                {
                    continue;
                }
                string relative = RepoPath.Relative(root, entry.FullName);
                assets.Add(new AssetEntry(relative, isDirectory));
                // 심볼릭 링크 폴더는 따라 내려가지 않는다(고리를 막는다). 링크 자신은 대상이다.
                if (isDirectory && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    Walk(root, entry.FullName, assets, metas);
                }
            }
        }
    }
}

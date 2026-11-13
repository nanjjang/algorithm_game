using System;
using System.IO;
using System.Text;

namespace Wizard.ViewLogic.Assets
{
    // 디스크의 art/production 폴더에서 읽는다(dotnet 시험, 도구, 편집기 반입). 실행 중인 게임은 쓰지 않는다
    public sealed class DirectoryAssetSource : IAssetSource
    {
        private readonly string _root;

        public DirectoryAssetSource(string root)
        {
            _root = Path.GetFullPath(root);
        }

        public string Root => _root;

        public bool TryReadText(string path, out string text)
        {
            text = "";
            if (!TryResolve(path, out string full) || !File.Exists(full))
            {
                return false;
            }
            text = File.ReadAllText(full, new UTF8Encoding(false));
            return true;
        }

        public bool Exists(string path) => TryResolve(path, out string full) && File.Exists(full);

        private bool TryResolve(string path, out string full)
        {
            full = "";
            if (!AssetPaths.IsSafeRelative(path))
            {
                return false;
            }
            full = Path.Combine(_root, path.Replace(AssetPaths.Separator, Path.DirectorySeparatorChar));
            return full.StartsWith(_root, StringComparison.Ordinal);
        }
    }
}

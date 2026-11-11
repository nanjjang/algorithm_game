namespace Wizard.MetaGen
{
    public readonly struct AssetEntry
    {
        public AssetEntry(string path, bool isDirectory)
        {
            Path = path;
            IsDirectory = isDirectory;
        }

        // 저장소 기준 상대 경로(RepoPath 정규형).
        public string Path { get; }

        public bool IsDirectory { get; }
    }
}

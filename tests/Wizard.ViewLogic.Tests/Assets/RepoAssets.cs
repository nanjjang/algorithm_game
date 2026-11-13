using System;
using System.IO;
using Wizard.TestKit;
using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Tests.Assets
{
    // 저장소의 art/production 매니페스트 다섯 개와 data/presentation/asset_aliases.json 을 한 번 읽어 둔다.
    // 작업 트리에서 돌면 그 작업 트리의 파일을 읽는다(TestRepo).
    public static class RepoAssets
    {
        public const string ArtRoot = "art/production";

        private static readonly Lazy<AssetLoadResult> Loaded = new Lazy<AssetLoadResult>(Load);

        public static AssetLoadResult Result => Loaded.Value;

        public static AssetRegistry Registry => Result.Registry;

        public static AssetAliases Aliases => Result.Aliases;

        public static string AliasesJson => File.ReadAllText(TestRepo.Path(AssetAliases.RepoPath));

        public static DirectoryAssetSource Source => new DirectoryAssetSource(TestRepo.Path(ArtRoot));

        public static AssetLoadResult Load() => AssetManifestLoader.LoadAll(Source, AliasesJson);

        // 시험 쪽에서만 PNG 머리(IHDR)의 크기를 읽는다. 로더는 그림 파일을 열지 않는다
        public static PixelSize PngSize(string artRelativePath)
        {
            const int HeaderBytes = 24;
            const int WidthOffset = 16;
            const int HeightOffset = 20;
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            byte[] ihdr = { 0x49, 0x48, 0x44, 0x52 };
            string path = TestRepo.Path(ArtRoot + "/" + artRelativePath);
            var head = new byte[HeaderBytes];
            using (FileStream f = File.OpenRead(path))
            {
                int read = 0;
                while (read < HeaderBytes)
                {
                    int n = f.Read(head, read, HeaderBytes - read);
                    if (n == 0)
                    {
                        throw new InvalidDataException("short PNG: " + path);
                    }
                    read += n;
                }
            }
            for (int i = 0; i < signature.Length; i++)
            {
                if (head[i] != signature[i])
                {
                    throw new InvalidDataException("not a PNG: " + path);
                }
            }
            for (int i = 0; i < ihdr.Length; i++)
            {
                if (head[12 + i] != ihdr[i])
                {
                    throw new InvalidDataException("PNG without IHDR first: " + path);
                }
            }
            return new PixelSize(BigEndian(head, WidthOffset), BigEndian(head, HeightOffset));
        }

        private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
    }
}

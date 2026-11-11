using System;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Golden;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.TestKit
{
    // dotnet 테스트에서 저장소 파일을 찾는다. 시작점은 테스트 어셈블리의 위치(artifacts/bin/…)라서
    // 작업 트리에서 돌면 그 작업 트리의 파일을 읽는다(현재 폴더에 기대지 않는다, CLAUDE.md 9절).
    public static class TestRepo
    {
        private static readonly Lazy<string> RootLazy = new Lazy<string>(() => RepoRoot.Find(AppContext.BaseDirectory));

        public static string Root => RootLazy.Value;

        // 저장소 기준 상대 경로('/' 구분) → 절대 경로
        public static string Path(string relative) => RepoRoot.Resolve(Root, relative);

        public static string GoldenRoot => Path("tests/golden");

        public static GoldenCatalog LoadGolden() => GoldenCatalog.Load(GoldenRoot);

        public static VectorConfig LoadVectorConfig() => VectorConfig.LoadFromRepo(Root);
    }
}

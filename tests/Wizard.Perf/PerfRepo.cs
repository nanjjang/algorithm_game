using System;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.TestKit;

namespace Wizard.Perf
{
    // 벤치와 시험이 쓰는 팩: 이 체크아웃의 data/ 를 Wizard.DataBuild 로 묶은 것(Wizard.Sim.Tests 의 WorldData 와 같은 길).
    // 저장소 뿌리는 실행 파일의 위치에서 찾는다(작업 트리에서 돌면 그 작업 트리의 data/ 를 읽는다, CLAUDE.md 9절).
    internal static class PerfRepo
    {
        private static readonly Lazy<ContentDb> DbLazy = new Lazy<ContentDb>(Build);

        public static ContentDb Db => DbLazy.Value;

        private static ContentDb Build()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(),
                                             PackOptions.Default(PackOptions.DefaultDataVersion), d);
            if (d.HasErrors || o.Pack == null)
            {
                throw new InvalidOperationException("perf: data/ does not build: " + string.Join("\n", d.Sorted()));
            }
            return ContentDb.Build(ContentPack.Load(o.Pack));
        }
    }
}

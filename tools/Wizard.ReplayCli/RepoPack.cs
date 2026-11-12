using System;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.ReplayCli
{
    // 저장소의 data/ 로 만든 팩(DataBuild 와 같은 길, 기본 data_version). 골든과 결정론 묶음이 이 팩으로 돈다.
    // 팩이 바뀌어 해시가 바뀌면 그 작업이 골든을 다시 만들고 CHANGELOG 에 적는다(tests/golden/README.md).
    public static class RepoPack
    {
        public static ContentDb Build(string repoRoot)
        {
            if (repoRoot == null)
            {
                throw new ArgumentNullException(nameof(repoRoot));
            }
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(repoRoot, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            if (d.HasErrors || o.Pack == null)
            {
                throw new InvalidOperationException("data build failed:\n" + string.Join("\n", d.Sorted()));
            }
            return ContentDb.Build(ContentPack.Load(o.Pack));
        }
    }
}

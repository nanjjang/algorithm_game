using System;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.BalanceLab.Core
{
    // 저장소의 data/ 로 만든 팩(DataBuild 와 같은 길, 기본 data_version). 계측은 수치를 바꾸지 않고 이 팩의 값 그대로 잰다
    internal static class LabPack
    {
        public static ContentDb Build(string repoRoot)
        {
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

using System;
using Wizard.Content;
using Wizard.Content.Rules;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Netcode.Tests.Clock
{
    // 저장소의 data/rules/net.json 으로 만든 팩의 NetRuleSet(작업 트리의 data 를 읽는다)
    internal static class NetData
    {
        private static readonly Lazy<NetRuleSet> RepoNetLazy = new Lazy<NetRuleSet>(Build);

        public static NetRuleSet RepoNet => RepoNetLazy.Value;

        private static NetRuleSet Build()
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(TestRepo.Root, ModuleRegistry.Discover(),
                                             PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return ContentDb.Build(ContentPack.Load(o.Pack!)).Net;
        }

        // 코드가 경계값을 갖지 않는다는 것을 보이는 데 쓰는, 명세와 다른 표
        public static NetRuleSet WithTable(params (int rttUpToMs, int delayTicks)[] rows)
        {
            var steps = new InputDelayStep[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                steps[i] = new InputDelayStep(rows[i].rttUpToMs, rows[i].delayTicks);
            }
            return new NetRuleSet { InputDelayTable = steps };
        }

        // 코드가 한도(5,000ppm)를 갖지 않는다는 것을 보이는 데 쓰는, 명세와 다른 한도
        public static NetRuleSet WithSlewMax(int ppm) => new NetRuleSet { ClockSlewMaxPpm = ppm };

        // 코드가 창의 크기(16)와 hold 상한을 갖지 않는다는 것을 보이는 데 쓰는, 명세와 다른 창
        public static NetRuleSet WithWindow(int samples, int holdCapMs) =>
            new NetRuleSet { ClockSyncSamples = samples, RttHoldCapMs = holdCapMs };
    }
}

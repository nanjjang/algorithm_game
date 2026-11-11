using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Wizard.Analyzers.Tests
{
    // ARCHITECTURE 3.2 의 의존 방향을 실제로 빌드된 어셈블리의 참조로 확인한다(분석기 WZ0023 은 컴파일 때, 이것은 결과물에서).
    public sealed class LayeringTests
    {
        private static readonly Type[] Anchors =
        {
            typeof(Wizard.Core.Fix64), typeof(Wizard.Content.BlockId), typeof(Wizard.Text.ITextResolver), typeof(Wizard.Sim.SimVersion),
            typeof(Wizard.Rules.RuleCheck), typeof(Wizard.Protocol.IJsonCodec), typeof(Wizard.Netcode.Duel.EventPhase),
            typeof(Wizard.Client.IBackend), typeof(Wizard.ViewLogic.FixConvert),
            typeof(Wizard.Server.Data.IClock), typeof(Wizard.Server.App.Mail.IMailSender), typeof(Wizard.Server.Api.Program),
            typeof(Wizard.Server.Duel.Program), typeof(Wizard.Server.Verify.Program), typeof(Wizard.Server.Admin.Program),
            typeof(Wizard.Server.Api.DevHost.Program),
        };

        [Fact]
        public void BuiltAssembliesRespectDependencyDirection()
        {
            foreach (Type anchor in Anchors)
            {
                Assembly asm = anchor.Assembly;
                string name = asm.GetName().Name!;
                foreach (AssemblyName r in asm.GetReferencedAssemblies())
                {
                    Assert.False(AssemblyRoles.IsForbiddenReference(name, r.Name!), $"{name} must not reference {r.Name}");
                }
            }
        }

        [Fact]
        public void SharedPackagesTargetNetStandard21()
        {
            foreach (Type anchor in Anchors.Take(9))
            {
                var tfm = anchor.Assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
                Assert.Equal(".NETStandard,Version=v2.1", tfm?.FrameworkName);
            }
        }

        [Fact]
        public void CoreAssembliesHaveNoExternalDependencies()
        {
            foreach (Type anchor in Anchors.Take(4))
            {
                foreach (AssemblyName r in anchor.Assembly.GetReferencedAssemblies())
                {
                    Assert.True(r.Name == "netstandard" || r.Name!.StartsWith("Wizard.", StringComparison.Ordinal),
                        $"{anchor.Assembly.GetName().Name} references external {r.Name}");
                }
            }
        }
    }
}

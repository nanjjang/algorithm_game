using System;
using System.Collections.Generic;

namespace Wizard.Analyzers
{
    // 어셈블리 이름으로 적용할 규칙 묶음을 정한다. 분석기는 컴파일의 어셈블리 이름만 본다.
    public static class AssemblyRoles
    {
        public static readonly string[] SharedPackages =
        {
            "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules",
            "Wizard.Protocol", "Wizard.Netcode", "Wizard.Client", "Wizard.ViewLogic", "Wizard.TestKit.Shared",
        };

        // ARCHITECTURE 3.2 의 허용 참조(전이 포함). 목록에 없는 Wizard.* 참조는 WZ0023 이다.
        private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Wizard.Core"] = Array.Empty<string>(),
            ["Wizard.Content"] = new[] { "Wizard.Core" },
            ["Wizard.Text"] = new[] { "Wizard.Core" },
            ["Wizard.Sim"] = new[] { "Wizard.Core", "Wizard.Content" },
            ["Wizard.Rules"] = new[] { "Wizard.Core", "Wizard.Content", "Wizard.Sim" },
            ["Wizard.Protocol"] = new[] { "Wizard.Core", "Wizard.Content", "Wizard.Sim", "Wizard.Rules" },
            ["Wizard.Netcode"] = new[] { "Wizard.Core", "Wizard.Content", "Wizard.Sim", "Wizard.Rules", "Wizard.Protocol" },
            ["Wizard.Client"] = new[] { "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules", "Wizard.Protocol", "Wizard.Netcode" },
            ["Wizard.ViewLogic"] = new[] { "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules", "Wizard.Protocol", "Wizard.Netcode", "Wizard.Client" },
            ["Wizard.TestKit.Shared"] = new[] { "Wizard.Core" },
        };

        // 서버 프로젝트의 금지 참조(나머지 Wizard.* 는 허용)
        private static readonly Dictionary<string, string[]> ServerForbidden = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Wizard.Server.Duel"] = new[] { "Wizard.Server.Data", "Wizard.Server.App", "Wizard.Server.Api", "Wizard.Server.Payments.Fakes" },
            ["Wizard.Server.Api"] = new[] { "Wizard.Server.Payments.Fakes" },
            ["Wizard.Server.App"] = new[] { "Wizard.Server.Payments.Fakes" },
            ["Wizard.Server.Data"] = new[] { "Wizard.Server.Payments.Fakes" },
            ["Wizard.Server.Admin"] = new[] { "Wizard.Server.Payments.Fakes" },
            ["Wizard.Server.Verify"] = new[] { "Wizard.Server.Payments.Fakes", "Wizard.Server.App" },
        };

        private static readonly string[] AlwaysForbiddenForServer = { "Wizard.Client", "Wizard.ViewLogic" };

        public static bool IsCore(string? name) => name == "Wizard.Core" || name == "Wizard.Content" || name == "Wizard.Sim";

        public static bool IsShared(string? name) => name != null && Array.IndexOf(SharedPackages, name) >= 0;

        public static bool BansFloatingPoint(string? name) => IsCore(name) || name == "Wizard.Rules";

        public static bool BansClock(string? name) => IsCore(name) || name == "Wizard.Netcode";

        public static bool IsServer(string? name) => name != null && name.StartsWith("Wizard.Server.", StringComparison.Ordinal);

        // 허용되지 않은 참조인가
        public static bool IsForbiddenReference(string assembly, string reference)
        {
            if (!reference.StartsWith("Wizard.", StringComparison.Ordinal) || reference == assembly)
            {
                return false;
            }
            if (Allowed.TryGetValue(assembly, out string[]? allowed))
            {
                return Array.IndexOf(allowed, reference) < 0;
            }
            if (IsServer(assembly))
            {
                if (Array.IndexOf(AlwaysForbiddenForServer, reference) >= 0)
                {
                    return true;
                }
                return ServerForbidden.TryGetValue(assembly, out string[]? forbidden) && Array.IndexOf(forbidden, reference) >= 0;
            }
            return false;
        }
    }
}

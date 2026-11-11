using System;

namespace Wizard.TestKit.Shared.Golden
{
    // 골든을 돌린 (OS, 런타임, 실행 방식, 아키텍처) 조합. 결과 파일 하나가 조합 하나다(T-30, T-39).
    // 이름: "<os>-<runtime>-<host>-<arch>". 예: macos-dotnet-test-arm64, macos-mono-editor-arm64,
    //       macos-mono-player-x64, macos-il2cpp-player-arm64, windows-mono-player-x64
    public readonly struct GoldenCombo : IEquatable<GoldenCombo>
    {
        public static readonly string[] Oses = { "macos", "windows", "linux" };
        public static readonly string[] Runtimes = { "dotnet", "mono", "il2cpp" };
        public static readonly string[] Hosts = { "test", "editor", "player" };
        public static readonly string[] Arches = { "arm64", "x64" };

        public GoldenCombo(string os, string runtime, string host, string arch)
        {
            Check(Oses, os, "os");
            Check(Runtimes, runtime, "runtime");
            Check(Hosts, host, "host");
            Check(Arches, arch, "arch");
            Os = os;
            Runtime = runtime;
            Host = host;
            Arch = arch;
        }

        public string Os { get; }
        public string Runtime { get; }
        public string Host { get; }
        public string Arch { get; }

        public string Name => Os + "-" + Runtime + "-" + Host + "-" + Arch;

        // 결과 파일 이름. 기본 위치는 저장소의 artifacts/golden/ 이다(커밋하지 않는다. 다른 기기의 결과는 게이트에 붙인다)
        public string ResultFileName => "golden-result." + Name + ".json";

        public static bool TryParse(string? name, out GoldenCombo combo)
        {
            combo = default;
            if (name == null)
            {
                return false;
            }
            string[] parts = name.Split('-');
            if (parts.Length != 4 || Array.IndexOf(Oses, parts[0]) < 0 || Array.IndexOf(Runtimes, parts[1]) < 0 ||
                Array.IndexOf(Hosts, parts[2]) < 0 || Array.IndexOf(Arches, parts[3]) < 0)
            {
                return false;
            }
            combo = new GoldenCombo(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        public bool Equals(GoldenCombo other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is GoldenCombo other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

        public override string ToString() => Name;

        private static void Check(string[] allowed, string value, string what)
        {
            if (Array.IndexOf(allowed, value) < 0)
            {
                throw new ArgumentException(what + " '" + value + "' is not one of " + string.Join(", ", allowed));
            }
        }
    }
}

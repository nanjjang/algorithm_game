using System;
using System.Runtime.InteropServices;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.TestKit
{
    // dotnet 테스트 프로세스의 골든 조합. x64 는 `dotnet test -a x64`(Rosetta 2)로 돌 때 나온다(T-30).
    public static class DotnetCombo
    {
        public static GoldenCombo Current()
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : "linux";
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture other => throw new PlatformNotSupportedException("golden combos cover arm64 and x64, not " + other),
            };
            return new GoldenCombo(os, "dotnet", "test", arch);
        }
    }
}

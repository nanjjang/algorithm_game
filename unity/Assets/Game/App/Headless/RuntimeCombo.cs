#nullable enable
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless
{
    // 이 Unity 프로세스의 골든 조합(tests/golden/README.md "조합별 결과 파일"): <os>-<runtime>-<host>-<arch>.
    //   os       Application.platform
    //   runtime  IL2CPP 로 만든 플레이어면 il2cpp, 그 밖(편집기, Mono 플레이어)은 mono
    //   host     편집기면 editor, 플레이어면 player
    //   arch     프로세스의 아키텍처. Rosetta 2 로 도는 Intel 64-bit 플레이어는 x64 다
    public static class RuntimeCombo
    {
        public static GoldenCombo Detect()
        {
            return new GoldenCombo(Os(Application.platform), Runtime(), Application.isEditor ? "editor" : "player",
                                   Arch(RuntimeInformation.ProcessArchitecture));
        }

        public static string Runtime()
        {
#if ENABLE_IL2CPP
            return "il2cpp";
#else
            return "mono";
#endif
        }

        public static string Os(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "macos";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "windows";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                default:
                    throw new PlatformNotSupportedException("golden combos cover macOS, Windows and Linux desktops, not " + platform);
            }
        }

        public static string Arch(Architecture architecture)
        {
            switch (architecture)
            {
                case Architecture.Arm64:
                    return "arm64";
                case Architecture.X64:
                    return "x64";
                default:
                    throw new PlatformNotSupportedException("golden combos cover arm64 and x64, not " + architecture);
            }
        }
    }
}

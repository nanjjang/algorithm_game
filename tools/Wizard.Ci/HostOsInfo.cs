using System;

namespace Wizard.Ci
{
    // 이 기기의 OS 와 OS 별 작은 규칙
    public static class HostOsInfo
    {
        public static HostOs Current =>
            OperatingSystem.IsWindows() ? HostOs.Windows : OperatingSystem.IsMacOS() ? HostOs.MacOS : HostOs.Linux;

        // 그 OS 의 PATH 구분자
        public static char PathSeparator(HostOs os) => os == HostOs.Windows ? ';' : ':';
    }
}

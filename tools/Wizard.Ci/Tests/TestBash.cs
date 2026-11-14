using System;
using System.IO;

namespace Wizard.Ci.Tests
{
    // 시험이 셸 명령과 스크립트를 띄울 bash(제품 코드와 같은 BashLocator 의 순서: WIZARD_BASH > Git for Windows > /bin/bash. T-67 3번).
    // 판정 함수가 프로세스 환경을 읽지 않게(FIX-4) 프로세스 환경을 읽는 일은 시험 쪽의 이 도우미가 한다.
    internal static class TestBash
    {
        private static readonly Lazy<string> Found = new Lazy<string>(() =>
            BashLocator.Locate(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows(), File.Exists, BashLocator.GitExecPath));

        public static string Path => Found.Value;
    }
}

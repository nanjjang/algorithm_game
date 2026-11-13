#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using Wizard.TestKit.Shared.Golden;
using Wizard.Unity.App.Headless;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // 조합 이름(tests/golden/README.md): <os>-<runtime>-<host>-<arch>
    public sealed class RuntimeComboTests
    {
        [Test]
        public void PlatformsMapToComboOsNames()
        {
            Assert.That(RuntimeCombo.Os(RuntimePlatform.OSXPlayer), Is.EqualTo("macos"));
            Assert.That(RuntimeCombo.Os(RuntimePlatform.OSXEditor), Is.EqualTo("macos"));
            Assert.That(RuntimeCombo.Os(RuntimePlatform.WindowsPlayer), Is.EqualTo("windows"));
            Assert.That(RuntimeCombo.Os(RuntimePlatform.LinuxPlayer), Is.EqualTo("linux"));
            Assert.Throws<PlatformNotSupportedException>(() => RuntimeCombo.Os(RuntimePlatform.Android));
        }

        [Test]
        public void ArchitecturesMapToComboArchNames()
        {
            Assert.That(RuntimeCombo.Arch(Architecture.Arm64), Is.EqualTo("arm64"));
            Assert.That(RuntimeCombo.Arch(Architecture.X64), Is.EqualTo("x64"));
            Assert.Throws<PlatformNotSupportedException>(() => RuntimeCombo.Arch(Architecture.X86));
        }

        // 편집기는 Mono 이고 host 는 editor 다. 아키텍처는 운영체제가 보는 이 프로세스의 것과 같다(Rosetta 2 로 돌면 x64)
        [Test]
        public void TheEditorIsAMonoEditorComboOfThisProcessArchitecture()
        {
            GoldenCombo combo = RuntimeCombo.Detect();
            Assert.That(combo.Runtime, Is.EqualTo("mono"));
            Assert.That(combo.Host, Is.EqualTo("editor"));
            Assert.That(combo.Os, Is.EqualTo(RuntimeCombo.Os(Application.platform)));
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                return;                                   // uname 대조는 macOS 편집기(이 프로젝트의 개발 기기)에서만 한다
            }
            string uname = Uname();
            Assert.That(combo.Arch, Is.EqualTo(uname == "x86_64" ? "x64" : uname), "uname -m says " + uname);
        }

        // uname 은 부른 프로세스의 아키텍처를 물려받는다(Rosetta 2 아래에서는 x86_64)
        private static string Uname()
        {
            var info = new ProcessStartInfo("/usr/bin/uname", "-m")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using (Process p = Process.Start(info)!)
            {
                string text = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                return text;
            }
        }
    }
}

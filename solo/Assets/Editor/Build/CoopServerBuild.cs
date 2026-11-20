using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wizard.Solo.EditorTools
{
    /// <summary>
    /// 협동 전용 서버 빌드(11-online.md 3단계). 보통 맥 플레이어 빌드와 같은 것이고, 실행할 때 -batchmode -nographics -coopServer로 띄운다.
    /// (Dedicated Server 모듈이 설치되면 StandaloneBuildSubtarget.Server로 바꿔 더 가볍게 만들 수 있다.)
    /// 배치 모드: Unity -batchmode -quit -projectPath solo -executeMethod Wizard.Solo.EditorTools.CoopServerBuild.Build
    /// 결과: solo/Builds/CoopServer/Wizard.app
    /// </summary>
    public static class CoopServerBuild
    {
        public const string OutDir = "Builds/CoopServer";

        [MenuItem("Wizard/Build Coop Server (macOS)")]
        public static void Build()
        {
            var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes in build settings (run Wizard/Project Setup).");
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, "Wizard.app"),
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            Debug.Log("[coop-server-build] " + sum.result + " " + sum.totalSize + " bytes, " + sum.totalErrors + " errors -> " + opts.locationPathName);
            if (sum.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}

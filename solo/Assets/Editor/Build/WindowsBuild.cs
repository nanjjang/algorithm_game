using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wizard.Solo.EditorTools
{
    /// <summary>
    /// 윈도우(64비트) 플레이어 빌드.
    /// 배치 모드: Unity -batchmode -quit -projectPath solo -executeMethod Wizard.Solo.EditorTools.WindowsBuild.Build
    /// 결과: solo/Builds/Windows/Wizard.exe (폴더째로 옮겨야 실행된다)
    /// </summary>
    public static class WindowsBuild
    {
        public const string OutDir = "Builds/Windows";

        [MenuItem("Wizard/Build Windows (x64)")]
        public static void Build()
        {
            var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes in build settings (run Wizard/Project Setup).");
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, "Wizard.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            Debug.Log("[windows-build] " + sum.result + " " + sum.totalSize + " bytes, " + sum.totalErrors + " errors -> " + opts.locationPathName);
            if (sum.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}

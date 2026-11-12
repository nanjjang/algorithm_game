using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Log = UnityEngine.Debug;

namespace Wizard.Unity.Editor
{
    // 배치 모드 진입점(ARCHITECTURE 4.1, PIPELINE 4.5). Launcher~/unity.sh 가 metagen 을 먼저 돌리고 부른다.
    // 여기 있는 진입점은 전부 동기로 끝나므로 -quit 와 같이 쓴다.
    // 테스트 진입점은 여기에 두지 않는다. 테스트는 -runTests 로 돌린다(-quit 가 붙으면 테스트가 시작도 하기 전에 0으로 끝난다).
    public static class Ci
    {
        // T-32: 장면 넷, 각 장면에는 시작 개체 하나만. 나머지는 코드가 만든다.
        public static readonly string[] SceneNames = { "Boot", "Menu", "Battle", "Plaza" };
        public const string ScenesFolder = "Assets/Scenes";
        public const string StartObjectName = "Bootstrap";

        // 프로젝트에 있어야 하는 asmdef. 공유 9 + TestKit.Shared + Game 5 + 편집기 + 테스트(ARCHITECTURE 3.1, 4.2, 4.3).
        private static readonly string[] AsmdefNames =
        {
            "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules",
            "Wizard.Protocol", "Wizard.Netcode", "Wizard.Client", "Wizard.ViewLogic",
            "Wizard.TestKit.Shared",
            "Wizard.Unity.App", "Wizard.Unity.Input", "Wizard.Unity.Presentation", "Wizard.Unity.UI", "Wizard.Unity.Debug",
            "Wizard.Unity.Editor.Ci", "Wizard.Unity.Editor.Ci.Tests", "Wizard.Unity.Editor.Ci.PlayModeProbe",
            "Wizard.Unity.Tests.EditMode", "Wizard.Unity.Tests.PlayMode",
        };

        // 소스가 있어 반드시 컴파일되어야 하는 어셈블리. 소스가 없는 asmdef 는 Unity 가 컴파일하지 않는다.
        private static readonly string[] MustCompile =
        {
            "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules",
            "Wizard.Protocol", "Wizard.Netcode", "Wizard.Client", "Wizard.ViewLogic",
            "Wizard.TestKit.Shared",
            "Wizard.Unity.Presentation",
            "Wizard.Unity.Editor.Ci",
        };

        private const string Il2CppInstallCommand =
            "\"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub\" -- --headless install-modules --version {0} -m mac-il2cpp";

        // -batchmode -nographics -quit -executeMethod Wizard.Unity.Editor.Ci.Compile
        public static void Compile()
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                Fail("scripts have compile errors");
                return;
            }

            var compiled = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                compiled.Add(assembly.name);
            }

            var missing = new List<string>();
            foreach (string name in AsmdefNames)
            {
                if (CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name) == null)
                {
                    missing.Add(name + " (asmdef not found)");
                }
            }
            foreach (string name in MustCompile)
            {
                if (!compiled.Contains(name))
                {
                    missing.Add(name + " (not compiled)");
                }
            }
            if (missing.Count > 0)
            {
                Fail("missing assemblies: " + string.Join(", ", missing));
                return;
            }
            Log.Log("ci: compile ok (" + compiled.Count + " editor assemblies)");
        }

        // 설정을 코드로 맞춘다. unity.sh apply-settings 만 부른다(이 모드만 Unity 가 쓴 설정 파일을 남긴다).
        public static void ApplyProjectSettings()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Minimal); // T-44
            Physics2D.simulationMode = SimulationMode2D.Script;                                                 // ARCHITECTURE 4.1
            Physics.simulationMode = SimulationMode.Script;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (string name in SceneNames)
            {
                string path = ScenesFolder + "/" + name + ".unity";
                if (!File.Exists(path))
                {
                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject(StartObjectName);
                    if (!EditorSceneManager.SaveScene(scene, path))
                    {
                        Fail("cannot save scene " + path);
                        return;
                    }
                }
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Log.Log("ci: project settings applied");
        }

        // -batchmode -nographics -quit -executeMethod Wizard.Unity.Editor.Ci.BuildPlayer -wizardBackend mono|il2cpp -wizardArch arm64|x64
        //   [-wizardContentPack <팩 경로>](없으면 <저장소>/artifacts/content/base.wzpack = databuild build 의 기본 출력)
        // 결과: <저장소>/artifacts/unity/<backend>-<arch>/Wizard.app 와 build.json(팩의 SHA-256 포함)
        public static void BuildPlayer()
        {
            string? backend = Arg("-wizardBackend");
            string? arch = Arg("-wizardArch");
            ScriptingImplementation implementation;
            if (backend == "mono")
            {
                implementation = ScriptingImplementation.Mono2x;
            }
            else if (backend == "il2cpp")
            {
                implementation = ScriptingImplementation.IL2CPP;
            }
            else
            {
                Fail("-wizardBackend must be mono or il2cpp (got '" + backend + "')");
                return;
            }
            if (arch != "arm64" && arch != "x64")
            {
                Fail("-wizardArch must be arm64 or x64 (got '" + arch + "')");
                return;
            }
            if (implementation == ScriptingImplementation.IL2CPP && !HasMacIl2CppModule())
            {
                // 건너뛰지 않고 실패한다(PIPELINE 4.6, T-30).
                Fail("mac-il2cpp module is not installed. The user installs it: " + string.Format(Il2CppInstallCommand, Application.unityVersion));
                return;
            }
            if (PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) != ManagedStrippingLevel.Minimal)
            {
                Fail("Managed Stripping Level for Standalone must be Minimal (T-44)");
                return;
            }
            if (!SetMacArchitecture(arch))
            {
                return;
            }

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outDir = Path.Combine(repoRoot, "artifacts", "unity", backend + "-" + arch);
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, true);
            }
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(outDir, "Wizard.app"),
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = HasArg("-wizardDevelopment") ? BuildOptions.Development : BuildOptions.None,
            };

            // 기본 팩을 StreamingAssets/content/ 에 놓는다(ARCHITECTURE 8.2, FIX-3). 팩은 플레이어 빌드 스크립트가 Unity 를 띄우기 전에
            // databuild build 로 만든다. 없거나 읽히지 않으면 건너뛰지 않고 실패한다(팩 없는 플레이어는 조립 루트가 세션을 꽂지 않는다).
            string packSource = Path.GetFullPath(Arg("-wizardContentPack") ?? ContentPackStage.DefaultSourcePath(repoRoot));
            if (!ContentPackStage.TryPlace(packSource, Application.streamingAssetsPath, ContentPackStage.LoadValidator(),
                    out ContentPackStage? stage, out string staged) || stage == null)
            {
                Fail(staged);
                return;
            }
            Log.Log("ci: " + staged);

            ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            BuildReport report;
            string unstaged;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, implementation);
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous);
                // 놓은 팩과 Unity 가 만든 .meta 를 지운다(성공이든 실패든). *.wzpack 은 커밋하지 않는다
                if (!stage.TryRemove(out unstaged))
                {
                    Log.LogError("ci: " + unstaged);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            Log.Log("ci: " + unstaged);

            // 빌드된 앱 안의 팩이 databuild 출력과 바이트가 같은지 SHA-256 으로 남긴다. 스크립트가 다시 대조한다
            string playerPack = ContentPackStage.PlayerPackPath(options.locationPathName);
            string playerPackSha = File.Exists(playerPack) ? ContentPackStage.Sha256Hex(File.ReadAllBytes(playerPack)) : "";

            var summary = new BuildSummary
            {
                backend = backend,
                arch = arch,
                unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(),
                totalErrors = report.summary.totalErrors,
                totalSizeBytes = (long)report.summary.totalSize,
                output = options.locationPathName,
                contentPackSource = packSource,
                contentPackSha256 = stage.Sha256,
                playerContentPack = playerPack,
                playerContentPackSha256 = playerPackSha,
                contentPackUnstaged = stage.Removed,
            };
            File.WriteAllText(Path.Combine(outDir, "build.json"), JsonUtility.ToJson(summary, true) + "\n");

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail("player build " + backend + "-" + arch + " ended with " + report.summary.result);
                return;
            }
            if (!stage.Removed)
            {
                Fail(unstaged);
                return;
            }
            if (playerPackSha.Length == 0)
            {
                Fail("the built player has no content pack at " + playerPack);
                return;
            }
            if (playerPackSha != stage.Sha256)
            {
                Fail("the content pack in the player (" + playerPack + ", sha256 " + playerPackSha + ") differs from " + packSource + " (sha256 " + stage.Sha256 + ")");
                return;
            }
            Log.Log("ci: player built at " + options.locationPathName + " with content pack sha256 " + playerPackSha);
        }

        private static bool HasMacIl2CppModule()
        {
            string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.StandaloneOSX, BuildOptions.None);
            string variations = Path.Combine(engine, "Variations");
            return Directory.Exists(variations) && Directory.GetDirectories(variations, "*il2cpp*").Length > 0;
        }

        private static bool SetMacArchitecture(string arch)
        {
#if UNITY_EDITOR_OSX
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = arch == "arm64" ? OSArchitecture.ARM64 : OSArchitecture.x64;
            return true;
#else
            Fail("macOS players are built only from the macOS editor (arch " + arch + ")");
            return false;
#endif
        }

        private static string? Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static bool HasArg(string name)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        }

        private static void Fail(string message)
        {
            Log.LogError("ci: " + message);
            EditorApplication.Exit(1);
        }

        [Serializable]
        private sealed class BuildSummary
        {
            public string? backend;
            public string? arch;
            public string? unityVersion;
            public string? result;
            public int totalErrors;
            public long totalSizeBytes;
            public string? output;
            public string? contentPackSource;          // databuild 출력(기본 <저장소>/artifacts/content/base.wzpack)
            public string? contentPackSha256;          // 그 바이트의 SHA-256(소문자 hex)
            public string? playerContentPack;          // 앱 안의 StreamingAssets/content/base.wzpack
            public string? playerContentPackSha256;    // 그 바이트의 SHA-256. 없으면 빈 문자열
            public bool contentPackUnstaged;           // 놓은 팩과 .meta 를 지웠는가
        }
    }
}

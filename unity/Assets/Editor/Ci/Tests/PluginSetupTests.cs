using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace Wizard.Unity.Editor
{
    // FIX-6: Assets/Plugins 의 관리 DLL 이 편집기와 Standalone 플레이어 컴파일에 모두 들어가는지 본다(ARCHITECTURE 4.2, T-37).
    // 결함의 경위: .meta 의 serializedVersion 이 platformData 의 꼴과 어긋나면 편집기 컴파일에는 DLL 이 들어가고
    // 플레이어 컴파일(rsp)에는 빠져서, 플레이어 빌드에서만 CS0246 이 난다. 병합 게이트는 EditMode 만 돌리므로 여기서 잡는다.
    public sealed class PluginSetupTests
    {
        private const string PluginFolder = "Assets/Plugins";

        private static readonly BuildTarget[] StandaloneTargets =
        {
            BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64,
        };

        private static string[] ManagedPluginPaths()
        {
            return Directory.GetFiles(PluginFolder, "*.dll", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => AssetImporter.GetAtPath(p) is PluginImporter importer && !importer.isNativePlugin)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();
        }

        [Test]
        public void ManagedPlugins_AreEnabledForEditorAndStandaloneTargets()
        {
            string[] paths = ManagedPluginPaths();
            Assert.That(paths, Is.Not.Empty, "Assets/Plugins has no managed DLL");
            var failures = new List<string>();
            foreach (string path in paths)
            {
                var importer = (PluginImporter)AssetImporter.GetAtPath(path);
                // GetCompatibleWith{Editor,Platform} 는 플랫폼별 칸만 돌려준다. "Any" 로 켠 DLL 은 Any 가 켜지고 그 대상이 빠지지 않았는지로 본다.
                bool any = importer.GetCompatibleWithAnyPlatform();
                if (!((any && !importer.GetExcludeEditorFromAnyPlatform()) || importer.GetCompatibleWithEditor()))
                {
                    failures.Add($"{path}: not enabled for Editor");
                }
                if (!PluginImporter.GetImporters("Editor").Any(i => i.assetPath == path))
                {
                    failures.Add($"{path}: missing from PluginImporter.GetImporters(\"Editor\")");
                }
                foreach (BuildTarget target in StandaloneTargets)
                {
                    if (!((any && !importer.GetExcludeFromAnyPlatform(target)) || importer.GetCompatibleWithPlatform(target)))
                    {
                        failures.Add($"{path}: not enabled for {target}");
                    }
                    // 플레이어 빌드가 실제로 고르는 목록. 위 값과 따로 본다(빌드는 이 목록으로 rsp 를 만든다).
                    bool picked = PluginImporter.GetImporters(target).Any(i => i.assetPath == path);
                    if (!picked)
                    {
                        failures.Add($"{path}: missing from PluginImporter.GetImporters({target})");
                    }
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void ClientPrecompiledReferences_InPluginsFolder_AreAllChecked()
        {
            string? asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Wizard.Client");
            Assert.That(asmdefPath, Is.Not.Null, "Wizard.Client.asmdef");
            JObject asmdef = JObject.Parse(File.ReadAllText(asmdefPath!));
            string[] references = ((JArray)asmdef["precompiledReferences"]!).Select(t => (string)t!).ToArray();

            var checkedNames = new HashSet<string>(ManagedPluginPaths().Select(Path.GetFileName), System.StringComparer.Ordinal);
            string[] inPlugins = Directory.GetFiles(PluginFolder, "*.dll", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(n => references.Contains(n))
                .ToArray();

            // 지금 Assets/Plugins 에서 Wizard.Client 가 가리키는 DLL 은 BouncyCastle 하나다(Newtonsoft.Json 은 패키지에서 온다).
            Assert.That(inPlugins, Has.Member("BouncyCastle.Cryptography.dll"));
            foreach (string name in inPlugins)
            {
                Assert.That(checkedNames, Has.Member(name), $"{name} is referenced by Wizard.Client but not checked as a managed plugin");
            }
        }
    }
}

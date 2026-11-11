using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Wizard.Unity.Editor
{
    // M0-UE-01 수락 조건을 편집기 안에서 확인한다. 기대값은 ARCHITECTURE 4.1~4.3, T-02, T-32, T-44 의 글 그대로다.
    public sealed class ProjectSetupTests
    {
        private static readonly string[] SharedPackages =
        {
            "Wizard.Core", "Wizard.Content", "Wizard.Text", "Wizard.Sim", "Wizard.Rules",
            "Wizard.Protocol", "Wizard.Netcode", "Wizard.Client", "Wizard.ViewLogic",
        };

        private static readonly string[] SceneNames = { "Boot", "Menu", "Battle", "Plaza" };

        // ---- 패키지 (ARCHITECTURE 4.2, T-02) ----

        [Test]
        public void Manifest_ReferencesNineSharedPackagesAndTestKitByFile()
        {
            JObject manifest = JObject.Parse(File.ReadAllText("Packages/manifest.json"));
            var deps = (JObject)manifest["dependencies"]!;
            var expected = new Dictionary<string, string>
            {
                { "com.wizard.core", "file:../../src/Wizard.Core" },
                { "com.wizard.content", "file:../../src/Wizard.Content" },
                { "com.wizard.text", "file:../../src/Wizard.Text" },
                { "com.wizard.sim", "file:../../src/Wizard.Sim" },
                { "com.wizard.rules", "file:../../src/Wizard.Rules" },
                { "com.wizard.protocol", "file:../../src/Wizard.Protocol" },
                { "com.wizard.netcode", "file:../../src/Wizard.Netcode" },
                { "com.wizard.client", "file:../../src/Wizard.Client" },
                { "com.wizard.viewlogic", "file:../../src/Wizard.ViewLogic" },
                { "com.wizard.testkit.shared", "file:../../tests/Wizard.TestKit/Shared" },
            };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.That((string?)deps[pair.Key], Is.EqualTo(pair.Value), pair.Key);
                string folder = Path.Combine("Packages", pair.Value.Substring("file:".Length));
                JObject package = JObject.Parse(File.ReadAllText(Path.Combine(folder, "package.json")));
                Assert.That((string?)package["name"], Is.EqualTo(pair.Key), folder);
            }
            var wizardDeps = deps.Properties().Select(p => p.Name).Where(n => n.StartsWith("com.wizard.")).ToList();
            Assert.That(wizardDeps, Is.EquivalentTo(expected.Keys), "no other com.wizard.* packages");
        }

        [Test]
        public void SharedAsmdefs_HaveNoEngineReferences()
        {
            foreach (string name in SharedPackages)
            {
                string? path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                Assert.That(path, Is.Not.Null, name);
                JObject asmdef = JObject.Parse(File.ReadAllText(path!));
                Assert.That((bool?)asmdef["noEngineReferences"], Is.True, name);
            }
        }

        [Test]
        public void GameAsmdefs_AreTheFiveLayers()
        {
            string[] names = Directory.GetFiles("Assets/Game", "*.asmdef", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "Wizard.Unity.App", "Wizard.Unity.Debug", "Wizard.Unity.Input", "Wizard.Unity.Presentation", "Wizard.Unity.UI",
            }));
        }

        [Test]
        public void DebugAssembly_IsLeftOutOfReleasePlayers()
        {
            string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Wizard.Unity.Debug")!;
            JObject asmdef = JObject.Parse(File.ReadAllText(path));
            string[] constraints = ((JArray)asmdef["defineConstraints"]!).Select(t => (string)t!).ToArray();
            Assert.That(constraints, Is.EqualTo(new[] { "UNITY_EDITOR || DEVELOPMENT_BUILD" }));
        }

        // ---- 설정 (ARCHITECTURE 4.1) ----

        [Test]
        public void Input_ActiveHandlingIsInputSystemOnly()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Assert.Pass();
#else
            Assert.Fail("Active Input Handling must be 'Input System Package (New)' only");
#endif
        }

        [Test]
        public void Physics2D_AutoSimulationIsOff()
        {
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
        }

        [Test]
        public void Physics3D_AutoSimulationIsOff()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));
        }

        [Test]
        public void Rendering_Is2DUniversalPipelineOnEveryQualityLevel()
        {
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                Assert.That(asset, Is.Not.Null, QualitySettings.names[i]);
                var renderers = new SerializedObject(asset).FindProperty("m_RendererDataList");
                Assert.That(renderers.arraySize, Is.GreaterThan(0), QualitySettings.names[i]);
                for (int r = 0; r < renderers.arraySize; r++)
                {
                    Assert.That(renderers.GetArrayElementAtIndex(r).objectReferenceValue, Is.InstanceOf<Renderer2DData>(), QualitySettings.names[i]);
                }
            }
            Assert.That(EditorSettings.defaultBehaviorMode, Is.EqualTo(EditorBehaviorMode.Mode2D));
        }

        [Test]
        public void Target_IsDesktopStandaloneWithMonoForDevelopment()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            Assert.That(new[] { BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64 }, Has.Member(target));
            Assert.That(BuildPipeline.GetBuildTargetGroup(target), Is.EqualTo(BuildTargetGroup.Standalone));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), Is.EqualTo(ScriptingImplementation.Mono2x));
        }

        // ---- IL2CPP 코드 제거 (T-44) ----

        [Test]
        public void Stripping_IsMinimalForStandalone()
        {
            Assert.That(PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone), Is.EqualTo(ManagedStrippingLevel.Minimal));
        }

        [Test]
        public void LinkXml_PreservesProtocolClientBouncyCastleNewtonsoft()
        {
            XDocument doc = XDocument.Load("Assets/link.xml");
            var preserved = doc.Root!.Elements("assembly")
                .Where(e => (string?)e.Attribute("preserve") == "all")
                .Select(e => (string)e.Attribute("fullname")!)
                .ToList();
            Assert.That(preserved, Is.SupersetOf(new[] { "Wizard.Protocol", "Wizard.Client", "BouncyCastle.Cryptography", "Newtonsoft.Json" }));
        }

        // ---- 장면 (ARCHITECTURE 4.3, T-32) ----

        [Test]
        public void Scenes_BuildListIsBootMenuBattlePlaza()
        {
            string[] paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(paths, Is.EqualTo(SceneNames.Select(n => "Assets/Scenes/" + n + ".unity").ToArray()));
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(SceneNames.Length), "no disabled extra scenes");
            string[] files = Directory.GetFiles("Assets/Scenes", "*.unity").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
            Assert.That(files, Is.EqualTo(SceneNames.OrderBy(n => n, System.StringComparer.Ordinal).ToArray()));
        }

        [TestCase("Boot")]
        [TestCase("Menu")]
        [TestCase("Battle")]
        [TestCase("Plaza")]
        public void Scene_HasOnlyOneStartObject(string name)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots.Length, Is.EqualTo(1), name + ": root objects");
                Assert.That(roots[0].transform.childCount, Is.EqualTo(0), name + ": children");
                Assert.That(roots[0].GetComponents<Component>().Length, Is.EqualTo(1), name + ": only Transform");
                Assert.That(roots[0].name, Is.EqualTo("Bootstrap"), name);
            }
            finally
            {
                if (SceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}

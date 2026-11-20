using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Wizard.Solo.Editor
{
    /// <summary>
    /// 프로젝트 설정을 코드로 맞춘다(통합 단계에서 한 번, 배치 모드):
    ///   Unity.exe -batchmode -quit -projectPath &lt;solo&gt; -executeMethod Wizard.Solo.Editor.ProjectSetup.Run -logFile &lt;절대경로&gt;
    /// 1. 빈 장면(카메라·라이트 없음)을 Assets/Scenes/Main.unity로 저장 — Bootstrap이 모든 것을 코드로 만든다.
    /// 2. EditorBuildSettings.scenes를 Main.unity 하나로 통째로 바꾼다(남아 있던 Boot·Menu·Battle·Plaza 줄을 지운다).
    /// 3. 없는 에셋을 가리키는 config 객체 com.unity.input.settings.actions를 지운다.
    /// 4. GraphicsSettings.defaultRenderPipeline이 비어 있으면 Assets/Settings의 URP 에셋을 넣는다.
    /// 5. PlayerSettings: 기본 해상도 1920×1080, 전체 화면 창, 활성 입력 = Input System, 백그라운드 실행.
    ///    Time.fixedDeltaTime은 건드리지 않는다(실행기는 누적기 방식).
    /// 실패하면 로그를 남기고 배치 모드에서는 EditorApplication.Exit(1).
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string InputActionsConfigName = "com.unity.input.settings.actions";
        public const string SettingsFolder = "Assets/Settings";
        public const string PreferredPipelinePath = "Assets/Settings/UniversalRP.asset";
        /// <summary>PlayerSettings.activeInputHandler: 0 = 옛 입력, 1 = Input System 패키지, 2 = 둘 다.</summary>
        public const int InputSystemOnly = 1;

        [MenuItem("Wizard/Project Setup")]
        static void Menu()
        {
            try
            {
                Apply();
                Debug.Log("[ProjectSetup] done");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>배치 모드 진입점(-executeMethod).</summary>
        public static void Run()
        {
            try
            {
                Apply();
                Debug.Log("[ProjectSetup] done");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProjectSetup] failed: " + ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            SaveEmptyMainScene();
            SetBuildScenes();
            RemoveMissingInputActionsConfig();
            EnsureRenderPipeline();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SaveEmptyMainScene()
        {
            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir.Replace('\\', '/')))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(dir));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // EmptyScene은 카메라·라이트가 없다. 그래도 다른 것이 섞였으면 지운다(카메라는 WorldViewHost가 만든다).
            foreach (var go in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SaveScene failed: " + ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new InvalidOperationException("scene asset not found after save: " + ScenePath);
            }
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length != 1 || scenes[0].path != ScenePath)
                throw new InvalidOperationException("EditorBuildSettings.scenes not applied");
        }

        static void RemoveMissingInputActionsConfig()
        {
            UnityEngine.Object obj;
            bool found = EditorBuildSettings.TryGetConfigObject(InputActionsConfigName, out obj);
            // 있는데 가리키는 에셋이 없으면(guid가 이 프로젝트에 없음) 지운다. 살아 있는 에셋이면 그대로 둔다.
            if (!found || obj == null) EditorBuildSettings.RemoveConfigObject(InputActionsConfigName);
        }

        static void EnsureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PreferredPipelinePath);
            if (rp == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineAsset", new[] { SettingsFolder }))
                {
                    rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (rp != null) break;
                }
            }
            if (rp == null) throw new InvalidOperationException("no RenderPipelineAsset under " + SettingsFolder);
            GraphicsSettings.defaultRenderPipeline = rp;
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            SetActiveInputHandler(InputSystemOnly);
        }

        /// <summary>activeInputHandler는 공개 API가 없어 ProjectSettings.asset의 직렬화 속성으로 고친다.</summary>
        static void SetActiveInputHandler(int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) throw new InvalidOperationException("ProjectSettings.asset not loadable");
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null) throw new InvalidOperationException("activeInputHandler property not found");
            if (prop.intValue == value) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

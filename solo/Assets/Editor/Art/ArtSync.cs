using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wizard.Solo.Editor
{
    /// <summary>
    /// 정본 그림 복사(00 문서 10절). 메뉴 'Wizard/Sync Art', 배치: -executeMethod Wizard.Solo.Editor.ArtSync.Run.
    /// 대응표(artsync.json)대로 D:\algorithm_game\art → Assets/Resources/Art 로 복사하고, 없어진 파일을 정리한 뒤 AssetDatabase.Refresh.
    /// Unity 없이 같은 일을 하는 것은 Tools/artsync.sh.
    /// </summary>
    public static class ArtSync
    {
        [MenuItem("Wizard/Sync Art")]
        public static void Run()
        {
            string root = ProjectRoot();
            ArtSyncRules rules;
            try
            {
                rules = ArtSyncRules.Load(root);
            }
            catch (Exception e)
            {
                Fail("대응표를 읽지 못했다: " + e.Message);
                return;
            }
            var rep = rules.Sync(root);
            if (rep.MissingSources.Count > 0)
            {
                Fail("원본 폴더가 없다: " + string.Join(", ", rep.MissingSources));
                return;
            }
            if (rep.Collisions.Count > 0)
            {
                Fail("Resources 경로(확장자 뺀)가 겹친다: " + string.Join(", ", rep.Collisions));
                return;
            }
            Debug.Log("[ArtSync] " + rep);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>바꾸지 않고 보고만(메뉴 'Wizard/Sync Art (dry run)').</summary>
        [MenuItem("Wizard/Sync Art (dry run)")]
        public static void DryRun()
        {
            string root = ProjectRoot();
            var rep = ArtSyncRules.Load(root).Sync(root, true);
            Debug.Log("[ArtSync dry run] " + rep +
                      (rep.MissingSources.Count > 0 ? " / 없는 원본: " + string.Join(", ", rep.MissingSources) : "") +
                      (rep.Collisions.Count > 0 ? " / 겹침: " + string.Join(", ", rep.Collisions) : ""));
        }

        public static string ProjectRoot() => ArtSyncRules.Slash(Directory.GetParent(Application.dataPath).FullName);

        static void Fail(string msg)
        {
            Debug.LogError("[ArtSync] " + msg);
            // 배치 모드에서는 예외로 끝내 끝 코드가 0이 되지 않게 한다.
            if (Application.isBatchMode) throw new InvalidOperationException("ArtSync: " + msg);
        }
    }
}

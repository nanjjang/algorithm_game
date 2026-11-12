#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wizard.Content;

namespace Wizard.Unity.App.Core
{
    // 빌드에 든 기본 팩(ARCHITECTURE 8.2: StreamingAssets/content/base.wzpack)을 읽어 ContentDb 를 만든다. 오프라인 프로필은 언제나 이 팩이다.
    // 편집기에서는 그 자리에 팩이 없으면 databuild build 의 기본 출력(<저장소>/artifacts/content/base.wzpack)을 읽는다.
    // [임시] 팩을 StreamingAssets 에 놓는 일은 아직 어느 작업 카드에도 없다(OPEN-QUESTIONS). 서버 팩 내려받기는 뒤의 작업이다.
    public static class ContentPackFile
    {
        public const string FolderName = "content";
        public const string FileName = "base.wzpack";

        public static IReadOnlyList<string> CandidatePaths()
        {
            var paths = new List<string> { Path.Combine(Application.streamingAssetsPath, FolderName, FileName) };
#if UNITY_EDITOR
            DirectoryInfo? unityRoot = Directory.GetParent(Application.dataPath);
            DirectoryInfo? repoRoot = unityRoot?.Parent;
            if (repoRoot != null)
            {
                paths.Add(Path.Combine(repoRoot.FullName, "artifacts", FolderName, FileName));
            }
#endif
            return paths;
        }

        // 후보를 차례로 본다. 처음으로 있는 파일을 읽는다(있는데 깨졌으면 거기서 실패한다)
        public static bool TryLoadDefault(out ContentDb? db, out string message)
        {
            IReadOnlyList<string> paths = CandidatePaths();
            for (int i = 0; i < paths.Count; i++)
            {
                if (File.Exists(paths[i]))
                {
                    return TryLoad(paths[i], out db, out message);
                }
            }
            db = null;
            message = "content pack not found: " + string.Join(", ", paths);
            return false;
        }

        public static bool TryLoad(string path, out ContentDb? db, out string message)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                db = ContentDb.Build(ContentPack.Load(bytes));
                message = path;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ContentException)
            {
                db = null;
                message = "content pack " + path + ": " + e.Message;
                return false;
            }
        }
    }
}

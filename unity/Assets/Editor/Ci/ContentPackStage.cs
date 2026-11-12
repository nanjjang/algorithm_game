using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Wizard.Unity.Editor
{
    // 플레이어 빌드에 기본 팩을 싣는다(ARCHITECTURE 8.2, CONTRACTS 11.2, FIX-3).
    // databuild build 의 출력(<저장소>/artifacts/content/base.wzpack)을 빌드 직전에 Assets/StreamingAssets/content/base.wzpack 에 놓고,
    // 빌드가 끝나면(성공이든 실패든) 놓은 파일과 Unity 가 만든 .meta, 이 단계가 새로 만든 폴더(와 그 .meta)를 지운다.
    // *.wzpack 은 커밋하지 않으므로(.gitignore) 실행 뒤 작업 트리에 아무것도 남지 않아야 한다.
    // 그 자리에 원래 파일이 있었으면(개발자가 손으로 놓은 팩) 지우지 않고 원래 바이트로 되돌린다.
    // 파일 일만 하고 AssetDatabase 는 부르지 않는다(EditMode 시험이 임시 폴더로 그대로 돌린다). 새로 읽히는 것은 Ci.BuildPlayer 가 한다.
    public sealed class ContentPackStage
    {
        public const string FolderName = "content";        // ContentPackFile.FolderName 과 같다(Wizard.Unity.App. 이 어셈블리는 그것을 참조하지 않는다)
        public const string FileName = "base.wzpack";      // ContentPackFile.FileName, databuild 의 PackFileName 과 같다
        public const string MetaSuffix = ".meta";
        public const string DataBuildCommand = "dotnet run --project tools/Wizard.DataBuild -c Release -- build";

        private readonly string _streamingAssetsRoot;
        private readonly List<string> _createdFolders;     // 바깥에서 안쪽 순서
        private readonly byte[]? _previousFile;
        private readonly byte[]? _previousMeta;
        private bool _removed;

        private ContentPackStage(string streamingAssetsRoot, string targetPath, string sha256, List<string> createdFolders,
            byte[]? previousFile, byte[]? previousMeta)
        {
            _streamingAssetsRoot = streamingAssetsRoot;
            TargetPath = targetPath;
            Sha256 = sha256;
            _createdFolders = createdFolders;
            _previousFile = previousFile;
            _previousMeta = previousMeta;
        }

        public string TargetPath { get; }

        // 놓은 팩의 SHA-256(소문자 hex 64자). databuild 출력의 바이트와 같다
        public string Sha256 { get; }

        public bool Removed => _removed;

        public static string DefaultSourcePath(string repoRoot)
        {
            return Path.Combine(repoRoot, "artifacts", FolderName, FileName);
        }

        public static string TargetPathIn(string streamingAssetsRoot)
        {
            return Path.Combine(streamingAssetsRoot, FolderName, FileName);
        }

        // macOS 플레이어 안의 자리: <앱>/Contents/Resources/Data/StreamingAssets/content/base.wzpack
        public static string PlayerPackPath(string appPath)
        {
            return Path.Combine(appPath, "Contents", "Resources", "Data", "StreamingAssets", FolderName, FileName);
        }

        // validate: 팩 바이트를 받아 읽을 수 있으면 null, 아니면 이유. 빌드에서는 LoadValidator()(ContentPack.Load)를 준다.
        // 팩이 없거나 읽히지 않으면 아무것도 놓지 않고 false. 놓는 도중 실패하면 놓은 것을 되돌리고 false.
        public static bool TryPlace(string sourcePath, string streamingAssetsRoot, Func<byte[], string?> validate,
            out ContentPackStage? stage, out string message)
        {
            stage = null;
            if (!File.Exists(sourcePath))
            {
                message = "content pack not found at " + sourcePath + ". Build it first: " + DataBuildCommand;
                return false;
            }
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(sourcePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                message = "content pack " + sourcePath + " cannot be read: " + e.Message;
                return false;
            }
            string? invalid = validate(bytes);
            if (invalid != null)
            {
                message = "content pack " + sourcePath + " is not a loadable pack (ContentPack.Load): " + invalid + ". Rebuild it: " + DataBuildCommand;
                return false;
            }

            string targetPath = TargetPathIn(streamingAssetsRoot);
            string metaPath = targetPath + MetaSuffix;
            var created = new List<string>();
            foreach (string folder in new[] { streamingAssetsRoot, Path.Combine(streamingAssetsRoot, FolderName) })
            {
                if (!Directory.Exists(folder))
                {
                    created.Add(folder);
                }
            }

            byte[]? previousFile = null;
            byte[]? previousMeta = null;
            try
            {
                if (File.Exists(targetPath))
                {
                    previousFile = File.ReadAllBytes(targetPath);
                }
                if (File.Exists(metaPath))
                {
                    previousMeta = File.ReadAllBytes(metaPath);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                message = "cannot read the file already at " + targetPath + ": " + e.Message;
                return false;
            }

            string sha = Sha256Hex(bytes);
            var placed = new ContentPackStage(streamingAssetsRoot, targetPath, sha, created, previousFile, previousMeta);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.WriteAllBytes(targetPath, bytes);
                string written = Sha256Hex(File.ReadAllBytes(targetPath));
                if (written != sha)
                {
                    throw new IOException("written bytes differ from the source (sha256 " + written + " != " + sha + ")");
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                placed.TryRemove(out string undo);
                message = "cannot place the content pack at " + targetPath + ": " + e.Message + " (undo: " + undo + ")";
                return false;
            }
            stage = placed;
            message = "placed " + sourcePath + " at " + targetPath + " (sha256 " + sha + ")";
            return true;
        }

        // 놓은 파일과 .meta 를 지우거나 원래 것으로 되돌리고, 이 단계가 만든 폴더가 비었으면 그 폴더와 .meta 를 지운다.
        // 두 번 불러도 된다. 지우지 못한 것이 있으면 false 와 그 경로.
        public bool TryRemove(out string message)
        {
            var problems = new List<string>();
            var notes = new List<string>();
            RestoreOrDelete(TargetPath, _previousFile, problems);
            RestoreOrDelete(TargetPath + MetaSuffix, _previousMeta, problems);
            for (int i = _createdFolders.Count - 1; i >= 0; i--)
            {
                string folder = _createdFolders[i];
                try
                {
                    if (Directory.Exists(folder))
                    {
                        if (Directory.GetFileSystemEntries(folder).Length > 0)
                        {
                            // 이 단계가 놓지 않은 것이 들어 있다. 남의 파일이므로 지우지 않는다
                            notes.Add("kept " + folder + " (not empty)");
                            continue;
                        }
                        Directory.Delete(folder, false);
                    }
                    string folderMeta = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + MetaSuffix;
                    if (File.Exists(folderMeta))
                    {
                        File.Delete(folderMeta);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    problems.Add(folder + ": " + e.Message);
                }
            }
            _removed = problems.Count == 0;
            var text = new StringBuilder();
            text.Append(problems.Count == 0 ? "removed the staged content pack from " : "could not clean up the staged content pack in ");
            text.Append(_streamingAssetsRoot);
            if (problems.Count > 0)
            {
                text.Append(": ").Append(string.Join("; ", problems));
            }
            if (notes.Count > 0)
            {
                text.Append(" (").Append(string.Join("; ", notes)).Append(')');
            }
            message = text.ToString();
            return _removed;
        }

        private static void RestoreOrDelete(string path, byte[]? previous, List<string> problems)
        {
            try
            {
                if (previous != null)
                {
                    File.WriteAllBytes(path, previous);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problems.Add(path + ": " + e.Message);
            }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(bytes);
            }
            const string digits = "0123456789abcdef";
            var chars = new char[digest.Length * 2];
            for (int i = 0; i < digest.Length; i++)
            {
                chars[2 * i] = digits[digest[i] >> 4];
                chars[2 * i + 1] = digits[digest[i] & 0xF];
            }
            return new string(chars);
        }

        // ---- ContentPack.Load ----

        private delegate object PackLoad(ReadOnlySpan<byte> bytes);

        // 빌드에 쓰는 검사: 플레이어가 부르는 것과 같은 Wizard.Content.ContentPack.Load(magic, 머리말, 목차, SHA-256).
        // 이 편집기 어셈블리의 asmdef 는 Wizard.Content 를 참조하지 않아서(이 작업의 소유 밖) 편집기에 올라온 어셈블리에서 찾는다.
        // 찾지 못하면 모든 팩을 읽을 수 없다고 돌려준다(건너뛰지 않는다). EditMode 시험이 이 경로를 직접 돌린다.
        public static Func<byte[], string?> LoadValidator()
        {
            PackLoad? load = FindContentPackLoad(out string why);
            if (load == null)
            {
                return _ => why;
            }
            return bytes =>
            {
                try
                {
                    load(new ReadOnlySpan<byte>(bytes));
                    return null;
                }
                catch (Exception e)
                {
                    return e.GetType().Name + ": " + e.Message;
                }
            };
        }

        private static PackLoad? FindContentPackLoad(out string why)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Wizard.Content")
                {
                    continue;
                }
                Type? type = assembly.GetType("Wizard.Content.ContentPack", false);
                MethodInfo? method = type?.GetMethod("Load", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ReadOnlySpan<byte>) }, null);
                if (method == null)
                {
                    why = "Wizard.Content is loaded but has no public static ContentPack.Load(ReadOnlySpan<byte>)";
                    return null;
                }
                why = "";
                return (PackLoad)Delegate.CreateDelegate(typeof(PackLoad), method);
            }
            why = "assembly Wizard.Content is not loaded in the editor";
            return null;
        }
    }
}

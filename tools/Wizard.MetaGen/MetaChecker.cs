using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.MetaGen
{
    // metagen --check (CONTRACTS 11.5, T-34):
    //   빠진 .meta, 주인 없는 .meta, 중복 GUID, GUID 줄이 없거나 틀린 꼴의 .meta,
    //   그리고 src/ 와 unity/Assets/Game/ 에서는 GUID 가 MD5(저장소 기준 상대 경로)와 다른 .meta(Unity 가 먼저 만든 무작위 GUID).
    // 문제는 종류와 경로의 오름차순으로 돌려준다.
    public static class MetaChecker
    {
        public static List<MetaProblem> Check(string root, AssetTree tree)
        {
            var problems = new List<MetaProblem>();
            var assetPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetEntry entry in tree.Assets)
            {
                assetPaths.Add(entry.Path);
            }
            var metaPaths = new HashSet<string>(tree.Metas, StringComparer.Ordinal);

            foreach (AssetEntry entry in tree.Assets)
            {
                if (!metaPaths.Contains(entry.Path + AssetTree.MetaSuffix))
                {
                    problems.Add(new MetaProblem(MetaProblemKind.Missing, entry.Path,
                        entry.IsDirectory ? "folder has no .meta; run metagen" : "file has no .meta; run metagen"));
                }
            }

            var byGuid = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string meta in tree.Metas)
            {
                string target = meta.Substring(0, meta.Length - AssetTree.MetaSuffix.Length);
                bool owned = assetPaths.Contains(target);
                if (!owned)
                {
                    string targetFull = Path.Combine(root, target);
                    bool exists = File.Exists(targetFull) || Directory.Exists(targetFull);
                    problems.Add(new MetaProblem(MetaProblemKind.Orphan, meta, exists
                        ? "Unity does not import " + target + " (hidden name, package root, or outside a package); delete the .meta"
                        : "no asset at " + target + " (deleted or renamed); delete the .meta"));
                }

                string? guid = ReadGuid(Path.Combine(root, meta));
                if (guid == null)
                {
                    problems.Add(new MetaProblem(MetaProblemKind.BadMeta, meta, "no 'guid: <32 lowercase hex>' line"));
                    continue;
                }
                if (!byGuid.TryGetValue(guid, out List<string>? owners))
                {
                    owners = new List<string>();
                    byGuid.Add(guid, owners);
                }
                owners.Add(meta);

                // 주인 없는 .meta 는 지우라고 이미 알렸으므로 GUID 계산값과는 대조하지 않는다.
                if (owned && IsStrict(target))
                {
                    string expected = MetaGuid.ForPath(target);
                    if (guid != expected)
                    {
                        problems.Add(new MetaProblem(MetaProblemKind.GuidMismatch, meta,
                            "guid " + guid + " != md5(\"" + target + "\") " + expected +
                            "; delete the .meta and run metagen"));
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> pair in byGuid)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }
                pair.Value.Sort(string.CompareOrdinal);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var others = new List<string>(pair.Value);
                    others.RemoveAt(i);
                    problems.Add(new MetaProblem(MetaProblemKind.DuplicateGuid, pair.Value[i],
                        "guid " + pair.Key + " also in " + string.Join(", ", others)));
                }
            }

            problems.Sort((a, b) =>
            {
                int byKind = ((int)a.Kind).CompareTo((int)b.Kind);
                return byKind != 0 ? byKind : string.CompareOrdinal(a.Path, b.Path);
            });
            return problems;
        }

        public static bool IsStrict(string targetPath)
        {
            foreach (string scope in AssetTree.StrictGuidScopes)
            {
                if (RepoPath.IsUnder(targetPath, scope))
                {
                    return true;
                }
            }
            return false;
        }

        // 맨 앞에 들여쓰기 없이 있는 "guid: …" 줄의 값. 없거나 꼴이 틀리면 null.
        // 들여쓴 줄(예: "  script: {fileID: …, guid: …}")은 다른 자산을 가리키는 값이라 보지 않는다.
        public static string? ReadGuid(string metaFullPath)
        {
            foreach (string rawLine in File.ReadAllLines(metaFullPath))
            {
                if (!rawLine.StartsWith("guid:", StringComparison.Ordinal))
                {
                    continue;
                }
                string value = rawLine.Substring("guid:".Length).Trim();
                return MetaGuid.IsWellFormed(value) ? value : null;
            }
            return null;
        }
    }
}

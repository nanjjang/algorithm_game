using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Editor
{
    /// <summary>
    /// 그림 대응표(Assets/Editor/Art/artsync.json) 읽기와 풀기. Unity API 없이 파일만 다룬다(EditMode 테스트가 그대로 시험한다).
    /// Tools/artsync.sh와 같은 규칙: 글롭 ** = 폴더 여럿(0개 포함), * = 슬래시 없는 글자 여럿, ? = 슬래시 없는 글자 하나.
    /// 대응마다 src 폴더 아래 파일 중 include에 맞고 대응 exclude에 안 맞는 것 → dst/상대경로. 그 대상 경로가 전역 exclude에 맞으면 뺀다.
    /// </summary>
    public sealed class ArtSyncRules
    {
        public const string TableAssetPath = "Assets/Editor/Art/artsync.json";

        public sealed class Map
        {
            public string Src, Dst;
            public List<string> Include = new List<string>();
            public List<string> Exclude = new List<string>();
        }

        public struct Entry
        {
            /// <summary>원본 절대 경로(슬래시).</summary>
            public string Source;
            /// <summary>dest_root 기준 상대 경로(슬래시).</summary>
            public string Dest;
        }

        public string SourceRoot, DestRoot;
        public readonly List<string> Exclude = new List<string>();
        public readonly List<string> Keep = new List<string>();
        public readonly List<Map> Maps = new List<Map>();

        public static ArtSyncRules Parse(string json)
        {
            var o = JObject.Parse(json);
            var r = new ArtSyncRules
            {
                SourceRoot = (string)o["source_root"],
                DestRoot = (string)o["dest_root"]
            };
            AddStrings(o["exclude"], r.Exclude);
            AddStrings(o["keep"], r.Keep);
            if (o["maps"] is JArray maps)
            {
                foreach (var m in maps)
                {
                    var map = new Map { Src = (string)m["src"], Dst = (string)m["dst"] };
                    AddStrings(m["include"], map.Include);
                    AddStrings(m["exclude"], map.Exclude);
                    r.Maps.Add(map);
                }
            }
            return r;
        }

        public static ArtSyncRules Load(string projectRoot)
        {
            return Parse(File.ReadAllText(Path.Combine(projectRoot, TableAssetPath), Encoding.UTF8));
        }

        static void AddStrings(JToken t, List<string> into)
        {
            if (t is JArray a) foreach (var x in a) if (x.Type == JTokenType.String) into.Add((string)x);
        }

        // ---------- 글롭 ----------

        /// <summary>글롭 → 정규식(artsync.sh의 glob_re와 같은 변환).</summary>
        public static string GlobToRegex(string glob)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < glob.Length; i++)
            {
                char c = glob[i];
                if (c == '*')
                {
                    bool dbl = i + 1 < glob.Length && glob[i + 1] == '*';
                    if (dbl)
                    {
                        bool slash = i + 2 < glob.Length && glob[i + 2] == '/';
                        if (slash) { sb.Append("(.*/)?"); i += 2; }
                        else { sb.Append(".*"); i += 1; }
                    }
                    else sb.Append("[^/]*");
                }
                else if (c == '?') sb.Append("[^/]");
                else if ("\\.+()|^$[]{}".IndexOf(c) >= 0) sb.Append('\\').Append(c);
                else sb.Append(c);
            }
            sb.Append('$');
            return sb.ToString();
        }

        public static bool GlobMatch(string glob, string path)
        {
            return Regex.IsMatch(path, GlobToRegex(glob), RegexOptions.CultureInvariant);
        }

        static bool Any(List<string> globs, string path)
        {
            for (int i = 0; i < globs.Count; i++) if (GlobMatch(globs[i], path)) return true;
            return false;
        }

        /// <summary>대상 상대 경로가 전역 제외 규칙에 걸리는가.</summary>
        public bool IsExcluded(string destRelative) => Any(Exclude, destRelative);
        public bool IsKept(string destRelative) => Any(AlwaysKeep, destRelative) || Any(Keep, destRelative);

        /// <summary>
        /// 대응표의 keep과 상관없이 절대 지우지 않는 것: 사용자 교체본(_v2)과 솔로 덮어쓰기 표·보충 폴더.
        /// 대응표 글이 잘못 고쳐져 keep이 비어도 이것은 남는다. Tools/artsync.sh의 ALWAYS_KEEP과 같은 목록이다.
        /// </summary>
        public static readonly List<string> AlwaysKeep = new List<string>
        {
            "**/*_v2.*", "**/*_v2/**", "**/solo_*.json", "supplement/solo/**", "common/**", "ui/solo_kit/**", "wands/*/animation/**",
            "backgrounds/title_solo_*.png"
        };

        // ---------- 풀기 ----------

        public static string AbsoluteSourceRoot(string projectRoot, ArtSyncRules rules)
        {
            return Slash(Path.GetFullPath(Path.Combine(projectRoot, rules.SourceRoot)));
        }

        /// <summary>대응표를 풀어 (원본, 대상) 목록을 만든다. 없는 src 폴더는 missing에 적는다. 대상 경로 오름차순.</summary>
        public List<Entry> Resolve(string projectRoot, List<string> missing = null)
        {
            string srcRoot = AbsoluteSourceRoot(projectRoot, this);
            var list = new List<Entry>();
            foreach (var m in Maps)
            {
                string baseDir = Slash(Path.Combine(srcRoot, m.Src));
                if (!Directory.Exists(baseDir))
                {
                    missing?.Add(baseDir);
                    continue;
                }
                foreach (var f in Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories))
                {
                    string full = Slash(f);
                    string rel = full.Substring(baseDir.Length).TrimStart('/');
                    if (!Any(m.Include, rel) || Any(m.Exclude, rel)) continue;
                    string dest = m.Dst + "/" + rel;
                    if (IsExcluded(dest)) continue;
                    list.Add(new Entry { Source = full, Dest = dest });
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Dest, b.Dest));
            return list;
        }

        /// <summary>Resources.Load는 확장자를 보지 않는다 → 확장자를 뺀 경로가 겹치는 대상(겹치면 하나를 못 읽는다).</summary>
        public static List<string> ResourceCollisions(List<Entry> entries)
        {
            var seen = new Dictionary<string, string>();
            var dup = new List<string>();
            foreach (var e in entries)
            {
                string k = WithoutExtension(e.Dest);
                if (seen.TryGetValue(k, out var other)) dup.Add(other + " <-> " + e.Dest);
                else seen[k] = e.Dest;
            }
            return dup;
        }

        public static string WithoutExtension(string p)
        {
            int slash = p.LastIndexOf('/');
            int dot = p.LastIndexOf('.');
            return dot > slash ? p.Substring(0, dot) : p;
        }

        public static string Slash(string p) => p.Replace('\\', '/');

        // ---------- 복사 ----------

        public struct SyncReport
        {
            public int Total, Copied, Unchanged, Removed, Images;
            public long RawBytes;
            public List<string> MissingSources;
            public List<string> Collisions;
            public override string ToString()
            {
                return "대응 파일 " + Total + "개(그림 " + Images + "장, 무압축 " + (RawBytes / (1024 * 1024)) + "MB), 복사 " + Copied +
                       ", 같음 " + Unchanged + ", 정리 " + Removed;
            }
        }

        /// <summary>
        /// 대응표대로 복사하고, 대상 폴더에서 대응표에 없는 파일(keep 제외)을 .meta와 함께 지운다. 빈 폴더도 정리한다.
        /// 원본 폴더가 하나라도 없거나 Resources 경로가 겹치면 아무것도 바꾸지 않고 보고만 한다.
        /// </summary>
        public SyncReport Sync(string projectRoot, bool dryRun = false)
        {
            var rep = new SyncReport { MissingSources = new List<string>() };
            var entries = Resolve(projectRoot, rep.MissingSources);
            rep.Collisions = ResourceCollisions(entries);
            rep.Total = entries.Count;
            if (rep.MissingSources.Count > 0 || rep.Collisions.Count > 0) return rep;

            string destRoot = Slash(Path.GetFullPath(Path.Combine(projectRoot, DestRoot)));
            var want = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                want.Add(e.Dest);
                string target = destRoot + "/" + e.Dest;
                if (File.Exists(target) && SameBytes(e.Source, target)) rep.Unchanged++;
                else
                {
                    rep.Copied++;
                    if (!dryRun)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(e.Source, target, true);
                    }
                }
                if (e.Source.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && PngSize.TryRead(e.Source, out int w, out int h))
                {
                    rep.Images++;
                    rep.RawBytes += (long)w * h * 4;
                }
            }
            if (Directory.Exists(destRoot))
            {
                foreach (var f in Directory.GetFiles(destRoot, "*", SearchOption.AllDirectories))
                {
                    string full = Slash(f);
                    if (full.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    string rel = full.Substring(destRoot.Length + 1);
                    if (want.Contains(rel) || IsKept(rel)) continue;
                    rep.Removed++;
                    if (dryRun) continue;
                    File.Delete(full);
                    if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
                }
                if (!dryRun) CleanDirectories(destRoot);
            }
            return rep;
        }

        static void CleanDirectories(string root)
        {
            foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                string target = meta.Substring(0, meta.Length - ".meta".Length);
                if (!File.Exists(target) && !Directory.Exists(target)) File.Delete(meta);
            }
            foreach (var d in Directory.GetDirectories(root)) CleanDirectory(d);
        }

        static void CleanDirectory(string dir)
        {
            foreach (var d in Directory.GetDirectories(dir)) CleanDirectory(d);
            if (Directory.GetFileSystemEntries(dir).Length == 0)
            {
                Directory.Delete(dir);
                if (File.Exists(dir + ".meta")) File.Delete(dir + ".meta");
            }
        }

        static bool SameBytes(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            const int Buf = 1 << 16;
            using (var sa = fa.OpenRead())
            using (var sb = fb.OpenRead())
            {
                var ba = new byte[Buf];
                var bb = new byte[Buf];
                while (true)
                {
                    int na = sa.Read(ba, 0, Buf);
                    int nb = sb.Read(bb, 0, Buf);
                    if (na != nb) return false;
                    if (na == 0) return true;
                    for (int i = 0; i < na; i++) if (ba[i] != bb[i]) return false;
                }
            }
        }
    }

    /// <summary>PNG 머리(IHDR)에서 가로·세로를 읽는다(텍스처를 올리지 않고 크기 확인).</summary>
    public static class PngSize
    {
        public static bool TryRead(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                using (var s = File.OpenRead(path))
                {
                    var b = new byte[24];
                    if (s.Read(b, 0, 24) != 24) return false;
                    if (b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return false;
                    width = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    height = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}

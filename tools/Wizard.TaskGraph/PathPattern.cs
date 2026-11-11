using System.Text;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // owned_paths 의 꼴(tasks.json notes): '/'로 끝나면 폴더 전체, '*'는 한 경로 마디 안에서만 맞는다.
    // Overlap 은 tools/check-tasks.mjs 의 pathsOverlap 을 그대로 옮긴 것이다(두 도구가 같은 답을 내야 한다).
    // Glob 은 도구 안의 규칙 표(공유 파일, 다시 만드는 파일)에만 쓰고 '**'(마디 0개 이상)를 더 안다.
    public static class PathPattern
    {
        public static bool Overlap(string pa, string pb)
        {
            (bool aDir, string[] a) = Parse(pa);
            (bool bDir, string[] b) = Parse(pb);
            int n = a.Length < b.Length ? a.Length : b.Length;
            for (int i = 0; i < n; i++)
            {
                if (!SegOverlap(a[i], b[i]))
                {
                    return false;
                }
            }
            if (a.Length == b.Length)
            {
                return true;
            }
            return a.Length < b.Length ? aDir : bDir;
        }

        // 구체적인 파일 경로가 owned_paths 의 한 항목에 드는가.
        public static bool Owns(string pattern, string file)
        {
            (bool dir, string[] p) = Parse(pattern);
            string[] f = file.Split('/');
            if (dir ? f.Length <= p.Length : f.Length != p.Length)
            {
                return false;
            }
            for (int i = 0; i < p.Length; i++)
            {
                if (!SegMatch(p[i], f[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Glob(string glob, string file)
        {
            return GlobAt(glob.Split('/'), 0, file.Split('/'), 0);
        }

        private static bool GlobAt(string[] g, int i, string[] p, int j)
        {
            if (i == g.Length)
            {
                return j == p.Length;
            }
            if (g[i] == "**")
            {
                for (int k = j; k <= p.Length; k++)
                {
                    if (GlobAt(g, i + 1, p, k))
                    {
                        return true;
                    }
                }
                return false;
            }
            return j < p.Length && SegMatch(g[i], p[j]) && GlobAt(g, i + 1, p, j + 1);
        }

        private static (bool Dir, string[] Segs) Parse(string p)
        {
            bool dir = p.EndsWith('/');
            return (dir, (dir ? p.Substring(0, p.Length - 1) : p).Split('/'));
        }

        public static bool SegMatch(string pattern, string seg)
        {
            return pattern.Contains('*') ? SegRegex(pattern).IsMatch(seg) : pattern == seg;
        }

        private static bool SegOverlap(string a, string b)
        {
            bool sa = a.Contains('*');
            bool sb = b.Contains('*');
            if (!sa && !sb)
            {
                return a == b;
            }
            if (sa && !sb)
            {
                return SegRegex(a).IsMatch(b);
            }
            if (!sa && sb)
            {
                return SegRegex(b).IsMatch(a);
            }
            // 둘 다 '*': 고정된 앞머리와 꼬리가 서로 맞아야 겹칠 수 있다
            string ap = a.Substring(0, a.IndexOf('*'));
            string asfx = a.Substring(a.LastIndexOf('*') + 1);
            string bp = b.Substring(0, b.IndexOf('*'));
            string bsfx = b.Substring(b.LastIndexOf('*') + 1);
            return (ap.StartsWith(bp, System.StringComparison.Ordinal) || bp.StartsWith(ap, System.StringComparison.Ordinal))
                && (asfx.EndsWith(bsfx, System.StringComparison.Ordinal) || bsfx.EndsWith(asfx, System.StringComparison.Ordinal));
        }

        private static Regex SegRegex(string seg)
        {
            var sb = new StringBuilder("^");
            string[] parts = seg.Split('*');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append("[^/]*");
                }
                sb.Append(Regex.Escape(parts[i]));
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.CultureInvariant);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Names
{
    // tools/ci/names-scan.sh(fast)의 몸통: databuild names --scan. PIPELINE 7.1 G0, CONTRACTS 2.1절 끝, CLAUDE.md 5절.
    //   1. 제품 코드(src/, server/, unity/Assets/Game/ 의 .cs)에 이름 표 밖 한글 문자열·문자 리터럴이 없다.
    //      주석은 보지 않는다. 이스케이프(가)로 적은 한글도 잡는다. tests/ 는 보지 않는다(금칙어·글자 폭 벡터처럼 한글이 필요한 테스트가 있다).
    //   2. 작업용 이름의 로마자 표기(WorkingNames)가 src/, server/, data/ 의 식별자·열쇠·파일 이름에 없다.
    //      파일·폴더 이름 전부와 텍스트 파일의 내용(.cs·.sql 은 주석을 뺀다)을 낱말로 쪼개(영숫자 덩어리 → camelCase·숫자 경계)
    //      낱말이 이름과 같거나 이름으로 시작하거나 끝나면 걸린다("dalguHeat", "fac_nuru_chair", "NuruChair" 는 걸리고 "menuRunner" 는 아니다).
    //      예외: data/presentation/asset_aliases.json(그림 매니페스트의 폴더 열쇠를 적는 곳), "art/" 로 시작하는 문자열(그림 경로).
    // 결과는 경로·줄 순서로 정렬한 한 줄씩이다. 비었으면 통과.
    public static class NamesScan
    {
        public static readonly string[] WorkingNames =
        {
            "dalgu", "chorong", "galpi", "seonge", "geumeum", "nuru", "handeot", "dolmadang", "cheotmodak",
        };

        public const string AssetAliasesPath = "data/presentation/asset_aliases.json";
        public const string ArtPrefix = "art/";

        private static readonly string[] HangulCodeRoots = { "src", "server", "unity/Assets/Game" };
        private static readonly string[] RomanizedRoots = { "src", "server", "data" };
        private static readonly string[] SkipDirs = { "bin", "obj", "node_modules" };

        // 식별자·열쇠가 사는 파일(내용을 읽는다). 그 밖(문서, 셸·배포 설정의 글)은 파일 이름만 본다
        private static readonly string[] TextExtensions =
        {
            ".cs", ".sql", ".json", ".asmdef", ".asmref", ".csproj", ".props", ".targets", ".rsp",
        };

        public static List<string> Run(string root)
        {
            var problems = new List<string>();
            foreach (string top in HangulCodeRoots)
            {
                foreach (string rel in Files(root, top))
                {
                    if (rel.EndsWith(".cs", StringComparison.Ordinal))
                    {
                        ScanHangulLiterals(rel, File.ReadAllText(Path.Combine(root, rel)), problems);
                    }
                }
            }
            foreach (string top in RomanizedRoots)
            {
                foreach (string rel in Entries(root, top))
                {
                    if (rel == AssetAliasesPath)
                    {
                        continue;
                    }
                    ScanRomanizedName(rel, problems);
                    string abs = Path.Combine(root, rel);
                    if (File.Exists(abs) && IsTextFile(rel))
                    {
                        ScanRomanizedContent(rel, File.ReadAllText(abs), problems);
                    }
                }
            }
            problems.Sort(StringComparer.Ordinal);
            return problems;
        }

        // ── 1. 한글 리터럴 ──

        public static void ScanHangulLiterals(string rel, string text, List<string> problems)
        {
            foreach (CSharpLexer.Segment seg in CSharpLexer.Lex(text))
            {
                if (seg.SegmentKind != CSharpLexer.Kind.String && seg.SegmentKind != CSharpLexer.Kind.Char)
                {
                    continue;
                }
                string literal = text.Substring(seg.Start, seg.Length);
                string decoded = seg.DecodesEscapes ? CSharpLexer.DecodeEscapes(literal) : literal;
                if (ContainsHangul(decoded))
                {
                    problems.Add(rel + ":" + LineOf(text, seg.Start).ToString(CultureInfo.InvariantCulture)
                        + ": 이름 표 밖 한글 리터럴 " + Clip(literal) + " (화면에 보이는 글자는 " + NamesFile.RepoPath
                        + " 에만 두고 코드는 열쇠로 찾는다. 화면에 보이지 않는 한글 데이터는 data/rules/ 의 데이터 파일에 둔다)");
                }
            }
        }

        public static bool ContainsHangul(string s)
        {
            foreach (char c in s)
            {
                if ((c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏')
                    || (c >= 'ꥠ' && c <= '꥿') || (c >= 'ힰ' && c <= '퟿') || (c >= 'ﾠ' && c <= 'ￜ'))
                {
                    return true;
                }
            }
            return false;
        }

        // ── 2. 작업용 이름의 로마자 표기 ──

        public static void ScanRomanizedName(string rel, List<string> problems)
        {
            string name = rel.Substring(rel.LastIndexOf('/') + 1);
            string? hit = FindWorkingName(name);
            if (hit != null)
            {
                problems.Add(rel + ": 파일·폴더 이름에 작업용 이름의 로마자 표기 '" + hit + "' 가 있다(id 와 번호만 쓴다. CONTRACTS 2.1)");
            }
        }

        public static void ScanRomanizedContent(string rel, string text, List<string> problems)
        {
            string body = StripForRomanized(rel, text);
            string[] lines = body.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string? hit = FindWorkingName(lines[i]);
                if (hit != null)
                {
                    problems.Add(rel + ":" + (i + 1).ToString(CultureInfo.InvariantCulture) + ": 식별자·열쇠에 작업용 이름의 로마자 표기 '" + hit
                        + "' 가 있다(코드와 데이터는 id 와 번호만 쓴다. CONTRACTS 2.1)");
                }
            }
        }

        // 낱말이 이름과 같거나, 이름으로 시작하거나 끝나면 그 이름
        public static string? FindWorkingName(string text)
        {
            foreach (string word in Words(text))
            {
                foreach (string n in WorkingNames)
                {
                    if (word.StartsWith(n, StringComparison.Ordinal) || word.EndsWith(n, StringComparison.Ordinal))
                    {
                        return n;
                    }
                }
            }
            return null;
        }

        // 영문자 덩어리를 camelCase 경계에서 쪼갠 소문자 낱말들. 숫자·'_'·'.'·'-'·'/' 등은 낱말을 끊는다
        public static IEnumerable<string> Words(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (!IsAsciiLetter(text[i]))
                {
                    i++;
                    continue;
                }
                int start = i;
                while (i < text.Length && IsAsciiLetter(text[i]))
                {
                    i++;
                }
                foreach (string w in SplitCamel(text.Substring(start, i - start)))
                {
                    yield return w;
                }
            }
        }

        private static IEnumerable<string> SplitCamel(string run)
        {
            int start = 0;
            for (int i = 1; i < run.Length; i++)
            {
                bool lowerToUpper = char.IsLower(run[i - 1]) && char.IsUpper(run[i]);
                bool acronymEnd = char.IsUpper(run[i - 1]) && char.IsUpper(run[i]) && i + 1 < run.Length && char.IsLower(run[i + 1]);
                if (lowerToUpper || acronymEnd)
                {
                    yield return run.Substring(start, i - start).ToLowerInvariant();
                    start = i;
                }
            }
            yield return run.Substring(start).ToLowerInvariant();
        }

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        // .cs·.sql 의 주석을 지우고, "art/" 로 시작하는 문자열을 지운다(줄 번호를 지키려고 줄바꿈은 남긴다)
        private static string StripForRomanized(string rel, string text)
        {
            if (rel.EndsWith(".cs", StringComparison.Ordinal))
            {
                var sb = new StringBuilder(text.Length);
                foreach (CSharpLexer.Segment seg in CSharpLexer.Lex(text))
                {
                    string part = text.Substring(seg.Start, seg.Length);
                    bool drop = seg.SegmentKind == CSharpLexer.Kind.Comment
                        || (seg.SegmentKind == CSharpLexer.Kind.String && part.TrimStart('$', '@', '"').StartsWith(ArtPrefix, StringComparison.Ordinal));
                    sb.Append(drop ? Blank(part) : part);
                }
                return sb.ToString();
            }
            if (rel.EndsWith(".sql", StringComparison.Ordinal))
            {
                return StripSqlComments(text);
            }
            if (rel.EndsWith(".json", StringComparison.Ordinal))
            {
                return StripArtStrings(text);
            }
            return text;
        }

        private static string StripArtStrings(string json)
        {
            var sb = new StringBuilder(json.Length);
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] != '"')
                {
                    sb.Append(json[i]);
                    i++;
                    continue;
                }
                int j = i + 1;
                while (j < json.Length && json[j] != '"' && json[j] != '\n')
                {
                    j += json[j] == '\\' ? 2 : 1;
                }
                int end = Math.Min(json.Length, j + 1);
                string part = json.Substring(i, end - i);
                sb.Append(part.StartsWith("\"" + ArtPrefix, StringComparison.Ordinal) ? Blank(part) : part);
                i = end;
            }
            return sb.ToString();
        }

        private static string StripSqlComments(string sql)
        {
            var sb = new StringBuilder(sql.Length);
            int i = 0;
            while (i < sql.Length)
            {
                if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-')
                {
                    while (i < sql.Length && sql[i] != '\n')
                    {
                        i++;
                    }
                    continue;
                }
                if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
                {
                    int end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? sql.Length : end + 2;
                    sb.Append(Blank(sql.Substring(i, end - i)));
                    i = end;
                    continue;
                }
                sb.Append(sql[i]);
                i++;
            }
            return sb.ToString();
        }

        private static string Blank(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(c == '\n' ? '\n' : ' ');
            }
            return sb.ToString();
        }

        private static bool IsTextFile(string rel)
        {
            foreach (string ext in TextExtensions)
            {
                if (rel.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // 저장소 기준 상대 경로로 파일만(서수 오름차순)
        private static IEnumerable<string> Files(string root, string top)
        {
            foreach (string rel in Entries(root, top))
            {
                if (File.Exists(Path.Combine(root, rel)))
                {
                    yield return rel;
                }
            }
        }

        // 파일과 폴더 전부(서수 오름차순, 숨김·빌드 출력 폴더 제외)
        private static IEnumerable<string> Entries(string root, string top)
        {
            string abs = Path.Combine(root, top);
            if (!Directory.Exists(abs))
            {
                yield break;
            }
            var stack = new List<string> { top };
            var all = new List<string>();
            while (stack.Count > 0)
            {
                string dir = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                foreach (string e in DataLoader.SortedEntries(Path.Combine(root, dir)))
                {
                    string name = Path.GetFileName(e);
                    string rel = dir + "/" + name;
                    if (Directory.Exists(e))
                    {
                        if (Array.IndexOf(SkipDirs, name) >= 0)
                        {
                            continue;
                        }
                        stack.Add(rel);
                    }
                    all.Add(rel);
                }
            }
            all.Sort(StringComparer.Ordinal);
            foreach (string rel in all)
            {
                yield return rel;
            }
        }

        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }
            return line;
        }

        private static string Clip(string s)
        {
            string one = s.Replace("\n", " ");
            return one.Length <= 40 ? one : one.Substring(0, 40) + "…";
        }
    }
}

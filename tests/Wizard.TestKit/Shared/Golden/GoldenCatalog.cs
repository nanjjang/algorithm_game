using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Wizard.TestKit.Shared.Arith;

namespace Wizard.TestKit.Shared.Golden
{
    // tests/golden/ 전체를 읽는다.
    //   m<N>/<이름>/golden.json + expected.json + 입력 기록   마일스톤별 골든 항목
    //   arith/<묶음>.json                                     산술 경계 벡터 묶음
    //   README.md, CHANGELOG.md                               설명과 다시 만든 기록
    // 그 밖의 폴더나 파일이 있으면 형식 문제로 본다(이름을 잘못 지은 골든이 조용히 빠지지 않게).
    // 순서는 서수 오름차순으로 고정한다(파일 시스템의 나열 순서에 기대지 않는다).
    public sealed class GoldenCatalog
    {
        public const string ArithFolder = "arith";
        private static readonly Regex MilestoneFolder = new Regex("^m[0-9]+$", RegexOptions.CultureInvariant);
        private static readonly Regex EntryFolder = new Regex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);
        private static readonly string[] RootFiles = { "README.md", "CHANGELOG.md" };

        private GoldenCatalog(string root, IReadOnlyList<GoldenEntry> entries, IReadOnlyList<ArithBundle> arith)
        {
            Root = root;
            Entries = entries;
            ArithBundles = arith;
        }

        public string Root { get; }

        // id 오름차순(서수)
        public IReadOnlyList<GoldenEntry> Entries { get; }

        // 이름 오름차순(서수)
        public IReadOnlyList<ArithBundle> ArithBundles { get; }

        public GoldenEntry Get(string id)
        {
            foreach (GoldenEntry e in Entries)
            {
                if (string.Equals(e.Id, id, StringComparison.Ordinal))
                {
                    return e;
                }
            }
            throw new KeyNotFoundException("no golden entry '" + id + "' under " + Root);
        }

        // 마일스톤 폴더 하나(예: "m1")의 항목들
        public List<GoldenEntry> InMilestone(string milestone)
        {
            var list = new List<GoldenEntry>();
            string prefix = milestone + "/";
            foreach (GoldenEntry e in Entries)
            {
                if (e.Id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    list.Add(e);
                }
            }
            return list;
        }

        public static GoldenCatalog Load(string goldenRoot)
        {
            var problems = new List<string>();
            var entries = new List<GoldenEntry>();
            var arith = new List<ArithBundle>();
            if (!Directory.Exists(goldenRoot))
            {
                problems.Add("golden root does not exist: " + goldenRoot);
                GoldenFormatException.ThrowIfAny(problems);
            }

            foreach (string file in SortedNames(Directory.GetFiles(goldenRoot)))
            {
                if (Array.IndexOf(RootFiles, file) < 0)
                {
                    problems.Add("unexpected file at the golden root: " + file);
                }
            }
            foreach (string dir in SortedNames(Directory.GetDirectories(goldenRoot)))
            {
                string full = Path.Combine(goldenRoot, dir);
                if (dir == ArithFolder)
                {
                    LoadArith(full, arith, problems);
                }
                else if (MilestoneFolder.IsMatch(dir))
                {
                    LoadMilestone(full, dir, entries, problems);
                }
                else
                {
                    problems.Add("unexpected folder at the golden root: " + dir + " (expected m<N> or arith)");
                }
            }
            GoldenFormatException.ThrowIfAny(problems);
            return new GoldenCatalog(goldenRoot, entries, arith);
        }

        private static void LoadMilestone(string full, string milestone, List<GoldenEntry> entries, List<string> problems)
        {
            foreach (string file in SortedNames(Directory.GetFiles(full)))
            {
                if (file != "README.md")
                {
                    problems.Add(milestone + ": unexpected file " + file + " (each golden lives in its own folder)");
                }
            }
            foreach (string name in SortedNames(Directory.GetDirectories(full)))
            {
                string id = milestone + "/" + name;
                if (!EntryFolder.IsMatch(name))
                {
                    problems.Add(id + ": folder name must match " + EntryFolder);
                    continue;
                }
                try
                {
                    entries.Add(GoldenEntry.Load(Path.Combine(full, name), id));
                }
                catch (GoldenFormatException ex)
                {
                    problems.AddRange(ex.Problems);
                }
            }
        }

        private static void LoadArith(string full, List<ArithBundle> arith, List<string> problems)
        {
            foreach (string name in SortedNames(Directory.GetFiles(full)))
            {
                if (name == "README.md")
                {
                    continue;
                }
                if (!name.EndsWith(".json", StringComparison.Ordinal))
                {
                    problems.Add("arith: unexpected file " + name + " (bundles are <name>.json)");
                    continue;
                }
                try
                {
                    arith.Add(ArithBundle.Load(Path.Combine(full, name)));
                }
                catch (GoldenFormatException ex)
                {
                    problems.AddRange(ex.Problems);
                }
            }
            foreach (string dir in SortedNames(Directory.GetDirectories(full)))
            {
                problems.Add("arith: unexpected folder " + dir);
            }
        }

        private static List<string> SortedNames(string[] paths)
        {
            var names = new List<string>(paths.Length);
            foreach (string p in paths)
            {
                string n = Path.GetFileName(p);
                if (n == ".DS_Store")
                {
                    continue;
                }
                names.Add(n);
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}

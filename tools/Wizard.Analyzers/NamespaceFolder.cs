using System;
using System.Collections.Generic;

namespace Wizard.Analyzers
{
    // "네임스페이스 = 폴더"(CLAUDE.md 6절)를 CONTRACTS 머리 주석의 네임스페이스와 맞게 읽은 규칙이다: 폴더가 네임스페이스를 정한다.
    // [임시] 이 해석의 문서화(DECISIONS-TECH 항목, T-33·PIPELINE 5.2·CONTRACTS 0.1 문장)는 tech-lead 의 CCR 로 따로 넣는다.
    //   앞머리 — 네임스페이스는 어셈블리 이름 + 폴더 경로의 앞쪽 k 마디다(0 ≤ k ≤ 깊이).
    //            폴더는 소유 경로로 더 잘게 나뉘므로(Sim/Spell/Compile, Content/Ids) 모듈 네임스페이스보다 깊을 수 있다.
    //            폴더보다 깊은 이름, 옆 폴더의 이름, 다른 어셈블리의 이름, 전역 네임스페이스는 오류다.
    //   한 폴더 한 네임스페이스 — 같은 폴더의 파일은 모두 같은 네임스페이스를 쓴다.
    //   바닥 — 분석기 규칙이 네임스페이스로 모듈을 가르는 폴더(Floors)는 그 폴더 이름까지를 반드시 네임스페이스에 넣는다.
    //          얕은 네임스페이스로 적어 WZ0017·WZ0020 을 피할 수 없다.
    //   예외 둘 — 부분 클래스 파일 SimWorld.<모듈>.cs(Wizard.Sim), ContentDb.<절>.cs(Wizard.Content)는 뒤의 두 규칙에서 빠진다.
    //             그 네임스페이스 블록이 그 부분 클래스만 담을 때에 한한다(다른 타입을 끼워 넣어 바닥을 피할 수 없다).
    // src/Wizard.Core/Errors/(Wizard.Core)는 앞머리 규칙의 보통 경우다.
    internal static class NamespaceFolder
    {
        // (어셈블리, 프로젝트 폴더 바로 아래의 폴더). 이 폴더 아래의 파일은 네임스페이스가 "<어셈블리>.<폴더>" 이거나 그 아래여야 한다.
        //   Wizard.Content/Rules              — 코어 버전 해시 밖(T-18). WZ0017 이 Wizard.Content.Rules 로 가른다.
        //   Wizard.Rules/{Enhance,Match,League} — 유료 재화 값을 읽지 않는 곳(spec 12 10.7). WZ0020 이 네임스페이스로 가른다.
        private static readonly string[][] Floors =
        {
            new[] { "Wizard.Content", "Rules" },
            new[] { "Wizard.Rules", "Enhance" },
            new[] { "Wizard.Rules", "Match" },
            new[] { "Wizard.Rules", "League" },
        };

        // 경로가 프로젝트 폴더 아래가 아니면 null(탐침, 생성기 출력, obj 의 파일은 검사하지 않는다)
        public static string[]? FolderSegments(string assembly, string path)
        {
            string p = SymbolUtil.NormalizePath(path);
            string[] markers = assembly == "Wizard.TestKit.Shared"
                ? new[] { "/tests/Wizard.TestKit/Shared/" }
                : new[] { "/src/" + assembly + "/", "/server/" + assembly + "/" };
            int best = -1;
            string? hit = null;
            foreach (string m in markers)
            {
                int i = p.LastIndexOf(m, StringComparison.Ordinal);
                if (i > best)
                {
                    best = i;
                    hit = m;
                }
            }
            if (hit == null)
            {
                return null;
            }
            string rel = p.Substring(best + hit.Length);
            string[] parts = rel.Split('/');
            var dirs = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Length > 0)
                {
                    dirs.Add(parts[i]);
                }
            }
            return dirs.ToArray();
        }

        // 가장 깊은 허용 네임스페이스(폴더 경로 전부)
        public static string Expected(string assembly, string[] folders) => Join(assembly, folders, folders.Length);

        // 가장 얕은 허용 네임스페이스(바닥이 있으면 바닥, 없으면 어셈블리 이름)
        public static string Shallowest(string assembly, string[] folders) => Join(assembly, folders, FloorDepth(assembly, folders));

        // 앞머리: ns 가 "<어셈블리>" + 폴더 앞쪽 k 마디인가(k 는 minDepth 이상)
        public static bool IsFolderPrefix(string assembly, string[] folders, string ns, int minDepth)
        {
            for (int k = minDepth; k <= folders.Length; k++)
            {
                if (ns == Join(assembly, folders, k))
                {
                    return true;
                }
            }
            return false;
        }

        // 이 폴더에서 네임스페이스에 꼭 들어가야 하는 앞쪽 마디 수(바닥이 없으면 0)
        public static int FloorDepth(string assembly, string[] folders)
        {
            if (folders.Length == 0)
            {
                return 0;
            }
            foreach (string[] floor in Floors)
            {
                if (floor[0] == assembly && floor[1] == folders[0])
                {
                    return 1;
                }
            }
            return 0;
        }

        // 부분 클래스 파일의 예외: SimWorld.<모듈>.cs → Wizard.Sim, ContentDb.<절>.cs → Wizard.Content.
        // 파일 이름이 그 꼴이면 부분 클래스의 이름("SimWorld", "ContentDb")을, 아니면 null 을 돌려준다.
        public static string? PartialClassOf(string assembly, string path)
        {
            string file = FileName(path);
            if (assembly == "Wizard.Sim" && IsPartialFile(file, "SimWorld"))
            {
                return "SimWorld";
            }
            if (assembly == "Wizard.Content" && IsPartialFile(file, "ContentDb"))
            {
                return "ContentDb";
            }
            return null;
        }

        // 예외에 드는가. 모듈 폴더에 두되 네임스페이스는 어셈블리 이름이고, 그 블록은 그 부분 클래스만 담는다(onlyThatClass).
        // 한 폴더 한 네임스페이스와 바닥 규칙에서 빠진다.
        public static bool IsPartialClassException(string assembly, string path, string ns, bool onlyThatClass) =>
            onlyThatClass && ns == assembly && PartialClassOf(assembly, path) != null;

        // 파일 하나만 보고 판정할 수 있는 규칙(앞머리, 바닥, 예외). 한 폴더 한 네임스페이스는 컴파일 전체를 본 뒤에 따로 검사한다.
        public static bool IsAllowed(string assembly, string path, string ns, bool onlyPartialClass)
        {
            string[]? folders = FolderSegments(assembly, path);
            if (folders == null)
            {
                return true;
            }
            if (IsPartialClassException(assembly, path, ns, onlyPartialClass))
            {
                return true;
            }
            return IsFolderPrefix(assembly, folders, ns, FloorDepth(assembly, folders));
        }

        // 한 폴더 한 네임스페이스에서 폴더를 가리키는 열쇠("Wizard.Sim/Spell/Compile")
        public static string FolderKey(string assembly, string[] folders) =>
            folders.Length == 0 ? assembly : assembly + "/" + string.Join("/", folders);

        private static string Join(string assembly, string[] folders, int count) =>
            count == 0 ? assembly : assembly + "." + string.Join(".", folders, 0, count);

        private static string FileName(string path)
        {
            string p = SymbolUtil.NormalizePath(path);
            int slash = p.LastIndexOf('/');
            return slash < 0 ? p : p.Substring(slash + 1);
        }

        // "<접두>.<이름>.cs" (이름은 영문자·숫자·밑줄 한 마디)
        private static bool IsPartialFile(string file, string prefix)
        {
            string head = prefix + ".";
            if (file.Length <= head.Length + 3 || !file.StartsWith(head, StringComparison.Ordinal) || !file.EndsWith(".cs", StringComparison.Ordinal))
            {
                return false;   // "ContentDb.cs" 자체는 이 꼴이 아니다
            }
            string middle = file.Substring(head.Length, file.Length - head.Length - 3);
            foreach (char c in middle)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

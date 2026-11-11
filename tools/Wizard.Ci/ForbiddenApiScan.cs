using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wizard.Ci
{
    // pre-commit 의 금지 API 빠른 검사(PIPELINE 4.6 "금지 API 의 빠른 검사"). 올린(staged) 소스의 구문 나무만 본다(의미 분석 없음).
    // 정본은 분석기(tools/Wizard.Analyzers, 빌드와 determinism-guard.sh)이고, 이것은 커밋 전에 흔한 위반을 빨리 막는 그물이다.
    // 그래서 이름이 겹칠 수 있는 것(Task, Sort, 열거형 멤버 Random 등)은 보지 않고, 틀림없는 꼴만 잡는다(거짓 거절을 내지 않는다).
    // 주석과 문자열 속의 낱말은 보지 않는다.
    //   Core·Content·Sim(CLAUDE.md 3절): float/double/decimal 과 실수 리터럴, unsafe, checked, System.Random, Guid.NewGuid,
    //     DateTime(Offset), Stopwatch, Environment.TickCount, Thread·ThreadPool·Parallel·[ThreadStatic], BitConverter, MemoryMarshal,
    //     Unsafe, Marshal, Buffer.BlockCopy, Int128·UInt128, BitOperations, Math.BigMul, UnityEngine, using System.Threading(.Tasks)
    //   Rules: float/double/decimal 과 실수 리터럴
    //   Netcode: DateTime(Offset), Stopwatch, Environment.TickCount, Socket·UdpClient·TcpClient, using System.Net.Sockets
    public static class ForbiddenApiScan
    {
        public enum Role
        {
            None,
            Core,
            Rules,
            Netcode,
        }

        private static readonly string[] CoreTypes =
        {
            "DateTime", "DateTimeOffset", "Stopwatch", "Thread", "ThreadPool", "Parallel", "BitConverter", "MemoryMarshal", "Unsafe",
            "Marshal", "Int128", "UInt128", "BitOperations",
        };

        private static readonly string[] NetcodeTypes = { "DateTime", "DateTimeOffset", "Stopwatch", "Socket", "UdpClient", "TcpClient" };

        public static Role RoleOf(string path)
        {
            if (!path.EndsWith(".cs", StringComparison.Ordinal))
            {
                return Role.None;
            }
            if (path.StartsWith("src/Wizard.Core/", StringComparison.Ordinal) || path.StartsWith("src/Wizard.Content/", StringComparison.Ordinal) ||
                path.StartsWith("src/Wizard.Sim/", StringComparison.Ordinal))
            {
                return Role.Core;
            }
            if (path.StartsWith("src/Wizard.Rules/", StringComparison.Ordinal))
            {
                return Role.Rules;
            }
            if (path.StartsWith("src/Wizard.Netcode/", StringComparison.Ordinal))
            {
                return Role.Netcode;
            }
            return Role.None;
        }

        // 위반마다 "<경로>:<줄>: <무엇>" 한 줄
        public static List<string> Scan(string path, string source, Role role)
        {
            var found = new List<string>();
            if (role == Role.None)
            {
                return found;
            }
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9));
            SyntaxNode rootNode = tree.GetRoot();
            string what = role == Role.Core ? "코어(Core·Content·Sim)" : role == Role.Rules ? "Wizard.Rules" : "Wizard.Netcode";

            void Hit(SyntaxNodeOrToken at, string text)
            {
                int line = at.GetLocation()!.GetLineSpan().StartLinePosition.Line + 1;
                found.Add(path + ":" + line.ToString(CultureInfo.InvariantCulture) + ": " + text + " — " + what + "에서 금지(CLAUDE.md 3절)");
            }

            foreach (SyntaxToken t in rootNode.DescendantTokens())
            {
                if (role != Role.Netcode)
                {
                    if (t.IsKind(SyntaxKind.FloatKeyword) || t.IsKind(SyntaxKind.DoubleKeyword) || t.IsKind(SyntaxKind.DecimalKeyword))
                    {
                        Hit(t, "실수형 '" + t.Text + "'");
                    }
                    else if (t.IsKind(SyntaxKind.NumericLiteralToken) && !(t.Value is int || t.Value is uint || t.Value is long || t.Value is ulong))
                    {
                        Hit(t, "실수 리터럴 '" + t.Text + "'");
                    }
                }
                if (role == Role.Core)
                {
                    if (t.IsKind(SyntaxKind.UnsafeKeyword))
                    {
                        Hit(t, "'unsafe'");
                    }
                    else if (t.IsKind(SyntaxKind.CheckedKeyword))
                    {
                        Hit(t, "'checked'");
                    }
                }
            }

            foreach (SyntaxNode n in rootNode.DescendantNodes())
            {
                switch (n)
                {
                    case UsingDirectiveSyntax u when u.Name != null:
                    {
                        string name = u.Name.ToString();
                        if (role == Role.Core && (name == "System.Threading" || name == "System.Threading.Tasks" || name.StartsWith("UnityEngine", StringComparison.Ordinal)))
                        {
                            Hit(u, "using " + name);
                        }
                        else if (role == Role.Netcode && name == "System.Net.Sockets")
                        {
                            Hit(u, "using " + name);
                        }
                        break;
                    }
                    case IdentifierNameSyntax id when IsLeadingOrSystemQualified(id) && id.FirstAncestorOrSelf<UsingDirectiveSyntax>() == null:
                    {
                        // 맨 앞 이름(DateTime.Now 의 DateTime, new Stopwatch() 의 Stopwatch)과 System 으로 시작하는 이름의 끝
                        // (System.DateTime, System.Diagnostics.Stopwatch). 다른 것의 멤버 이름(x.Thread)은 보지 않는다
                        string name = id.Identifier.ValueText;
                        if (role == Role.Core && (Array.IndexOf(CoreTypes, name) >= 0 || name == "UnityEngine"))
                        {
                            Hit(id, "'" + name + "'");
                        }
                        else if (role == Role.Netcode && Array.IndexOf(NetcodeTypes, name) >= 0)
                        {
                            Hit(id, "'" + name + "'");
                        }
                        break;
                    }
                    case MemberAccessExpressionSyntax ma:
                    {
                        string left = ma.Expression.ToString();
                        string right = ma.Name.Identifier.ValueText;
                        bool clock = (left == "Environment" || left == "System.Environment") && (right == "TickCount" || right == "TickCount64");
                        if (role == Role.Core && (clock || ((left == "Guid" || left == "System.Guid") && right == "NewGuid") ||
                                                  ((left == "Buffer" || left == "System.Buffer") && right == "BlockCopy") ||
                                                  ((left == "Math" || left == "System.Math") && right == "BigMul") ||
                                                  ((left == "Random" || left == "System.Random") && right == "Shared")))
                        {
                            Hit(ma, "'" + left + "." + right + "'");
                        }
                        else if (role == Role.Netcode && clock)
                        {
                            Hit(ma, "'" + left + "." + right + "'");
                        }
                        break;
                    }
                    case ObjectCreationExpressionSyntax oc when role == Role.Core:
                    {
                        string type = oc.Type.ToString();
                        if (type == "Random" || type == "System.Random")
                        {
                            Hit(oc, "'new " + type + "'");
                        }
                        break;
                    }
                    case AttributeSyntax a when role == Role.Core:
                    {
                        string an = a.Name.ToString();
                        if (an == "ThreadStatic" || an == "ThreadStaticAttribute" || an == "System.ThreadStatic")
                        {
                            Hit(a, "[" + an + "]");
                        }
                        break;
                    }
                    case QualifiedNameSyntax q when role == Role.Core && q.ToString() == "System.Random":
                        Hit(q, "'System.Random'");
                        break;
                }
            }
            // 같은 자리를 두 규칙이 잡으면(using UnityEngine) 한 번만
            var distinct = new List<string>();
            foreach (string f in found)
            {
                if (!distinct.Contains(f))
                {
                    distinct.Add(f);
                }
            }
            return distinct;
        }

        private static bool IsLeadingOrSystemQualified(IdentifierNameSyntax id)
        {
            string? left = null;
            if (id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id)
            {
                left = ma.Expression.ToString();
            }
            else if (id.Parent is QualifiedNameSyntax qn && qn.Right == id)
            {
                left = qn.Left.ToString();
            }
            return left == null || left == "System" || left.StartsWith("System.", StringComparison.Ordinal) || left == "global::System" ||
                   left.StartsWith("global::System.", StringComparison.Ordinal);
        }
    }
}

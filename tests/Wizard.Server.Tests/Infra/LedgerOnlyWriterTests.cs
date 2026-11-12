using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Server.Tests.Infra
{
    // M10-DB-01 수락 2, CONTRACTS 7.2절 규칙: 유스케이스는 ILedger 를 거치지 않고 잔액·수량 표와 원장 표를 고치지 않는다.
    // server/ 의 C# 가운데 이 네 표에 쓰는 SQL 은 PgLedger.cs 에만 있어야 한다.
    public sealed class LedgerOnlyWriterTests
    {
        private const string LedgerFile = "server/Wizard.Server.Data/Core/PgLedger.cs";

        private static readonly Regex Write = new Regex(
            @"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM|TRUNCATE(\s+TABLE)?|MERGE\s+INTO|COPY)\s+(ONLY\s+)?(public\.)?""?(currency_balance|item_stack|currency_ledger|item_ledger)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static IEnumerable<string> ServerSources()
        {
            string root = TestRepo.Path("server");
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                            && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }

        [Fact]
        public void OnlyPgLedgerWritesBalancesStacksAndLedgers()
        {
            string ledger = Path.GetFullPath(TestRepo.Path(LedgerFile));
            var offenders = new List<string>();
            foreach (string file in ServerSources())
            {
                if (string.Equals(Path.GetFullPath(file), ledger, StringComparison.Ordinal))
                {
                    continue;
                }
                string text = File.ReadAllText(file);
                foreach (Match m in Write.Matches(text))
                {
                    offenders.Add(Path.GetRelativePath(TestRepo.Root, file) + ": " + m.Value);
                }
            }
            Assert.Empty(offenders);
        }

        [Fact]
        public void TheScanSeesThePgLedgerWrites()
        {
            // 검사식이 아무것도 못 잡는 꼴이 되면 위 시험이 헛돈다
            string text = File.ReadAllText(TestRepo.Path(LedgerFile));
            string[] tables = Write.Matches(text).Select(m => m.Groups[5].Value.ToLowerInvariant()).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "currency_balance", "currency_ledger", "item_ledger", "item_stack" }, tables);
        }
    }
}

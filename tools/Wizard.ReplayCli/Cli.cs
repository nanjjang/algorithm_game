using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Wizard.Content;
using Wizard.Sim;
using Wizard.TestKit.Shared;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.ReplayCli
{
    // 명령 줄. 종료 코드: 0 = 같다/통과, 1 = 갈라졌다/골든 불일치, 2 = 사용법·형식 오류.
    // 저장소 뿌리는 --root 가 없으면 이 도구가 놓인 자리에서 위로 찾는다(작업 트리에서 돌면 그 작업 트리. CLAUDE.md 9절).
    public static class Cli
    {
        public const int Ok = 0;
        public const int Mismatch = 1;
        public const int Usage = 2;

        private const string Help =
            "usage:\n" +
            "  replaycli hashes <record> [--kind endless|practice|duel] [--every <ticks>]\n" +
            "      tick and state hash at world.Tick == 0, every <ticks> (default 1) and the end\n" +
            "  replaycli trace <record> --out <file> [--kind K]\n" +
            "      per-tick state hash and state-section digests (compare runs from other machines with diff)\n" +
            "  replaycli diff <A> <B> [--kind K] [--kind-b K]\n" +
            "      A and B are records (.wzrc/.wzdr) or trace files; prints the first tick whose hash differs and the state sections that differ\n" +
            "  replaycli golden [--update] [--id <m1/name>]\n" +
            "      replays tests/golden/m*/ entries; --update rewrites expected.json (add a line to tests/golden/CHANGELOG.md)\n" +
            "  replaycli bot --kind K --ticks <n> --bot-seed <u64> [--seed <u64>] [--profile play|dense|stance|loadout] --out <file>\n" +
            "      records bot input as .wzrc (endless, practice) or .wzdr (duel)\n" +
            "common: --root <repo>  (default: the checkout this tool was built from)\n" +
            "kind: .wzrc defaults to endless, .wzdr is duel\n";

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                error.Write(Help);
                return args.Length == 0 ? Usage : Ok;
            }
            try
            {
                var a = new Args(args);
                switch (args[0])
                {
                    case "hashes":
                        return Hashes(a, output);
                    case "trace":
                        return Trace(a, output);
                    case "diff":
                        return Diff(a, output);
                    case "golden":
                        return Golden(a, output, error);
                    case "bot":
                        return Bot(a, output);
                    default:
                        error.Write("unknown command '" + args[0] + "'\n" + Help);
                        return Usage;
                }
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is IOException || e is KeyNotFoundException
                                      || e is GoldenFormatException || e is InvalidOperationException)
            {
                error.Write("replaycli: " + e.Message + "\n");
                return Usage;
            }
        }

        private static int Hashes(Args a, TextWriter output)
        {
            string path = a.Positional(1, "record");
            uint every = a.UInt("--every", 1);
            string? kindText = a.Optional("--kind");
            if (every == 0)
            {
                throw new FormatException("--every must be positive");
            }
            a.RejectUnknown();
            ContentDb db = RepoPack.Build(a.Root);
            RecordFeed feed = RecordFeed.Load(File.ReadAllBytes(path), GoldenRunner.Limits);
            WorldKind kind = KindOf(kindText, feed);
            ulong[] hashes = Replayer.TickHashes(ReplayWorlds.Create(db, kind, feed), feed);
            var sb = new StringBuilder();
            uint last = (uint)(hashes.Length - 1);
            for (uint t = 0; t <= last; t++)
            {
                if (t % every == 0 || t == last)
                {
                    sb.Append(t.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(HashText.Format(hashes[t])).Append('\n');
                }
            }
            output.Write(sb.ToString());
            if (last == feed.EndTick && hashes[last] != feed.FinalStateHash)
            {
                output.Write("# note: the record footer says " + HashText.Format(feed.FinalStateHash) +
                             " (recorded by another core or pack, or the record was made by a different run)\n");
            }
            return Ok;
        }

        private static int Trace(Args a, TextWriter output)
        {
            string path = a.Positional(1, "record");
            string outPath = a.Required("--out");
            string? kindText = a.Optional("--kind");
            a.RejectUnknown();
            ContentDb db = RepoPack.Build(a.Root);
            RecordFeed feed = RecordFeed.Load(File.ReadAllBytes(path), GoldenRunner.Limits);
            WorldKind kind = KindOf(kindText, feed);
            using (var w = new StreamWriter(outPath, false, new UTF8Encoding(false)))
            {
                TraceFile.Write(w, kind, ReplayWorlds.Create(db, kind, feed), feed);
            }
            output.Write("wrote " + outPath + "\n");
            return Ok;
        }

        private static int Diff(Args a, TextWriter output)
        {
            string pathA = a.Positional(1, "A");
            string pathB = a.Positional(2, "B");
            string? kindA = a.Optional("--kind");
            string? kindB = a.Optional("--kind-b") ?? kindA;
            a.RejectUnknown();
            byte[] bytesA = File.ReadAllBytes(pathA), bytesB = File.ReadAllBytes(pathB);
            bool traceA = IsTrace(bytesA), traceB = IsTrace(bytesB);
            DivergenceReport report;
            if (traceA && traceB)
            {
                report = TraceFile.Compare(TraceFile.Read(Encoding.UTF8.GetString(bytesA)), TraceFile.Read(Encoding.UTF8.GetString(bytesB)));
            }
            else if (!traceA && !traceB)
            {
                ContentDb db = RepoPack.Build(a.Root);
                RecordFeed fa = RecordFeed.Load(bytesA, GoldenRunner.Limits), fb = RecordFeed.Load(bytesB, GoldenRunner.Limits);
                WorldKind ka = KindOf(kindA, fa);
                WorldKind kb = KindOf(kindB, fb);
                report = Divergence.Compare(ReplayWorlds.Create(db, ka, fa), fa, ReplayWorlds.Create(db, kb, fb), fb);
            }
            else
            {
                throw new FormatException("diff compares two records or two traces, not one of each (make a trace with `replaycli trace`)");
            }
            output.Write(report + "\n");
            return report.Diverged ? Mismatch : Ok;
        }

        private static int Golden(Args a, TextWriter output, TextWriter error)
        {
            bool update = a.Flag("--update");
            string? only = a.Optional("--id");
            a.RejectUnknown();
            string goldenRoot = RepoRoot.Resolve(a.Root, "tests/golden");
            ContentDb db = RepoPack.Build(a.Root);
            int failed = 0, seen = 0;
            foreach ((string id, string dir) in GoldenRunner.EntryFolders(goldenRoot))
            {
                if (only != null && !string.Equals(only, id, StringComparison.Ordinal))
                {
                    continue;
                }
                seen++;
                GoldenEntry entry = update ? GoldenRunner.LoadForUpdate(dir, id) : GoldenEntry.Load(dir, id);
                IReadOnlyList<GoldenHash> actual = GoldenRunner.Run(db, entry);
                if (update)
                {
                    string path = Path.Combine(dir, GoldenEntry.ExpectedFileName);
                    string text = GoldenEntry.FormatExpected(id, actual);
                    bool same = File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text;
                    if (!same)
                    {
                        File.WriteAllText(path, text, new UTF8Encoding(false));
                    }
                    output.Write((same ? "same     " : "updated  ") + id + "\n");
                    continue;
                }
                GoldenDiff? d = entry.Compare(actual);
                output.Write((d == null ? "ok       " : "MISMATCH ") + id + (d == null ? "" : ": " + d) + "\n");
                if (d != null)
                {
                    failed++;
                }
            }
            if (only != null && seen == 0)
            {
                error.Write("replaycli: no golden entry '" + only + "'\n");
                return Usage;
            }
            if (update)
            {
                output.Write("expected.json rewritten where the hashes changed; add a line to tests/golden/CHANGELOG.md with the reason\n");
            }
            else if (failed > 0)
            {
                output.Write(failed + " golden(s) differ. Find the first differing tick and state section with " +
                             "`replaycli diff` (two records) or `replaycli trace` + `replaycli diff` (two machines)\n");
            }
            return failed == 0 ? Ok : Mismatch;
        }

        private static int Bot(Args a, TextWriter output)
        {
            WorldKind kind = ReplayWorlds.ParseKind(a.Required("--kind"));
            uint ticks = a.UInt("--ticks", 0);
            ulong botSeed = a.Seed("--bot-seed", null);
            ulong seed = a.Seed("--seed", 0UL);
            BotProfile profile = BotProfile.Named(a.Optional("--profile") ?? "play");
            string outPath = a.Required("--out");
            a.RejectUnknown();
            if (ticks == 0)
            {
                throw new FormatException("--ticks must be positive");
            }
            if (kind == WorldKind.Practice && seed != 0UL)
            {
                throw new FormatException("a practice world has no seed (PracticeSetup); leave --seed out");
            }
            ContentDb db = RepoPack.Build(a.Root);
            byte[] file = kind switch
            {
                WorldKind.Duel => BotRecorder.RecordDuel(db, BotRecorder.DuelSetupFor(db, seed), botSeed, ticks, profile),
                WorldKind.Practice => BotRecorder.RecordRun(db, kind, BotRecorder.PracticeHeader(db), botSeed, ticks, profile),
                _ => BotRecorder.RecordRun(db, kind, BotRecorder.EndlessHeader(db, seed), botSeed, ticks, profile),
            };
            File.WriteAllBytes(outPath, file);
            output.Write("wrote " + outPath + " (" + file.Length + " bytes, " + ticks + " ticks)\n");
            return Ok;
        }

        private static WorldKind KindOf(string? text, RecordFeed feed) =>
            text == null ? ReplayWorlds.DefaultKind(feed) : ReplayWorlds.ParseKind(text);

        private static bool IsTrace(byte[] bytes)
        {
            byte[] magic = Encoding.ASCII.GetBytes(TraceFile.Magic);
            return bytes.Length >= magic.Length && bytes.AsSpan(0, magic.Length).SequenceEqual(magic);
        }

        // 아주 작은 인자 판독기: 위치 인자, "--이름 값", "--깃발"
        private sealed class Args
        {
            private readonly List<string> _positional = new List<string>();
            private readonly Dictionary<string, string?> _named = new Dictionary<string, string?>(StringComparer.Ordinal);
            private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);
            private static readonly string[] Flags = { "--update" };

            public Args(string[] args)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string s = args[i];
                    if (s.StartsWith("--", StringComparison.Ordinal))
                    {
                        if (Array.IndexOf(Flags, s) >= 0)
                        {
                            _named[s] = null;
                        }
                        else if (i + 1 < args.Length)
                        {
                            _named[s] = args[++i];
                        }
                        else
                        {
                            throw new FormatException(s + " needs a value");
                        }
                    }
                    else
                    {
                        _positional.Add(s);
                    }
                }
                Root = Optional("--root") ?? RepoRoot.Find(AppContext.BaseDirectory);
            }

            public string Root { get; }

            public string Positional(int index, string what)
            {
                if (index >= _positional.Count)
                {
                    throw new FormatException("missing <" + what + ">");
                }
                return _positional[index];
            }

            public string? Optional(string name)
            {
                _used.Add(name);
                return _named.TryGetValue(name, out string? v) ? v : null;
            }

            public string Required(string name) => Optional(name) ?? throw new FormatException(name + " is required");

            public bool Flag(string name)
            {
                _used.Add(name);
                return _named.ContainsKey(name);
            }

            public uint UInt(string name, uint fallback)
            {
                string? v = Optional(name);
                if (v == null)
                {
                    return fallback;
                }
                if (!uint.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out uint n))
                {
                    throw new FormatException(name + " must be a non-negative integer");
                }
                return n;
            }

            public ulong Seed(string name, ulong? fallback)
            {
                string? v = Optional(name);
                if (v == null)
                {
                    return fallback ?? throw new FormatException(name + " is required");
                }
                if (!HashText.TryParseSeed(v, out ulong s))
                {
                    throw new FormatException(name + " must be \"0x\" + hex or a decimal u64");
                }
                return s;
            }

            public void RejectUnknown()
            {
                _used.Add("--root");
                foreach (string key in _named.Keys)
                {
                    if (!_used.Contains(key))
                    {
                        throw new FormatException("unknown option " + key);
                    }
                }
            }
        }
    }
}

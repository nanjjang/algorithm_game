using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Wizard.Sim;
using Wizard.TestKit.Shared;

namespace Wizard.ReplayCli
{
    // 틱마다 상태 해시와 절 다이제스트를 적은 글자 파일. 다른 기기·런타임에서 돌린 실행을 나중에 비교할 때 쓴다
    // (예: arm64 와 x64, dotnet 과 Unity 플레이어의 골든이 갈라졌을 때 각자 trace 를 내고 diff 로 처음 갈라진 틱과 절을 본다).
    //
    //   #replaycli-trace 1
    //   #kind practice
    //   <틱> <상태 해시> <절 id>=<절 다이제스트> …     (world.Tick == 틱 일 때. 생략된 풀 절은 적지 않는다)
    //
    // 실행마다 바뀌는 값(시각, 경로)은 적지 않는다. 같은 실행이면 같은 바이트다.
    public sealed class TraceFile
    {
        public const string Magic = "#replaycli-trace 1";
        private const string KindPrefix = "#kind ";

        private TraceFile(string kind, List<TraceLine> lines)
        {
            Kind = kind;
            Lines = lines;
        }

        public string Kind { get; }

        public IReadOnlyList<TraceLine> Lines { get; }

        public static void Write(TextWriter output, WorldKind kind, SimWorld world, RecordFeed feed)
        {
            var sink = new CountingSink();
            output.Write(Magic + "\n");
            output.Write(KindPrefix + ReplayWorlds.KindName(kind) + "\n");
            while (true)
            {
                StateImage image = StateSections.Capture(world);
                var sb = new StringBuilder();
                sb.Append(world.Tick.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(HashText.Format(world.ComputeStateHash()));
                foreach (SectionBytes s in image.Sections)
                {
                    if (s.Present)
                    {
                        sb.Append(' ').Append(s.Info.Id.ToString(CultureInfo.InvariantCulture)).Append('=').Append(HashText.Format(s.Digest));
                    }
                }
                output.Write(sb.Append('\n').ToString());
                if (world.Tick >= feed.EndTick || world.IsOver)
                {
                    break;
                }
                Replayer.StepOnce(world, feed, sink);
            }
        }

        public static TraceFile Read(string text)
        {
            string[] rows = text.Replace("\r\n", "\n").Split('\n');
            if (rows.Length < 2 || rows[0] != Magic || !rows[1].StartsWith(KindPrefix, StringComparison.Ordinal))
            {
                throw new FormatException("not a replaycli trace (first lines must be '" + Magic + "' and '" + KindPrefix + "…')");
            }
            var lines = new List<TraceLine>();
            for (int i = 2; i < rows.Length; i++)
            {
                if (rows[i].Length == 0)
                {
                    continue;
                }
                string[] parts = rows[i].Split(' ');
                if (parts.Length < 2 || !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick)
                    || !HashText.TryParseHash(parts[1], out ulong hash))
                {
                    throw new FormatException("trace line " + (i + 1) + " is not '<tick> <hash> …'");
                }
                var sections = new SortedList<ushort, ulong>();
                for (int p = 2; p < parts.Length; p++)
                {
                    int eq = parts[p].IndexOf('=');
                    if (eq <= 0 || !ushort.TryParse(parts[p].Substring(0, eq), NumberStyles.None, CultureInfo.InvariantCulture, out ushort id)
                        || !HashText.TryParseHash(parts[p].Substring(eq + 1), out ulong digest) || sections.ContainsKey(id))
                    {
                        throw new FormatException("trace line " + (i + 1) + ": bad section entry '" + parts[p] + "'");
                    }
                    sections.Add(id, digest);
                }
                lines.Add(new TraceLine(tick, hash, sections));
            }
            return new TraceFile(rows[1].Substring(KindPrefix.Length), lines);
        }

        public static DivergenceReport Compare(TraceFile a, TraceFile b)
        {
            int n = Math.Min(a.Lines.Count, b.Lines.Count);
            for (int i = 0; i < n; i++)
            {
                TraceLine x = a.Lines[i], y = b.Lines[i];
                if (x.Tick != y.Tick || x.Hash != y.Hash)
                {
                    var names = new List<string>();
                    var ids = new SortedSet<ushort>(x.Sections.Keys);
                    ids.UnionWith(y.Sections.Keys);
                    foreach (ushort id in ids)
                    {
                        bool inX = x.Sections.TryGetValue(id, out ulong dx), inY = y.Sections.TryGetValue(id, out ulong dy);
                        if (inX != inY || dx != dy)
                        {
                            names.Add(NameOf(id));
                        }
                    }
                    string? note = x.Tick != y.Tick ? "trace ticks differ at line " + (i + 3) : null;
                    return new DivergenceReport((uint)(i + 1), true, x.Tick, x.Hash, y.Hash, Array.Empty<SectionDifference>(), names, note);
                }
            }
            string? lengthNote = a.Lines.Count == b.Lines.Count ? null : "the traces have different lengths: " + a.Lines.Count + " vs " + b.Lines.Count;
            return new DivergenceReport((uint)n, false, 0, 0, 0, Array.Empty<SectionDifference>(), Array.Empty<string>(), lengthNote);
        }

        private static string NameOf(ushort id)
        {
            foreach (StateSectionInfo s in StateSections.List)
            {
                if (s.Id == id)
                {
                    return s.Name;
                }
            }
            return "section " + id.ToString(CultureInfo.InvariantCulture);
        }
    }
}

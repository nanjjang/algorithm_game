using System;
using System.Collections.Generic;
using Wizard.Sim;
using Wizard.Sim.Input;

namespace Wizard.ReplayCli
{
    // 두 실행을 틱마다 나란히 돌려 처음 갈라진 틱과 상태 절을 찾는다.
    // 같은 기록을 두 번(결정론), 두 기록(클라이언트의 기록과 서버가 받은 기록), 같은 기록의 다른 설정 등 무엇이든 (세계, 기록) 둘이면 된다.
    public static class Divergence
    {
        public static DivergenceReport Compare(SimWorld a, RecordFeed feedA, SimWorld b, RecordFeed feedB)
        {
            if (a == null || b == null || feedA == null || feedB == null)
            {
                throw new ArgumentNullException(a == null ? nameof(a) : b == null ? nameof(b) : feedA == null ? nameof(feedA) : nameof(feedB));
            }
            var sinkA = new CountingSink();
            var sinkB = new CountingSink();
            uint compared = 0;
            while (true)
            {
                ulong ha = a.ComputeStateHash();
                ulong hb = b.ComputeStateHash();
                compared++;
                if (ha != hb || a.Tick != b.Tick)
                {
                    return Diverged(a, feedA, b, feedB, compared, ha, hb);
                }
                bool endA = a.Tick >= feedA.EndTick || a.IsOver;
                bool endB = b.Tick >= feedB.EndTick || b.IsOver;
                if (endA || endB)
                {
                    string? note = endA == endB ? null
                        : "the runs have different lengths: " + a.Tick + (endA ? " (end)" : "") + " vs " + b.Tick + (endB ? " (end)" : "") +
                          "; ends are " + feedA.EndTick + " and " + feedB.EndTick;
                    return new DivergenceReport(compared, false, a.Tick, ha, hb, Array.Empty<SectionDifference>(), Array.Empty<string>(), note);
                }
                Replayer.StepOnce(a, feedA, sinkA);
                Replayer.StepOnce(b, feedB, sinkB);
            }
        }

        private static DivergenceReport Diverged(SimWorld a, RecordFeed feedA, SimWorld b, RecordFeed feedB, uint compared, ulong ha, ulong hb)
        {
            List<SectionDifference> sections = StateImage.Diff(StateSections.Capture(a), StateSections.Capture(b));
            var names = new List<string>();
            foreach (SectionDifference d in sections)
            {
                names.Add(d.Section.Name);
            }
            string? note = null;
            if (a.Tick != b.Tick)
            {
                note = "world ticks differ: " + a.Tick + " vs " + b.Tick;
            }
            else if (a.Tick > 0 && !SameInputs(feedA, feedB, a.Tick - 1))
            {
                note = "the records fed different inputs or commands at tick " + (a.Tick - 1);
            }
            return new DivergenceReport(compared, true, a.Tick, ha, hb, sections, names, note);
        }

        private static bool SameInputs(RecordFeed a, RecordFeed b, uint tick)
        {
            ReadOnlySpan<PlayerInput> ia = a.InputsAt(tick), ib = b.InputsAt(tick);
            ReadOnlySpan<RunCommand> ca = a.CommandsAt(tick), cb = b.CommandsAt(tick);
            if (ia.Length != ib.Length || ca.Length != cb.Length)
            {
                return false;
            }
            for (int i = 0; i < ia.Length; i++)
            {
                InputEvent x = ia[i].Event, y = ib[i].Event;
                if (ia[i].Slot.Value != ib[i].Slot.Value || x.Offset != y.Offset || x.Key != y.Key || x.Down != y.Down)
                {
                    return false;
                }
            }
            for (int i = 0; i < ca.Length; i++)
            {
                if (ca[i].Op != cb[i].Op || ca[i].A != cb[i].A || ca[i].B != cb[i].B || ca[i].Slot.Value != cb[i].Slot.Value)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

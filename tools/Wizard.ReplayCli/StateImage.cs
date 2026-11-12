using System;
using System.Collections.Generic;
using System.Text;

namespace Wizard.ReplayCli
{
    // 세계 상태 하나를 절로 나눈 것(StateSections.Capture). 절은 절 목록(StateSections.List)의 순서 그대로 다 있다
    public sealed class StateImage
    {
        private readonly SectionBytes[] _sections;

        internal StateImage(byte[] all, SectionBytes[] sections)
        {
            All = all;
            _sections = sections;
        }

        // WriteState 의 바이트 전체
        public byte[] All { get; }

        public IReadOnlyList<SectionBytes> Sections => _sections;

        public SectionBytes Section(ushort id)
        {
            foreach (SectionBytes s in _sections)
            {
                if (s.Info.Id == id)
                {
                    return s;
                }
            }
            throw new KeyNotFoundException("no state section " + id);
        }

        // 다른 절(id 오름차순). 같으면 빈 목록
        public static List<SectionDifference> Diff(StateImage expected, StateImage actual)
        {
            if (expected == null || actual == null)
            {
                throw new ArgumentNullException(expected == null ? nameof(expected) : nameof(actual));
            }
            var list = new List<SectionDifference>();
            for (int i = 0; i < expected._sections.Length; i++)
            {
                SectionBytes a = expected._sections[i];
                SectionBytes b = actual._sections[i];
                if (!a.SameAs(b))
                {
                    list.Add(new SectionDifference(a.Info, a.Present, b.Present, a.Bytes.Length, b.Bytes.Length, FirstDifference(a.Bytes, b.Bytes)));
                }
            }
            return list;
        }

        public static string Describe(IReadOnlyList<SectionDifference> differences)
        {
            if (differences.Count == 0)
            {
                return "(no section differs)";
            }
            var sb = new StringBuilder();
            foreach (SectionDifference d in differences)
            {
                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }
                sb.Append(d);
            }
            return sb.ToString();
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                {
                    return i;
                }
            }
            return n;
        }
    }
}

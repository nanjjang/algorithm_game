using System;
using System.Collections.Generic;
using System.IO;

namespace Wizard.DataBuild.Core
{
    // 검사가 찾은 문제. 한 줄에 하나: "<저장소 기준 경로>[:<JSON 위치>]: <무엇이 왜 틀렸는가>"
    // 찾은 순서가 아니라 정렬해서 낸다(같은 입력이면 같은 출력. 도구는 결정적이다).
    public sealed class Diagnostics
    {
        private readonly List<string> _errors = new List<string>();

        public int Count => _errors.Count;

        public bool HasErrors => _errors.Count > 0;

        public void Error(string path, string message) => _errors.Add(path + ": " + message);

        public void Error(string path, string pointer, string message) =>
            _errors.Add(path + (pointer.Length == 0 ? "" : ":" + pointer) + ": " + message);

        public IReadOnlyList<string> Sorted()
        {
            var copy = new List<string>(_errors);
            copy.Sort(StringComparer.Ordinal);
            return copy;
        }

        public void WriteTo(TextWriter w)
        {
            foreach (string e in Sorted())
            {
                w.WriteLine("FAIL  " + e);
            }
        }
    }
}

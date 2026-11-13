using System.Collections.Generic;
using Wizard.ViewLogic.Assets.Json;

namespace Wizard.ViewLogic.Assets.Loaders
{
    // 로더들이 문제를 모으는 곳. 필드 읽기 도우미를 같이 둔다(없는 필드·꼴이 틀린 필드를 문제로 적고 기본값을 준다)
    internal sealed class ProblemSink
    {
        private readonly List<AssetLoadProblem> _problems;

        public ProblemSink(List<AssetLoadProblem> problems, string file)
        {
            _problems = problems;
            File = file;
        }

        public string File { get; }

        public int Count => _problems.Count;

        public void Add(ManifestJson at, string message) => _problems.Add(new AssetLoadProblem(File, at.Pointer, message));

        public void Add(string pointer, string message) => _problems.Add(new AssetLoadProblem(File, pointer, message));

        public bool Object(ManifestJson node)
        {
            if (node.Kind == ManifestJsonKind.Object)
            {
                return true;
            }
            Add(node, "expected an object");
            return false;
        }

        public bool TryString(ManifestJson obj, string name, out string value)
        {
            value = "";
            if (!obj.TryGet(name, out ManifestJson v))
            {
                Add(obj, "missing '" + name + "'");
                return false;
            }
            if (!v.TryGetString(out value) || value.Length == 0)
            {
                Add(v, "'" + name + "' must be a non-empty string");
                return false;
            }
            return true;
        }

        public bool TryInt(ManifestJson obj, string name, out int value)
        {
            value = 0;
            if (!obj.TryGet(name, out ManifestJson v))
            {
                Add(obj, "missing '" + name + "'");
                return false;
            }
            if (!v.TryGetInt(out value))
            {
                Add(v, "'" + name + "' must be an integer");
                return false;
            }
            return true;
        }

        public bool TryPositive(ManifestJson obj, string name, out int value)
        {
            if (!TryInt(obj, name, out value))
            {
                return false;
            }
            if (value <= 0)
            {
                Add(obj.Get(name) ?? obj, "'" + name + "' must be positive");
                return false;
            }
            return true;
        }

        public bool TryInts(ManifestJson obj, string name, int length, out int[] values)
        {
            values = new int[length];
            if (!obj.TryGet(name, out ManifestJson v))
            {
                Add(obj, "missing '" + name + "'");
                return false;
            }
            return TryIntsOf(v, name, length, out values);
        }

        public bool TryIntsOf(ManifestJson v, string what, int length, out int[] values)
        {
            if (!v.TryGetIntArray(length, out values))
            {
                Add(v, "'" + what + "' must be an array of " + length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " integers");
                return false;
            }
            return true;
        }

        // 있으면 bool, 없으면 기본값. 꼴이 틀리면 문제
        public bool OptionalBool(ManifestJson obj, string name, bool fallback)
        {
            if (!obj.TryGet(name, out ManifestJson v))
            {
                return fallback;
            }
            if (!v.TryGetBool(out bool b))
            {
                Add(v, "'" + name + "' must be a boolean");
                return fallback;
            }
            return b;
        }
    }
}

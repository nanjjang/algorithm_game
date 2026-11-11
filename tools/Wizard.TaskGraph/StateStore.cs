using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Wizard.TaskGraph
{
    // 진행 상태 파일(.claude/taskstate.json. git 이 추적하지 않는다. DECISIONS-TECH T-40).
    //   { "schema": 1, "status": { "<작업 id>": "in_progress" | "review" | "done" | "todo", … } }
    // 적힌 것만 tasks.json 의 처음 분류(todo | blocked-on-assets)를 덮는다. 열쇠는 id 오름차순(같은 상태면 같은 바이트).
    //
    // 잠금: 상태 파일 자체를 배타로 연다(FileShare.None). .NET 은 유닉스에서 이것을 flock(LOCK_EX)으로 건다.
    // 잠근 채로 읽고, 고치고, 같은 파일 핸들에 제자리로 다시 쓴다. 임시 파일 + 이름 바꾸기를 쓰지 않는 이유:
    // 이름을 바꾸면 기다리던 프로세스가 옛 inode 를 잠그고 옛 내용을 읽어 변경을 잃을 수 있다.
    // 읽기만 하는 명령은 공유 잠금(FileShare.Read → LOCK_SH)으로 읽어 쓰는 도중의 파일을 보지 않는다.
    // 잠금을 기다리는 시간은 LockTimeout 까지다. 넘으면 실패를 알린다(조용히 넘어가지 않는다).
    public sealed class StateStore
    {
        public const int Schema = 1;
        public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(15);

        public static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            "todo", "in_progress", "review", "done", "blocked-on-assets",
        };

        public StateStore(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public SortedDictionary<string, string> Read()
        {
            if (!File.Exists(Path))
            {
                return new SortedDictionary<string, string>(StringComparer.Ordinal);
            }
            DateTime deadline = DateTime.UtcNow + LockTimeout;
            while (true)
            {
                try
                {
                    using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Parse(ReadAll(fs));
                }
                catch (FileNotFoundException)
                {
                    return new SortedDictionary<string, string>(StringComparer.Ordinal);
                }
                catch (DirectoryNotFoundException)
                {
                    return new SortedDictionary<string, string>(StringComparer.Ordinal);
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(RetryDelay);
                }
                catch (IOException e)
                {
                    throw new TaskGraphException($"상태 파일을 읽으려고 {LockTimeout.TotalSeconds}초를 기다렸지만 잠금이 풀리지 않았다: {Path} — {e.Message}");
                }
            }
        }

        // 배타 잠금을 잡고 mutate 를 부른다. mutate 가 상태를 바꾸면(같지 않으면) 다시 쓴다. 예외가 나면 쓰지 않는다.
        public T Update<T>(Func<SortedDictionary<string, string>, T> mutate)
        {
            string? dir = System.IO.Path.GetDirectoryName(Path);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            using FileStream fs = OpenExclusive();
            SortedDictionary<string, string> state = Parse(ReadAll(fs));
            string before = Serialize(state);
            T result = mutate(state);
            string after = Serialize(state);
            if (after != before || fs.Length == 0)
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(after);
                fs.Position = 0;
                fs.SetLength(0);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            return result;
        }

        // .NET 은 이 환경 변수가 켜져 있으면 FileShare 를 flock 으로 옮기지 않는다. 그러면 잠금 없이 쓰게 되므로 거절한다.
        public static void EnsureLockingEnabled()
        {
            string? v = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_IO_DISABLEFILELOCKING");
            if (v != null && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                throw new TaskGraphException("DOTNET_SYSTEM_IO_DISABLEFILELOCKING 이 켜져 있어 파일 잠금이 걸리지 않는다. 이 변수를 지우고 다시 돌린다");
            }
            if (AppContext.TryGetSwitch("System.IO.DisableFileLocking", out bool disabled) && disabled)
            {
                throw new TaskGraphException("System.IO.DisableFileLocking 이 켜져 있어 파일 잠금이 걸리지 않는다");
            }
        }

        private FileStream OpenExclusive()
        {
            EnsureLockingEnabled();
            DateTime deadline = DateTime.UtcNow + LockTimeout;
            while (true)
            {
                try
                {
                    return new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(RetryDelay);
                }
                catch (IOException e)
                {
                    throw new TaskGraphException($"상태 파일의 잠금을 {LockTimeout.TotalSeconds}초 안에 잡지 못했다: {Path} — {e.Message}");
                }
            }
        }

        private static string ReadAll(FileStream fs)
        {
            fs.Position = 0;
            var ms = new MemoryStream();
            fs.CopyTo(ms);
            return new UTF8Encoding(false).GetString(ms.ToArray());
        }

        public SortedDictionary<string, string> Parse(string text)
        {
            var state = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (text.Trim().Length == 0)
            {
                return state;
            }
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(text);
            }
            catch (JsonException e)
            {
                throw new TaskGraphException($"상태 파일이 깨졌다: {Path} — {e.Message}. 지우고 `taskgraph reconcile`로 다시 만든다");
            }
            if (root is not JsonObject obj || obj["status"] is not JsonObject st)
            {
                throw new TaskGraphException($"상태 파일의 꼴이 틀렸다: {Path}. 지우고 `taskgraph reconcile`로 다시 만든다");
            }
            foreach (KeyValuePair<string, JsonNode?> kv in st)
            {
                string? s = kv.Value is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
                if (s == null || !Known.Contains(s))
                {
                    throw new TaskGraphException($"상태 파일에 모르는 상태가 있다: {kv.Key} = {kv.Value?.ToJsonString()} ({Path})");
                }
                state[kv.Key] = s;
            }
            return state;
        }

        public static string Serialize(SortedDictionary<string, string> state)
        {
            var st = new JsonObject();
            foreach (KeyValuePair<string, string> kv in state.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                st[kv.Key] = kv.Value;
            }
            var root = new JsonObject { ["schema"] = Schema, ["status"] = st };
            return JsStyleJson.Serialize(root, 1);
        }
    }
}

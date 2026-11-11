using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Wizard.Ci
{
    // 결과 JSON 쓰기: 들여쓰기 두 칸, 줄바꿈 LF, 한글을 \u 로 바꾸지 않는다, 마지막 줄바꿈 하나.
    public static class JsonFile
    {
        public static readonly JsonWriterOptions Options = new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static void Write(string path, Action<Utf8JsonWriter> body)
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, ToText(body), new UTF8Encoding(false));
        }

        public static string ToText(Action<Utf8JsonWriter> body)
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, Options))
            {
                body(w);
            }
            return Encoding.UTF8.GetString(ms.ToArray()) + "\n";
        }
    }
}

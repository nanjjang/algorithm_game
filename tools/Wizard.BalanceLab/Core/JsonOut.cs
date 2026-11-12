using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Wizard.BalanceLab.Core
{
    // 판정 근거 JSON. 들여쓰기 2칸, 줄바꿈 \n, 한글은 그대로 쓴다. 숫자는 전부 정수다
    internal static class JsonOut
    {
        public static string Build(Action<Utf8JsonWriter> body)
        {
            var stream = new MemoryStream();
            var options = new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            using (var w = new Utf8JsonWriter(stream, options))
            {
                body(w);
            }
            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }
    }
}

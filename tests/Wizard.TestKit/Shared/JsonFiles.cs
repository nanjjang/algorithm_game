using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wizard.TestKit.Shared
{
    // TestKit 이 읽고 쓰는 JSON 의 공통 규칙.
    // - 날짜처럼 생긴 문자열을 DateTime 으로 바꾸지 않는다(DateParseHandling.None).
    // - 실수는 읽은 뒤 각 로더가 거절한다(골든·벡터 설정에는 정수와 문자열만 둔다. 실수는 런타임마다 다르게 읽힐 수 있다).
    // - 쓸 때는 UTF-8(BOM 없음), LF, 들여쓰기 2칸, 문화권 고정. 같은 내용이면 바이트가 같다.
    public static class JsonFiles
    {
        public static JObject ParseObject(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Decimal;
                reader.Culture = CultureInfo.InvariantCulture;
                JObject obj = JObject.Load(reader);
                if (reader.Read())
                {
                    throw new JsonReaderException("unexpected content after the root object");
                }
                return obj;
            }
        }

        public static JObject LoadObject(string path)
        {
            return ParseObject(File.ReadAllText(path, Encoding.UTF8));
        }

        public static JsonTextWriter CreateWriter(StringWriter target)
        {
            target.NewLine = "\n";
            var w = new JsonTextWriter(target)
            {
                Formatting = Formatting.Indented,
                Indentation = 2,
                IndentChar = ' ',
                Culture = CultureInfo.InvariantCulture,
            };
            return w;
        }

        public static StringWriter CreateText()
        {
            return new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        }

        public static void WriteUtf8(string path, string content)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }
}

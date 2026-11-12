using System;
using System.IO;
using System.Text;
using Wizard.TestKit;

namespace Wizard.Content.Tests.Pack
{
    // 임시 저장소(Wizard.sln 표지 + data/). 저장소의 _common.schema.json 을 복사하고 빈 이름 표로 시작한다.
    internal sealed class TempDataRepo : IDisposable
    {
        public TempDataRepo()
        {
            Root = Path.Combine(Path.GetTempPath(), "wizard-content-pack-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "Wizard.sln"), "");
            Write("data/schema/_common.schema.json", File.ReadAllText(TestRepo.Path("data/schema/_common.schema.json")));
            Write("data/names/names.ko.json", "{\n  \"~end\": \"\"\n}\n");
        }

        public string Root { get; }

        public void Write(string relative, string text)
        {
            string path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        }

        public void Delete(string relative) => File.Delete(Path.Combine(Root, relative));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }

        public static string Schema(string name, string body) =>
            "{\n  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\n  \"$id\": \"https://wizard.invalid/data/schema/" + name + "\",\n" + body + "\n}\n";
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wizard.Content;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Names
{
    // G5 "모든 id에 이름 열쇠"(ARCHITECTURE 8.2 3단계, 8.3, PIPELINE 7.1). M1 부터 돈다.
    // data/sim·data/rules 의 파일에서 데이터가 정의하는 항목의 id 는 객체의 "id" 속성(문자열)에 둔다(이 검사가 정한 꼴).
    // 그 값은 spec 14 1.1의 ContentId 문법을 따르고(최대 64자), data/names/names.ko.json 에 같은 열쇠가 있어야 한다.
    // 다른 항목을 가리키는 참조(예: "core": "block.magic_bolt")는 이름 열쇠를 보지 않는다(정의한 쪽에서 본다. 참조 무결성은 따로 검사한다).
    // 없는 열쇠는 화면에 열쇠 문자열 그대로 찍히므로 데이터 작업은 자기 id 의 열쇠를 같이 더한다.
    public sealed class NameKeyCoverage : IDataValidator
    {
        public const string IdProperty = "id";
        public const int MaxIdLength = 64;

        private static readonly Regex ContentIdGrammar = new Regex("^[a-z][a-z0-9_]*(\\.[a-z0-9][a-z0-9_]*)+$", RegexOptions.CultureInvariant);

        public string Name => "name-keys";

        public void Validate(DataSet data, Diagnostics diagnostics)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.Section == ContentSection.None)
                {
                    continue;
                }
                Walk(f.Root, "", f, data.Names, diagnostics);
            }
        }

        private static void Walk(JsonElement e, string pointer, DataFile f, NamesFile? names, Diagnostics diagnostics)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty p in e.EnumerateObject())
                    {
                        string child = pointer + "/" + JsonPointer.Escape(p.Name);
                        if (p.Name == IdProperty && p.Value.ValueKind == JsonValueKind.String)
                        {
                            CheckId(p.Value.GetString() ?? "", child, f, names, diagnostics);
                        }
                        Walk(p.Value, child, f, names, diagnostics);
                    }
                    break;
                case JsonValueKind.Array:
                    int i = 0;
                    foreach (JsonElement item in e.EnumerateArray())
                    {
                        Walk(item, pointer + "/" + i.ToString(CultureInfo.InvariantCulture), f, names, diagnostics);
                        i++;
                    }
                    break;
            }
        }

        private static void CheckId(string id, string pointer, DataFile f, NamesFile? names, Diagnostics diagnostics)
        {
            if (id.Length > MaxIdLength || !ContentIdGrammar.IsMatch(id))
            {
                diagnostics.Error(f.RepoPath, pointer, "id '" + id + "' 가 spec 14 1.1의 문법(^[a-z][a-z0-9_]*(\\.[a-z0-9][a-z0-9_]*)+$, 64자 이하)에 맞지 않는다");
                return;
            }
            if (names == null)
            {
                return;   // 이름 표를 읽지 못한 사실은 이미 진단에 있다
            }
            if (!names.Contains(id))
            {
                diagnostics.Error(f.RepoPath, pointer, "id '" + id + "' 의 이름 열쇠가 " + NamesFile.RepoPath + " 에 없다(자기 id 의 열쇠를 같이 더한다)");
            }
        }
    }
}

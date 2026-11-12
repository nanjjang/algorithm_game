using System.Text.Json;
using Wizard.Content;

namespace Wizard.DataBuild.Core
{
    // data/ 의 JSON 파일 하나(스키마 검사 전후 같은 객체). 수는 JsonElement.GetDecimal 로 읽는다(실수형으로 읽지 않는다).
    public sealed class DataFile
    {
        public DataFile(string dataPath, ContentSection section, string schemaFile, JsonDocument document)
        {
            DataPath = dataPath;
            Section = section;
            SchemaFile = schemaFile;
            Document = document;
        }

        // data/ 기준 상대 경로('/' 구분). 예: "sim/characters.json", "presentation/event_bindings.json"
        public string DataPath { get; }

        // 저장소 기준 상대 경로. 진단에 쓴다
        public string RepoPath => DataLoader.DataDir + "/" + DataPath;

        // 팩 절(data/sim, data/rules). 팩에 들어가지 않는 파일(data/presentation)은 None
        public ContentSection Section { get; }

        // data/schema/ 안의 스키마 파일 이름. 예: "characters.schema.json"
        public string SchemaFile { get; }

        public JsonDocument Document { get; }

        public JsonElement Root => Document.RootElement;
    }
}

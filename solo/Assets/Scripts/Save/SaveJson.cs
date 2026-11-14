using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Wizard.Solo.Save
{
    /// <summary>
    /// 저장 파일의 JSON 설정(07 요약서 9.3절): snake_case 필드, 사전 열쇠는 그대로(정본 id "mat.stone_low", 옵션 열쇠),
    /// 날짜 꼴 글자를 DateTime으로 바꾸지 않는다(모르는 필드·JObject를 바이트 그대로 되살리려고).
    /// </summary>
    public static class SaveJson
    {
        public static readonly JsonSerializerSettings Settings = Create();

        static JsonSerializerSettings Create()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = false,
                        ProcessExtensionDataNames = false,
                        OverrideSpecifiedNames = true
                    }
                },
                // 필드 초기값(new List …)에 덧붙지 않고 파일 값으로 바꾼다.
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Include,
                DateParseHandling = DateParseHandling.None,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Formatting = Formatting.Indented
            };
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}

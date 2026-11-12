namespace Wizard.DataBuild.Names
{
    // 이름 표의 항목 하나. RawValue 는 파일에 적힌 따옴표 안의 글자 그대로(JSON 이스케이프 포함)이고 정규 꼴을 쓸 때 그대로 옮긴다.
    public sealed class NameEntry
    {
        public NameEntry(string key, string value, string rawValue)
        {
            Key = key;
            Value = value;
            RawValue = rawValue;
        }

        public string Key { get; }

        public string Value { get; }

        public string RawValue { get; }
    }
}

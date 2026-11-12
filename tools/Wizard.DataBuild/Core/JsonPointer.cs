namespace Wizard.DataBuild.Core
{
    // RFC 6901 의 마디 꼴(진단의 JSON 위치를 "/blocks/0/id" 처럼 적는다)
    public static class JsonPointer
    {
        public static string Escape(string segment) => segment.Replace("~", "~0").Replace("/", "~1");
    }
}

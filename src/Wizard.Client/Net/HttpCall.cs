namespace Wizard.Client.Net
{
    // CONTRACTS 6.2. HTTP 요청 하나. 머리말(Authorization, Idempotency-Key)은 M10-CLI-01 이 더한다
    public sealed class HttpCall
    {
        public string Method = "GET";
        public string Path = "";
        public string? Body;
    }
}

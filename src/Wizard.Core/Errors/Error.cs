namespace Wizard.Core
{
    // CONTRACTS 0.2. 화면 문구는 Code 로 이름 표에서 찾는다("error.<code>"). Detail 은 개발용이다.
    public readonly struct Error
    {
        public readonly ErrorCode Code;
        public readonly string? Detail;
        public readonly int RetryAfterMs;   // 0이면 없음

        public Error(ErrorCode code, string? detail = null, int retryAfterMs = 0)
        {
            Code = code;
            Detail = detail;
            RetryAfterMs = retryAfterMs;
        }
    }
}

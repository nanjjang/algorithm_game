namespace Wizard.Core
{
    // CONTRACTS 0.2. 클라이언트의 모든 비동기 호출은 Task<Result<T>> 다. 통신 실패도 오류 값으로 온다.
    public readonly struct Result<T>
    {
        private readonly T _value;
        private readonly Error _error;
        private readonly bool _ok;

        private Result(bool ok, T value, Error error)
        {
            _ok = ok;
            _value = value;
            _error = error;
        }

        public bool Ok => _ok;
        public T Value => _value;           // Ok일 때만 뜻이 있다
        public Error Error => _error;

        public static Result<T> Success(T value) => new Result<T>(true, value, default);
        public static Result<T> Fail(Error error) => new Result<T>(false, default!, error);
        public static Result<T> Fail(ErrorCode code) => new Result<T>(false, default!, new Error(code));
    }
}

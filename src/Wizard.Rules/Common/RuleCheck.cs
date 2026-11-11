using Wizard.Core;

namespace Wizard.Rules
{
    // CONTRACTS 4.1, 0.2. 규칙 함수의 거절은 예외가 아니라 이 값이다
    public readonly struct RuleCheck
    {
        public readonly bool Ok;
        public readonly ErrorCode Code;

        private RuleCheck(bool ok, ErrorCode code)
        {
            Ok = ok;
            Code = code;
        }

        public static RuleCheck Pass => new RuleCheck(true, ErrorCode.None);

        public static RuleCheck Fail(ErrorCode code) => new RuleCheck(false, code);
    }
}

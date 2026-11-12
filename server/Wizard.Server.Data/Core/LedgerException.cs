using System;
using Wizard.Core;

namespace Wizard.Server.Data
{
    // 원장이 코드에서 거절한 쓰기(DB CHECK 가 잡지 못하는 것). 지금은 별빛 차감(spec 12 10.1절)만 쓴다.
    // 잔액 부족 같은 규칙상 거절은 유스케이스가 Wizard.Rules 로 먼저 걸러 결과 값으로 돌려준다. 여기까지 오면 그 검사가 빠진 것이다.
    // DB CHECK 위반(qty ≥ 0, amount ≥ 0)은 PostgresException(23514)으로 그대로 올라가 트랜잭션이 취소된다(T-IDEM-10·11).
    public sealed class LedgerException : Exception
    {
        public LedgerException(ErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        public ErrorCode Code { get; }
    }
}

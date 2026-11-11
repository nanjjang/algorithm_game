using System;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. 원장 한 줄의 원인
    public readonly struct LedgerRef
    {
        public readonly LedgerReason Reason;
        public readonly string RefType;                   // "run", "trade" …
        public readonly string RefId;
        public readonly Guid? RequestId;
        
        public LedgerRef(LedgerReason reason, string refType, string refId, Guid? requestId)
        {
        Reason = reason;
        RefType = refType;
        RefId = refId;
        RequestId = requestId;
        }
    }
}

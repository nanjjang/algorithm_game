using System;

namespace Wizard.Server.Data
{
    // CONTRACTS 7.2. 서버 시각. 클라이언트 시계는 믿지 않는다
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}

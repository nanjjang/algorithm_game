using System;

namespace Wizard.Server.Data
{
    // 운영 시계. 서버 시각만 쓴다(spec 12 7절: 클라이언트 시계는 믿지 않는다). 테스트는 IClock 을 바꿔 끼운다.
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}

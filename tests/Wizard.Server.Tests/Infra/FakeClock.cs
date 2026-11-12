using System;
using Wizard.Server.Data;

namespace Wizard.Server.Tests.Infra
{
    // 테스트가 움직이는 서버 시계
    public sealed class FakeClock : IClock
    {
        private long _ticks;

        public FakeClock(DateTime startUtc)
        {
            _ticks = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc).Ticks;
        }

        public DateTime UtcNow => new DateTime(System.Threading.Interlocked.Read(ref _ticks), DateTimeKind.Utc);

        public void Set(DateTime utc) => System.Threading.Interlocked.Exchange(ref _ticks, DateTime.SpecifyKind(utc, DateTimeKind.Utc).Ticks);

        public void Advance(TimeSpan by) => System.Threading.Interlocked.Add(ref _ticks, by.Ticks);
    }
}

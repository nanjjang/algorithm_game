using System;
using System.Runtime;

namespace Wizard.Perf
{
    // 할당을 재는 구간에서 GC 가 돌지 않게 한다(GC.TryStartNoGCRegion).
    // GC.GetAllocatedBytesForCurrentThread 는 GC 가 이 스레드의 할당 문맥(약 8KB 덩어리)을 거둘 때 쓰지 않은 나머지를 할당한 것처럼
    // 더하는 일이 있다. 같은 프로세스의 다른 스레드(병렬로 도는 시험)가 GC 를 일으키면 Step 이 아무것도 할당하지 않아도 수천 바이트가
    // 잡힌다(Wizard.Sim.Tests 의 할당 0 시험이 가끔 6,232·7,832바이트로 실패한 것이 이것이다). 구간 안에서는 GC 가 없으므로
    // 잰 값은 그 구간에서 이 스레드가 실제로 할당한 바이트다. 구간이 시작되지 않았거나 중간에 GC 가 돌았으면 Valid 가 false 다.
    internal sealed class NoGcWindow : IDisposable
    {
        // 구간에서 허용하는 할당. Step 은 0이어야 하므로 이만큼 할당하기 전에 결함이 드러난다(결함 주입 시험의 할당도 이 안이다)
        private const long BudgetBytes = 64L * 1024 * 1024;

        private readonly bool _started;
        private readonly int _gen0;

        private NoGcWindow()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            try
            {
                _started = GC.TryStartNoGCRegion(BudgetBytes);
            }
            catch (InvalidOperationException)
            {
                _started = false;
            }
            _gen0 = GC.CollectionCount(0);
        }

        public static NoGcWindow Open() => new NoGcWindow();

        // 구간이 처음부터 지금까지 GC 없이 이어졌는가
        public bool Valid => _started && GC.CollectionCount(0) == _gen0 && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;

        public void Dispose()
        {
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }
        }
    }
}

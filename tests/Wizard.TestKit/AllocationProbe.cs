using System;

namespace Wizard.TestKit
{
    // 할당 0 시험의 공통 측정(PIPELINE 7.5절 "Step 의 관리 힙 할당 0바이트", CONTRACTS 3.2절).
    //
    // 같은 각본을 새 대상으로 여러 번 돌려 재는 구간에서 이 스레드가 할당한 바이트의 최솟값을 돌려준다.
    // - 각본이 어느 경로에서든 할당하면(대상마다 한 번 만드는 버퍼, 틱마다의 박싱 …) 모든 실행에서 0보다 크므로 최솟값도 0이 아니다.
    //   그래서 "새 대상"이어야 한다. 같은 대상을 다시 재면 대상이 첫 실행에서 한 번 만든 것이 두 번째에는 보이지 않는다.
    // - 한 번 재기는 xUnit 이 시험을 병렬로 돌릴 때 가끔 각본과 무관한 바이트(수백 ~ 8천 바이트 미만)를 더 센다.
    //   FIX-1에서 확인한 원인: 다른 시험이 일으킨 백그라운드(동시) gen2 GC 가 도는 동안, 이 스레드의 할당 문맥이
    //   이 스레드가 아무것도 할당하지 않았는데 다른 자리로 바뀌고(재는 구간 앞뒤의 객체 주소가 이어지지 않는다)
    //   GC.GetAllocatedBytesForCurrentThread 가 그만큼(할당 단위 8KB 안쪽)을 더 센다. gen0/1/2 횟수는 대개 그대로다.
    //   할당이 없는 정수 연산 구간에서도 똑같이 나오고, DOTNET_gcConcurrent=0 이면 나오지 않는다. 그러니 각본의 경로가 하는 할당이 아니다.
    //   백그라운드 GC 는 드물어 같은 각본을 다시 돌리면 대개 사라진다. 최솟값은 그것만 걸러 낸다.
    // - 프로세스에 한 번만 일어나는 할당(처음 닿은 형식의 정적 생성자)은 첫 실행에만 있으므로 걸러진다.
    //   그런 것은 분석기가 막는 가변 정적 필드가 아니면 틱마다의 할당이 아니다.
    public static class AllocationProbe
    {
        // 한 번 재기가 흔들리는 일은 묶음 한 번에 백그라운드 GC 몇 번의 순간뿐이다(FIX-1의 되풀이 실행). 연달아 다섯 번이 다 겹칠 일은 없는 것으로 본다.
        public const int DefaultRuns = 5;

        // arrange: 새 대상을 만들고 데운 뒤(JIT, 첫 Step 의 캐시) 재는 구간의 동작을 돌려준다. arrange 자체는 재지 않는다.
        // 돌려받은 동작은 재는 구간에서 한 번 불린다. 대리자 호출은 할당하지 않는다.
        // verify: 실행마다 재는 구간이 끝난 뒤 불린다(재지 않는다). 각본이 정말 그 경로를 지났는지 실행마다 확인하는 자리다.
        public static long LeastBytes(Func<Action> arrange, Action? verify = null) => LeastBytes(DefaultRuns, arrange, verify);

        public static long LeastBytes(int runs, Func<Action> arrange, Action? verify = null)
        {
            if (runs < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(runs), runs, "need at least two runs to take a minimum");
            }
            if (arrange == null)
            {
                throw new ArgumentNullException(nameof(arrange));
            }
            long least = long.MaxValue;
            for (int run = 0; run < runs; run++)
            {
                Action act = arrange() ?? throw new InvalidOperationException("arrange returned no action");
                long before = GC.GetAllocatedBytesForCurrentThread();
                act();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                verify?.Invoke();
                if (bytes < least)
                {
                    least = bytes;
                }
            }
            return least;
        }
    }
}

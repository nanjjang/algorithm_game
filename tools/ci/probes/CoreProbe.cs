// 결정론 가드 탐침(tools/ci/determinism-guard.sh). 일부러 금지 목록을 어긴다. 빌드가 이 진단들로 실패해야 한다.
// 이 파일은 어느 프로젝트에도 들어 있지 않다. 탐침 빌드에서만 -p:WizardAnalyzerProbe 로 끼워 넣는다.
namespace Wizard.Probe
{
    internal static class CoreProbe
    {
        private static int s_counter;                                              // WZ0010

        internal static double Violate(int a, int b, System.Collections.Generic.Dictionary<int, int> d, int[] arr)
        {
            var random = new System.Random(1);                                     // WZ0002
            long now = System.DateTime.UtcNow.Ticks;                               // WZ0003
            System.Guid id = System.Guid.NewGuid();                                // WZ0004
            int sum = 0;
            foreach (var kv in d)                                                  // WZ0005
            {
                sum += kv.Key;
            }
            System.Array.Sort(arr);                                                // WZ0006
            string text = a.ToString();                                            // WZ0007
            System.Threading.Tasks.Task.Run(() => { });                            // WZ0008
            int hash = text.GetHashCode();                                         // WZ0009
            byte[] raw = System.BitConverter.GetBytes(a);                          // WZ0013
            int q = a / b;                                                         // WZ0014
            int c = checked(a + b);                                                // WZ0015
            s_counter++;
            return 1.5 + random.Next() + now + id.GetHashCode() + sum + hash + raw.Length + q + c;   // WZ0001
        }
    }
}

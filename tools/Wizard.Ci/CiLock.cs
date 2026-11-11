using System;
using System.IO;
using System.Threading;

namespace Wizard.Ci
{
    // 한 체크아웃(작업 트리)에서 ci.sh 가 동시에 둘 돌지 않게 한다(bash 3.2 에는 flock 이 없다. T-40).
    // artifacts/ci/.lock 을 배타로 연다(FileShare.None → .NET 이 유닉스에서 flock(LOCK_EX)으로 건다). 프로세스가 죽으면 커널이 푼다.
    // 다른 실행이 잡고 있으면 Wait 까지 기다리고, 넘으면 무엇이 막는지 알리고 실패한다(조용히 넘어가지 않는다).
    public sealed class CiLock : IDisposable
    {
        private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(200);
        private readonly FileStream _stream;

        private CiLock(FileStream stream)
        {
            _stream = stream;
        }

        public static string PathFor(string root) => Path.Combine(root, "artifacts", "ci", ".lock");

        public static CiLock Acquire(string root, TimeSpan wait, TextWriter log)
        {
            string? v = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_IO_DISABLEFILELOCKING");
            if (v != null && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                throw new CiException("DOTNET_SYSTEM_IO_DISABLEFILELOCKING 이 켜져 있어 ci 잠금이 걸리지 않는다. 이 변수를 지우고 다시 돌린다");
            }
            string path = PathFor(root);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            DateTime deadline = DateTime.UtcNow + wait;
            bool told = false;
            while (true)
            {
                try
                {
                    return new CiLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
                catch (IOException) when (DateTime.UtcNow < deadline)
                {
                    if (!told)
                    {
                        log.WriteLine("ci: 이 작업 트리에서 다른 ci.sh 가 돌고 있다(" + path + "). 끝나기를 기다린다");
                        told = true;
                    }
                    Thread.Sleep(Retry);
                }
                catch (IOException e)
                {
                    throw new CiException("다른 ci.sh 가 " + ((int)wait.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                          "초 넘게 잠금을 쥐고 있다(" + path + "): " + e.Message + ". 그 실행이 끝난 뒤 다시 돌린다");
                }
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}

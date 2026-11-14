using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wizard.Ci
{
    // Windows 에서 시간 제한에 걸린 프로세스 나무를 끊는 길(T-40, T-67 1번).
    // Git for Windows 의 bash 가 띄운 손자(MSYS 프로세스)는 fork·exec 흉내 때문에 Windows 의 부모 번호가 이미 끝난 프로세스를 가리키는 일이 있어서
    // Process.Kill(entireProcessTree) 가 찾지 못한다(확인: `bash -c '/bin/sleep 600 & wait'` 의 sleep 이 남았다).
    // 그래서 자식을 띄운 직후 작업 개체(job object)에 넣는다. 그 뒤에 생긴 자손은 모두 같은 작업 개체에 들고, 시간 제한에서 작업 개체를 통째로 끝낸다.
    // 작업 개체를 닫아도 안의 프로세스를 끝내지 않는다(KILL_ON_JOB_CLOSE 를 걸지 않는다: 검사가 띄운 빌드 서버는 남아도 된다).
    // 자식을 띄운 순간부터 작업 개체에 넣기까지의 아주 짧은 사이에 생긴 손자는 빠질 수 있다. 그래서 Kill(entireProcessTree) 도 같이 한다.
    internal sealed class WindowsJob : IDisposable
    {
        private IntPtr _handle;

        private WindowsJob(IntPtr handle)
        {
            _handle = handle;
        }

        // Windows 가 아니거나 만들거나 넣지 못하면 null(그때는 Kill(entireProcessTree) 만 쓴다)
        public static WindowsJob? TryAttach(Process p)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }
            IntPtr h = CreateJobObjectW(IntPtr.Zero, null);
            if (h == IntPtr.Zero)
            {
                return null;
            }
            if (!AssignProcessToJobObject(h, p.Handle))
            {
                CloseHandle(h);
                return null;
            }
            return new WindowsJob(h);
        }

        public void Terminate(int exitCode)
        {
            if (_handle != IntPtr.Zero)
            {
                TerminateJobObject(_handle, (uint)exitCode);
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

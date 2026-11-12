using System.Runtime.InteropServices;

namespace Wizard.Perf
{
    // macOS 에서 재는 스레드의 서비스 품질을 "사용자 상호작용"으로 올린다. 애플 실리콘은 성능 코어와 효율 코어가 섞여 있고
    // 기본 품질의 스레드는 바쁠 때 효율 코어로 옮겨져 같은 빌드가 두 배 가까이 느리게 잰다. 기기 설정은 바꾸지 않는다(이 스레드만).
    // 다른 운영체제에서는 아무것도 하지 않는다. 결과는 machine.thread_qos 칸에 남는다
    internal static class ThreadQos
    {
        private const uint QosClassUserInteractive = 0x21;     // <sys/qos.h> QOS_CLASS_USER_INTERACTIVE

        [DllImport("libSystem.dylib", EntryPoint = "pthread_set_qos_class_self_np")]
        private static extern int SetSelf(uint qosClass, int relativePriority);

        public static string Raise()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "default";
            }
            return SetSelf(QosClassUserInteractive, 0) == 0 ? "user-interactive" : "default";
        }
    }
}

// 결정론 가드 탐침: Wizard.Netcode 에는 시계(WZ0003)와 소켓(WZ0019)이 없다.
namespace Wizard.Probe
{
    internal static class NetcodeProbe
    {
        internal static object Violate() => (System.Diagnostics.Stopwatch.GetTimestamp(), typeof(System.Net.Sockets.Socket));
    }
}

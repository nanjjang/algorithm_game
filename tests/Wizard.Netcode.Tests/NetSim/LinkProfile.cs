using System.Collections.Generic;

namespace Wizard.Netcode.Tests.NetSim
{
    // 한 방향 링크(클라이언트 → 호스트, 또는 호스트 → 클라이언트)의 조건.
    //
    // 지연: 패킷마다 편도 = BaseUs + U[0, JitterUs] (균등, 독립). "왕복 R, 지터 J"는 두 방향이 각각 R/2 + U[0, J] 라는 뜻이다.
    //   D 는 지터가 없는 왕복(R)으로 고른다(경기 전 16회 중 가장 짧은 값, spec 10 14.4). 지터는 그 위에 얹힌다.
    // 순서 바뀜: 지터가 틱 길이(16.7ms)를 넘으면 저절로 생긴다. ReorderPpm 은 그 위에 U[0, ReorderExtraUs]를 더 얹는 패킷의 비율이다.
    // 중복: DuplicatePpm 의 비율로 같은 바이트가 U[0, DuplicateExtraUs] 늦게 한 번 더 닿는다.
    // 유실은 셋이 겹친다.
    //   독립 유실: LossPpm.
    //   연속 유실(두 상태 모형, Gilbert-Elliott): 패킷마다 좋음 → 나쁨 BurstEnterPpm, 나쁨 → 좋음 BurstExitPpm 으로 상태가 바뀌고
    //     나쁨 상태에서는 BurstLossPpm 의 비율로 잃는다. 평균 연속 길이 = 1 / BurstExit.
    //   정해진 구간: SendLoss(이 구간에 보낸 패킷은 전부 잃는다 = 정해진 연속 유실), ReceiveGaps(이 구간에 닿을 패킷은 전부 잃는다 = 수신 공백).
    internal sealed class LinkProfile
    {
        public long BaseUs;
        public long JitterUs;
        public int LossPpm;
        public int BurstEnterPpm;
        public int BurstExitPpm = 1000000;
        public int BurstLossPpm = 1000000;
        public int ReorderPpm;
        public long ReorderExtraUs;
        public int DuplicatePpm;
        public long DuplicateExtraUs;
        public readonly List<(long fromUs, long toUs)> SendLoss = new List<(long, long)>();
        public readonly List<(long fromUs, long toUs)> ReceiveGaps = new List<(long, long)>();

        public static LinkProfile OneWay(long baseUs, long jitterUs = 0, int lossPpm = 0) =>
            new LinkProfile { BaseUs = baseUs, JitterUs = jitterUs, LossPpm = lossPpm };

        public static bool InAny(List<(long fromUs, long toUs)> windows, long us)
        {
            foreach ((long from, long to) in windows)
            {
                if (us >= from && us < to)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

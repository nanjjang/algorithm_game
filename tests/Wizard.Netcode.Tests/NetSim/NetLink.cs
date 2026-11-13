using System.Collections.Generic;

namespace Wizard.Netcode.Tests.NetSim
{
    // 한 방향 링크. Transmit 은 보낸 시각을 받아 닿는 시각(0~2개)을 돌려준다. 패킷마다 난수를 같은 수만큼 쓴다:
    // 같은 씨앗이면 같은 보냄 열에 같은 닿음 열이다
    internal sealed class NetLink
    {
        private readonly LinkProfile _p;
        private readonly NetRng _rng;
        private bool _bad;
        private int _run;

        public long Sent;
        public long Lost;
        public long Duplicated;
        public long Reordered;
        public int LongestLossRun;                       // 보낸 순서로 이어 잃은 가장 긴 길이
        public readonly List<int> LossRuns = new List<int>();

        public NetLink(LinkProfile profile, ulong seed)
        {
            _p = profile;
            _rng = new NetRng(seed);
        }

        public LinkProfile Profile => _p;

        public int Transmit(long sendUs, long[] arrivals)
        {
            Sent++;
            bool toBad = _rng.Ppm(_p.BurstEnterPpm);
            bool toGood = _rng.Ppm(_p.BurstExitPpm);
            bool burstLoss = _rng.Ppm(_p.BurstLossPpm);
            bool independentLoss = _rng.Ppm(_p.LossPpm);
            long jitter = _rng.UpTo(_p.JitterUs);
            bool reorder = _rng.Ppm(_p.ReorderPpm);
            long reorderExtra = _rng.UpTo(_p.ReorderExtraUs);
            bool duplicate = _rng.Ppm(_p.DuplicatePpm);
            long duplicateExtra = _rng.UpTo(_p.DuplicateExtraUs);

            _bad = _bad ? !toGood : toBad;
            bool lost = (_bad && burstLoss) || independentLoss || LinkProfile.InAny(_p.SendLoss, sendUs);
            long at = sendUs + _p.BaseUs + jitter;
            if (reorder)
            {
                at += reorderExtra;
                Reordered++;
            }
            if (!lost && LinkProfile.InAny(_p.ReceiveGaps, at))
            {
                lost = true;
            }
            if (lost)
            {
                Lost++;
                _run++;
                if (_run > LongestLossRun)
                {
                    LongestLossRun = _run;
                }
                return 0;
            }
            if (_run > 0)
            {
                LossRuns.Add(_run);
                _run = 0;
            }
            int n = 0;
            arrivals[n++] = at;
            long dupAt = at + duplicateExtra;
            if (duplicate && !LinkProfile.InAny(_p.ReceiveGaps, dupAt))
            {
                arrivals[n++] = dupAt;
                Duplicated++;
            }
            return n;
        }
    }
}

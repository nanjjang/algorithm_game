using System.Collections.Generic;

namespace Wizard.BalanceLab.Feel
{
    // FeelAnalyzer 가 낸 세어 놓은 값(판정 전). 분모와 분자는 전부 정수다
    internal sealed class FeelResult
    {
        public uint Ticks;                                    // 기록의 길이(틱)
        public long PracticeTicks;                            // "10분 연습" 이 끝나는 틱
        public int KPresses;                                  // K 누름 전체
        public List<AttemptRecord> Attempts = new List<AttemptRecord>();

        // 섬: 연습 뒤(틱 ≥ PracticeTicks)의 시도만
        public int JustAttempts;
        public int JustSuccesses;

        // 자세 중 오발: 자세 중 이동하다 누른, 두 번 누르는 시도가 아닌 K 가운데 기술이 나간 것
        public int MisfireCasts;                              // 분모
        public int Misfires;                                  // Burst 가 아닌 기술이 나간 수
        public int MisfireRecoil;
        public int MisfireSpear;
        public int MisfireOther;
        public int IgnoredOrUnresolvedMoving;                // 이동 중 자세 K 인데 기술이 나가지 않은 것(분모에서 뺌)

        public int TwoPressAttempts => Attempts.Count;

        public int TwoPressSuccesses
        {
            get
            {
                int n = 0;
                foreach (AttemptRecord a in Attempts)
                {
                    if (a.Success)
                    {
                        n++;
                    }
                }
                return n;
            }
        }
    }
}

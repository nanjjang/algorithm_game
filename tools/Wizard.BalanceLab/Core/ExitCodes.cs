namespace Wizard.BalanceLab.Core
{
    // balancelab 명령의 종료 코드. Program 이 쓰는 2(미구현)와 64(사용법)와 겹치지 않는다.
    //   0 모든 축 통과, 1 하나라도 목표 미달, 3 판정할 자료가 모자람(시도 없음 등), 4 입력(기록·저장소)을 읽을 수 없음
    internal static class ExitCodes
    {
        public const int Pass = 0;
        public const int Fail = 1;
        public const int Inconclusive = 3;
        public const int InputError = 4;
    }
}

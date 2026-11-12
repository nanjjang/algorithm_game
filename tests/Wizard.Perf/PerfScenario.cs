namespace Wizard.Perf
{
    // 재는 세계의 종류. 번호는 쓰지 않는다(결과 파일에는 Id 의 글자로 남는다, ScenarioInfo)
    internal enum PerfScenario
    {
        // 연습 세계 + 시제품 내용(M1-SIM-09: 3갈래 마력탄·화염구, 슬라임 무한 생성, 3갈래 탄 포탑, 기술 ①②⑥).
        // M1 에서 탄과 적이 가장 많은 세계다. 무한 웨이브의 최악 사례(PIPELINE 7.5)는 웨이브·적 모듈(M3) 뒤에 이 자리를 넘겨받는다
        PracticeProto,

        // 결투 세계 두 사람. M1 의 결투 세계에는 이동·자세·열·스침만 있고 탄이 없다(주문 실행기 M2, 결투 모듈 M14)
        Duel2p,

        // 무한 웨이브 세계 한 사람(재실행 속도용. ReplayRunner.Run 은 무한 웨이브 기록을 돌린다)
        Endless1p,
    }
}

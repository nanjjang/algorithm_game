namespace Wizard.ReplayCli
{
    // 재실행할 세계의 종류. 골든 항목의 "kind" 와 글자가 같다(tests/golden/README.md).
    // .wzrc 는 무한 웨이브와 연습이 같이 쓰므로(머리말이 같은 꼴) 어느 쪽인지는 부르는 쪽이 정한다. .wzdr 은 결투뿐이다
    public enum WorldKind : byte
    {
        Endless = 1,
        Practice = 2,
        Duel = 3,
    }
}

namespace Wizard.ReplayCli
{
    // 절의 틀(CONTRACTS 3.2): 스칼라 절은 언제나 (id, 내용)을 쓰고, 풀 절은 개수가 0이면 id 까지 생략한다
    public enum SectionShape : byte
    {
        Scalar = 1,
        Pool = 2,
    }
}

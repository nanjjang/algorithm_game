namespace Wizard.VectorCoverage
{
    // 소스에서 찾은 벡터 테스트 메서드 하나. 이름 규칙은 CLAUDE.md 8절: S<명세 번호>_<벡터 id>__<설명>
    public sealed class VectorTest
    {
        public VectorTest(string spec, string id, string method, string file, int line)
        {
            Spec = spec;
            Id = id;
            Method = method;
            File = file;
            Line = line;
        }

        public string Spec { get; }

        // '_' 를 '-' 로 되돌린 명세의 id. 예: "T-H04"
        public string Id { get; }

        // 형식 이름을 붙인 메서드 이름. 예: "Wizard.Sim.Tests.HeatTests.S01_T_H04__LockAtExactly100"
        public string Method { get; }

        // 저장소 기준 경로('/' 구분)
        public string File { get; }

        public int Line { get; }

        public string Key => "S" + Spec + ":" + Id;

        public override string ToString() => Method + " (" + File + ":" + Line + ")";
    }
}

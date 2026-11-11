namespace Wizard.TestKit.Shared.Vectors
{
    // 명세의 테스트 벡터 하나.
    public sealed class SpecVector
    {
        public SpecVector(string spec, string section, string id, bool provisional, string file, int line, bool inVectorChapter = true)
        {
            Spec = spec;
            Section = section;
            Id = id;
            Provisional = provisional;
            File = file;
            Line = line;
            InVectorChapter = inVectorChapter;
        }

        // 명세 번호 두 자리. 예: "01"
        public string Spec { get; }

        // 벡터가 든 "### N.M" 의 번호. 예: "14.5". 벡터 장 밖에서 ### 소절 없이 "## N." 장 바로 아래에 정의된 벡터는 장 번호 "N"
        // (spec 11 16절의 TV-11-165·166 → "16")
        public string Section { get; }

        // 예: "T-H04", "TV-11-017", "N71", "T023"
        public string Id { get; }

        // 명세가 별표(*)로 [임시] 규칙에 기댄다고 표시한 벡터
        public bool Provisional { get; }

        // 명세 파일 이름. 예: "01-sim-core.md"
        public string File { get; }

        // 1부터 센 줄 번호
        public int Line { get; }

        // "## N. 테스트 벡터" 장 안에 정의되었는가. false 면 다른 장의 굵은 항목·표 행이 이 명세의 id 를 정의한 것이다
        // (spec 11 16절·17.5절의 TV-11-162~166)
        public bool InVectorChapter { get; }

        // tasks.json 의 선택자와 같은 꼴. 예: "S01:T-H04"
        public string Key => VectorNames.Key(Spec, Id);

        public override string ToString() => Key + " (§" + Section + ", " + File + ":" + Line + ")";
    }
}

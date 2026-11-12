namespace Wizard.DataBuild.Core
{
    // 교차 검사 하나(ARCHITECTURE 8.2 3단계: 참조 무결성, 상성 분배, 회피 검산, 이름 열쇠 …). tools/Wizard.DataBuild/Validate/ 등에 두고
    // 등록하지 않는다(ModuleRegistry 가 찾는다). 공개 매개변수 없는 생성자가 있어야 한다. 이름 순서로 돈다.
    // 스키마 검사를 통과하지 못한 파일이 있으면 교차 검사는 돌지 않는다(스키마가 보장한 꼴에 기대도 된다).
    public interface IDataValidator
    {
        // 진단과 정렬에 쓰는 이름(영문 kebab-case. 예: "name-keys")
        string Name { get; }

        void Validate(DataSet data, Diagnostics diagnostics);
    }
}

using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 검산 대상을 데이터에서 모으는 쪽. 적(EnemiesEvasionSource)처럼 데이터 파일 하나에 하나를 둔다.
    // EvasionValidator 가 이 어셈블리에서 찾는다(등록하지 않는다. 공개 매개변수 없는 생성자, 형식 이름 순서로 돈다).
    // 보스·변덕의 공격도 같은 검산을 받는다(spec 05 10.3 "같은 검증기, 같은 표") → 그 데이터를 만드는 작업이 원본을 하나 더한다.
    public interface IEvasionSource
    {
        void Collect(DataSet data, EvasionInput input, Diagnostics diagnostics);
    }
}

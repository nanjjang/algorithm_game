using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 상성 표에 거는 규칙 하나. AffinityValidator 가 이 어셈블리에서 찾아 형식 이름 순서로 돈다(공개 매개변수 없는 생성자).
    // 이 작업이 둔 규칙: 속성별 개수 차이 ≤ 1(AffinityBalanceRule), 약점 ≠ 저항(AffinityDistinctRule).
    // 보스의 저항 = 자기 속성, 웨이브 1~4 에 나오는 종의 저항 ≠ 비전(spec 14 TV-035·TV-036)은 보스 데이터와 함께 BossRules/ 에 더한다(M8-DAT-02).
    public interface IAffinityRule
    {
        void Check(AffinityTable table, Diagnostics diagnostics);
    }
}

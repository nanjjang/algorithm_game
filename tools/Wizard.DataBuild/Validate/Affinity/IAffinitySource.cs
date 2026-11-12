using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 약점·저항을 가진 줄을 데이터에서 모으는 쪽(일반 적: EnemiesAffinitySource. 보스는 보스 데이터를 만드는 작업이 더한다).
    // AffinityValidator 가 이 어셈블리에서 찾는다(공개 매개변수 없는 생성자, 형식 이름 순서).
    public interface IAffinitySource
    {
        void Collect(DataSet data, AffinityTable table, Diagnostics diagnostics);
    }
}

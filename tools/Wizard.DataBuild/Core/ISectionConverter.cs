using Wizard.Content;
using Wizard.Core;

namespace Wizard.DataBuild.Core
{
    // 팩 절 하나의 변환기(ARCHITECTURE 8.2 2·4단계, T-33). tools/Wizard.DataBuild/Convert/<절>Converter.cs 에 하나씩 두고
    // 등록하지 않는다(ModuleRegistry 가 이 어셈블리에서 찾는다). 공개 매개변수 없는 생성자가 있어야 한다.
    //   - file 은 스키마 검사를 통과한 data/sim·data/rules 의 파일이다.
    //   - 수는 JsonElement.GetDecimal 로 읽고 Units 로 정수로 바꾼다(실수형으로 읽지 않는다).
    //   - output 에 정규 순서(LE, id 오름차순)로 쓴다. 같은 입력이면 같은 바이트다(디스크·사전의 나열 순서에 기대지 않는다).
    //   - 규칙 위반은 diagnostics.Error(file.RepoPath, <JSON 위치>, <이유>) 로 적는다. DataBuildException 을 던져도 된다(한 줄로 적힌다).
    // 읽는 쪽은 ContentDb 의 Load<절> 훅(src/Wizard.Content/Pack/Core/ContentDb.Core.cs)을 구현하는 절 파일이다.
    public interface ISectionConverter
    {
        ContentSection Section { get; }

        void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics);
    }
}

using System;

namespace Wizard.DataBuild.Core
{
    // 데이터가 규칙을 어겼다(범위, 형식, 정밀도). 변환기는 이것을 Diagnostics 의 한 줄(파일, JSON 위치, 이유)로 바꾼다.
    public sealed class DataBuildException : Exception
    {
        public DataBuildException(string message) : base(message)
        {
        }
    }
}

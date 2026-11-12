using System;

namespace Wizard.Server.Data
{
    // 마이그레이션을 적용하지 않고 멈춘 이유(이름 꼴, 겹치는 번호, 바뀐 체크섬, 사라진 파일, 순서 어긋남)
    public sealed class MigrationException : Exception
    {
        public MigrationException(string message)
            : base(message)
        {
        }
    }
}

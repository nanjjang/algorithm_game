using System;

namespace Wizard.AssetCheck.Png
{
    // 읽을 수 없는 PNG. 반입 검사는 이것을 그 파일의 실패로 적는다
    public sealed class PngFormatException : Exception
    {
        public PngFormatException(string message)
            : base(message)
        {
        }
    }
}

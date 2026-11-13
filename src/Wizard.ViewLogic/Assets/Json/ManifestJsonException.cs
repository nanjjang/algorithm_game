using System;

namespace Wizard.ViewLogic.Assets.Json
{
    // 매니페스트 JSON 문법 오류. Offset 은 글자 위치(0부터)
    public sealed class ManifestJsonException : Exception
    {
        public ManifestJsonException(string message, int offset)
            : base(message + " (offset " + offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")")
        {
            Offset = offset;
        }

        public int Offset { get; }
    }
}

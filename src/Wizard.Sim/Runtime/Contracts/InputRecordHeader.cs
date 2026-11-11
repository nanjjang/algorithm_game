namespace Wizard.Sim
{
    // CONTRACTS 11.1. .wzrc 머리말. 코덱(InputRecordCodec)은 M1-SIM-07 이 만든다
    public sealed class InputRecordHeader
    {
        public ushort FormatVersion = 1;
        public string CoreVersion = string.Empty;         // hex 32자(16바이트)
        public uint DataVersion;
        public EndlessSetup Setup = new EndlessSetup();   // 머리말의 나머지 바이트 = EndlessSetup 의 정규 바이트
    }
}

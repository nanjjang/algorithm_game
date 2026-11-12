namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1. .wzrc 머리말을 읽은 것. 머리말의 바이트는 Setup.WriteCanonical 이 내는 바이트와 같다(T-46).
    // core_version·data_version 은 Setup 에 있다(같은 값을 두 곳에 두지 않는다). 코덱(InputRecordCodec)은 M1-SIM-07 이 만든다
    public sealed class InputRecordHeader
    {
        public ushort FormatVersion = EndlessSetup.CanonicalFormatVersion;
        public EndlessSetup Setup = new EndlessSetup();
    }
}

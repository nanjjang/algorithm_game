namespace Wizard.Sim
{
    // CONTRACTS 3.2. CoreVersion 은 Generated/SimVersion.g.cs(tools/Wizard.VersionStamp)에 있다.
    public static partial class SimVersion
    {
        public const byte FactsFormat = 1;   // 이 코어가 쓰는 RunResultFacts 형식 판(CONTRACTS 3.7)
        public const int TickRate = 60;
    }
}

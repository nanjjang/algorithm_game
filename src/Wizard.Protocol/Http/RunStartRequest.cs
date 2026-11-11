namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.3. wand.character ≠ character 면 NotEligible
    public sealed class RunStartRequest
    {
        public int Character { get; set; }
        public long WandId { get; set; }
        public string Kind { get; set; } = "normal";
        public int StartWave { get; set; } = 1;
        public string CoreVersion { get; set; } = "";
        public int DataVersion { get; set; }
    }
}

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2 { block, qty }
    public sealed class SacrificeDto
    {
        public string Block { get; set; } = "";
        public int Qty { get; set; }
    }
}

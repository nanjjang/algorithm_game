namespace Wizard.Server.Data
{
    // request 행 하나(Dapper 대응용)
    internal sealed class StoredRequest
    {
        public string Kind { get; set; } = "";
        public byte[] Fingerprint { get; set; } = System.Array.Empty<byte>();
        public string? Result { get; set; }
    }
}

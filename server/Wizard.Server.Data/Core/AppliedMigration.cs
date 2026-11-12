namespace Wizard.Server.Data
{
    // schema_migration 의 한 줄(Dapper 대응용)
    internal sealed class AppliedMigration
    {
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public byte[] Checksum { get; set; } = System.Array.Empty<byte>();
    }
}

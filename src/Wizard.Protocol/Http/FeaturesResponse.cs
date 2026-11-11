namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.1 GET /v1/features
    public sealed class FeaturesResponse
    {
        public int ProtocolVersion { get; set; }
        public string CoreVersion { get; set; } = "";
        public int DataVersion { get; set; }
        public bool RecoveryMailEnabled { get; set; }
        public bool PaymentsEnabled { get; set; }
        public bool SignupOpen { get; set; }
        public bool Maintenance { get; set; }
    }
}

using System;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.1
    public sealed class AuthTokens
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime AccessExpiresAt { get; set; }
        public bool NewDevice { get; set; }
    }
}

using System.Collections.Generic;

namespace Wizard.Protocol.Http
{
    // CONTRACTS 8.2
    public sealed class LoadoutDto
    {
        public string? Core { get; set; }                                       // "block.magic_bolt" 또는 null
        public List<string?> Links { get; set; } = new List<string?>();         // 기본 연결 칸 + 확장 칸
    }
}

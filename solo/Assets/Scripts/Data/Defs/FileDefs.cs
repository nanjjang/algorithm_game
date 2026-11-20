using System.Collections.Generic;

// data/sim/*.json 의 맨 바깥 꼴(배열 하나를 담은 객체). ContentDb가 파일을 통째로 읽을 때만 쓴다.
// "_note" 같은 설명 열쇠는 읽지 않는다(대응하는 필드가 없으면 Newtonsoft가 건너뛴다).
namespace Wizard.Solo.Data
{
    /// <summary>blocks.json.</summary>
    public sealed class BlocksFile { public List<BlockDef> Blocks = new List<BlockDef>(); }

    /// <summary>arts.json.</summary>
    public sealed class ArtsFile { public List<ArtDef> Arts = new List<ArtDef>(); }

    /// <summary>enemies.json.</summary>
    public sealed class EnemiesFile { public List<EnemyDef> Enemies = new List<EnemyDef>(); }

    /// <summary>elites.json.</summary>
    public sealed class ElitesFile { public List<EliteDef> Elites = new List<EliteDef>(); }

    /// <summary>frames.json.</summary>
    public sealed class FramesFile { public List<FrameDef> Frames = new List<FrameDef>(); }

    /// <summary>modifiers.json.</summary>
    public sealed class ModifiersFile { public List<ModifierDef> Modifiers = new List<ModifierDef>(); }

    /// <summary>tod.json.</summary>
    public sealed class TodsFile { public List<TodDef> Tods = new List<TodDef>(); }

    /// <summary>weather_events.json.</summary>
    public sealed class WeatherEventsFile { public List<WeatherEventDef> Events = new List<WeatherEventDef>(); }

    /// <summary>arena.json.</summary>
    public sealed class ArenasFile { public List<ArenaDef> Arenas = new List<ArenaDef>(); }
}

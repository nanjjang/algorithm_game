using System;
using System.Collections.Generic;
using Wizard.ViewLogic.Assets;

namespace Wizard.ViewLogic.Tests.Assets
{
    // 시험용 매니페스트 원천: 글은 표에서, 파일 존재는 집합에서. 무엇을 읽고 물었는지 남긴다
    public sealed class MemoryAssetSource : IAssetSource
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.Ordinal);

        public List<string> Reads { get; } = new List<string>();

        public List<string> ExistsQueries { get; } = new List<string>();

        public MemoryAssetSource Text(string path, string json)
        {
            _texts[path] = json;
            return this;
        }

        public MemoryAssetSource File(string path)
        {
            _files.Add(path);
            return this;
        }

        public bool TryReadText(string path, out string text)
        {
            Reads.Add(path);
            if (_texts.TryGetValue(path, out string? t))
            {
                text = t;
                return true;
            }
            text = "";
            return false;
        }

        public bool Exists(string path)
        {
            ExistsQueries.Add(path);
            return _files.Contains(path) || _texts.ContainsKey(path);
        }

        // 다섯 매니페스트를 아주 작게 채운 원천(형식마다 항목 하나)
        public static MemoryAssetSource Minimal()
        {
            return new MemoryAssetSource()
                .Text("ui/manifest.json", MinimalUi)
                .Text("players/manifest.json", MinimalPlayers)
                .Text("wands/manifest.json", MinimalWands)
                .Text("monsters/manifest.json", MinimalMonsters)
                .Text("effects/manifest.json", MinimalFx);
        }

        public const string MinimalUi = @"{
  ""sampling"": ""nearest"", ""mipmaps"": false, ""texture_compression"": false,
  ""external"": { ""S-9001"": { ""file"": ""../backgrounds/missing_1920x1080.png"" } },
  ""assets"": [
    { ""id"": ""K-20"", ""file"": ""kit/k-20_button_large_48x64_hover.png"", ""width"": 48, ""height"": 64, ""frames"": 1,
      ""nine_slice_ltrb"": [12, 12, 12, 12], ""atlas"": { ""page"": ""atlas/kit-0.png"", ""rect"": [100, 200, 48, 64] } },
    { ""id"": ""I-B1"", ""file"": ""icon/i-b1_block_fireball_48x48.png"", ""width"": 48, ""height"": 48, ""frames"": 1, ""element"": ""frost"" }
  ]
}";

        public const string MinimalPlayers = @"{
  ""filter"": ""point"", ""mipmaps"": false, ""canvas"": [64, 64], ""pivotPx"": [30, 60], ""mirrorRightForLeft"": true,
  ""characters"": { ""folder_a"": { ""body"": ""folder_a/body.png"", ""frames"": 2,
    ""portrait"": ""folder_a/p.png"", ""selection"": ""folder_a/s.png"", ""silhouette"": ""folder_a/t.png"",
    ""clips"": { ""idle_down"": { ""fps"": 7, ""loop"": true, ""frames"": [
      { ""rect"": [0, 0, 64, 64], ""feet"": [30, 60], ""hand"": [22, 44], ""head"": [30, 13], ""shoulder"": [30, 35] },
      { ""rect"": [64, 0, 64, 64], ""feet"": [30, 60], ""hand"": [22, 43], ""head"": [30, 12], ""shoulder"": [30, 34] } ] } } } }
}";

        public const string MinimalWands = @"{
  ""handCanvas"": [32, 64], ""grip"": [16, 48], ""filter"": ""point"", ""mipmaps"": false,
  ""sprites"": { ""folder_a"": {
    ""hand"": { ""file"": ""folder_a/hand.png"", ""cell"": [32, 64], ""columns"": 2, ""rows"": [""down_hold"", ""down_aim"", ""right_hold"", ""right_aim"", ""up_hold"", ""up_aim""] },
    ""inspection"": { ""file"": ""folder_a/inspection.png"", ""cell"": [192, 192], ""columns"": 2, ""rows"": 1 },
    ""icon"": { ""file"": ""folder_a/icon.png"", ""cell"": [64, 64], ""columns"": 2 },
    ""stages"": [ { ""stage"": 0, ""head"": [16, 33], ""mutations"": [[12, 36], [20, 36]], ""expansion"": [18, 49] },
                  { ""stage"": 1, ""head"": [16, 30], ""mutations"": [[12, 33], [20, 33]], ""expansion"": [18, 49] } ] } },
  ""detachedEffects"": {}
}";

        public const string MinimalMonsters = @"{
  ""schema"": 1, ""scaleMode"": ""nearest"",
  ""entries"": { ""enemy.slime_moss"": { ""path"": ""enemy.slime_moss.png"", ""size"": [64, 64], ""grid"": [2, 1], ""origin"": [32, 58],
    ""clips"": { ""move"": [ { ""rect"": [0, 0, 64, 64], ""origin"": [32, 58] }, { ""rect"": [64, 0, 64, 64], ""origin"": [32, 54] } ] } } }
}";

        public const string MinimalFx = @"{
  ""player"": { ""hit_fire"": { ""path"": ""player/hit_fire_32x32_f3.png"", ""size"": [32, 32], ""frames"": [[0, 0, 32, 32], [32, 0, 32, 32]], ""fps"": 12, ""loop"": false } },
  ""notes"": { ""origin"": ""top-left"", ""filter"": ""point"", ""mipmaps"": false }
}";

        public const string MinimalAliases = @"{
  ""element"": { ""frost"": ""ice"", ""ice"": ""ice"" },
  ""character_folder"": { ""folder_a"": ""char.1"" },
  ""wand_folder"": { ""folder_a"": ""wand.1"" },
  ""wand_detached"": {},
  ""block_projectile_fx"": { ""_note"": ""ignored"", ""block.fireball"": ""player/hit_fire"" },
  ""ui_icons"": [ { ""content_prefix"": ""block."", ""icons"": [""I-B1""], ""variant_prefix"": ""block_"" } ],
  ""character_sheets"": { ""portrait"": { ""cell"": [64, 64], ""frames"": 4 }, ""selection"": { ""cell"": [192, 192], ""frames"": 1 }, ""silhouette"": { ""cell"": [32, 32], ""frames"": 1 } },
  ""ui_external_size"": {},
  ""wand_up_split"": {}
}";
    }
}

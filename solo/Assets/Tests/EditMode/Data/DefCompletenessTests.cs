using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Data
{
    /// <summary>
    /// JSON의 열쇠가 하나도 빠짐없이 Def의 필드에 닿는지(snake_case 대응, 숫자가 든 열쇠 포함) 확인한다.
    /// 일부러 읽지 않는 절: "_"로 시작하는 설명 열쇠, arts.json의 duel(결투 — 솔로 제외, ArtDef 주석).
    /// JObject/JToken으로 둔 절(적 ai, 엘리트 params, 보스 movement 등)은 안을 보지 않는다.
    /// </summary>
    public class DefCompletenessTests
    {
        static readonly string[] IgnoredKeys = { "duel" };

        static readonly KeyValuePair<string, Type>[] Roots =
        {
            new KeyValuePair<string, Type>(ContentDb.Paths.Blocks, typeof(BlocksFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Elements, typeof(ElementsDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Characters, typeof(CharactersFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Arts, typeof(ArtsFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Tries, typeof(TriesDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Enemies, typeof(EnemiesFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Elites, typeof(ElitesFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Bosses, typeof(BossesFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Waves, typeof(WavesDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Frames, typeof(FramesFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Modifiers, typeof(ModifiersFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Tods, typeof(TodsFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Scoring, typeof(ScoringDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Starfall, typeof(StarfallDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.WeatherEvents, typeof(WeatherEventsFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Arena, typeof(ArenasFile)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Enhance, typeof(EnhanceConfig)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Economy, typeof(EconomyConfig)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Mutations, typeof(MutationsConfig)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Drops, typeof(DropsConfig)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Camp, typeof(CampConfig)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Const, typeof(SoloConstDef)),
            new KeyValuePair<string, Type>(ContentDb.Paths.Practice, typeof(PracticeConfig)),
        };

        [Test]
        public void S00_Defs__EveryJsonKeyMapsToAField()
        {
            var resolver = (DefaultContractResolver)ContentDb.CreateSettings().ContractResolver;
            var missing = new List<string>();
            foreach (var root in Roots)
            {
                string path = Path.Combine(TestContent.DataDir, root.Key + ".json");
                Assert.IsTrue(File.Exists(path), path);
                var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
                Walk(resolver, token, root.Value, root.Key, missing);
            }
            // 재료 표는 JObject로 읽고 MaterialsDef로 다시 읽는다 → 그 형도 본다.
            var drops = JObject.Parse(File.ReadAllText(Path.Combine(TestContent.DataDir, ContentDb.Paths.Drops + ".json"), Encoding.UTF8));
            Walk(resolver, drops["materials"], typeof(MaterialsDef), "drops.materials", missing);
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        // 솔로 보충 JSON은 Def의 필드를 전부 채운다(빠지면 0으로 읽혀 조용히 틀린다).
        [Test]
        public void S00_Defs__SoloJsonFillsEveryField()
        {
            var resolver = (DefaultContractResolver)ContentDb.CreateSettings().ContractResolver;
            var missing = new List<string>();
            foreach (var root in Roots)
            {
                if (!root.Key.StartsWith("solo/", StringComparison.Ordinal)) continue;
                var jo = JObject.Parse(File.ReadAllText(Path.Combine(TestContent.DataDir, root.Key + ".json"), Encoding.UTF8));
                var contract = (JsonObjectContract)resolver.ResolveContract(root.Value);
                foreach (var p in contract.Properties)
                {
                    if (p.Ignored || !p.Writable) continue;
                    if (jo.Property(p.PropertyName) == null) missing.Add(root.Key + ": " + p.PropertyName);
                }
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        static void Walk(DefaultContractResolver resolver, JToken token, Type type, string path, List<string> missing)
        {
            if (token == null || token.Type == JTokenType.Null) return;
            if (type == typeof(JObject) || type == typeof(JToken) || type == typeof(JArray)) return;
            var contract = resolver.ResolveContract(Nullable.GetUnderlyingType(type) ?? type);
            var arr = contract as JsonArrayContract;
            if (arr != null)
            {
                if (token.Type != JTokenType.Array) { missing.Add(path + ": expected array"); return; }
                int i = 0;
                foreach (var item in token) Walk(resolver, item, arr.CollectionItemType, path + "[" + i++ + "]", missing);
                return;
            }
            var dict = contract as JsonDictionaryContract;
            if (dict != null)
            {
                if (token.Type != JTokenType.Object) { missing.Add(path + ": expected object"); return; }
                foreach (var p in ((JObject)token).Properties())
                    if (!p.Name.StartsWith("_", StringComparison.Ordinal))
                        Walk(resolver, p.Value, dict.DictionaryValueType, path + "." + p.Name, missing);
                return;
            }
            var obj = contract as JsonObjectContract;
            if (obj == null) return;   // 기본형
            if (token.Type != JTokenType.Object) { missing.Add(path + ": expected object for " + type.Name); return; }
            foreach (var p in ((JObject)token).Properties())
            {
                if (p.Name.StartsWith("_", StringComparison.Ordinal)) continue;
                if (Array.IndexOf(IgnoredKeys, p.Name) >= 0) continue;
                JsonProperty prop = null;
                foreach (var cp in obj.Properties)
                    if (!cp.Ignored && string.Equals(cp.PropertyName, p.Name, StringComparison.Ordinal)) { prop = cp; break; }
                if (prop == null) { missing.Add(path + "." + p.Name + " (no field in " + type.Name + ")"); continue; }
                Walk(resolver, p.Value, prop.PropertyType, path + "." + p.Name, missing);
            }
        }
    }
}

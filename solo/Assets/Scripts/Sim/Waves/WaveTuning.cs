using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Waves
{
    /// <summary>
    /// 웨이브 디렉터의 보충 수치(Resources/DataSolo/G09.json의 "waves" 절)와 솔로 조정 손잡이("horde" 절).
    /// "waves" 절은 Def에 없는 명세 값만 든다. 조각이 없거나 열쇠가 빠지면 아래 기본값(조각과 같은 명세 값)을 쓴다 — 콘텐츠 없는 테스트가 죽지 않게.
    /// "horde" 절(떼 출현)은 명세 값이 아니라 사용자가 정한 솔로 조정이다. 코드 기본값은 전부 "꺼짐 = 명세 그대로"이고,
    /// 켜는 값은 JSON에만 둔다. 명세 벡터 테스트는 <see cref="From(IContentDb, bool)"/>에 includeHorde = false를 줘 명세 그대로 돈다.
    /// </summary>
    public sealed class WaveTuning
    {
        public const string ExtraName = "G09";
        const string Section = "waves";
        const string HordeSection = "horde";
        /// <summary>horde.reward_mode 값: 배율로 늘어난 적은 보상 없음(처치 보상 합계 = 명세 구성의 합계).</summary>
        public const string RewardKeepTotal = "keep_total";
        /// <summary>horde.reward_mode 값: 늘어난 적도 한 마리마다 보통 보상(합계가 배율만큼 는다).</summary>
        public const string RewardFull = "full";

        /// <summary>종 기본 가중치(06 요약서 1.5절 [임시 U-01]).</summary>
        public int SpeciesBaseWeight = 100;
        /// <summary>스폰 자리가 피하는 모서리 길이(칸).</summary>
        public float SpawnCornerExclusion = 1f;
        /// <summary>스폰 자리가 다른 개체에게서 띄우는 거리(칸).</summary>
        public float SpawnEntityClearance = 1f;
        /// <summary>다른 개체를 피해 자리를 다시 뽑는 횟수(솔로 임시).</summary>
        public int SpawnRetryCount = 8;
        /// <summary>뿌리 적(가시덩굴)의 벽·플레이어 거리(05 요약서 4절 [임시 U-60]).</summary>
        public float RootedWallClearance = 2f;
        public float RootedPlayerClearance = 3f;
        /// <summary>날마다 바뀌는 풀밭 무늬·소품 배치 벌 수(01 요약서 4.1절).</summary>
        public int PropVariants = 6;

        // ------------------------------------------------------------ 떼 출현(솔로 조정, 기본 꺼짐)

        /// <summary>떼 출현 손잡이를 쓰는가. false면 아래 값과 무관하게 명세 그대로.</summary>
        public bool HordeEnabled;
        /// <summary>잡몹 엔트리 수 배율(1 이상). 2.5 = 잡몹 한 마리가 평균 2.5마리. 정예·보스·특수 적은 늘지 않는다.</summary>
        public float HordeNormalCountMultiplier = 1f;
        /// <summary>잡몹으로 치는 역할(enemies.json role). 종 상한이 있는 종·졸개는 역할과 무관하게 빠진다.</summary>
        public readonly List<string> HordeNormalRoles = new List<string>();
        /// <summary>동시 생존 상한(waves.json max_alive) 배율(1 이상).</summary>
        public float HordeMaxAliveMultiplier = 1f;
        /// <summary>무리 출현: 한 가장자리 자리에서 뭉쳐 나오는 잡몹 수의 범위(양 끝 포함). 최대가 1 이하면 무리 출현 없음.</summary>
        public int HordePackSizeMin = 1;
        public int HordePackSizeMax = 1;
        /// <summary>무리 한 마리가 무리 자리에서 흩어지는 최대 거리(칸). 전장 안쪽으로만 흩어진다.</summary>
        public float HordePackScatterRadius;
        /// <summary>true = 배율로 늘어난 적은 보상 없음(합계 유지). false = 늘어난 적도 보통 보상.</summary>
        public bool HordeKeepRewardTotal = true;

        /// <summary>떼 출현이 실제로 무엇이든 바꾸는가.</summary>
        public bool HordeMultiplies => HordeEnabled && HordeNormalCountMultiplier > 1f;
        public bool HordePacks => HordeEnabled && HordePackSizeMax > 1;

        public static WaveTuning From(IContentDb db) => From(db, true);

        /// <summary>includeHorde = false면 "horde" 절을 읽지 않는다(명세 벡터 테스트와 명세 기준 도구용).</summary>
        public static WaveTuning From(IContentDb db, bool includeHorde)
        {
            var t = new WaveTuning();
            JObject root = db != null ? db.SoloExtra(ExtraName) : null;
            if (root == null) return t;
            var sec = root[Section] as JObject;
            if (sec != null)
            {
                t.SpeciesBaseWeight = Int(sec, "species_base_weight", t.SpeciesBaseWeight);
                t.SpawnCornerExclusion = Float(sec, "spawn_corner_exclusion", t.SpawnCornerExclusion);
                t.SpawnEntityClearance = Float(sec, "spawn_entity_clearance", t.SpawnEntityClearance);
                t.SpawnRetryCount = Int(sec, "spawn_retry_count", t.SpawnRetryCount);
                t.RootedWallClearance = Float(sec, "rooted_wall_clearance", t.RootedWallClearance);
                t.RootedPlayerClearance = Float(sec, "rooted_player_clearance", t.RootedPlayerClearance);
                t.PropVariants = Int(sec, "prop_variants", t.PropVariants);
            }
            if (includeHorde) ReadHorde(root[HordeSection] as JObject, t);
            return t;
        }

        /// <summary>"horde" 절을 읽는다(테스트가 손으로 만든 절을 줄 때도 쓴다). 값은 범위 밖이면 명세 쪽으로 자른다.</summary>
        public static void ReadHorde(JObject h, WaveTuning t)
        {
            if (h == null || t == null) return;
            var en = h["enabled"];
            t.HordeEnabled = en != null && en.Type == JTokenType.Boolean && en.Value<bool>();
            t.HordeNormalCountMultiplier = System.Math.Max(1f, Float(h, "normal_count_multiplier", 1f));
            t.HordeMaxAliveMultiplier = System.Math.Max(1f, Float(h, "max_alive_multiplier", 1f));
            t.HordePackSizeMin = System.Math.Max(1, Int(h, "pack_size_min", 1));
            t.HordePackSizeMax = System.Math.Max(t.HordePackSizeMin, Int(h, "pack_size_max", 1));
            t.HordePackScatterRadius = System.Math.Max(0f, Float(h, "pack_scatter_radius", 0f));
            var mode = h["reward_mode"];
            t.HordeKeepRewardTotal = mode == null || mode.Type != JTokenType.String || mode.Value<string>() != RewardFull;
            t.HordeNormalRoles.Clear();
            var roles = h["normal_roles"] as JArray;
            if (roles != null)
                for (int i = 0; i < roles.Count; i++)
                    if (roles[i].Type == JTokenType.String) t.HordeNormalRoles.Add(roles[i].Value<string>());
        }

        static bool IsNumber(JToken v) => v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float);
        static int Int(JObject o, string key, int fallback) { var v = o[key]; return IsNumber(v) ? v.Value<int>() : fallback; }
        static float Float(JObject o, string key, float fallback) { var v = o[key]; return IsNumber(v) ? v.Value<float>() : fallback; }
    }
}

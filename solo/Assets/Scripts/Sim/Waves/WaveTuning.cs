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
        /// <summary>horde.reward_mode 값: 원래 한 마리 몫 골드·점수·체력 조각 확률을 무리 전체(원본 + 늘린 사본)에 나눈다.
        /// 마리마다 작은 보상이 튀고 합계는 명세 구성과 같다(소수점은 Run의 누적기가 결정론적으로 나른다).</summary>
        public const string RewardSplit = "split";
        const string BossPreviewSection = "boss_preview";

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
        /// <summary>
        /// 웨이브별 무리 크기 계단(pack_size_by_wave: [[웨이브, 최소, 최대], …], 웨이브 오름차순). 그 웨이브 이하의 마지막 점을 쓴다(계단, 직선 아님).
        /// 첫 점보다 앞이면 첫 점. 비어 있으면 <see cref="HordePackSizeMin"/>·<see cref="HordePackSizeMax"/> 하나.
        /// </summary>
        public readonly List<PackSizeStep> HordePackSizeByWave = new List<PackSizeStep>();
        /// <summary>무리 한 마리가 무리 자리에서 흩어지는 최대 거리(칸). 전장 안쪽으로만 흩어진다.</summary>
        public float HordePackScatterRadius;
        /// <summary>true = 배율로 늘어난 적은 보상 없음(합계 유지, keep_total). false = 늘어난 적도 보상(full 또는 split).</summary>
        public bool HordeKeepRewardTotal = true;
        /// <summary>reward_mode = split: 늘어난 적도 보상을 받되 한 무리가 원래 한 마리 몫을 나눈다(Run이 나눈다).</summary>
        public bool HordeSplitReward;
        /// <summary>
        /// 웨이브별 잡몹 배율 곡선(normal_count_multiplier_by_wave: [[웨이브, 배율], …], 웨이브 오름차순). 점 사이는 직선, 양 끝 밖은 끝 값.
        /// 비어 있으면 <see cref="HordeNormalCountMultiplier"/> 하나.
        /// </summary>
        public readonly List<KeyValuePair<int, float>> HordeMultiplierByWave = new List<KeyValuePair<int, float>>();
        /// <summary>역할별 배율 상한(role_multiplier_cap: {역할: 상한}). 곡선 값이 이보다 크면 이 값.</summary>
        public readonly Dictionary<string, float> HordeRoleMultiplierCap = new Dictionary<string, float>();
        /// <summary>빠른 처리 목표 시간(scoring.json fast_clear.target_seconds)에 그 웨이브의 떼 배율(늘린 뒤 잡몹 수 ÷ 명세 잡몹 수)을 곱하는가.</summary>
        public bool HordeScaleFastClearTarget;

        /// <summary>
        /// 보스 시연(boss_preview 절, 솔로 조정): 보스 웨이브 바로 앞 웨이브에 그 보스의 패턴을 미리 보여 주는 일반 적을 고정 개수로 넣는다
        /// (예산 안, 종 상한은 지킨다). 열쇠 = 보스 id. 비어 있으면 없음.
        /// </summary>
        public readonly Dictionary<string, List<SpeciesCountDef>> BossPreview = new Dictionary<string, List<SpeciesCountDef>>();

        /// <summary>그 웨이브·역할의 잡몹 배율(1 이상). 꺼져 있으면 1.</summary>
        public float HordeMultiplier(int wave, string role)
        {
            if (!HordeEnabled) return 1f;
            float m = HordeNormalCountMultiplier;
            var c = HordeMultiplierByWave;
            if (c.Count > 0)
            {
                if (wave <= c[0].Key) m = c[0].Value;
                else if (wave >= c[c.Count - 1].Key) m = c[c.Count - 1].Value;
                else
                    for (int i = 1; i < c.Count; i++)
                    {
                        if (wave > c[i].Key) continue;
                        float t = (wave - c[i - 1].Key) / (float)System.Math.Max(1, c[i].Key - c[i - 1].Key);
                        m = c[i - 1].Value + (c[i].Value - c[i - 1].Value) * t;
                        break;
                    }
            }
            float cap;
            if (role != null && HordeRoleMultiplierCap.TryGetValue(role, out cap) && m > cap) m = cap;
            return System.Math.Max(1f, m);
        }

        /// <summary>떼 출현이 실제로 무엇이든 바꾸는가.</summary>
        public bool HordeMultiplies => HordeEnabled && (HordeNormalCountMultiplier > 1f || MaxCurve() > 1f);

        float MaxCurve()
        {
            float m = 1f;
            for (int i = 0; i < HordeMultiplierByWave.Count; i++) if (HordeMultiplierByWave[i].Value > m) m = HordeMultiplierByWave[i].Value;
            return m;
        }
        public bool HordePacks => HordeEnabled && (HordePackSizeMax > 1 || MaxPackCurve() > 1);

        int MaxPackCurve()
        {
            int m = 1;
            for (int i = 0; i < HordePackSizeByWave.Count; i++) if (HordePackSizeByWave[i].Max > m) m = HordePackSizeByWave[i].Max;
            return m;
        }

        /// <summary>그 웨이브의 무리 크기 범위(양 끝 포함). 계단이 있으면 그 웨이브 이하의 마지막 점, 없으면 pack_size_min·max.</summary>
        public void PackSizeRange(int wave, out int min, out int max)
        {
            min = HordePackSizeMin;
            max = HordePackSizeMax;
            var c = HordePackSizeByWave;
            if (c.Count == 0) return;
            int k = 0;
            for (int i = 1; i < c.Count; i++) if (c[i].Wave <= wave) k = i;
            min = c[k].Min;
            max = c[k].Max;
        }

        /// <summary>무리 크기 계단의 한 점.</summary>
        public struct PackSizeStep
        {
            public int Wave, Min, Max;
        }

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
            if (includeHorde)
            {
                ReadHorde(root[HordeSection] as JObject, t);
                ReadBossPreview(root[BossPreviewSection] as JObject, t);
            }
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
            t.HordePackSizeByWave.Clear();
            var packs = h["pack_size_by_wave"] as JArray;
            if (packs != null)
                for (int i = 0; i < packs.Count; i++)
                {
                    var pt = packs[i] as JArray;
                    if (pt == null || pt.Count < 3 || !IsNumber(pt[0]) || !IsNumber(pt[1]) || !IsNumber(pt[2])) continue;
                    int wave = pt[0].Value<int>();
                    if (t.HordePackSizeByWave.Count > 0 && wave <= t.HordePackSizeByWave[t.HordePackSizeByWave.Count - 1].Wave) continue;
                    int lo = System.Math.Max(1, pt[1].Value<int>());
                    int hi = System.Math.Max(lo, pt[2].Value<int>());
                    t.HordePackSizeByWave.Add(new PackSizeStep { Wave = wave, Min = lo, Max = hi });
                }
            var mode = h["reward_mode"];
            string m = mode != null && mode.Type == JTokenType.String ? mode.Value<string>() : null;
            t.HordeKeepRewardTotal = m != RewardFull && m != RewardSplit;
            t.HordeSplitReward = m == RewardSplit;
            t.HordeMultiplierByWave.Clear();
            var curve = h["normal_count_multiplier_by_wave"] as JArray;
            if (curve != null)
                for (int i = 0; i < curve.Count; i++)
                {
                    var pt = curve[i] as JArray;
                    if (pt == null || pt.Count < 2 || !IsNumber(pt[0]) || !IsNumber(pt[1])) continue;
                    int wave = pt[0].Value<int>();
                    if (t.HordeMultiplierByWave.Count > 0 && wave <= t.HordeMultiplierByWave[t.HordeMultiplierByWave.Count - 1].Key) continue;
                    t.HordeMultiplierByWave.Add(new KeyValuePair<int, float>(wave, System.Math.Max(1f, pt[1].Value<float>())));
                }
            t.HordeRoleMultiplierCap.Clear();
            var caps = h["role_multiplier_cap"] as JObject;
            if (caps != null)
                foreach (var kv in caps)
                    if (IsNumber(kv.Value)) t.HordeRoleMultiplierCap[kv.Key] = System.Math.Max(1f, kv.Value.Value<float>());
            var fast = h["scale_fast_clear_target"];
            t.HordeScaleFastClearTarget = fast != null && fast.Type == JTokenType.Boolean && fast.Value<bool>();
            t.HordeNormalRoles.Clear();
            var roles = h["normal_roles"] as JArray;
            if (roles != null)
                for (int i = 0; i < roles.Count; i++)
                    if (roles[i].Type == JTokenType.String) t.HordeNormalRoles.Add(roles[i].Value<string>());
        }

        /// <summary>"boss_preview" 절: { "enabled": bool, "bosses": { 보스 id: [ { species, min, max }, … ] } }.</summary>
        public static void ReadBossPreview(JObject b, WaveTuning t)
        {
            t.BossPreview.Clear();
            if (b == null) return;
            var en = b["enabled"];
            if (en == null || en.Type != JTokenType.Boolean || !en.Value<bool>()) return;
            var bosses = b["bosses"] as JObject;
            if (bosses == null) return;
            foreach (var kv in bosses)
            {
                var arr = kv.Value as JArray;
                if (arr == null) continue;
                var list = new List<SpeciesCountDef>();
                for (int i = 0; i < arr.Count; i++)
                {
                    var o = arr[i] as JObject;
                    if (o == null || o["species"] == null) continue;
                    int min = Int(o, "min", 1);
                    list.Add(new SpeciesCountDef { Species = (string)o["species"], Min = min, Max = System.Math.Max(min, Int(o, "max", min)) });
                }
                if (list.Count > 0) t.BossPreview[kv.Key] = list;
            }
        }

        static bool IsNumber(JToken v) => v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float);
        static int Int(JObject o, string key, int fallback) { var v = o[key]; return IsNumber(v) ? v.Value<int>() : fallback; }
        static float Float(JObject o, string key, float fallback) { var v = o[key]; return IsNumber(v) ? v.Value<float>() : fallback; }
    }
}

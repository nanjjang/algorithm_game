using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Records;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Stats;

namespace Wizard.Solo.Screens.Settlement
{
    /// <summary>판 끝 요약의 문턱(DataSolo/Recap.json, 솔로 임시 값).</summary>
    public sealed class RecapTuning
    {
        public const string ExtraName = "Recap";
        public int ManyHits, LowGrazePerWave, HeatLocksMany, ShortRunWave, GrazeChainPraise, BossPatternWindowSeconds;

        public static RecapTuning From(IContentDb db) => From(db != null ? db.SoloExtra(ExtraName) : null);

        public static RecapTuning From(JObject j)
        {
            var t = new RecapTuning();
            if (j == null) return t;
            t.ManyHits = I(j, "many_hits");
            t.LowGrazePerWave = I(j, "low_graze_per_wave");
            t.HeatLocksMany = I(j, "heat_locks_many");
            t.ShortRunWave = I(j, "short_run_wave");
            t.GrazeChainPraise = I(j, "graze_chain_praise");
            t.BossPatternWindowSeconds = I(j, "boss_pattern_window_seconds");
            return t;
        }

        static int I(JObject j, string k) => j[k] != null && j[k].Type == JTokenType.Integer ? (int)j[k] : 0;
    }

    public enum RecapEnd { Died = 0, Abandoned = 1, Unknown = 2 }

    /// <summary>"이번 판 최고 순간" 칸 하나.</summary>
    public sealed class RecapHighlight
    {
        public string LabelKey;
        public long Value;
        /// <summary>값 대신 그릴 블록 아이콘(주력 블록). 없으면 null.</summary>
        public string IconId;
        /// <summary>값이 웨이브다("N일째 웨이브 M"으로 그린다).</summary>
        public bool IsWave;
        /// <summary>그 값을 모르는 칸(통계 없음) — "—".</summary>
        public bool Unknown;
        /// <summary>캐릭터 개인 최고를 넘었다(새 기록 딱지 + 축하).</summary>
        public bool NewBest;
    }

    /// <summary>
    /// 판 끝 요약(정산 1쪽의 "이번 판 돌아보기") 규칙 — 엔진 없음. 좋은 로그라이크는 죽음마다 "무엇을 다르게 할까"에 답한다:
    /// 사인(무엇의 어떤 공격에 맞았나, 예고가 있었나) → 이번 판 최고 순간 → 다음 판에 해 볼 것(사인·통계 규칙 문구) → 스승의 한마디.
    /// 통계는 표시 이벤트를 모은 RunStatsCollector에서 온다(없으면 판 요약으로 채울 수 있는 것만 그린다).
    /// 글자는 전부 이름 표(str.run_recap.*), 문턱은 DataSolo/Recap.json.
    /// </summary>
    public sealed class RunRecap
    {
        public const string P = "str.run_recap.";
        public const string TitleKey = P + "title", CauseTitleKey = P + "cause.title", CauseLineKey = P + "cause.line",
            CauseAbandonKey = P + "cause.abandon", CauseUnknownKey = P + "cause.unknown", UnknownEnemyKey = P + "cause.unknown_enemy",
            CauseHitsKey = P + "cause.hits", CauseMostKey = P + "cause.most", HighlightTitleKey = P + "hl.title", HighlightNewKey = P + "hl.new",
            HighlightNoneKey = P + "hl.none", HighlightShotsKey = P + "hl.shots", TipTitleKey = P + "tip.title", MasterNameKey = "npc.master";
        public const string HlMaxHit = P + "hl.max_hit", HlGrazeChain = P + "hl.graze_chain", HlCombo = P + "hl.combo",
            HlTopBlock = P + "hl.top_block", HlKills = P + "hl.kills", HlReached = P + "hl.reached", WaveFormatKey = "str.format.wave";

        public RecapEnd End;
        public bool HasStats;
        /// <summary>사인(죽었을 때만): 때린 것의 종 id(모르면 null), 공격 이름 열쇠, 예고 꼬리표 열쇠.</summary>
        public string CauseDefId, CauseAttackKey, CauseTagKey;
        public bool CauseTelegraphed;
        public int HitsTaken;
        /// <summary>가장 많이 맞은 것(맞은 적이 없으면 null).</summary>
        public RunHitTally MostHit;
        public readonly List<RecapHighlight> Highlights = new List<RecapHighlight>();
        public RunHighlightBests Bests;
        public string TipKey;
        /// <summary>팁 틀의 채움 값: 적 이름 열쇠, 공격 이름 열쇠, 맞은 횟수.</summary>
        public string TipEnemyKey, TipAttackKey;
        public int TipCount;
        public string MasterLineKey;

        public bool HasCause => End == RecapEnd.Died && CauseAttackKey != null;
        /// <summary>새 기록이 하나라도 있다(축하 연출).</summary>
        public bool AnyNewBest => Bests.Any;

        // ------------------------------------------------------------ 열쇠

        public static string SourceKey(PlayerDamageSource s)
        {
            switch (s)
            {
                case PlayerDamageSource.Projectile: return P + "source.projectile";
                case PlayerDamageSource.Telegraph: return P + "source.telegraph";
                case PlayerDamageSource.Lingering: return P + "source.lingering";
                case PlayerDamageSource.Scripted: return P + "source.scripted";
                case PlayerDamageSource.Dash: return P + "source.dash";
                default: return P + "source.contact";
            }
        }

        public static string AttackKey(RunHitTally t) =>
            t == null ? null : !string.IsNullOrEmpty(t.Pattern) ? P + "pattern." + t.Pattern : SourceKey(t.Source);

        public static string TagKey(PlayerDamageSource s)
        {
            switch (s)
            {
                case PlayerDamageSource.Telegraph:
                case PlayerDamageSource.Dash: return P + "tag.telegraphed";
                case PlayerDamageSource.Projectile: return P + "tag.projectile";
                case PlayerDamageSource.Lingering: return P + "tag.lingering";
                default: return P + "tag.contact";
            }
        }

        static string TipKeyFor(PlayerDamageSource s)
        {
            switch (s)
            {
                case PlayerDamageSource.Dash: return P + "tip.dash";
                case PlayerDamageSource.Telegraph: return P + "tip.telegraph";
                case PlayerDamageSource.Projectile: return P + "tip.projectile";
                case PlayerDamageSource.Lingering: return P + "tip.lingering";
                default: return P + "tip.contact";
            }
        }

        // ------------------------------------------------------------ 만들기

        /// <param name="stats">그 판의 표시 통계(없으면 null — 자동 정산 등).</param>
        /// <param name="rec">정산 전의 캐릭터 기록(개인 최고와 견준다).</param>
        /// <param name="newBest">점수·웨이브 최고 기록 갱신(정산 화면의 딱지와 같은 값).</param>
        public static RunRecap Build(RunSummary summary, RunStats stats, CharacterRecord rec, bool newBest, RecapTuning t)
        {
            if (t == null) t = new RecapTuning();
            var r = new RunRecap { HasStats = stats != null && stats.HasData };
            if (summary == null) { r.End = RecapEnd.Unknown; r.TipKey = P + "tip.default"; r.MasterLineKey = P + "master.default.1"; return r; }
            r.End = summary.EndReason == RunEndReason.Abandon ? RecapEnd.Abandoned : r.HasStats ? RecapEnd.Died : RecapEnd.Unknown;

            if (r.HasStats)
            {
                r.HitsTaken = stats.HitsTaken;
                r.MostHit = stats.MostHit;
                if (r.End == RecapEnd.Died && stats.HasLastHit && stats.LastHit != null)
                {
                    r.CauseDefId = stats.LastHit.DefId;
                    r.CauseAttackKey = AttackKey(stats.LastHit);
                    r.CauseTagKey = TagKey(stats.LastHit.Source);
                    r.CauseTelegraphed = stats.LastHit.Telegraphed;
                }
                r.Bests = RunHighlightBook.Compare(rec, stats.MaxHitDamage, stats.MaxGrazeChain, stats.MaxCombo);
            }

            // 최고 순간 여섯 칸.
            bool k = r.HasStats;
            r.Highlights.Add(new RecapHighlight { LabelKey = HlMaxHit, Value = k ? stats.MaxHitDamage : 0, Unknown = !k, NewBest = r.Bests.Hit });
            r.Highlights.Add(new RecapHighlight { LabelKey = HlGrazeChain, Value = k ? stats.MaxGrazeChain : 0, Unknown = !k, NewBest = r.Bests.GrazeChain });
            r.Highlights.Add(new RecapHighlight { LabelKey = HlCombo, Value = k ? stats.MaxCombo : 0, Unknown = !k, NewBest = r.Bests.Combo });
            r.Highlights.Add(new RecapHighlight
            {
                LabelKey = HlTopBlock, IconId = k ? stats.TopBlockId : null, Value = k ? stats.TopBlockShots : 0,
                Unknown = !k || string.IsNullOrEmpty(stats.TopBlockId)
            });
            int kills = summary.TotalKills > 0 ? summary.TotalKills : (k ? stats.Kills : 0);
            r.Highlights.Add(new RecapHighlight { LabelKey = HlKills, Value = kills });
            r.Highlights.Add(new RecapHighlight { LabelKey = HlReached, Value = summary.ReachedWave, IsWave = true });

            ChooseTip(r, summary, stats, t);
            ChooseMaster(r, summary, stats, newBest, t);
            return r;
        }

        static void ChooseTip(RunRecap r, RunSummary s, RunStats st, RecapTuning t)
        {
            RunHitTally focus = null;
            if (r.HasStats)
            {
                RunHitTally most = st.MostHit;
                int many = t.ManyHits > 0 ? t.ManyHits : 1;
                if (most != null && most.Count >= many) focus = most;
                else if (r.End == RecapEnd.Died && st.LastHit != null) focus = st.LastHit;
            }
            if (focus != null && focus.Source != PlayerDamageSource.Scripted)
            {
                r.TipKey = TipKeyFor(focus.Source);
                r.TipEnemyKey = focus.DefId ?? UnknownEnemyKey;
                r.TipAttackKey = AttackKey(focus);
                r.TipCount = focus.Count;
                return;
            }
            int waves = s.ClearedDirect > 1 ? s.ClearedDirect : 1;
            if (r.HasStats && st.ArtsFired == 0 && s.ClearedDirect > 0) { r.TipKey = P + "tip.use_arts"; return; }
            if (r.HasStats && t.HeatLocksMany > 0 && st.HeatLocks >= t.HeatLocksMany) { r.TipKey = P + "tip.heat"; return; }
            if (r.HasStats && st.Grazes < t.LowGrazePerWave * waves) { r.TipKey = P + "tip.graze"; return; }
            r.TipKey = P + "tip.default";
        }

        static void ChooseMaster(RunRecap r, RunSummary s, RunStats st, bool newBest, RecapTuning t)
        {
            int v = 1 + (s.ReachedWave > 0 ? s.ReachedWave % 2 : 0);
            string key;
            if (s.KilledPhantomAtWave20) key = "master.revenge";
            else if (s.PhantomFirstDone) key = "master.phantom_first";
            else if (newBest || r.Bests.Any) key = "master.new_best." + v;
            else if (r.End == RecapEnd.Abandoned) key = "master.abandon." + v;
            else if (t.ShortRunWave > 0 && s.ReachedWave < t.ShortRunWave) key = "master.short." + v;
            else if (r.HasStats && t.GrazeChainPraise > 0 && st.MaxGrazeChain >= t.GrazeChainPraise) key = "master.graze";
            else if (r.HasCause && r.CauseTelegraphed) key = "master.telegraph." + v;
            else if (r.HasCause && st.LastHit.Source == PlayerDamageSource.Contact) key = "master.contact";
            else if (r.HasCause && st.LastHit.Source == PlayerDamageSource.Projectile) key = "master.projectile";
            else key = "master.default." + v;
            r.MasterLineKey = P + key;
        }

        // ------------------------------------------------------------ 글

        public string CauseText(INameTable n)
        {
            if (n == null) return CauseAttackKey ?? CauseAbandonKey;
            if (End == RecapEnd.Abandoned) return n.Get(CauseAbandonKey);
            if (!HasCause) return n.Get(CauseUnknownKey);
            return n.Format(CauseLineKey, "enemy", n.Get(CauseDefId ?? UnknownEnemyKey), "attack", n.Get(CauseAttackKey));
        }

        public string MostHitText(INameTable n)
        {
            if (MostHit == null || n == null) return null;
            return n.Format(CauseMostKey, "enemy", n.Get(MostHit.DefId ?? UnknownEnemyKey), "attack", n.Get(AttackKey(MostHit)), "n", MostHit.Count);
        }

        public string TipText(INameTable n)
        {
            if (n == null) return TipKey;
            if (TipAttackKey == null) return n.Get(TipKey);
            return n.Format(TipKey, "enemy", n.Get(TipEnemyKey), "attack", n.Get(TipAttackKey), "n", TipCount);
        }
    }
}

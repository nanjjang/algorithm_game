using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Hud
{
    /// <summary>상태에서 정하는 한 줄 안내(우선순위: 과열 잠금 &gt; 사건 안내 &gt; 출발 안내).</summary>
    public enum HudStateNotice { None = 0, HeatLock = 1, DepartHint = 2, WaitingParty = 3 }

    /// <summary>
    /// 한 프레임에 HUD가 그릴 것(상태에서만 계산한다 — 이벤트 없이 Build 직후에도 같은 값이 나온다).
    /// 글자는 들어 있지 않다(HudModel의 글자 함수가 이름 표로 만든다).
    /// </summary>
    public struct HudFrame
    {
        public RunPhase Phase;
        /// <summary>연습·튜토리얼 방: 웨이브·지갑·점수 요소를 숨긴다.</summary>
        public bool InPractice;
        public int Wave;
        public int Day;
        public bool BossWave;

        public bool ShowWaveProgress;
        public int WaveProgressPermille;

        public bool ShowDayWave;
        /// <summary>보스 웨이브 꼴: 16px 한 줄 (760,76) 400×24(TV-046).</summary>
        public bool DayWaveBossLayout;
        public string TodId;
        public string Whim0, Whim1;

        public bool ShowElapsed;
        public int ElapsedTicks;

        /// <summary>허상까지 n웨이브(0 = 줄 없음). RevengeTracker.Build(...).HudWavesLeft 그대로.</summary>
        public int RevengeWavesLeft;

        /// <summary>w.Boss != null일 때만.</summary>
        public bool ShowBoss;

        public bool ShowWallet;
        public int Purse, Score, Kills;
        /// <summary>
        /// 이번 웨이브 진행 중 쌓인 몫(처치·스침, Score.WaveProgressScore). 판 점수(Score)는 넘긴 웨이브의 합이라(01 요약서 6.2절)
        /// 웨이브 중에는 바뀌지 않는다 — 고장처럼 보이지 않게 옆에 "+n"으로 보인다. WaveCombat에서만 0보다 크다.
        /// </summary>
        public int ScorePending;

        public HudStateNotice StateNotice;
        public int HeatLockSecondsLeft;
        /// <summary>협동 야영: 출발 준비를 마친 사람 수 / 기다리는 사람 수(StateNotice == WaitingParty일 때).</summary>
        public int PartyReady, PartyTotal;
        public float HeatLockRemainRatio;

        /// <summary>콤보를 초상 옆(HUD)에 둔다(옵션 combo_position = hud).</summary>
        public bool ComboInHud;
    }

    /// <summary>"지금 K" 두 줄의 글.</summary>
    public struct NowKText
    {
        public bool Visible;
        /// <summary>아랫줄(또는 한 줄): 지금 K를 누르면 나갈 기술.</summary>
        public string Main;
        /// <summary>윗줄(섬 기술이 해금된 노드에서만, U-29). 없으면 null.</summary>
        public string Just;
        public bool Fizzle;
    }

    /// <summary>
    /// HUD의 표시 값 계산(순수 함수: 상태 → 문자열·비율·참거짓). 08 요약서 6절, 02 요약서 6.6절, 04 요약서 6절, 10 요약서 1.3·2.3·3.4절.
    /// HUD는 상태를 읽기만 하고 고치지 않는다. 복수 추적기 값은 Meta.RevengeTracker 하나로만 얻는다(직접 계산하지 않는다).
    /// </summary>
    public static class HudModel
    {
        // ------------------------------------------------------------ 이름 표 열쇠
        public const string KeyDayWave = "str.battle_hud.day_wave";
        public const string KeyWallet = "str.battle_hud.wallet";
        public const string KeyScore = "str.battle_hud.score";
        public const string KeyScorePending = "str.battle_hud.score_pending";
        public const string KeyKills = "str.battle_hud.kills";
        public const string KeyNoticeHeatLock = "str.battle_hud.notice.heat_lock";
        public const string KeyNoticeWaveClear = "str.battle_hud.notice.wave_clear";
        public const string KeyNowK = "str.battle_hud.now_k";
        public const string KeyNowKDebt = "str.battle_hud.now_k_debt";
        public const string KeyNowKFizzle = "str.battle_hud.now_k_fizzle";
        public const string KeyNowKFizzleLate = "str.battle_hud.now_k_fizzle_late";
        public const string KeyNowKJust = "str.battle_hud.now_k_just";
        public const string KeyNowKFinal = "str.battle_hud.now_k_final";
        public const string KeyBossRage = "str.battle_hud.boss.rage";
        public const string KeyBossFinalRage = "str.battle_hud.boss.final_rage";
        public const string KeyBossWeak = "str.battle_hud.boss.weak";
        public const string KeyBossResist = "str.battle_hud.boss.resist";
        public const string KeyRevengeHud = "revenge.tracker.hud";
        public const string KeyRevengeTag = "revenge.tag";
        public const string KeyDepartHint = "str.camp.depart_hint";
        /// <summary>협동: 출발 준비를 마치고 동료를 기다리는 중({n}/{m}).</summary>
        public const string KeyWaitingParty = "str.coop.wait_party_depart";
        public const string KeyWandStage = "str.format.wand_stage";
        public const string KeyLateInput = "str.dummy_practice.fail.late_input";
        public const string KeyMasterAdviceLinePrefix = "event.master_advice.line.";

        /// <summary>열 100 = 12,000 HU, 임박 = 임계 시작 − 15, 넘치기 직전 = 95(02 요약서 6.6절).</summary>
        public const int HeatMaxHu = 100 * SimConst.HuPerHeat;
        public const int ImminentBelowCritHu = 15 * SimConst.HuPerHeat;
        public const int BrinkHu = 95 * SimConst.HuPerHeat;

        // ------------------------------------------------------------ 날·웨이브

        /// <summary>날 = DayCycle.Day(wave)(= (wave + 9) / 10, TV-042).</summary>
        public static int Day(int wave) => DayCycle.Day(wave);
        /// <summary>보스 웨이브 = wave % 10 == 0.</summary>
        public static bool IsBossWave(int wave) => DayCycle.IsBossWave(wave);

        /// <summary>날·웨이브 줄의 자리: 보통 (760,36) 400×40 32px / 보스 웨이브 (760,76) 400×24 16px(TV-046).</summary>
        public static HudRect DayWaveRect(bool bossWave) => bossWave ? HudLayout.DayWaveBoss : HudLayout.DayWave;

        public static string DayWaveText(INameTable names, int wave)
        {
            if (names == null) return string.Empty;
            return names.Format(KeyDayWave, "day", Day(wave), "wave", wave);
        }

        /// <summary>
        /// 허상까지 n웨이브(0 = 줄 없음). 판 안의 복수 중 여부는 w.Run.RevengeActive(판에서 첫 만남을 치른 바로 그 판 포함, 00 문서 5.5절)로
        /// RevengeTracker에 넘긴다. TV-043: 웨이브 14 → 6, TV-044: 10/19/20 → 없음/1/없음, TV-045: 만나기 전 → 없음.
        /// </summary>
        public static int RevengeWavesLeft(ProfileData profile, IContentDb db, RunState run)
        {
            if (run == null) return 0;
            return RevengeTracker.Build(profile, db, run.Wave, metThisRun: run.RevengeActive).HudWavesLeft;
        }

        public static string RevengeText(INameTable names, int wavesLeft)
        {
            if (names == null || wavesLeft <= 0) return string.Empty;
            return names.Format(KeyRevengeHud, "n", wavesLeft);
        }

        /// <summary>웨이브 진행 바: 남은 적 체력 비율 → 0..1000‰.</summary>
        public static int WaveProgressPermille(float remainingRatio)
        {
            if (float.IsNaN(remainingRatio)) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(remainingRatio * 1000f), 0, 1000);
        }

        /// <summary>판의 경과 틱(야영 제외): 끝낸 웨이브의 점수 시계 합 + 지금 웨이브의 점수 시계(WaveTick은 WaveCombat 동안만 흐른다).</summary>
        public static int RunElapsedTicks(SimWorld w)
        {
            if (w == null) return 0;
            int sum = 0;
            bool current = false;
            var rs = w.Run.WaveResults;
            for (int i = 0; i < rs.Count; i++)
            {
                var r = rs[i];
                if (r == null) continue;
                if (r.Stats != null) sum += r.Stats.ElapsedTicks;
                if (r.Wave == w.Wave.Wave) current = true;
            }
            if (!current) sum += Mathf.Max(0, w.Wave.WaveTick);
            return sum;
        }

        /// <summary>경과 시간 글(분:초, 숫자만이라 이름 표를 거치지 않는다).</summary>
        public static string ElapsedText(int ticks)
        {
            if (ticks < 0) ticks = 0;
            int sec = ticks / SimConst.TicksPerSecond;
            return (sec / 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                   (sec % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ 표시 여부

        /// <summary>웨이브·지갑·점수 요소를 보이는 단계인가(연습·튜토리얼 방 Practice에서는 숨긴다).</summary>
        public static bool ShowsRunElements(RunPhase phase) => phase != RunPhase.Practice && phase != RunPhase.NotStarted;

        /// <summary>웨이브 진행 바가 보이는 단계(웨이브를 싸우는 동안).</summary>
        public static bool WaveProgressPhase(RunPhase phase) =>
            phase == RunPhase.WavePrelude || phase == RunPhase.BossIntro || phase == RunPhase.WaveCombat || phase == RunPhase.WaveClearing;

        /// <summary>"지금 K" 줄이 뜻 있는 단계(발사·비기가 되는 단계와 보스 등장 중).</summary>
        public static bool NowKPhase(RunPhase phase) =>
            phase == RunPhase.WavePrelude || phase == RunPhase.BossIntro || phase == RunPhase.WaveCombat || phase == RunPhase.Practice;

        /// <summary>출발 안내: 야영 전장(FireIdle·DepartHold)에서 활동 중인 플레이어가 출발 표식 반경 안에 있을 때만(D-52).</summary>
        public static bool ShowDepartHint(SimWorld w)
        {
            if (w == null || !w.InCamp) return false;
            var me = w.Me;
            if (me == null || !me.Active) return false;
            var mine = w.Run.Rewards[me.Slot];
            if (mine.CampSub == CampSubState.CampScreen || mine.DepartReady) return false;
            float r = w.Run.DepartMarkerRadius;
            if (r <= 0f) return false;
            return (me.Pos - w.Run.DepartMarkerPos).sqrMagnitude <= r * r;
        }

        /// <summary>협동 야영: 내가 출발 준비를 마치고 동료를 기다리는 중이면 true(준비된 수·기다리는 수).</summary>
        public static bool WaitingParty(SimWorld w, out int ready, out int total)
        {
            ready = total = 0;
            if (w == null || !w.InCamp) return false;
            var me = w.Me;
            if (me == null || !me.Active || !w.Run.Rewards[me.Slot].DepartReady) return false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                if (p == null || !p.Active || p.Left || p.Downed) continue;
                total++;
                if (w.Run.Rewards[i].DepartReady) ready++;
            }
            return total > 1;
        }

        /// <summary>콤보 자리: 옵션 combo_position이 hud이면 초상 옆, 그 밖(near_character, 기본)이면 캐릭터 옆.</summary>
        public static bool ComboInHud(ViewOptions o) => o != null && o.ComboPosition == OptionValues.Hud;

        /// <summary>과열 잠금 남은 초(올림). 안내 "과열 잠금 {n}초".</summary>
        public static int HeatLockSecondsLeft(int unlockTick, int tick)
        {
            int left = unlockTick - tick;
            if (left <= 0) return 0;
            return (left + SimConst.TicksPerSecond - 1) / SimConst.TicksPerSecond;
        }

        /// <summary>잠금 남은 비율(1 = 막 잠김, 0 = 풀림). LockTotal = UnlockTick − LockStartTick(02 요약서 6.5절).</summary>
        public static float LockRemainRatio(int lockStartTick, int unlockTick, int tick)
        {
            int total = unlockTick - lockStartTick;
            if (total <= 0) return 0f;
            return Mathf.Clamp01((unlockTick - tick) / (float)total);
        }

        /// <summary>한 프레임의 HUD 값을 상태에서 계산한다(슬롯 0이 HUD의 주인. 캐릭터 둘레는 플레이어마다 따로).</summary>
        public static HudFrame Compute(SimWorld w, ProfileData profile, IContentDb db, ViewOptions options)
        {
            var f = new HudFrame();
            if (w == null) return f;
            var run = w.Run;
            f.Phase = run.Phase;
            f.InPractice = run.Phase == RunPhase.Practice;
            f.Wave = run.Wave;
            f.Day = run.Wave > 0 ? Day(run.Wave) : 0;
            f.BossWave = IsBossWave(run.Wave);
            bool runElems = ShowsRunElements(run.Phase) && run.Wave > 0;

            f.ShowBoss = w.Boss != null;
            f.ShowWaveProgress = runElems && WaveProgressPhase(run.Phase) && !f.BossWave && !f.ShowBoss;
            f.WaveProgressPermille = WaveProgressPermille(w.Wave.RemainingRatio);

            f.ShowDayWave = runElems;
            f.DayWaveBossLayout = f.BossWave;
            var plan = PlanFor(w);
            f.TodId = plan != null && !string.IsNullOrEmpty(plan.Tod) ? plan.Tod : (run.Wave > 0 ? DayCycle.Tod(run.Wave) : null);
            if (plan != null)
            {
                if (plan.Whims.Count > 0) f.Whim0 = plan.Whims[0];
                if (plan.Whims.Count > 1) f.Whim1 = plan.Whims[1];
            }

            f.ShowElapsed = runElems;
            f.ElapsedTicks = RunElapsedTicks(w);
            f.RevengeWavesLeft = runElems ? RevengeWavesLeft(profile, db, run) : 0;

            f.ShowWallet = runElems;
            f.Purse = run.Purse;
            f.Score = run.Score;
            f.ScorePending = ScorePending(w, db);
            int kills = 0;
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i] != null && w.Players[i].Active) kills += w.Players[i].Kills;
            f.Kills = kills > 0 ? kills : run.TotalKills;

            var p0 = w.Me;
            if (p0 != null && p0.Active && p0.Locked && p0.UnlockTick > w.Tick)
            {
                f.StateNotice = HudStateNotice.HeatLock;
                f.HeatLockSecondsLeft = HeatLockSecondsLeft(p0.UnlockTick, w.Tick);
                f.HeatLockRemainRatio = LockRemainRatio(p0.LockStartTick, p0.UnlockTick, w.Tick);
            }
            else if (WaitingParty(w, out f.PartyReady, out f.PartyTotal)) f.StateNotice = HudStateNotice.WaitingParty;
            else if (ShowDepartHint(w)) f.StateNotice = HudStateNotice.DepartHint;

            f.ComboInHud = ComboInHud(options);
            return f;
        }

        /// <summary>HUD "+n": 웨이브 전투 중일 때만 시뮬의 진행 중 몫(읽기 전용 규칙 함수)을 그대로 읽는다. 처치 이벤트로 추정하지 않는다.</summary>
        public static int ScorePending(SimWorld w, IContentDb db)
        {
            if (w == null || w.Run.Phase != RunPhase.WaveCombat || w.Wave == null || w.Wave.Stats == null || w.Mods == null) return 0;
            int v = w.Mods.Score.WaveProgressScore(db ?? w.Content, w.Wave.Stats);
            return v > 0 ? v : 0;
        }

        /// <summary>"+n" 글(str.battle_hud.score_pending).</summary>
        public static string ScorePendingText(INameTable names, int n)
        {
            if (names == null) return "+" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return names.Format(KeyScorePending, "n", names.Number(n));
        }

        /// <summary>지금 웨이브(또는 야영에서 다음에 싸울 웨이브)의 구성.</summary>
        public static WavePlan PlanFor(SimWorld w)
        {
            if (w == null) return null;
            int wave = w.Run.Wave;
            if (w.Wave.Plan != null && w.Wave.Plan.Wave == wave) return w.Wave.Plan;
            if (w.Run.NextPlan != null && w.Run.NextPlan.Wave == wave) return w.Run.NextPlan;
            return w.Wave.Plan;
        }

        // ------------------------------------------------------------ 지갑·체력·지팡이

        public static string WalletRow(INameTable names, string labelKey, long value)
        {
            if (names == null) return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return names.Get(labelKey) + " " + names.Number(value);
        }

        public static string WandStageText(INameTable names, int stage) => names != null ? names.Format(KeyWandStage, "n", stage) : string.Empty;

        /// <summary>체력 1칸 남음(귀퉁이 위험색 허용).</summary>
        public static bool HpLastCell(PlayerState p) => p != null && p.HpCells == 1;

        /// <summary>회로 칸 수 = 코어 1 + 연결 칸(최대 8).</summary>
        public static int CircuitCells(PlayerState p)
        {
            if (p == null) return 0;
            int links = p.Loadout != null && p.Loadout.Links != null ? p.Loadout.Links.Length : 0;
            return Mathf.Min(HudLayout.CircuitMaxCells, 1 + Mathf.Max(0, links));
        }

        /// <summary>회로 칸 i의 블록 id(0 = 코어, 1.. = 연결). 빈 칸이면 null.</summary>
        public static string CircuitBlock(PlayerState p, int i)
        {
            if (p == null || p.Loadout == null) return null;
            if (i == 0) return p.Loadout.Core.IsEmpty ? null : p.Loadout.Core.BlockId;
            var links = p.Loadout.Links;
            if (links == null || i - 1 >= links.Length) return null;
            return links[i - 1].IsEmpty ? null : links[i - 1].BlockId;
        }

        // ------------------------------------------------------------ 열 링 (02 요약서 6.6절)

        /// <summary>
        /// 열 링 상태. 잠금 &gt; 차가움(열 0) &gt; 넘치기 직전(95 ≤ 열 ≤ 100) &gt; 임계(임계 시작 ≤ 열 &lt; 95) &gt; 임박(임계 시작 − 15 ≤ 열) &gt; 오르는 중.
        /// 잠금이 아닌데 100을 넘는 값은 넘치기 직전으로 본다.
        /// </summary>
        public static HeatRingState RingState(int heatHu, int critStartHu, bool locked)
        {
            if (locked) return HeatRingState.Locked;
            if (heatHu <= 0) return HeatRingState.Cold;
            if (heatHu >= BrinkHu) return HeatRingState.Brink;
            if (heatHu >= critStartHu) return HeatRingState.Critical;
            if (heatHu >= critStartHu - ImminentBelowCritHu) return HeatRingState.Imminent;
            return HeatRingState.Rising;
        }

        /// <summary>플레이어의 임계 시작(HU). Traits가 채운 Derived가 있으면 그것, 없으면 캐릭터 정의의 crit_start.</summary>
        public static int CritStartHu(PlayerState p)
        {
            if (p == null) return HeatMaxHu;
            if (p.Derived.CritStartHu > 0) return p.Derived.CritStartHu;
            if (p.Def != null && p.Def.CritStart > 0f) return SimConst.HeatToHu(p.Def.CritStart);
            return HeatMaxHu;
        }

        public static HeatRingState RingState(PlayerState p) => p == null ? HeatRingState.Cold : RingState(p.HeatHu, CritStartHu(p), p.Locked);

        /// <summary>열 비율(0..1).</summary>
        public static float HeatRatio(int heatHu) => Mathf.Clamp01(heatHu / (float)HeatMaxHu);

        /// <summary>링 위의 각(도, 12시에서 시계 방향) — 임계 시작 눈금 자리.</summary>
        public static float RingAngleDegrees(int heatHu) => HeatRatio(heatHu) * 360f;

        // ------------------------------------------------------------ 콤보 (10 요약서 1.3절)

        /// <summary>콤보 막대의 남은 비율 = 1 − idle/120(0..1).</summary>
        public static float ComboBarRatio(int comboIdleTicks) =>
            Mathf.Clamp01(1f - comboIdleTicks / (float)HudLayout.ComboTimeoutTicks);

        /// <summary>다음 결정까지 남은 콤보(15마다, 제자 2는 12마다).</summary>
        public static int ComboLeftToCrystal(int combo, int perCrystal)
        {
            if (perCrystal <= 0) return int.MaxValue;
            if (combo < 0) combo = 0;
            return perCrystal - combo % perCrystal;
        }

        /// <summary>결정 직전(남은 콤보 3 이하): 숫자 옆 작은 결정 윤곽이 차오른다.</summary>
        public static bool ComboNearCrystal(int combo, int perCrystal) =>
            combo > 0 && perCrystal > 0 && ComboLeftToCrystal(combo, perCrystal) <= HudLayout.ComboNearCrystalLeft;

        /// <summary>결정 윤곽의 차오름(0..1) = 이번 결정까지 모은 비율.</summary>
        public static float ComboCrystalFill(int combo, int perCrystal) =>
            perCrystal <= 0 ? 0f : Mathf.Clamp01((combo % perCrystal) / (float)perCrystal);

        /// <summary>5·10 단위 색 단계: 0 = 흰색(5 미만), 1 = 보조색(5 이상), 2 = 주색(10 이상).</summary>
        public static int ComboTier(int combo) => combo >= 10 ? 2 : combo >= 5 ? 1 : 0;

        /// <summary>콤보 숫자의 자리 수 글리프(0~9 프레임 번호), 앞자리부터.</summary>
        public static int ComboDigits(int combo, int[] into)
        {
            if (combo < 0) combo = 0;
            string s = combo.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int n = Mathf.Min(s.Length, into != null ? into.Length : 0);
            for (int i = 0; i < n; i++) into[i] = s[i] - '0';
            return n;
        }

        // ------------------------------------------------------------ 지금 K (04 요약서 6절)

        /// <summary>늦음 초를 소수 둘째 자리 올림한 1/100초 수: ceil(LateBy × 100 / 15360). 153u → 1, 307u → 2, 1,075u → 7, 1u → 1.</summary>
        public static int LateCentiFromU(long lateU)
        {
            if (lateU <= 0) return 0;
            long num = lateU * 100L;
            return (int)((num + SimConst.UnitsPerSecond - 1) / SimConst.UnitsPerSecond);
        }

        /// <summary>초(ArtPreview.LateSeconds, ArtFizzled.F)에서 같은 올림. 이미 올림한 값이 와도 그대로 남게 아주 작은 오차는 내린다.</summary>
        public static int LateCentiFromSeconds(float seconds)
        {
            if (!(seconds > 0f)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(seconds * 100f - 1e-3f));
        }

        /// <summary>1/100초 수 → "0.04".</summary>
        public static string FormatCenti(int centi)
        {
            if (centi < 0) centi = 0;
            return (centi / 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." +
                   (centi % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>머리 위 "n번째 입력이 x초 늦음"(만개 간격 초과 불발).</summary>
        public static string LateInputText(INameTable names, int index, float lateSeconds)
        {
            if (names == null) return string.Empty;
            return names.Format(KeyLateInput, "n", index, "sec", FormatCenti(LateCentiFromSeconds(lateSeconds)));
        }

        /// <summary>
        /// "지금 K" 글. 한 줄 = "K = {art} (◆×{n})", 외상이면 "{line} 외상 {n}". 불발 자리는 기술 이름 대신 불발 표시
        /// (만개 간격 초과면 "불발(n번째 입력이 x초 늦음)"). 해금된 섬 기술이 있으면 윗줄 = "섬:" + 섬 기술 줄(U-29).
        /// </summary>
        public static NowKText NowK(INameTable names, in ArtPreview pv)
        {
            var t = new NowKText();
            if (names == null) return t;
            if (string.IsNullOrEmpty(pv.ArtId) && !pv.Fizzle) return t;
            t.Visible = true;
            t.Fizzle = pv.Fizzle;
            string art;
            if (pv.Fizzle)
            {
                art = pv.Reason == ArtFizzleReason.FullBloomLate && pv.LateIndex > 0
                    ? names.Format(KeyNowKFizzleLate, "n", pv.LateIndex, "sec", FormatCenti(LateCentiFromSeconds(pv.LateSeconds)))
                    : names.Get(KeyNowKFizzle);
            }
            else art = names.Get(pv.ArtId);
            string line = names.Format(KeyNowK, "art", art, "n", pv.Cost);
            if (!pv.Fizzle && pv.DebtUsed > 0) line = names.Format(KeyNowKDebt, "line", line, "n", pv.DebtUsed);
            t.Main = line;
            if (pv.HasJustLine && !string.IsNullOrEmpty(pv.JustArtId))
                t.Just = names.Get(KeyNowKJust) + " " + names.Format(KeyNowK, "art", names.Get(pv.JustArtId), "n", pv.JustCost);
            else if (!string.IsNullOrEmpty(pv.FinalArtId))
                t.Just = names.Format(KeyNowKFinal, "art", names.Get(pv.FinalArtId), "n", pv.FinalCost);
            return t;
        }

        /// <summary>
        /// 재사용 대기 테두리: 둘레를 왼쪽 위에서 시계 방향으로 ratio만큼 채운 네 변의 길이(위, 오른, 아래, 왼).
        /// 0.4초 동안 다시 참(04 요약서 7절).
        /// </summary>
        public static void PerimeterFill(float ratio, float w, float h, out float top, out float right, out float bottom, out float left)
        {
            float len = Mathf.Clamp01(ratio) * 2f * (w + h);
            top = Mathf.Min(len, w); len -= top;
            right = Mathf.Clamp(len, 0f, h); len -= right;
            bottom = Mathf.Clamp(len, 0f, w); len -= bottom;
            left = Mathf.Clamp(len, 0f, h);
        }

        // ------------------------------------------------------------ 보스 (05 요약서 8.3절, 08 6.1절)

        /// <summary>단계 눈금(%) = 단계의 enter_at_hp_percent(100 제외). 첫 만남이면 없음(50% 멈춤을 눈금·자물쇠로 미리 알리지 않는다, TV-047).</summary>
        public static List<int> BossTickPercents(BossDef def, bool firstEncounter)
        {
            var l = new List<int>();
            if (def == null || firstEncounter || def.Phases == null) return l;
            for (int i = 0; i < def.Phases.Count; i++)
            {
                int pct = def.Phases[i] != null ? def.Phases[i].EnterAtHpPercent : 100;
                if (pct > 0 && pct < 100 && !l.Contains(pct)) l.Add(pct);
            }
            return l;
        }

        /// <summary>눈금 x(바 왼쪽 기준 px, 눈금 가운데).</summary>
        public static float BossTickX(int percent, float barWidth) => barWidth * percent / 100f;

        public static float BossHpRatio(EnemyState e)
        {
            if (e == null || e.MaxHp <= 0f) return 0f;
            return Mathf.Clamp01(e.Hp / e.MaxHp);
        }

        /// <summary>복수 꼬리표: 복수 중인 판의 웨이브 20 허상(첫 만남 아님). BossIntroBegin의 A와 같은 판단을 상태로 한다(Build용).</summary>
        public static bool RevengeTagFromState(SimWorld w)
        {
            if (w == null || w.Boss == null || w.Boss.Def == null) return false;
            return w.Boss.Def.Id == Ids.Boss.MirrorPhantom && !w.Boss.FirstEncounter && w.Run.RevengeActive
                   && w.Boss.Wave == RevengeTracker.PhantomWave;
        }

        /// <summary>격노 글: 최종 격노면 "최종 격노", 쌓임이 있으면 "격노 {n}", 없으면 빈 글.</summary>
        public static string BossRageText(INameTable names, BossState b)
        {
            if (names == null || b == null) return string.Empty;
            if (b.FinalRage) return names.Get(KeyBossFinalRage);
            if (b.RageStacks > 0) return names.Format(KeyBossRage, "n", b.RageStacks);
            return string.Empty;
        }

        public static string MasterAdviceKey(int line) => KeyMasterAdviceLinePrefix + line.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // ------------------------------------------------------------ 결정 (10 요약서 2.3절)

        /// <summary>결정 칸 i의 꼴: 0 빈 칸 / 1 찬 칸 / 2 빚 칸(그믐, 빈 칸에 보라 테). 빚이 있으면 결정은 0개다(불변식).</summary>
        public static int CrystalSlotKind(int index, int crystals, int debt)
        {
            if (index < crystals) return 1;
            if (debt > 0 && index < debt) return 2;
            return 0;
        }

        /// <summary>결정 칸 수 = 그 캐릭터의 상한(4~7).</summary>
        public static int CrystalCells(PlayerState p)
        {
            if (p == null) return 0;
            int cap = p.Def != null ? p.Def.CrystalCap : 0;
            return Mathf.Max(0, Mathf.Max(cap, Mathf.Max(p.Crystals, p.Debt)));
        }

        /// <summary>캐릭터 발밑(판정 중심 + 0.4칸, 시뮬레이션 y 아래 +). 02 요약서 4절.</summary>
        public static Vector2 FeetOf(Vector2 hitCenter) => hitCenter + new Vector2(0f, FeetOffset);
        public const float FeetOffset = 0.4f;

        /// <summary>시야 화살표 색: 보통 반투명 / 자세 불투명 / 반전 옅은 파랑 RGB(0.56, 0.70, 1.0).</summary>
        public static Color ArrowColor(FacingArrowMode mode, float normalAlpha)
        {
            switch (mode)
            {
                case FacingArrowMode.Stance: return HudColors.ArrowStance;
                case FacingArrowMode.Reverse: return HudColors.ArrowReverse;
                default: return new Color(HudColors.ArrowNormal.r, HudColors.ArrowNormal.g, HudColors.ArrowNormal.b, normalAlpha);
            }
        }

        /// <summary>표시 위치 보간(10 요약서 8절 Q-17).</summary>
        public static Vector2 Interp(PlayerState p, float alpha) => p == null ? Vector2.zero : ViewMath.Lerp(p.PrevPos, p.Pos, alpha);

        /// <summary>속성 id → 결정 그림 열쇠 접미사(player/crystal_&lt;el&gt;). 무 속성은 null.</summary>
        public static string ElementSuffix(string elementId)
        {
            if (string.IsNullOrEmpty(elementId) || !elementId.StartsWith("element.", StringComparison.Ordinal)) return null;
            string s = elementId.Substring("element.".Length);
            return s == "neutral" ? null : s;
        }
    }
}

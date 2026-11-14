using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.World
{
    /// <summary>몸 클립을 어떻게 돌리는가.</summary>
    public enum ClipPlay
    {
        /// <summary>manifest의 반복 여부대로 지역 시계로 돈다(서기·걷기).</summary>
        Loop = 0,
        /// <summary>사건 시각부터 한 번 돈다(발사·비기·피격·쓰러짐). 끝나면 마지막 장에 머문다.</summary>
        Once = 1,
        /// <summary>한 번을 거꾸로 돈다(일어남 — 그림이 없어 쓰러짐을 거꾸로).</summary>
        Reverse = 2,
        /// <summary>첫 장에 멈춘다(자세로 서 있음).</summary>
        Hold = 3
    }

    /// <summary>클립 고르기의 입력(상태 + 사건 창). 시뮬레이션 상태와 이벤트에서 층이 채운다.</summary>
    public struct BodyInput
    {
        public Dir8 Facing;
        public bool Moving;
        public bool InStance;
        public bool Downed;
        /// <summary>일어남 연출 중(PlayerRose 뒤 쓰러짐 클립 길이만큼).</summary>
        public bool Rising;
        public bool HitActive;
        public bool ArtActive;
        /// <summary>쓴 비기가 그 캐릭터의 고유 비기다.</summary>
        public bool ArtIsSignature;
        public bool FireActive;
    }

    /// <summary>고른 몸 클립.</summary>
    public struct ClipChoice
    {
        public string Clip;
        public Facing3 Facing;
        public bool FlipX;
        public ClipPlay Play;
        /// <summary>지팡이를 내민다(자세·발사·비기 중 — 09 3절 "aim = 자세 걷기·발사").</summary>
        public bool WandAim;
        /// <summary>손 지팡이를 그리지 않는다(쓰러짐 마지막 장이 지팡이만 남은 몸이다 — 09 2.2절).</summary>
        public bool HideWand;
    }

    /// <summary>지팡이 열 반응 상태(임계·잠금). wands/*.md 4.4절, 02 요약서 6.6절.</summary>
    public enum WandHeatLook { Normal = 0, Critical = 1, Locked = 2 }

    /// <summary>
    /// G14의 순수 규칙(Unity 그리기 없이 시험한다): 방향 대응, 클립 고르기, 발 위치, 타일 배치 번호, 출발 고리 비율,
    /// 옵션 → 그릴 것, 시간대 고르기.
    /// </summary>
    public static class WorldViewRules
    {
        // ------------------------------------------------------------ 방향

        /// <summary>
        /// Dir8 → 그림 세 방향 + 좌우 뒤집기. 대각은 **좌우 우선**: NE·SE → 오른쪽, NW·SW → 오른쪽 뒤집음.
        /// N → 위, S → 아래, W → 오른쪽 뒤집음(09 1절: 왼쪽은 right를 뒤집는다).
        /// 까닭: 대각 이동은 좌우 성분이 늘 있고, 옆 그림이 이동 방향(사선)을 더 잘 읽히게 한다(위·아래 그림은 정면·뒷면이라 좌우가 안 보인다).
        /// </summary>
        public static Facing3 ToFacing3(Dir8 d, out bool flipX)
        {
            switch (d)
            {
                case Dir8.E: case Dir8.NE: case Dir8.SE: flipX = false; return Facing3.Right;
                case Dir8.W: case Dir8.NW: case Dir8.SW: flipX = true; return Facing3.Right;
                case Dir8.N: flipX = false; return Facing3.Up;
                default: flipX = false; return Facing3.Down;
            }
        }

        // ------------------------------------------------------------ 위치

        /// <summary>판정 중심(시뮬레이션 좌표) → 발 기준점(시뮬레이션 좌표): 0.4칸 아래(y +).</summary>
        public static Vector2 FeetSim(Vector2 center, float feetBelowCells) => new Vector2(center.x, center.y + feetBelowCells);

        /// <summary>판정 중심 → 발 기준점의 Unity 좌표(전장 높이에서 y를 뒤집는다). 예 (12,7), 0.4, 14 → (12, 6.6).</summary>
        public static Vector3 FeetView(Vector2 center, float feetBelowCells, float arenaHeight) =>
            SimConst.ToView(FeetSim(center, feetBelowCells), arenaHeight);

        /// <summary>보간한 판정 중심(ViewMath.Lerp: 한 틱 1칸 넘게 움직이면 보간 없음 — 10 요약서 Q-17).</summary>
        public static Vector2 InterpolatedCenter(PlayerState p, float alpha) => ViewMath.Lerp(p.PrevPos, p.Pos, alpha);

        /// <summary>프레임 안 점(px, 왼쪽 위 원점) 사이 차이 → Unity 좌표 차이(칸). 뒤집으면 x 차이 부호만 바꾼다(09 1절 AnchorMirror).</summary>
        public static Vector2 PixelDelta(PixelPoint from, PixelPoint to, bool flipX)
        {
            float dx = (to.X - from.X) / (float)SimConst.PixelsPerUnit;
            float dy = -(to.Y - from.Y) / (float)SimConst.PixelsPerUnit;
            return new Vector2(flipX ? -dx : dx, dy);
        }

        /// <summary>
        /// 지팡이 칸 안의 점(32×64 칸 px) → 손잡이 기준점(지팡이 sprite의 pivot)에서의 Unity 차이(칸).
        /// 점은 픽셀 가운데(+0.5)로 잰다. 뒤집힌 지팡이는 x' = 31 − x [임시 U-04]인데, 손잡이(16)가 칸 가운데라
        /// 이는 손잡이 둘레 반전(x 차이 부호만 바뀜)과 같다: (31 − x + 0.5 − 16) = −(x + 0.5 − 16).
        /// </summary>
        public static Vector2 WandPointOffset(PixelPoint point, PixelPoint grip, bool flipX)
        {
            float dx = (point.X + 0.5f - grip.X) / SimConst.PixelsPerUnit;
            float dy = -(point.Y + 0.5f - grip.Y) / SimConst.PixelsPerUnit;
            return new Vector2(flipX ? -dx : dx, dy);
        }

        // ------------------------------------------------------------ 클립

        /// <summary>
        /// 상태 → 몸 클립(09 2.2절 클립 표). 우선순위: 쓰러짐 > 일어남 > 피격 > 비기 시전 > 발사 > 자세(걷기/멈춤) > 걷기 > 서기.
        /// 아래 방향만 있는 클립(death)은 방향·뒤집기 없이 Down. 자세 중 멈춰 있으면 자세 걷기 첫 장에 멈춘다.
        /// </summary>
        public static ClipChoice SelectClip(in BodyInput s, WorldTuning t)
        {
            var c = new ClipChoice();
            c.Facing = ToFacing3(s.Facing, out c.FlipX);
            if (s.Downed)
            {
                c.Clip = t.ClipDeath; c.Play = ClipPlay.Once; c.HideWand = true;
            }
            else if (s.Rising)
            {
                c.Clip = t.ClipDeath; c.Play = ClipPlay.Reverse; c.HideWand = true;
            }
            else if (s.HitActive) { c.Clip = t.ClipHit; c.Play = ClipPlay.Once; c.WandAim = s.InStance; }
            else if (s.ArtActive) { c.Clip = s.ArtIsSignature ? t.ClipSignature : t.ClipSpecial; c.Play = ClipPlay.Once; c.WandAim = true; }
            else if (s.FireActive) { c.Clip = t.ClipFire; c.Play = ClipPlay.Once; c.WandAim = true; }
            else if (s.InStance) { c.Clip = t.ClipStance; c.Play = s.Moving ? ClipPlay.Loop : ClipPlay.Hold; c.WandAim = true; }
            else if (s.Moving) { c.Clip = t.ClipWalk; c.Play = ClipPlay.Loop; }
            else { c.Clip = t.ClipIdle; c.Play = ClipPlay.Loop; }

            if (c.Clip != null && t.DownOnlyClips.Contains(c.Clip))
            {
                c.Facing = Facing3.Down;
                c.FlipX = false;
            }
            return c;
        }

        /// <summary>클립을 돈 시간(초) → 프레임 번호. Once는 마지막 장에 머물고, Reverse는 마지막 장부터 첫 장으로, Hold는 0.</summary>
        public static int FrameIndex(ClipPlay play, int frameCount, float fps, bool loop, float seconds)
        {
            if (frameCount <= 0) return 0;
            if (play == ClipPlay.Hold) return 0;
            int i = fps <= 0f ? 0 : (int)(Mathf.Max(0f, seconds) * fps);
            switch (play)
            {
                case ClipPlay.Loop:
                    return loop ? i % frameCount : Mathf.Min(i, frameCount - 1);
                case ClipPlay.Reverse:
                    return frameCount - 1 - Mathf.Min(i, frameCount - 1);
                default:
                    return Mathf.Min(i, frameCount - 1);
            }
        }

        // ------------------------------------------------------------ 무적·피격

        /// <summary>
        /// 무적 깜빡임(08 14.2절, 10 3.7절): 전체(EffectsLevel 0) = 초당 hz회 반투명 깜빡임, 줄임·최소 = 깜빡임 없이 고정 투명 + 흰 테두리.
        /// seconds = 무적이 시작된 뒤 흐른 초(깜빡임의 위상). 돌려주는 값 = 몸 불투명도, whiteOutline = 흰 테두리 두께(px, 0 = 없음).
        /// </summary>
        public static float InvulnAlpha(int effectsLevel, float seconds, WorldTuning t, out float whiteOutlinePx)
        {
            if (effectsLevel <= 0)
            {
                whiteOutlinePx = 0f;
                if (t.InvulnBlinkHz <= 0f) return 1f;
                // 초당 hz회 = 한 주기(1/hz초)의 앞 절반은 보임, 뒤 절반은 반투명.
                int half = (int)Mathf.Floor(Mathf.Max(0f, seconds) * t.InvulnBlinkHz * 2f);
                return (half & 1) == 0 ? 1f : t.InvulnBlinkAlpha;
            }
            whiteOutlinePx = t.InvulnReducedOutlinePx;
            return t.InvulnReducedAlpha;
        }

        /// <summary>피격 흰 번쩍의 양(0~1): 사건 뒤 hit_flash_seconds 동안 1에서 0으로 줄어든다.</summary>
        public static float HitFlashAmount(float secondsSinceHit, WorldTuning t)
        {
            if (secondsSinceHit < 0f || t.HitFlashSeconds <= 0f || secondsSinceHit >= t.HitFlashSeconds) return 0f;
            return 1f - secondsSinceHit / t.HitFlashSeconds;
        }

        // ------------------------------------------------------------ 지팡이

        /// <summary>
        /// 지팡이 효과 단계로 줄인 대기 입자 수(effects 9.3·18절): 전부 = 요청, 줄임 = 절반(올림), 최소 = 0.
        /// 본체 모양과 단계 표식은 단계와 무관하게 늘 그린다.
        /// </summary>
        public static int WandParticleBudget(string wandFxLevel, int requested)
        {
            if (requested <= 0) return 0;
            if (wandFxLevel == Meta.OptionValues.Minimal) return 0;
            if (wandFxLevel == Meta.OptionValues.Reduced) return (requested + 1) / 2;
            return requested;
        }

        /// <summary>지팡이 효과(빛·입자)에 곱하는 불투명도: wand_fx_opacity(0.2~1). 줄임·최소에서도 본체는 그대로다.</summary>
        public static float WandFxAlpha(ViewOptions o, float baseAlpha) => baseAlpha * Mathf.Clamp01(o != null ? o.WandFxOpacity : 1f);

        /// <summary>열 상태 → 지팡이 반응. 잠금이 먼저, 임계 = 열 ≥ 임계 시작(경계 포함).</summary>
        public static WandHeatLook HeatLook(PlayerState p)
        {
            if (p.Locked) return WandHeatLook.Locked;
            if (p.Derived.CritStartHu > 0 && p.HeatHu >= p.Derived.CritStartHu) return WandHeatLook.Critical;
            return WandHeatLook.Normal;
        }

        /// <summary>
        /// 잠금 중 다시 밝아지는 정도(0 = 다 꺼짐, 1 = 원래 색): rebright초마다 한 칸씩 밝아져 풀릴 때 1(wands/dalgu 4.4절).
        /// </summary>
        public static float LockBrightness(int tick, int lockStartTick, int unlockTick, float rebrightSeconds)
        {
            int total = unlockTick - lockStartTick;
            if (total <= 0) return 1f;
            int step = SimConst.Ticks(rebrightSeconds);
            if (step <= 0) return Mathf.Clamp01((tick - lockStartTick) / (float)total);
            int steps = Mathf.Max(1, Mathf.CeilToInt(total / (float)step));
            int done = Mathf.Clamp((tick - lockStartTick) / step, 0, steps);
            return done / (float)steps;
        }

        /// <summary>요청 입자 수(열 상태별, G14.json wand_idle_particles).</summary>
        public static int IdleParticleRequest(WandHeatLook look, WorldTuning t)
        {
            switch (look)
            {
                case WandHeatLook.Critical: return t.WandIdleParticlesCritical;
                case WandHeatLook.Locked: return t.WandIdleParticlesLocked;
                default: return t.WandIdleParticlesNormal;
            }
        }

        /// <summary>분리 조각을 그리는 단계인가(G14.json wand_detached_stages).</summary>
        public static bool DetachedVisible(string charId, int stage, WorldTuning t)
        {
            return charId != null && t.WandDetachedStages.TryGetValue(charId, out var r) && stage >= r.x && stage <= r.y;
        }

        /// <summary>흔적 조각 수 = min(최대, record_peak − stage), 흔적이 없으면 0(07 4.9절).</summary>
        public static int ScarCount(int stage, int recordPeak, int max) => stage < recordPeak ? Mathf.Min(max, recordPeak - stage) : 0;

        /// <summary>유리 지팡이 임시 하락의 흰 금 줄 수 = min(최대, 내려간 수)(07 10절).</summary>
        public static int CrackCount(int tempDrop, int max) => tempDrop > 0 ? Mathf.Min(max, tempDrop) : 0;

        /// <summary>확장 표식 수 = 연결 칸 총수 − 캐릭터 기본 칸(07 10절 "확장 칸 수만큼").</summary>
        public static int ExpansionCount(int linkSlotCount, int baseLinkSlots) => Mathf.Max(0, linkSlotCount - baseLinkSlots);

        // ------------------------------------------------------------ 덧그림

        /// <summary>입은 야생 물 단계(1 = 없음) → 붙일 단계들(이어 붙임 표 + 자기 단계, 오름차순).</summary>
        public static List<int> WildStainStages(int worn, WorldTuning t)
        {
            var r = new List<int>();
            if (worn < 2) return r;
            if (t.WildStainInheritance.TryGetValue(worn, out var prev))
                for (int i = 0; i < prev.Count; i++) if (!r.Contains(prev[i])) r.Add(prev[i]);
            if (!r.Contains(worn)) r.Add(worn);
            r.Sort();
            return r;
        }

        /// <summary>보강 층 열쇠: "wild-stain/char.N/stage-K/부품"(그림 창고가 char.N을 폴더로 바꾼다).</summary>
        public static string WildStainKey(string charId, int stage, string part) => "wild-stain/" + charId + "/stage-" + stage + "/" + part;

        // ------------------------------------------------------------ 전장

        /// <summary>날마다 바뀌는 맵 번호(PropVariant 0~5) → 맵 id. 범위 밖은 감아 돈다.</summary>
        public static string MapIdFor(int propVariant, WorldTuning t)
        {
            int n = t.MapIds.Count;
            if (n == 0) return null;
            int i = ((propVariant % n) + n) % n;
            return t.MapIds[i];
        }

        /// <summary>칸 (x, y)에서 폭 w칸 높이 h칸을 차지하는 소품이 가운데 비움 구역(가운데 cx×cy칸)에 걸치는가.</summary>
        public static bool OverlapsCenter(int x, int y, int w, int h, int arenaW, int arenaH, Vector2Int centerClear)
        {
            int x0 = (arenaW - centerClear.x) / 2, y0 = (arenaH - centerClear.y) / 2;
            int x1 = x0 + centerClear.x, y1 = y0 + centerClear.y;
            return x < x1 && x + w > x0 && y < y1 && y + h > y0;
        }

        /// <summary>칸 좌표 + 그림 기준점(origin px) → 기준점의 시뮬레이션 좌표. 예 64×64·(32,62) 칸 (3,0) → (3.5, 0.96875).</summary>
        public static Vector2 SpriteAnchorSim(Vector2Int tile, PixelPoint origin) =>
            new Vector2(tile.x + origin.X / (float)SimConst.PixelsPerUnit, tile.y + origin.Y / (float)SimConst.PixelsPerUnit);

        /// <summary>
        /// 지금 화면에 깔 시간대(상태만 본다 — Build가 이벤트 없이 고른다). 야영 중 = 다음 웨이브의 시간대(4초 전환의 목표),
        /// 웨이브 진행 중(Prelude~LootPick) = 그 웨이브의 시간대, 그 밖(연습·시작 전) = 낮.
        /// </summary>
        public static string TargetTod(SimWorld w)
        {
            if (w == null) return Ids.Tod.Day;
            var run = w.Run;
            if (w.InCamp)
            {
                if (run.NextPlan != null && !string.IsNullOrEmpty(run.NextPlan.Tod)) return run.NextPlan.Tod;
                int next = run.LastClearedWave > 0 ? run.LastClearedWave + 1 : Mathf.Max(1, run.Wave);
                return DayCycle.Tod(next);
            }
            switch (run.Phase)
            {
                case RunPhase.WavePrelude:
                case RunPhase.BossIntro:
                case RunPhase.WaveCombat:
                case RunPhase.PhantomFinale:
                case RunPhase.PhantomDowned:
                case RunPhase.WaveClearing:
                case RunPhase.LootPick:
                case RunPhase.RunEnded:
                    if (w.Wave != null && w.Wave.Plan != null && !string.IsNullOrEmpty(w.Wave.Plan.Tod)) return w.Wave.Plan.Tod;
                    if (w.Wave != null && w.Wave.Wave > 0) return DayCycle.Tod(w.Wave.Wave);
                    if (run.Wave > 0) return DayCycle.Tod(run.Wave);
                    return Ids.Tod.Day;
                default:
                    return Ids.Tod.Day;
            }
        }

        public static bool IsNightLike(string tod) => tod == Ids.Tod.Night || tod == Ids.Tod.Boss;

        /// <summary>
        /// 출발 누름의 남은 시간 고리 비율(0~1). 야영(Camp·StartCamp)의 DepartHold일 때만 (tick − 시작) / depart_hold 틱, 그 밖은 −1(그리지 않음).
        /// 0틱 0, 30틱 0.5, 60틱 1(06 4.3절, 08 8.2절 TV-072).
        /// </summary>
        public static float DepartHoldRatio(SimWorld w, CampConfig camp)
        {
            if (w == null || !w.InCamp || w.Run.CampSub != CampSubState.DepartHold || w.Run.DepartHoldStartTick < 0) return -1f;
            int hold = camp != null ? SimConst.Ticks(camp.DepartHoldSeconds) : 0;
            if (hold <= 0) return 1f;
            return Mathf.Clamp01((w.Tick - w.Run.DepartHoldStartTick) / (float)hold);
        }
    }

    /// <summary>
    /// 전장이 지금 그려야 할 것(상태에서 바로 고른다). ArenaLayer가 매 프레임 이것을 만들어 그린다. 시험은 이것을 본다.
    /// </summary>
    public struct ArenaScene
    {
        public string TodId;
        public string MapId;
        public bool InCamp;
        public bool ShowCampfire;
        public Vector2 FirePos, MarkerPos;
        public float FireRadius, MarkerRadius;
        /// <summary>−1 = 고리 없음.</summary>
        public float DepartRatio;
        public bool Shower, Fog;
        /// <summary>빗줄기·안개 입자(날씨 입자 옵션이 꺼지면 false). 물웅덩이(바닥)는 옵션과 무관하다.</summary>
        public bool DrawRain, DrawFog, DrawPuddles;
        public float FogEdgeCells;
        public bool NightLike, Dusk, BossMoon;
        public int DiscardedItemVariant;

        public static ArenaScene From(SimWorld w, ViewOptions options, WorldTuning t, IContentDb db)
        {
            var s = new ArenaScene();
            s.TodId = WorldViewRules.TargetTod(w);
            s.MapId = WorldViewRules.MapIdFor(w != null ? w.Weather.PropVariant : 0, t);
            s.NightLike = WorldViewRules.IsNightLike(s.TodId);
            s.Dusk = s.TodId == Ids.Tod.Dusk;
            s.BossMoon = s.TodId == Ids.Tod.Boss;
            if (w == null) { s.DepartRatio = -1f; s.DiscardedItemVariant = -1; return s; }

            s.InCamp = w.InCamp;
            s.ShowCampfire = w.InCamp;
            s.FirePos = w.Run.CampFirePos;
            s.MarkerPos = w.Run.DepartMarkerPos;
            s.FireRadius = w.Run.CampInteractRadius;
            s.MarkerRadius = w.Run.DepartMarkerRadius;
            s.DepartRatio = WorldViewRules.DepartHoldRatio(w, db != null ? db.Camp : null);

            s.Shower = w.Weather.Active.Contains(Ids.Event.Shower);
            s.Fog = w.Weather.Active.Contains(Ids.Event.Fog);
            bool particles = options == null || options.WeatherParticles;
            s.DrawRain = s.Shower && particles;
            s.DrawFog = s.Fog && particles;
            s.DrawPuddles = s.Shower;
            s.FogEdgeCells = 0f;
            if (db != null && db.WeatherEvents != null)
                for (int i = 0; i < db.WeatherEvents.Count; i++)
                    if (db.WeatherEvents[i].Id == Ids.Event.Fog) s.FogEdgeCells = db.WeatherEvents[i].EdgeWidth;
            s.DiscardedItemVariant = w.Weather.DiscardedItemVariant;
            return s;
        }
    }

    /// <summary>
    /// 시간대 전환(야영 중 4초 — 01 4.1절). 목표가 바뀌면 야영 중일 때만 tod_transition 틱에 걸쳐 섞고, 야영 밖에서는 바로 바꾼다.
    /// 처음(Build)에는 목표를 그대로 지금 값으로 둔다(이벤트 없이 상태만으로 그린다).
    /// </summary>
    public sealed class TodTransition
    {
        public string From { get; private set; }
        public string To { get; private set; }
        public int StartTick { get; private set; }
        public int LengthTicks { get; private set; }

        public void Reset(string tod)
        {
            From = To = tod;
            StartTick = 0;
            LengthTicks = 0;
        }

        /// <summary>목표를 넘긴다. 바뀌었으면 전환을 시작한다(야영 중) 또는 바로 바꾼다.</summary>
        public void Update(string target, int tick, bool inCamp, int transitionTicks)
        {
            if (To == null) { Reset(target); return; }
            if (target == To) return;
            if (inCamp && transitionTicks > 0)
            {
                // 전환 중에 또 바뀌면 지금 보이는 쪽(진행 절반 넘으면 To)에서 다시 시작한다.
                From = Progress(tick) >= 0.5f ? To : From;
                To = target;
                StartTick = tick;
                LengthTicks = transitionTicks;
            }
            else Reset(target);
        }

        /// <summary>0 = From만, 1 = To만.</summary>
        public float Progress(int tick)
        {
            if (LengthTicks <= 0 || From == To) return 1f;
            return Mathf.Clamp01((tick - StartTick) / (float)LengthTicks);
        }

        public bool Blending(int tick) => From != To && Progress(tick) < 1f;
    }
}

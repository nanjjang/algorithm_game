using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim
{
    /// <summary>
    /// 판 하나의 시뮬레이션 세계. MonoBehaviour가 아니다. GameRunner가 60Hz로 Step을 부르고, EditMode 테스트는 직접 Step을 부른다.
    /// 전역 상태가 없다: 세계 둘을 동시에 만들어도 서로 닿지 않는다. 플레이어는 배열(슬롯 순)이고 솔로는 슬롯 0만 Active다.
    /// 상태 목록은 누구나 읽지만, 쓰는 것은 주인 모듈뿐이다(00-architecture.md 5.2절 표, Modules.cs 머리의 공통 규칙).
    /// </summary>
    public sealed class SimWorld
    {
        public readonly IContentDb Content;
        public readonly SimModuleSet Mods;

        /// <summary>지금 처리 중인(또는 다음에 처리할) 틱 번호.</summary>
        public int Tick;
        public ulong Seed;
        public float ArenaWidth = 24f;
        public float ArenaHeight = 14f;

        public readonly PlayerState[] Players = new PlayerState[SimConst.MaxPlayers];
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<ProjectileEntity> Projectiles = new List<ProjectileEntity>();
        public readonly List<EnemyShotEntity> EnemyShots = new List<EnemyShotEntity>();
        public readonly List<ZoneEntity> Zones = new List<ZoneEntity>();
        public readonly List<HazardEntity> Hazards = new List<HazardEntity>();
        public readonly List<ObstacleEntity> Obstacles = new List<ObstacleEntity>();
        public readonly List<PlayerZoneEntity> PlayerZones = new List<PlayerZoneEntity>();
        public readonly List<DropEntity> Drops = new List<DropEntity>();

        public RunState Run = new RunState();
        public WaveState Wave = new WaveState();
        /// <summary>보스 싸움 중에만 있다(그 밖엔 null). Bosses.SpawnBoss가 만들고 Bosses.Dismiss가 null로 돌린다.</summary>
        public BossState Boss;
        public readonly StarfallState Starfall = new StarfallState();
        public readonly WeatherState Weather = new WeatherState();

        /// <summary>이 틱까지 쌓인 표시 이벤트. 러너가 틱 뒤에 DrainEvents로 가져간다.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>();
        /// <summary>슬롯별로 아직 처리하지 않은 입력(시간순). 2단계가 자기 틱의 것만 꺼낸다.</summary>
        public readonly List<InputEvent>[] InputQueues = new List<InputEvent>[SimConst.MaxPlayers];
        /// <summary>0단계가 적용할 화면 명령.</summary>
        public readonly List<RunCommand> Commands = new List<RunCommand>();

        readonly Pcg32[] _rng = new Pcg32[(int)RngStream.Count];
        int _nextEntityId = 1;
        int _nextCastId = 1;

        public SimWorld(IContentDb content, SimModuleSet mods, ulong seed = 1)
        {
            Content = content;
            Mods = mods ?? SimModuleSet.Stubs();
            Seed = seed;
            for (int i = 0; i < Players.Length; i++)
            {
                Players[i] = new PlayerState { Slot = i };
                InputQueues[i] = new List<InputEvent>();
            }
            var field = content != null ? content.Field : null;
            if (field != null) { ArenaWidth = field.Width; ArenaHeight = field.Height; }
        }

        /// <summary>판을 시작한다(Run.StartRun에 넘긴다). 씨앗은 cfg.Seed.</summary>
        public void StartRun(RunConfig cfg)
        {
            Seed = cfg.Seed;
            for (int i = 0; i < _rng.Length; i++) _rng[i] = null;
            Mods.Run.StartRun(this, cfg);
        }

        // ------------------------------------------------------------ 틱

        /// <summary>
        /// 한 틱을 진행한다. 단계 순서는 여기 하나뿐이다(02 요약서 2절, 05 요약서 4절).
        /// 일시정지는 러너가 Step을 부르지 않는 것으로 한다(틱을 멈추는 것은 일시정지뿐).
        /// </summary>
        public void Step()
        {
            var m = Mods;
            // 0. 야영·전리품·포기 명령
            m.Run.Stage0_Commands(this);
            // 1. 기한 처리(과열 잠금 풀림)
            m.Player.Stage1_Timers(this);
            // 2. 입력: 키 상태 → 자세 → 시야 → 커맨드 판정기
            m.Player.Stage2_Input(this);
            // 3. 대각 시야 유지 창 만료
            m.Player.Stage3_ResolveFacing(this);
            // 4. 플레이어 이동과 벽
            m.Player.Stage4_Move(this);
            m.Run.Stage4_DuelSync(this);
            // 5. 비기 발동, 플레이어 지대
            m.Arts.Stage5_Arts(this);
            // 6. J 발사
            m.Player.Stage6_Fire(this);
            // 7. 웨이브 방출 → 적 AI → 보스 AI (예고는 "요청"만)
            m.Waves.Stage7_Spawns(this);
            m.Enemies.Stage7_Ai(this);
            m.Bosses.Stage7_Ai(this);
            // 8. 예고 대기 줄
            m.Threats.Stage8_Queue(this);
            // 9. 이동과 충돌: 적 → 내 탄 → 적 탄 → 구역 → 스침·줍기
            m.Enemies.Stage9_Move(this);
            m.Spells.Stage9_Projectiles(this);
            m.Threats.Stage9_Shots(this);
            m.Threats.Stage9_Zones(this);
            m.Player.Stage9_GrazeAndPickups(this);
            // 10. 확정: 플레이어 피격 → 적 사망 → 보스 단계 (같은 틱이면 플레이어 피해가 먼저)
            m.Player.Stage10_ResolveHits(this);
            m.Enemies.Stage10_Deaths(this);
            m.Bosses.Stage10_AfterDamage(this);
            // 11. 틱 끝: 변이 창 → 냉각·콤보 타이머 → 웨이브 시계·별똥·날씨 → 판 상태 기계
            m.Traits.Stage11_TickEnd(this);
            m.Player.Stage11_TickEnd(this);
            m.Waves.Stage11_TickEnd(this);
            m.Run.Stage11_Phase(this);
            Compact();
            // 12.
            Tick++;
        }

        /// <summary>Dead 표시된 개체를 목록에서 뺀다(순서 유지).</summary>
        void Compact()
        {
            Enemies.RemoveAll(e => e.Dead);
            Projectiles.RemoveAll(p => p.Dead);
            EnemyShots.RemoveAll(s => s.Dead);
            Zones.RemoveAll(z => z.Cancelled || z.Detonated);
            Hazards.RemoveAll(h => h.Dead);
            Obstacles.RemoveAll(o => o.Dead);
            PlayerZones.RemoveAll(z => z.Dead);
            Drops.RemoveAll(d => d.Picked);
        }

        // ------------------------------------------------------------ 상태 질의

        public PlayerState Player(int slot) => Players[slot];
        /// <summary>솔로 편의: 슬롯 0. 규칙 코드는 이것 대신 Players를 돈다.</summary>
        public PlayerState P0 => Players[0];
        /// <summary>표시 전용: 이 화면이 "나"로 그리는 슬롯(솔로 = 0, 온라인 손님 = 서버가 준 슬롯). 시뮬 규칙은 읽지 않는다.</summary>
        public int ViewSlot;
        /// <summary>표시 전용: HUD·소리·힌트가 보는 내 캐릭터(Players[ViewSlot]).</summary>
        public PlayerState Me => Players[ViewSlot >= 0 && ViewSlot < Players.Length ? ViewSlot : 0];
        public long NowU => SimConst.TickStartU(Tick);

        /// <summary>전투가 도는 상태인가(적 AI·피해 판정).</summary>
        public bool CombatActive =>
            Run.Phase == RunPhase.WaveCombat || Run.Phase == RunPhase.Practice || Run.Phase == RunPhase.PhantomFinale || Run.Phase == RunPhase.DuelFight;
        /// <summary>플레이어가 움직일 수 있는 상태인가(전리품 고르기와 판 종료만 아니면).</summary>
        public bool PlayerControlActive =>
            Run.Phase != RunPhase.NotStarted && Run.Phase != RunPhase.RunEnded && Run.Phase != RunPhase.LootPick
            && Run.Phase != RunPhase.DuelCountdown && Run.Phase != RunPhase.DuelRoundEnd
            && !(InCamp && Run.CampSub == CampSubState.CampScreen);
        /// <summary>야영 중인가(Camp 또는 출발 야영 StartCamp). 둘 다 CampSub·모닥불·출발 표식을 쓴다.</summary>
        public bool InCamp => Run.Phase == RunPhase.Camp || Run.Phase == RunPhase.StartCamp;
        /// <summary>발사·비기가 되는 상태인가: WavePrelude·WaveCombat·Practice이고 AttackLocked·InputLocked·Downed가 아니다.
        /// Player.Stage6_Fire, Arts.OnSkillPress·Stage5_Arts가 이것 하나로 본다(Modules.cs 공통 규칙 7).</summary>
        public bool AttackAllowed(PlayerState p) =>
            (Run.Phase == RunPhase.WavePrelude || Run.Phase == RunPhase.WaveCombat || Run.Phase == RunPhase.Practice || Run.Phase == RunPhase.DuelFight)
            && p != null && p.Active && !p.AttackLocked && !p.InputLocked && !p.Downed;
        /// <summary>플레이어가 피해를 받는 상태인가(WaveClearing부터는 피해 판정 없음).</summary>
        public bool PlayerDamageActive => Run.Phase == RunPhase.WaveCombat || Run.Phase == RunPhase.Practice || Run.Phase == RunPhase.DuelFight;

        public EnemyState FindEnemy(int id)
        {
            for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Id == id) return Enemies[i];
            return null;
        }

        /// <summary>살아 있는 적 수. includeNoReward = 갈라진·불러낸 개체도 센다.</summary>
        public int AliveEnemies(bool includeNoReward = true)
        {
            int n = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead || e.PendingDeath) continue;
                if (!includeNoReward && e.NoReward) continue;
                n++;
            }
            return n;
        }

        /// <summary>가장 가까운 활동 중인 플레이어(없으면 null). 동률은 슬롯 순.</summary>
        public PlayerState NearestPlayer(Vector2 pos)
        {
            PlayerState best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                if (!p.Active || p.Downed) continue;
                float d = (p.Pos - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        public bool InArena(Vector2 pos, float margin = 0f) =>
            pos.x >= margin && pos.x <= ArenaWidth - margin && pos.y >= margin && pos.y <= ArenaHeight - margin;

        /// <summary>몸 반지름만큼 벽 안으로 축별로 자른다.</summary>
        public Vector2 ClampToArena(Vector2 pos, float radius) =>
            new Vector2(Mathf.Clamp(pos.x, radius, ArenaWidth - radius), Mathf.Clamp(pos.y, radius, ArenaHeight - radius));

        // ------------------------------------------------------------ 번호·난수·이벤트·입력

        /// <summary>적·탄·구역·드랍이 함께 쓰는 증가 번호(작을수록 먼저 생긴 것).</summary>
        public int NewEntityId() => _nextEntityId++;
        /// <summary>시전 묶음 번호(스침은 같은 시전에서 1회).</summary>
        public int NewCastId() => _nextCastId++;

        /// <summary>판 씨앗에서 갈라 낸 흐름별 난수기(판 동안 이어 쓴다).</summary>
        public Pcg32 Rng(RngStream stream)
        {
            int i = (int)stream;
            return _rng[i] ?? (_rng[i] = Pcg32.Derive(Seed, stream));
        }

        /// <summary>표시 이벤트를 쌓는다. 플레이어와 무관한 이벤트는 slot을 주지 않는다(-1).</summary>
        public void Emit(SimEventType type, int slot = -1, int entityId = 0, int a = 0, int b = 0, float f = 0f,
            Vector2 pos = default, string id = null, Vector2 pos2 = default)
        {
            Events.Add(new SimEvent
            {
                Type = type, Tick = Tick, Slot = slot, EntityId = entityId, A = a, B = b, F = f, Pos = pos, Pos2 = pos2, Id = id
            });
        }

        /// <summary>쌓인 이벤트를 into로 옮기고 비운다.</summary>
        public void DrainEvents(List<SimEvent> into)
        {
            into.AddRange(Events);
            Events.Clear();
        }

        public void EnqueueInput(int slot, in InputEvent e) => InputQueues[slot].Add(e);
        public void Enqueue(in RunCommand cmd) => Commands.Add(cmd);
    }
}

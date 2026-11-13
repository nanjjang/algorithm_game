using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Proto;
using Wizard.Sim.World;

namespace Wizard.Sim
{
    // 손맛 시제품(M1-SIM-09, GDD 16 1단계)의 임시 내용. 이 폴더(Proto/)의 파일 전부가 한 덩어리이고 M3-CB-02 가 통째로 지운다.
    //   SimWorld.ProtoSpell.cs   — 고정 투사체 2종(마력탄, 화염구)과 3갈래 전환: Stage00_Spell, Stage06_Spell, Stage09_Spell, Spell 절
    //   SimWorld.ProtoEnemies.cs — 슬라임 무한 생성, 3갈래 탄 포탑 1개: Stage07_Enemies, Stage09_Enemies, Stage10_Enemies, Enemies 절
    //   SimWorld.ProtoArts.cs    — 판정기 + 임시 효과의 기술 ①②⑥, 섬 간격 보고: Stage02_Arts, Stage05_Arts, Arts 절
    //
    // 붙는 길은 둘뿐이다(수락 조건): CONTRACTS 3.2 의 훅(위 단계 훅과 상태 훅. 뼈대가 선언한 부분 메서드의 구현)과 SimConfig 스위치.
    // 켜지는 조건은 SimConfig.Mode == Practice 이고 팩에 characters·blocks 절이 있을 때다(ProtoOn). 다른 모드에서는 모든 훅이 바로 돌아가고
    // 절에 아무것도 쓰지 않으므로(풀 절은 생략, 스칼라 절은 빈 내용) 무한 웨이브·결투·광장의 상태 바이트와 해시가 이 폴더의 유무와 무관하다.
    // 발사 스위치는 SimConfig.FireEnabled(Heat 가 본다), 비기 스위치는 SimConfig.SkillEnabled 다.
    // 뼈대와 다른 모듈의 파일은 이 폴더를 모른다. 이 폴더를 지우면 위 훅의 구현만 사라지고(구현 없는 부분 메서드는 호출이 컴파일에서 빠진다)
    // 나머지는 그대로 컴파일된다.
    //
    // 다른 모듈과의 이음(CONTRACTS 3.2 "모듈 사이의 이음"): 읽기 — PlayerAt, PlayerLastInput, PlayerFireDirection, PlayerInvulnerable,
    // HeatFiredThisTick, GrazeHitRadius. 연산 — SetHeatShot(Heat), QueuePlayerHit(Player), OfferGrazeThreat·ReleaseGrazeCast(Graze).
    // 남의 상태 배열을 쓰지 않는다.
    //
    // 시작 상태는 첫 Proto 훅이 한 번 세운다(EnsureProto). 5단계 훅은 틱마다 불리므로 첫 Step 의 6단계 발사 전에는 언제나 서 있다.
    // 세계 생성 때의 초기화 훅이 계약에 없는 것은 Player·Heat 와 같은 사정이다.
    public sealed partial class SimWorld
    {
        // ── 상태(Spell 절: 탄 풀, Enemies 절: 생성기·적·적 탄, Arts 절: 준비 표시와 조립, 판정기. 절 6 SpellTable 은 실제 표의 것이다, T-57)
        private readonly ProtoBullet[] _protoBullets = new ProtoBullet[ProtoTuning.BulletCapacity];
        private int _protoBulletCount;
        private readonly ProtoEnemy[] _protoEnemies = new ProtoEnemy[ProtoTuning.EnemyCapacity];
        private int _protoEnemyCount;
        private readonly ProtoEnemyShot[] _protoShots = new ProtoEnemyShot[ProtoTuning.ShotCapacity];
        private int _protoShotCount;
        private readonly ProtoLoadout[] _protoLoadouts = new ProtoLoadout[MaxPlayers];
        private bool _protoReady;
        private uint _protoNextSlimeTick;
        private int _protoLastCastId;                      // 포탑 시전 묶음 id(1부터)
        private readonly CommandParser[] _protoParsers = ProtoArts.NewParsers(MaxPlayers);

        // ── 캐시(상태 아님): 팩과 캐릭터에서 다시 얻을 수 있다. 첫 Proto 훅이 한 번 만든다
        private readonly ProtoWallet[] _protoWallets = NewProtoWallets();
        private readonly ShotHeatBlock[] _protoHeatBlocks = new ShotHeatBlock[ProtoTuning.SplitThreeCount];
        private ArenaGeometry? _protoArena;
        private bool _protoCachesReady;
        private UnlockSet _protoUnlocks;

        // ── 틱 안의 임시 값(상태 아님): 2단계의 K 판정을 5단계가 낸다(spec 01 3.8 "5. 비기 발동: 이번 틱에 판정된 K")
        private readonly ArtDecision[] _protoArtPending = new ArtDecision[MaxPlayers * ProtoTuning.ArtDecisionsPerTick];
        private readonly int[] _protoArtPendingCounts = new int[MaxPlayers];

        private static ProtoWallet[] NewProtoWallets()
        {
            var w = new ProtoWallet[MaxPlayers];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = new ProtoWallet();
            }
            return w;
        }

        // SimConfig 스위치: 연습 모드에서만 켜진다. 캐릭터와 블록이 없는 팩(뼈대 시험의 빈 팩)에서는 꺼진다
        internal bool ProtoOn =>
            _config.Mode == SimMode.Practice && PlayerModuleOn
            && _db.Pack.TryGetSection((ushort)ContentSection.Blocks, out _);

        internal bool ProtoReady => _protoReady;

        // ── 표시 계층과 시험이 읽는 값(ProtoReadout 이 공개한다)

        internal int ProtoBulletCount => _protoBulletCount;

        internal ref readonly ProtoBullet ProtoBulletAt(int index)
        {
            RequireProtoIndex(index, _protoBulletCount);
            return ref _protoBullets[index];
        }

        internal int ProtoEnemyCount => _protoEnemyCount;

        internal ref readonly ProtoEnemy ProtoEnemyAt(int index)
        {
            RequireProtoIndex(index, _protoEnemyCount);
            return ref _protoEnemies[index];
        }

        internal int ProtoShotCount => _protoShotCount;

        internal ref readonly ProtoEnemyShot ProtoShotAt(int index)
        {
            RequireProtoIndex(index, _protoShotCount);
            return ref _protoShots[index];
        }

        internal ProtoLoadout ProtoLoadoutOf(int index)
        {
            RequireProtoIndex(index, PlayerCount);
            return _protoLoadouts[index];
        }

        internal CommandParser ProtoParserOf(int index)
        {
            RequireProtoIndex(index, PlayerCount);
            return _protoParsers[index];
        }

        internal uint ProtoNextSlimeTick => _protoNextSlimeTick;

        // ── 시작 상태와 캐시

        // 캐시: 지갑의 상한(캐릭터 값), 전장 기하, 해금. ReadState 로만 상태를 받은 세계도 첫 훅에서 세운다
        private void EnsureProtoCaches()
        {
            if (_protoCachesReady)
            {
                return;
            }
            if (!PlayersSpawned)
            {
                EnsurePlayers();
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                CharacterId c = PlayerAt(i).Character;
                if (c != _protoParsers[i].Trie.Character)
                {
                    throw new SimInvariantException("proto: the practice character differs from the prototype trie");
                }
                _protoWallets[i].SetCap(_db.Character(c).CrystalCap);
            }
            _protoArena = new ArenaGeometry(_db.Arena(_mode));
            _protoUnlocks = ProtoArts.Unlocks();
            _protoCachesReady = true;
        }

        // 시작 상태(한 번): 조립 = 마력탄(3갈래 없음), 포탑 1개, 슬라임 생성 시계. 그리고 열 모듈에 주문을 넣는다
        private void EnsureProto(ISimEventSink sink)
        {
            EnsureProtoCaches();
            if (_protoReady)
            {
                return;
            }
            _protoReady = true;
            _protoBulletCount = 0;
            _protoEnemyCount = 0;
            _protoShotCount = 0;
            _protoLastCastId = 0;
            _protoNextSlimeTick = unchecked(_tick + (uint)ProtoTuning.SlimeSpawnIntervalTicks);
            for (int i = 0; i < PlayerCount; i++)
            {
                _protoLoadouts[i] = new ProtoLoadout { Core = BlockId.MagicBolt, SplitThree = false };
                ArmProtoShot(i);
            }
            SpawnProtoTurret(sink);
        }

        // 열 모듈에 지금 조립의 발사 열과 간격을 넣는다(spec 01 7.2 ShotHeatHu, HT-1: 같은 스코프의 첫 분열 배수)
        private void ArmProtoShot(int i)
        {
            ProtoLoadout l = _protoLoadouts[i];
            BlockDef core = _db.Block(l.Core);
            int n = 0;
            _protoHeatBlocks[n++] = new ShotHeatBlock(0, 0, core.HeatHu, 0);
            if (l.SplitThree)
            {
                _protoHeatBlocks[n++] = new ShotHeatBlock(0, 1, 0, ProtoTuning.SplitThreeHeatMulPermille);
            }
            int heat = ShotHeat.ShotHeatHu(new ReadOnlySpan<ShotHeatBlock>(_protoHeatBlocks, 0, n), ShotHeat.NoUnderline, 0,
                                           ShotHeat.NoMirrorPermille);
            SetHeatShot(SlotAt(i), l.Core, heat, core.IntervalTicks);
        }

        // ── 공용 도우미

        // 칸 단위 Fix64(천분율 정수 → 칸)
        private static Fix64 ProtoMilli(int milli) => Fix64.FromRatio(milli, ProtoTuning.MilliPerCell);

        // 센티 칸/초 → 칸/틱
        private static Fix64 ProtoSpeedPerTick(int centiPerSecond) =>
            Fix64.FromRatio(centiPerSecond, (long)ProtoTuning.CentiPerCell * SimVersion.TickRate);

        // 가장 가까운 플레이어(동률은 슬롯이 작은 쪽). 플레이어가 서 있어야 한다
        private int ProtoNearestPlayer(FixVec2 from)
        {
            int best = 0;
            Fix64 bestD = FixVec2.DistanceSq(from, PlayerAt(0).Pos);
            for (int i = 1; i < PlayerCount; i++)
            {
                Fix64 d = FixVec2.DistanceSq(from, PlayerAt(i).Pos);
                if (d < bestD)
                {
                    best = i;
                    bestD = d;
                }
            }
            return best;
        }

        private static bool ProtoOutside(ArenaGeometry arena, FixVec2 p) =>
            p.X < Fix64.Zero || p.Y < Fix64.Zero || p.X > arena.Width || p.Y > arena.Height || arena.IsInsidePillar(p);

        private static void RequireProtoIndex(int index, int count)
        {
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}

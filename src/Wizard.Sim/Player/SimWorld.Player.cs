using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Wizard.Sim.World;

namespace Wizard.Sim
{
    // Player 모듈(절 1). spec 01 3.7(시작 상태), 3.9(필드), 5.2~5.6(이동·시야·자세), 6.1~6.2(체력·피격·무적).
    // 훅: Stage02_Player(KeyState → 자세 → 시야), Stage03_Player(대각 창 만료), Stage04_Player(이동과 벽),
    //     Stage10_Player(무적 끝, 피격 확정). 상태 훅 넷.
    //
    // 세계 생성 때 모듈이 상태를 세울 훅이 계약에 없다(CONTRACTS 3.2 훅 표, M1-SIM-02 결과 보고의 계약 변경 요청 1).
    // 그래서 시작 상태(spec 01 3.7)는 첫 번째 Player 훅(또는 QueuePlayerHit)이 한 번 세운다 [임시]. 세웠는지(_playersSpawned)는
    // 상태라 해시·직렬화·스냅숏에 들어간다. 능력치·전장 기하는 팩에서 다시 얻을 수 있는 캐시라 상태가 아니고,
    // 같은 때 한 번 만든다(ArenaGeometry 할당 하나. 첫 Step 의 일이다. 초기화 훅이 생기면 그리로 옮긴다).
    //
    // 팩에 characters 절이 없으면(뼈대 시험의 빈 팩) 이 모듈은 꺼진다: 훅은 아무것도 하지 않고 절의 내용은 비어 있다.
    // 캐릭터가 없는 세계에는 플레이어 수치가 없기 때문이고, 모듈을 더해도 빈 팩 세계의 정규 바이트가 바뀌지 않는다.
    public sealed partial class SimWorld
    {
        private const int PermilleOne = 1000;             // 천분율 1.0(단위 환산)

        // 상태(절 1). 색인 = 슬롯. 길이는 MaxPlayers 이고 0..PlayerCount-1 만 쓴다
        private readonly PlayerState[] _players = new PlayerState[MaxPlayers];
        private bool _playersSpawned;

        // 캐시(상태 아님): 슬롯마다의 캐릭터 정의, 전장 기하
        private readonly CharacterDef?[] _playerDefs = new CharacterDef?[MaxPlayers];
        private ArenaGeometry? _playerArena;
        private bool _playerCachesReady;

        // 이벤트마다의 임시 값(상태 아님). Stage02_Arts 가 같은 이벤트 안에서 읽는다
        private readonly PlayerInputResult[] _playerInputResults = new PlayerInputResult[MaxPlayers];

        // ── 다른 모듈과 시험이 읽는 값(같은 어셈블리 안)

        internal bool PlayerModuleOn => _db.Pack.TryGetSection((ushort)ContentSection.Characters, out _);

        internal bool PlayersSpawned => _playersSpawned;

        internal ref readonly PlayerState PlayerAt(int index)
        {
            RequirePlayerIndex(index);
            return ref _players[index];
        }

        // 2단계에서 마지막으로 처리한 이 플레이어의 입력 이벤트의 결과(판정기가 같은 이벤트 안에서 읽는다)
        internal ref readonly PlayerInputResult PlayerLastInput(int index)
        {
            RequirePlayerIndex(index);
            return ref _playerInputResults[index];
        }

        // 발사 방향은 언제나 시야다(spec 01 5.3). 자세 중에는 잠긴 축 방향이라 대각 사격이 없다
        internal Dir8 PlayerFireDirection(int index) => PlayerAt(index).Facing;

        // 이번 틱 10단계에서 피격이 확정되었는가(콤보 끊김·결정 파괴가 같은 단계의 뒤에서 읽는다, spec 01 6.2)
        internal bool PlayerWasHitThisTick(int index)
        {
            ref readonly PlayerState p = ref PlayerAt(index);
            return p.HasBeenHit && p.LastHitTick == _tick;
        }

        internal bool PlayerInvulnerable(int index) => _tick < PlayerAt(index).InvulnUntil;

        // 9단계(충돌)가 이번 틱의 피격 후보를 넣는다. 같은 틱의 후보는 공격 id 가 가장 작은 것 하나만 남는다(spec 01 6.2, T-P05).
        // 무적·피해·이벤트는 10단계(Stage10_Player)가 정한다. damageCells 는 적 쪽 3칸 상한이 이미 걸린 값이고 1 이상이다
        internal void QueuePlayerHit(PlayerSlot slot, EntityId attack, int damageCells)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("player: hit for a slot that is not in this world");
            }
            if (damageCells < 1 || attack.IsNone)
            {
                throw new SimInvariantException("player: hit needs an attack id and at least one cell of damage");
            }
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            ref PendingHit h = ref _players[slot.Value].Hit;
            if (!h.Active || attack.Value < h.Attack.Value)
            {
                h.Active = true;
                h.Attack = attack;
                h.DamageCells = damageCells;
            }
        }

        // 비기의 무적(잔상 걸음, 벼락 질주. spec 01 6.2)은 InvulnUntil 을 늘린다. 이미 더 길면 그대로다
        internal void GrantPlayerInvuln(PlayerSlot slot, uint untilTick)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("player: invuln for a slot that is not in this world");
            }
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            ref PlayerState p = ref _players[slot.Value];
            if (untilTick > p.InvulnUntil)
            {
                p.InvulnUntil = untilTick;
            }
            p.InvulnActive = _tick < p.InvulnUntil;
        }

        // ── 단계 훅

        partial void Stage02_Player(in PlayerInput input, ISimEventSink sink)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            int i = input.Slot.Value;
            PlayerMotion.ApplyInput(ref _players[i], in input.Event, input.Slot, _tick, sink, out _playerInputResults[i]);
        }

        partial void Stage03_Player(ISimEventSink sink)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            for (int i = 0; i < PlayerCount; i++)
            {
                PlayerMotion.ResolvePending(ref _players[i], SlotAt(i), _tick, sink);
            }
        }

        // 4단계: 이동과 벽(spec 01 5.6). 자세·반전 자세는 속도를 StanceSpeedCenti 로 바꾼다(배율이 아니라 값 교체).
        // 스침 가속은 Graze 모듈의 상태를 읽는다(GrazeBuffActive: t < 가속 기한. CONTRACTS 3.2 "모듈 사이의 이음"의 읽기, T-50).
        // 느려짐(적 명세의 PlayerSlowPermille)은 그 모듈이 생기면 여기서 읽는다. 지금은 없음(1000)이다
        partial void Stage04_Player(ISimEventSink sink)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            ArenaGeometry arena = _playerArena!;
            for (int i = 0; i < PlayerCount; i++)
            {
                ref PlayerState p = ref _players[i];
                if (!PlayerRules.TryMoveDir(in p.Keys, out Dir8 dir))
                {
                    continue;
                }
                CharacterDef def = _playerDefs[i]!;
                int speed = Movement.SpeedCenti(def, p.Stance != StanceMode.Normal);
                int mul = Movement.MovePermille(def, GrazeBuffActive(i), PermilleOne);
                p.Pos = Movement.Integrate(arena, p.Pos, dir, Movement.AxisStepRaw(speed, mul), def.BodyRadius);
            }
        }

        // 10단계: 무적 끝(InvulnEnded), 피격 확정(spec 01 6.2 PlayerHealth.OnHit). 같은 틱의 후보는 하나뿐이다(QueuePlayerHit)
        partial void Stage10_Player(ISimEventSink sink)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            EnsurePlayers();
            for (int i = 0; i < PlayerCount; i++)
            {
                ref PlayerState p = ref _players[i];
                if (p.InvulnActive && _tick >= p.InvulnUntil)
                {
                    p.InvulnActive = false;
                    EmitPlayer(sink, SimEventType.InvulnEnded, i, EntityId.None, 0, 0);
                }
                if (!p.Hit.Active)
                {
                    continue;
                }
                PendingHit hit = p.Hit;
                p.Hit = default;
                // 결투의 피격(순간 피해 상한, 벌칙 창, spec 01 12.3)은 Duel 모듈의 것이다(M14-DUL-02). 여기서는 무한 웨이브·연습만
                if (_mode != SimMode.Duel)
                {
                    ResolvePlayerHit(i, in hit, sink);
                }
            }
        }

        // spec 01 6.2. 무적 중이면 피해도 벌칙도 없다. 쓰러진 뒤(체력 0)의 피격은 받지 않는다 [임시: 명세가 정하지 않았다]
        private void ResolvePlayerHit(int i, in PendingHit hit, ISimEventSink sink)
        {
            ref PlayerState p = ref _players[i];
            if (_tick < p.InvulnUntil || p.HpPoints <= 0)
            {
                return;
            }
            // 그믐의 빚 +1칸(spec 01 6.2)은 빚(Combo 모듈의 결정 상태, M3-CMB-01)이 생기면 그 값을 읽는다. 지금은 빚이 없다
            int dmg = PlayerHealth.DamagePoints(hit.DamageCells, false);
            int hpAfter = PlayerHealth.HpAfter(p.HpPoints, dmg);
            p.HpPoints = hpAfter;
            int invuln = PlayerInvulnTicks(i);
            p.InvulnUntil = unchecked(_tick + (uint)invuln);
            p.InvulnActive = invuln > 0;
            p.HasBeenHit = true;
            p.LastHitTick = _tick;
            // 콤보 0(9.4)·결정 파괴(10.4)·지팡이 훅은 같은 10단계의 뒤 모듈이 PlayerWasHitThisTick 으로 읽는다
            EmitPlayer(sink, SimEventType.PlayerHit, i, hit.Attack, dmg, hpAfter);
            if (hpAfter == 0)
            {
                EmitPlayer(sink, SimEventType.PlayerDowned, i, hit.Attack, 0, 0);   // 처리는 모드가 한다(6.3)
            }
        }

        // 피격 후 무적(틱). SimConfig.InvulnTicksOverride 가 −1이면 캐릭터 값(spec 01 12.1)
        private int PlayerInvulnTicks(int i)
        {
            int o = _config.InvulnTicksOverride;
            if (o >= 0)
            {
                return o;
            }
            int t = _playerDefs[i]!.InvulnTicks;
            return t > 0 ? t : 0;
        }

        private void EmitPlayer(ISimEventSink sink, SimEventType type, int i, EntityId source, int a, int b)
        {
            var e = new SimEvent { Type = type, Slot = SlotAt(i), Tick = _tick, Source = source, A = a, B = b };
            sink.Emit(in e);
        }

        // ── 시작 상태와 캐시

        private void EnsurePlayers()
        {
            if (!_playerCachesReady)
            {
                for (int i = 0; i < PlayerCount; i++)
                {
                    _playerDefs[i] = _db.Character(StartCharacter(i));
                }
                _playerArena = new ArenaGeometry(_db.Arena(_mode));
                _playerCachesReady = true;
            }
            if (!_playersSpawned)
            {
                SpawnPlayers();
            }
        }

        // spec 01 3.7: 체력 가득, 시야 아래(S)(결투는 상대 쪽: 왼쪽 E, 오른쪽 W) [임시], 위치는 전장의 시작 위치(슬롯 순).
        // 자세 밖, 키 없음, 무적 없음. 마지막에 누른 축 방향은 시작 시야로 둔다 [임시: 명세가 정하지 않았다]
        private void SpawnPlayers()
        {
            FixVec2[] spawns = _db.Arena(_mode).SpawnPoints;
            for (int i = 0; i < PlayerCount; i++)
            {
                if (i >= spawns.Length)
                {
                    throw new ContentException("player: the arena has no start position for this slot");
                }
                Dir8 facing = StartFacing(i);
                int maxHp = StartMaxHp(i);
                ref PlayerState p = ref _players[i];
                p = default;
                p.Character = StartCharacter(i);
                p.Pos = spawns[i];
                p.Facing = facing;
                p.LastPressedCardinal = facing;
                p.Stance = StanceMode.Normal;
                p.StanceLockedFacing = facing;
                p.MaxHpPoints = maxHp;
                p.HpPoints = maxHp;
            }
            _playersSpawned = true;
        }

        // 지팡이는 그 캐릭터만 쓴다(DECISIONS). 연습의 시작 조건(PracticeSetup)에는 아직 칸이 없어 기준값 캐릭터다 [임시]
        private CharacterId StartCharacter(int i)
        {
            switch (_mode)
            {
                case SimMode.Endless:
                    return _endlessSetup!.Wand?.Character ?? CharacterId.Baseline;
                case SimMode.Duel:
                    return _duelSetup!.Players[i].AppliedWand?.Character ?? CharacterId.Baseline;
                default:
                    return CharacterId.Baseline;
            }
        }

        private Dir8 StartFacing(int i)
        {
            if (_mode == SimMode.Duel)
            {
                return i == 0 ? Dir8.E : Dir8.W;
            }
            return Dir8.S;
        }

        // 결투는 리그 제한을 적용한 DuelPlayerSetup.MaxHpPoints(0이면 캐릭터 값), 그 밖은 칸 × 100
        private int StartMaxHp(int i)
        {
            if (_mode == SimMode.Duel)
            {
                int setupHp = _duelSetup!.Players[i].MaxHpPoints;
                if (setupHp > 0)
                {
                    return setupHp;
                }
            }
            return PlayerHealth.MaxHpPoints(_playerDefs[i]!);
        }

        private void RequirePlayerIndex(int index)
        {
            if ((uint)index >= (uint)PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        // ── 상태 훅

        partial void Snapshot_Save_Player(SimWorld target)
        {
            new ReadOnlySpan<PlayerState>(_players).CopyTo(target._players);
            target._playersSpawned = _playersSpawned;
        }

        partial void Snapshot_Load_Player(SimWorld source)
        {
            new ReadOnlySpan<PlayerState>(source._players).CopyTo(_players);
            _playersSpawned = source._playersSpawned;
        }

        // 내용(모듈이 켜져 있을 때만): spawned bool, 플레이어마다(슬롯 순) PlayerState.Write
        partial void State_Write_Player(ref StateWriter w)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            w.Bool(_playersSpawned);
            for (int i = 0; i < PlayerCount; i++)
            {
                _players[i].Write(ref w);
            }
        }

        partial void State_Read_Player(ref StateReader r)
        {
            if (!PlayerModuleOn)
            {
                return;
            }
            bool spawned = r.Bool();
            for (int i = 0; i < PlayerCount; i++)
            {
                _players[i].Read(ref r);
                if (spawned && _players[i].Character != StartCharacter(i))
                {
                    throw new SimInvariantException("state: player character differs from the world's setup");
                }
            }
            _playersSpawned = spawned;
        }
    }
}

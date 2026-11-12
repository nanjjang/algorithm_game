using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Player;
using Wizard.Sim.Proto;
using Wizard.Sim.World;

namespace Wizard.Sim
{
    // 시제품의 적 쪽(M1-SIM-09): 슬라임 무한 생성과 3갈래 탄 포탑 1개(spec 01 13절 7번, GDD 16 1단계).
    // 실제 적 런타임(M3-ENM-01~03)과 웨이브 디렉터(M3-WAV-01)가 같은 훅을 구현하므로 그 작업 전에 이 폴더가 빠져야 한다(결과 보고).
    //
    // 7단계: 생성기 → AI(id 순). 슬라임은 가장 가까운 플레이어 쪽으로 직선 이동 의도(spec 05 4.1 Chase, 길찾기 없음 U-26).
    //        포탑은 Idle → Cast(24틱, 이것이 예고다) → 3발 동시 발사 → Recovery(36틱) → Idle. 시전 간격 2초, 사거리 9칸 안일 때만 시전한다.
    //        생성: 1.5초마다, 살아 있는 슬라임이 상한보다 적으면 한 마리. 자리는 플레이어에게서 먼 두 변 가운데 하나(GDD 2.2) [임시].
    //        난수는 보관 흐름 WaveSpawn 만 쓴다(한 마리에 두 번: 변, 변 위의 자리).
    // 9단계(Stage09_Spell 이 먼저 적·탄을 움직인 뒤): 적 탄 → 플레이어(맞으면 QueuePlayerHit, 아니면 스침 후보 OfferGrazeThreat),
    //        슬라임 접촉 → 플레이어(spec 05 3.6: 무적 중이면 쿨다운을 쓰지 않는다 U-15). 사라진 시전 묶음은 ReleaseGrazeCast.
    // 10단계: 넉백 → 피해 확정 → 사망(체력 0, EnemyDeath). 체력은 0 아래로 내려가지 않는다(적 체력은 포화).
    public sealed partial class SimWorld
    {
        // ── 7단계

        partial void Stage07_Enemies(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            if (_tick >= _protoNextSlimeTick)
            {
                _protoNextSlimeTick = unchecked(_tick + (uint)ProtoTuning.SlimeSpawnIntervalTicks);
                if (CountProtoSlimes() < ProtoTuning.SlimeMaxAlive && _protoEnemyCount < _protoEnemies.Length)
                {
                    SpawnProtoSlime(sink);
                }
            }
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ref ProtoEnemy en = ref _protoEnemies[e];
                int target = ProtoNearestPlayer(en.Pos);
                FixVec2 targetPos = PlayerAt(target).Pos;
                if (en.Kind == ProtoEnemyKind.Slime)
                {
                    en.MoveDir = (targetPos - en.Pos).Normalized();
                    continue;
                }
                TickProtoTurret(ref en, targetPos, sink);
            }
        }

        private int CountProtoSlimes()
        {
            int n = 0;
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                if (_protoEnemies[e].Kind == ProtoEnemyKind.Slime)
                {
                    n++;
                }
            }
            return n;
        }

        // 플레이어에게서 먼 두 변(GDD 2.2) 가운데 하나를 고르고 그 변 위의 자리를 고른다 [임시]
        private void SpawnProtoSlime(ISimEventSink sink)
        {
            ArenaGeometry arena = _protoArena!;
            // 변: 0 왼, 1 오른, 2 위, 3 아래. 플레이어마다의 그 변까지 거리를 더한 값이 큰 두 변. 같으면 번호가 작은 쪽이 먼저
            Fix64 d0 = Fix64.Zero, d1 = Fix64.Zero, d2 = Fix64.Zero, d3 = Fix64.Zero;
            for (int i = 0; i < PlayerCount; i++)
            {
                FixVec2 p = PlayerAt(i).Pos;
                d0 = d0 + p.X;
                d1 = d1 + (arena.Width - p.X);
                d2 = d2 + p.Y;
                d3 = d3 + (arena.Height - p.Y);
            }
            int first = FarthestEdge(d0, d1, d2, d3, -1);
            int second = FarthestEdge(d0, d1, d2, d3, first);
            ref Pcg32 rng = ref HeldRng(RngStream.WaveSpawn);
            int edge = rng.NextInt(2) == 0 ? first : second;
            Fix64 t = rng.NextFix01();
            Fix64 body = ProtoMilli(ProtoTuning.SlimeBodyMilli);
            FixVec2 at;
            switch (edge)
            {
                case 0: at = new FixVec2(body, arena.Height * t); break;
                case 1: at = new FixVec2(arena.Width - body, arena.Height * t); break;
                case 2: at = new FixVec2(arena.Width * t, body); break;
                default: at = new FixVec2(arena.Width * t, arena.Height - body); break;
            }
            at = arena.ResolveBody(at, body);
            Fix64 hp = ProtoMilli(ProtoTuning.SlimeHpMilli);
            AddProtoEnemy(new ProtoEnemy { Kind = ProtoEnemyKind.Slime, Pos = at, Hp = hp, MaxHp = hp }, EnemyTypeId.SlimeMoss, sink);
        }

        // 시험이 Step 밖에서 부르는 문: 정한 자리에 슬라임 하나를 둔다(생성기의 시계와 난수는 쓰지 않는다). 시제품이 서 있어야 한다
        internal EntityId ProtoPlaceSlime(FixVec2 at, ISimEventSink sink)
        {
            if (!ProtoOn || !_protoReady)
            {
                throw new SimInvariantException("proto: place a slime after the prototype is running");
            }
            Fix64 hp = ProtoMilli(ProtoTuning.SlimeHpMilli);
            AddProtoEnemy(new ProtoEnemy
            {
                Kind = ProtoEnemyKind.Slime, Pos = _protoArena!.ResolveBody(at, ProtoMilli(ProtoTuning.SlimeBodyMilli)), Hp = hp, MaxHp = hp,
            }, EnemyTypeId.SlimeMoss, sink);
            return _protoEnemies[_protoEnemyCount - 1].Id;
        }

        private static int FarthestEdge(Fix64 d0, Fix64 d1, Fix64 d2, Fix64 d3, int skip)
        {
            int best = -1;
            Fix64 bestD = Fix64.MinValue;
            for (int k = 0; k < 4; k++)
            {
                if (k == skip)
                {
                    continue;
                }
                Fix64 d = k == 0 ? d0 : (k == 1 ? d1 : (k == 2 ? d2 : d3));
                if (best < 0 || d > bestD)
                {
                    best = k;
                    bestD = d;
                }
            }
            return best;
        }

        // 포탑 하나(시작 상태). 맞지 않고 죽지 않는다 [임시]. 체력 칸은 표시용 1이다
        private void SpawnProtoTurret(ISimEventSink sink)
        {
            var at = new FixVec2(ProtoMilli(ProtoTuning.TurretXMilli), ProtoMilli(ProtoTuning.TurretYMilli));
            AddProtoEnemy(new ProtoEnemy
            {
                Kind = ProtoEnemyKind.Turret, Pos = at, Hp = Fix64.One, MaxHp = Fix64.One,
                Phase = ProtoTurretPhase.Idle, NextCastTick = _tick,
            }, EnemyTypeId.ShamanCap, sink);
        }

        private void AddProtoEnemy(ProtoEnemy en, EnemyTypeId type, ISimEventSink sink)
        {
            if (_protoEnemyCount >= _protoEnemies.Length)
            {
                throw new SimInvariantException("proto: enemy pool is full");
            }
            en.Id = NewEntityId();
            en.ContactReadyTick = _tick;
            _protoEnemies[_protoEnemyCount++] = en;
            // EnemySpawn: Source = 적, A = 종(EnemyTypeId. 포탑은 술사의 3갈래 탄을 빌린 것이라 ShamanCap), B = ProtoEnemyKind, X·Y = 자리
            var e = new SimEvent
            {
                Type = SimEventType.EnemySpawn, Tick = _tick, Slot = PlayerSlot.None, Source = en.Id,
                A = (int)type, B = (int)en.Kind, X = en.Pos.X.Raw, Y = en.Pos.Y.Raw,
            };
            sink.Emit(in e);
        }

        private void TickProtoTurret(ref ProtoEnemy en, FixVec2 targetPos, ISimEventSink sink)
        {
            switch (en.Phase)
            {
                case ProtoTurretPhase.Idle:
                {
                    Fix64 range = ProtoMilli(ProtoTuning.TurretRangeMilli);
                    if (_tick >= en.NextCastTick && FixVec2.DistanceSq(en.Pos, targetPos) <= range * range)
                    {
                        en.Phase = ProtoTurretPhase.Cast;
                        en.PhaseEndTick = unchecked(_tick + (uint)ProtoTuning.TurretCastTicks);
                        en.NextCastTick = unchecked(_tick + (uint)ProtoTuning.TurretCooldownTicks);
                        // 예고(시전 시작): EnemyWeaponTintOn, Source = 포탑, A = 발사 틱
                        EmitProtoEnemy(sink, SimEventType.EnemyWeaponTintOn, in en, unchecked((int)en.PhaseEndTick));
                    }
                    break;
                }
                case ProtoTurretPhase.Cast:
                    if (_tick >= en.PhaseEndTick)
                    {
                        FireProtoTurret(in en, targetPos, sink);
                        en.Phase = ProtoTurretPhase.Recovery;
                        en.PhaseEndTick = unchecked(_tick + (uint)ProtoTuning.TurretRecoveryTicks);
                        EmitProtoEnemy(sink, SimEventType.EnemyWeaponTintOff, in en, 0);
                    }
                    break;
                default:
                    if (_tick >= en.PhaseEndTick)
                    {
                        en.Phase = ProtoTurretPhase.Idle;
                    }
                    break;
            }
        }

        // 3발 동시(spec 05 4.4): 가운데는 발사 틱의 플레이어 쪽, 좌우는 ±20도. 한 묶음(시전 묶음 id 하나)
        private void FireProtoTurret(in ProtoEnemy en, FixVec2 targetPos, ISimEventSink sink)
        {
            Angle aim = Angle.OfVector(targetPos - en.Pos);
            _protoLastCastId = unchecked(_protoLastCastId + 1);
            int cast = _protoLastCastId;
            Fix64 range = ProtoMilli(ProtoTuning.TurretRangeMilli);
            for (int k = 0; k < ProtoTuning.TurretShotCount; k++)
            {
                if (_protoShotCount >= _protoShots.Length)
                {
                    break;                                       // 적 탄 상한: 새 탄을 버린다 [임시]
                }
                Angle a = aim + (k - 1) * ProtoTuning.TurretSpreadCenti;
                EntityId id = NewEntityId();
                _protoShots[_protoShotCount++] = new ProtoEnemyShot
                {
                    Id = id, CastId = cast, Pos = en.Pos, Dir = FixVec2.FromAngle(a), RangeLeft = range,
                };
                // EnemyProjectileFire: Source = 탄, Target = 쏜 적, A = 시전 묶음, C = 방향(센티도), X·Y = 자리
                var e = new SimEvent
                {
                    Type = SimEventType.EnemyProjectileFire, Tick = _tick, Slot = PlayerSlot.None, Source = id, Target = en.Id,
                    A = cast, C = a.Centi, X = en.Pos.X.Raw, Y = en.Pos.Y.Raw,
                };
                sink.Emit(in e);
            }
        }

        private void EmitProtoEnemy(ISimEventSink sink, SimEventType type, in ProtoEnemy en, int a)
        {
            var e = new SimEvent
            {
                Type = type, Tick = _tick, Slot = PlayerSlot.None, Source = en.Id, A = a, X = en.Pos.X.Raw, Y = en.Pos.Y.Raw,
            };
            sink.Emit(in e);
        }

        // ── 9단계의 이동(Stage09_Spell 이 부른다: 적 이동 → 플레이어 탄 → 적 탄)

        private void MoveProtoEnemies(ArenaGeometry arena)
        {
            Fix64 speed = ProtoSpeedPerTick(ProtoTuning.SlimeSpeedCenti);
            Fix64 body = ProtoMilli(ProtoTuning.SlimeBodyMilli);
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ref ProtoEnemy en = ref _protoEnemies[e];
                if (en.Kind != ProtoEnemyKind.Slime)
                {
                    continue;
                }
                en.Pos = arena.ResolveBody(en.Pos + en.MoveDir * speed, body);
            }
        }

        private void MoveProtoShots(ArenaGeometry arena, ISimEventSink sink)
        {
            Fix64 speed = ProtoSpeedPerTick(ProtoTuning.TurretShotSpeedCenti);
            for (int s = 0; s < _protoShotCount; s++)
            {
                ref ProtoEnemyShot x = ref _protoShots[s];
                x.Pos = x.Pos + x.Dir * speed;
                x.RangeLeft = x.RangeLeft - speed;
                if (ProtoOutside(arena, x.Pos) || x.RangeLeft <= Fix64.Zero)
                {
                    DespawnProtoShot(ref x, 0, sink);
                }
            }
        }

        // ── 9단계: 적 탄·접촉 ↔ 플레이어(id 순, 플레이어는 슬롯 순). 스침 판정은 이 뒤의 Stage09_Graze 가 한다

        partial void Stage09_Enemies(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            Fix64 shotRadius = ProtoMilli(ProtoTuning.TurretShotRadiusMilli);
            for (int s = 0; s < _protoShotCount; s++)
            {
                ref ProtoEnemyShot x = ref _protoShots[s];
                if (x.Dead)
                {
                    continue;
                }
                for (int i = 0; i < PlayerCount; i++)
                {
                    ref readonly PlayerState p = ref PlayerAt(i);
                    if (p.HpPoints <= 0)
                    {
                        continue;
                    }
                    Fix64 r = shotRadius + GrazeHitRadius(i);
                    bool overlaps = FixVec2.DistanceSq(x.Pos, p.Pos) <= r * r;
                    if (overlaps && !PlayerInvulnerable(i))
                    {
                        QueuePlayerHit(SlotAt(i), x.Id, ProtoTuning.TurretShotCells);
                        DespawnProtoShot(ref x, 1, sink);
                        break;
                    }
                    // 무적 중에 겹친 탄은 지나간다 [임시]. 스침 후보는 모두 넣는다(띠 판정과 무적·피격 거르기는 Graze 의 것)
                    var threat = GrazeThreat.Bullet(x.Id, x.CastId, x.Pos, shotRadius);
                    OfferGrazeThreat(SlotAt(i), in threat);
                }
            }
            Fix64 body = ProtoMilli(ProtoTuning.SlimeBodyMilli);
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ref ProtoEnemy en = ref _protoEnemies[e];
                if (en.Kind != ProtoEnemyKind.Slime)
                {
                    continue;
                }
                for (int i = 0; i < PlayerCount; i++)
                {
                    ref readonly PlayerState p = ref PlayerAt(i);
                    if (p.HpPoints <= 0 || _tick < en.ContactReadyTick || PlayerInvulnerable(i))
                    {
                        continue;
                    }
                    Fix64 r = body + GrazeHitRadius(i);
                    if (FixVec2.DistanceSq(en.Pos, p.Pos) <= r * r)
                    {
                        QueuePlayerHit(SlotAt(i), en.Id, ProtoTuning.SlimeContactCells);
                        en.ContactReadyTick = unchecked(_tick + (uint)ProtoTuning.SlimeContactCooldownTicks);
                        break;
                    }
                }
            }
            CompactProtoShots();
        }

        // hit: 0 = 벽·사거리, 1 = 플레이어에 맞음
        private void DespawnProtoShot(ref ProtoEnemyShot x, int hit, ISimEventSink sink)
        {
            x.Dead = true;
            // EnemyProjectileDespawn: Source = 탄, A = 시전 묶음, B = 맞음(0/1), X·Y = 자리
            var e = new SimEvent
            {
                Type = SimEventType.EnemyProjectileDespawn, Tick = _tick, Slot = PlayerSlot.None, Source = x.Id,
                A = x.CastId, B = hit, X = x.Pos.X.Raw, Y = x.Pos.Y.Raw,
            };
            sink.Emit(in e);
        }

        // 죽은 탄을 빼고(순서 유지), 탄이 하나도 남지 않은 시전 묶음을 스침 기록에서 지운다(spec 01 8.2 [임시]).
        // 같은 묶음의 탄은 풀에서 이웃해 있다(한 번의 발사로 이어서 만들었고, 빼도 순서가 유지된다)
        private void CompactProtoShots()
        {
            int s = 0;
            while (s < _protoShotCount)
            {
                int cast = _protoShots[s].CastId;
                bool anyAlive = false, anyDead = false;
                int end = s;
                while (end < _protoShotCount && _protoShots[end].CastId == cast)
                {
                    if (_protoShots[end].Dead)
                    {
                        anyDead = true;
                    }
                    else
                    {
                        anyAlive = true;
                    }
                    end++;
                }
                if (anyDead && !anyAlive)
                {
                    ReleaseGrazeCast(cast);
                }
                s = end;
            }
            int n = 0;
            for (int k = 0; k < _protoShotCount; k++)
            {
                if (!_protoShots[k].Dead)
                {
                    _protoShots[n++] = _protoShots[k];
                }
            }
            for (int k = n; k < _protoShotCount; k++)
            {
                _protoShots[k] = default;
            }
            _protoShotCount = n;
        }

        // ── 10단계: 넉백, 피해 확정, 사망

        partial void Stage10_Enemies(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            ArenaGeometry arena = _protoArena!;
            Fix64 body = ProtoMilli(ProtoTuning.SlimeBodyMilli);
            int n = 0;
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ProtoEnemy en = _protoEnemies[e];
                if (en.PendingKnock != FixVec2.Zero)
                {
                    en.Pos = arena.ResolveBody(en.Pos + en.PendingKnock, body);
                    en.PendingKnock = FixVec2.Zero;
                }
                bool dies = false;
                if (en.PendingDamage > Fix64.Zero)
                {
                    en.Hp = en.PendingDamage >= en.Hp ? Fix64.Zero : en.Hp - en.PendingDamage;
                    en.PendingDamage = Fix64.Zero;
                    dies = en.Hp == Fix64.Zero;
                }
                if (dies)
                {
                    // EnemyDeath: Source = 적, A = 종(EnemyTypeId), X·Y = 자리
                    var d = new SimEvent
                    {
                        Type = SimEventType.EnemyDeath, Tick = _tick, Slot = PlayerSlot.None, Source = en.Id,
                        A = (int)EnemyTypeId.SlimeMoss, X = en.Pos.X.Raw, Y = en.Pos.Y.Raw,
                    };
                    sink.Emit(in d);
                    continue;
                }
                _protoEnemies[n++] = en;
            }
            for (int e = n; e < _protoEnemyCount; e++)
            {
                _protoEnemies[e] = default;
            }
            _protoEnemyCount = n;
        }

        // ── 상태 훅: Enemies(9, 풀). 시제품이 서 있으면 언제나 쓴다(생성기의 시계와 포탑이 있다)

        partial void Snapshot_Save_Enemies(SimWorld target)
        {
            new ReadOnlySpan<ProtoEnemy>(_protoEnemies).CopyTo(target._protoEnemies);
            target._protoEnemyCount = _protoEnemyCount;
            new ReadOnlySpan<ProtoEnemyShot>(_protoShots).CopyTo(target._protoShots);
            target._protoShotCount = _protoShotCount;
            target._protoNextSlimeTick = _protoNextSlimeTick;
            target._protoLastCastId = _protoLastCastId;
        }

        partial void Snapshot_Load_Enemies(SimWorld source)
        {
            new ReadOnlySpan<ProtoEnemy>(source._protoEnemies).CopyTo(_protoEnemies);
            _protoEnemyCount = source._protoEnemyCount;
            new ReadOnlySpan<ProtoEnemyShot>(source._protoShots).CopyTo(_protoShots);
            _protoShotCount = source._protoShotCount;
            _protoNextSlimeTick = source._protoNextSlimeTick;
            _protoLastCastId = source._protoLastCastId;
        }

        // 내용: next_slime_tick u32, last_cast_id i32, 적 개수, 적마다 ProtoEnemy.Write, 적 탄 개수, 탄마다 ProtoEnemyShot.Write(모두 id 순)
        partial void State_Write_Enemies(ref StateWriter w)
        {
            if (!ProtoOn || !TryBeginPoolSection(ref w, StateSection.Enemies, _protoReady ? 1 : 0))
            {
                return;
            }
            w.U32(_protoNextSlimeTick);
            w.I32(_protoLastCastId);
            w.Count(_protoEnemyCount);
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                _protoEnemies[e].Write(ref w);
            }
            w.Count(_protoShotCount);
            for (int s = 0; s < _protoShotCount; s++)
            {
                _protoShots[s].Write(ref w);
            }
        }

        partial void State_Read_Enemies(ref StateReader r)
        {
            if (!ProtoOn)
            {
                return;
            }
            Array.Clear(_protoEnemies, 0, _protoEnemies.Length);
            Array.Clear(_protoShots, 0, _protoShots.Length);
            _protoEnemyCount = 0;
            _protoShotCount = 0;
            _protoNextSlimeTick = 0;
            _protoLastCastId = 0;
            if (!TryBeginPoolSection(ref r, StateSection.Enemies))
            {
                return;
            }
            _protoNextSlimeTick = r.U32();
            _protoLastCastId = r.I32();
            int n = r.Count();
            if (n < 0 || n > _protoEnemies.Length)
            {
                throw new SimInvariantException("proto: enemy count out of range");
            }
            int prev = 0;
            for (int e = 0; e < n; e++)
            {
                _protoEnemies[e].Read(ref r);
                if (_protoEnemies[e].Id.Value <= prev)
                {
                    throw new SimInvariantException("proto: enemies are not in ascending id order");
                }
                prev = _protoEnemies[e].Id.Value;
            }
            _protoEnemyCount = n;
            int m = r.Count();
            if (m < 0 || m > _protoShots.Length)
            {
                throw new SimInvariantException("proto: enemy shot count out of range");
            }
            prev = 0;
            for (int s = 0; s < m; s++)
            {
                _protoShots[s].Read(ref r);
                if (_protoShots[s].Id.Value <= prev || _protoShots[s].CastId > _protoLastCastId)
                {
                    throw new SimInvariantException("proto: enemy shots are not in ascending id order");
                }
                prev = _protoShots[s].Id.Value;
            }
            _protoShotCount = m;
        }
    }
}

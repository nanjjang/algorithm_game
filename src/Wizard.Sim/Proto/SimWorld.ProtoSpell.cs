using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Wizard.Sim.Proto;
using Wizard.Sim.World;

namespace Wizard.Sim
{
    // 시제품의 주문 쪽(M1-SIM-09): 고정 투사체 2종(마력탄, 화염구)과 3갈래 전환(spec 01 13절 6번, GDD 16 1단계).
    // 실제 컴파일러·실행기(M2-SPL-02~04)가 같은 훅을 구현하므로 그 작업 전에 이 폴더가 빠져야 한다(결과 보고).
    //
    // 조립 바꾸기는 RunCommand SetSlot 하나로 한다(CONTRACTS 3.1: A = 슬롯 번호, B = 블록 인스턴스 번호, 0이면 비움).
    // 시제품의 보관함은 고정 블록 셋이고 인스턴스 번호 = (int)BlockId 다 [임시]:
    //   A = 0(코어): B = 1(마력탄) 또는 2(화염구). A = 1(첫 연결 칸): B = 8(3갈래 분열) 또는 0(비움). 그 밖은 무효 명령이다.
    // 0단계에서 바꾸면 같은 틱 6단계의 발사부터 새 조립이다(열과 간격은 Heat 에 SetHeatShot 으로 넣는다).
    //
    // 발사(6단계): Heat 가 정한 발사(HeatFiredThisTick)마다 시야 방향으로 탄을 낸다. 3갈래면 −15/0/+15도의 셋(spec 02 9.3)(만든 순서 = id 순).
    // 탄의 피해 = 블록 기본 피해 × 임계 배율(×2) × 비기 배율. 속성 배율은 맞을 때 대상에 따라 곱한다.
    // 9단계(spec 01 3.8의 9단계 순서): 적 이동 → 탄 이동(플레이어 탄, 적 탄) → 플레이어 탄↔적. 적 탄·접촉↔플레이어는 Stage09_Enemies 가 한다.
    // 탄은 전장 밖·기둥 안이면 사라지고, 사거리를 다 가면 사라지며, 적 하나에 맞으면 사라진다(관통 없음).
    public sealed partial class SimWorld
    {
        // ── 0단계: 조립 바꾸기

        partial void Stage00_Spell(in RunCommand command, bool apply, ref bool valid, ISimEventSink sink)
        {
            if (!ProtoOn || !IsProtoSetSlot(in command))
            {
                return;
            }
            if (!apply)
            {
                valid = true;
                return;
            }
            EnsureProto(sink);
            int i = command.Slot.Value;
            ProtoLoadout l = _protoLoadouts[i];
            if (command.A == 0)
            {
                l.Core = (BlockId)command.B;
            }
            else
            {
                l.SplitThree = command.B != 0;
            }
            _protoLoadouts[i] = l;
            ArmProtoShot(i);
            // CompileChanged: A = 코어 블록, B = 3갈래(0/1)
            var e = new SimEvent
            {
                Type = SimEventType.CompileChanged, Slot = command.Slot, Tick = _tick,
                A = (int)l.Core, B = l.SplitThree ? 1 : 0,
            };
            sink.Emit(in e);
        }

        private static bool IsProtoSetSlot(in RunCommand c)
        {
            if (c.Op != RunCommandOp.SetSlot)
            {
                return false;
            }
            if (c.A == 0)
            {
                return ProtoLoadout.IsCoreBlock((BlockId)c.B) && c.B > 0 && c.B <= byte.MaxValue;
            }
            return c.A == 1 && (c.B == 0 || c.B == (int)BlockId.SplitThree);
        }

        // ── 6단계: J 발사의 탄

        partial void Stage06_Spell(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            for (int i = 0; i < PlayerCount; i++)
            {
                if (!HeatFiredThisTick(i, out HeatFire fire))
                {
                    continue;
                }
                int crit = fire.IsCrit ? ProtoTuning.CritMulPermille : ProtoTuning.NoMulPermille;
                int made = SpawnProtoTree(i, crit, false, fire.ShotId, sink);
                // SpellCast: A = shotId, B = 낸 탄 수, C = 임계(0/1)
                var e = new SimEvent
                {
                    Type = SimEventType.SpellCast, Slot = SlotAt(i), Tick = _tick, A = fire.ShotId, B = made, C = fire.IsCrit ? 1 : 0,
                };
                sink.Emit(in e);
            }
        }

        // 지팡이 트리 1회(시제품: 코어 하나, 3갈래면 셋). mulPermille = 임계 배율 또는 비기 배율(둘은 같이 곱하지 않는다, spec 02 11절).
        // 낸 탄 수를 돌려준다. 풀이 차면 남은 탄을 버리고 SpellCapDropped 를 한 번 낸다
        private int SpawnProtoTree(int i, int mulPermille, bool knockback, int shotId, ISimEventSink sink)
        {
            ProtoLoadout l = _protoLoadouts[i];
            BlockDef core = _db.Block(l.Core);
            ref readonly PlayerState p = ref PlayerAt(i);
            Angle facing = Angle.FromDir8(PlayerFireDirection(i));
            int count = l.SplitThree ? ProtoTuning.SplitThreeCount : 1;
            Fix64 damage = Fix64.MulPermille(core.BaseDamage, mulPermille);
            int made = 0;
            for (int k = 0; k < count; k++)
            {
                if (_protoBulletCount >= _protoBullets.Length)
                {
                    var drop = new SimEvent { Type = SimEventType.SpellCapDropped, Slot = SlotAt(i), Tick = _tick, A = count - made };
                    sink.Emit(in drop);
                    break;
                }
                // 3갈래: k = 0, 1, 2 → −15, 0, +15도(spec 02 9.3, TV-F02)
                int offset = count == 1 ? 0 : (k - 1) * ProtoTuning.SplitThreeStepCenti;
                Angle a = facing + offset;
                EntityId id = NewEntityId();
                _protoBullets[_protoBulletCount++] = new ProtoBullet
                {
                    Id = id, Owner = SlotAt(i), ShotId = shotId, Block = l.Core, Element = core.Element,
                    Pos = p.Pos, Dir = FixVec2.FromAngle(a), SpeedPerTick = core.SpeedPerTick, RangeLeft = core.Range,
                    Radius = core.HitRadius, Damage = damage, Knockback = knockback,
                };
                made++;
                // ProjectileSpawned: Source = 탄 id, A = 코어 블록, B = shotId(비기는 0), C = 방향(센티도), X·Y = 위치
                var e = new SimEvent
                {
                    Type = SimEventType.ProjectileSpawned, Slot = SlotAt(i), Tick = _tick, Source = id,
                    A = (int)l.Core, B = shotId, C = a.Centi, X = p.Pos.X.Raw, Y = p.Pos.Y.Raw,
                };
                sink.Emit(in e);
            }
            return made;
        }

        // 지팡이 트리 1회의 피해 T(spec 02 12절 StandardT [임시]: 루트 스코프 탄 하나하나의 기본 피해 합, 분열 반영)
        private Fix64 ProtoStandardT(int i)
        {
            ProtoLoadout l = _protoLoadouts[i];
            Fix64 one = _db.Block(l.Core).BaseDamage;
            return l.SplitThree ? one * ProtoTuning.SplitThreeCount : one;
        }

        // ── 9단계: 적 이동 → 탄 이동 → 플레이어 탄↔적

        partial void Stage09_Spell(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            ArenaGeometry arena = _protoArena!;
            MoveProtoEnemies(arena);
            for (int b = 0; b < _protoBulletCount; b++)
            {
                ref ProtoBullet x = ref _protoBullets[b];
                x.Pos = x.Pos + x.Dir * x.SpeedPerTick;
                x.RangeLeft = x.RangeLeft - x.SpeedPerTick;
                if (ProtoOutside(arena, x.Pos))
                {
                    ExpireProtoBullet(ref x, sink);
                }
            }
            MoveProtoShots(arena, sink);
            for (int b = 0; b < _protoBulletCount; b++)
            {
                ref ProtoBullet x = ref _protoBullets[b];
                if (x.Dead)
                {
                    continue;
                }
                int target = ProtoBulletTarget(in x);
                if (target >= 0)
                {
                    HitProtoEnemy(ref x, target, sink);
                }
                else if (x.RangeLeft <= Fix64.Zero)
                {
                    ExpireProtoBullet(ref x, sink);
                }
            }
            CompactProtoBullets();
        }

        // 겹치는 적 가운데 id 가 가장 작은 것(풀이 id 순이라 처음 만난 것). 포탑은 맞지 않는다 [임시]
        private int ProtoBulletTarget(in ProtoBullet x)
        {
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ref readonly ProtoEnemy en = ref _protoEnemies[e];
                if (en.Kind != ProtoEnemyKind.Slime)
                {
                    continue;
                }
                Fix64 r = x.Radius + ProtoMilli(ProtoTuning.SlimeBodyMilli);
                if (FixVec2.DistanceSq(x.Pos, en.Pos) <= r * r)
                {
                    return e;
                }
            }
            return -1;
        }

        private void HitProtoEnemy(ref ProtoBullet x, int target, ISimEventSink sink)
        {
            ref ProtoEnemy en = ref _protoEnemies[target];
            Fix64 dmg = AddProtoDamage(ref en, x.Damage, x.Element, x.Owner, x.Id, sink);
            if (x.Knockback)
            {
                en.PendingKnock = en.PendingKnock + x.Dir * ProtoMilli(ProtoTuning.RecoilKnockbackMilli);
            }
            // ProjectileHit: Source = 탄, Target = 적, V = 피해(Raw), X·Y = 맞은 자리
            var e = new SimEvent
            {
                Type = SimEventType.ProjectileHit, Slot = x.Owner, Tick = _tick, Source = x.Id, Target = en.Id,
                V = dmg.Raw, X = x.Pos.X.Raw, Y = x.Pos.Y.Raw,
            };
            sink.Emit(in e);
            x.Dead = true;
        }

        // 속성 배율을 곱해 적의 이번 틱 피해에 더한다(확정은 10단계). 이음 이벤트 HitEnemy·HitWeakness·HitResistance 를 낸다.
        // 슬라임: 약점 화염, 저항 냉기(spec 05 2.5)
        private Fix64 AddProtoDamage(ref ProtoEnemy en, Fix64 baseDamage, Element element, PlayerSlot by, EntityId source,
                                     ISimEventSink sink)
        {
            int mul = ProtoTuning.NoMulPermille;
            SimEventType affinity = SimEventType.None;
            if (element == Element.Fire)
            {
                mul = ProtoTuning.WeaknessMulPermille;
                affinity = SimEventType.HitWeakness;
            }
            else if (element == Element.Ice)
            {
                mul = ProtoTuning.ResistanceMulPermille;
                affinity = SimEventType.HitResistance;
            }
            Fix64 dmg = Fix64.MulPermille(baseDamage, mul);
            en.PendingDamage = en.PendingDamage + dmg;
            // HitEnemy: Source = 탄(창은 없음), Target = 적, A = 속성, B = 배율(천분율), V = 피해(Raw)
            var hit = new SimEvent
            {
                Type = SimEventType.HitEnemy, Slot = by, Tick = _tick, Source = source, Target = en.Id,
                A = (int)element, B = mul, V = dmg.Raw, X = en.Pos.X.Raw, Y = en.Pos.Y.Raw,
            };
            sink.Emit(in hit);
            if (affinity != SimEventType.None)
            {
                var a = new SimEvent { Type = affinity, Slot = by, Tick = _tick, Source = source, Target = en.Id, A = (int)element };
                sink.Emit(in a);
            }
            return dmg;
        }

        private void ExpireProtoBullet(ref ProtoBullet x, ISimEventSink sink)
        {
            x.Dead = true;
            // ProjectileExpired: Source = 탄, X·Y = 사라진 자리
            var e = new SimEvent
            {
                Type = SimEventType.ProjectileExpired, Slot = x.Owner, Tick = _tick, Source = x.Id, X = x.Pos.X.Raw, Y = x.Pos.Y.Raw,
            };
            sink.Emit(in e);
        }

        // 죽은 탄을 뺀다(순서 유지 = id 순 유지)
        private void CompactProtoBullets()
        {
            int n = 0;
            for (int b = 0; b < _protoBulletCount; b++)
            {
                if (!_protoBullets[b].Dead)
                {
                    _protoBullets[n++] = _protoBullets[b];
                }
            }
            for (int b = n; b < _protoBulletCount; b++)
            {
                _protoBullets[b] = default;
            }
            _protoBulletCount = n;
        }

        // ── 상태 훅: Spell(5, 풀) = 플레이어 탄. SpellTable(6, 스칼라) = 시제품의 준비 표시와 조립(컴파일러의 SpellTable 자리)

        partial void Snapshot_Save_Spell(SimWorld target)
        {
            new ReadOnlySpan<ProtoBullet>(_protoBullets).CopyTo(target._protoBullets);
            target._protoBulletCount = _protoBulletCount;
        }

        partial void Snapshot_Load_Spell(SimWorld source)
        {
            new ReadOnlySpan<ProtoBullet>(source._protoBullets).CopyTo(_protoBullets);
            _protoBulletCount = source._protoBulletCount;
        }

        // 내용(탄이 있을 때만 절이 생긴다): 개수, 탄마다 ProtoBullet.Write(id 순)
        partial void State_Write_Spell(ref StateWriter w)
        {
            if (!ProtoOn || !TryBeginPoolSection(ref w, StateSection.Spell, _protoBulletCount))
            {
                return;
            }
            w.Count(_protoBulletCount);
            for (int b = 0; b < _protoBulletCount; b++)
            {
                _protoBullets[b].Write(ref w);
            }
        }

        partial void State_Read_Spell(ref StateReader r)
        {
            if (!ProtoOn)
            {
                return;
            }
            Array.Clear(_protoBullets, 0, _protoBullets.Length);
            _protoBulletCount = 0;
            if (!TryBeginPoolSection(ref r, StateSection.Spell))
            {
                return;
            }
            int n = r.Count();
            if (n < 1 || n > _protoBullets.Length)
            {
                throw new SimInvariantException("proto: bullet count out of range");
            }
            int prev = 0;
            for (int b = 0; b < n; b++)
            {
                _protoBullets[b].Read(ref r);
                if (_protoBullets[b].Id.Value <= prev || !IsValidSlot(_protoBullets[b].Owner))
                {
                    throw new SimInvariantException("proto: bullets are not in ascending id order");
                }
                prev = _protoBullets[b].Id.Value;
            }
            _protoBulletCount = n;
        }

        partial void Snapshot_Save_SpellTable(SimWorld target)
        {
            new ReadOnlySpan<ProtoLoadout>(_protoLoadouts).CopyTo(target._protoLoadouts);
            target._protoReady = _protoReady;
        }

        partial void Snapshot_Load_SpellTable(SimWorld source)
        {
            new ReadOnlySpan<ProtoLoadout>(source._protoLoadouts).CopyTo(_protoLoadouts);
            _protoReady = source._protoReady;
        }

        // 내용(시제품이 켜져 있을 때만): ready bool, 플레이어마다(슬롯 순) ProtoLoadout.Write. 준비 전에는 조립 칸이 기본값이다
        partial void State_Write_SpellTable(ref StateWriter w)
        {
            if (!ProtoOn)
            {
                return;
            }
            w.Bool(_protoReady);
            for (int i = 0; i < PlayerCount; i++)
            {
                if (_protoReady)
                {
                    _protoLoadouts[i].Write(ref w);
                }
            }
        }

        partial void State_Read_SpellTable(ref StateReader r)
        {
            if (!ProtoOn)
            {
                return;
            }
            _protoReady = r.Bool();
            for (int i = 0; i < PlayerCount; i++)
            {
                if (_protoReady)
                {
                    _protoLoadouts[i].Read(ref r);
                }
                else
                {
                    _protoLoadouts[i] = default;
                }
            }
        }
    }
}

using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Wizard.Sim.Proto;

namespace Wizard.Sim
{
    // 시제품의 비기 쪽(M1-SIM-09): 커맨드 판정기(M1-SIM-08)와 효과가 임시인 기술 ①②⑥, 섬 간격 보고(GDD 16 1단계, spec 03 21절 끝).
    // 실제 비기 공통(M5-ART-01)이 같은 훅을 구현하므로 그 작업 전에 이 폴더가 빠져야 한다(결과 보고).
    //
    // 2단계(입력 이벤트마다, Player 다음): Player 가 낸 결과(PlayerLastInput)를 CommandInputRouter 로 판정기에 옮긴다.
    //   방향이 판정기에 들어가면 CmdDirInput, K 를 누르면 섬 간격 보고(PracticeGapReport)를 낸다.
    //   판정(발동·불발)은 이 틱 5단계에 낸다(spec 01 3.8의 5단계 "이번 틱에 판정된 K").
    // 5단계: 이번 틱의 판정 → 보관된 K(TryTakeBuffered(틱 × 256), T-52) 순으로 효과를 낸다. 결정은 무한이다(ProtoWallet, GDD 13).
    //   ① 마력 방출(K): 트리 1회, 피해 2T(탄마다 ×2), 열 없음(J 발사가 아니다).
    //   ② 반동 사격(뒤, K): 트리 1회, 그 탄에 맞은 적은 탄 방향으로 2칸 밀린다. 시전자가 뒤로 2칸 가는 것은 빠졌다 [임시: Player 에 위치를 옮기는 연산이 없다].
    //   ⑥ 꿰뚫는 창(앞, 앞, K): 시야 방향의 즉발 선분(폭 0.5, 사거리 12) 위의 슬라임 전부에 3T. 속성은 코어 블록의 속성.
    // SimConfig.SkillEnabled 가 꺼져 있으면 판정기에 아무것도 넣지 않는다.
    public sealed partial class SimWorld
    {
        // ── 2단계

        partial void Stage02_Arts(in PlayerInput input, ISimEventSink sink)
        {
            if (!ProtoOn || !_config.SkillEnabled)
            {
                return;
            }
            EnsureProto(sink);
            int i = input.Slot.Value;
            CommandParser parser = _protoParsers[i];
            ref readonly PlayerInputResult r = ref PlayerLastInput(i);
            bool skillPress = r.Accepted && r.Key == LogicalKey.Skill && r.Down;
            // 섬 간격(spec 03 21절 끝 "섬 간격을 밀리초로 표시"): 마지막 방향 입력에서 K 까지(입력 시각 단위). 자세 밖이거나 방향이 없으면 −1.
            // 판정이 커서와 기록을 비우므로 판정 전에 센다
            long gap = skillPress && parser.InStance && parser.DirCount > 0 ? unchecked(r.Time - parser.RecentDirTime(0)) : -1L;
            bool bufferedBefore = parser.HasBuffered;
            bool decided = CommandInputRouter.Route(parser, in r, _protoWallets[i], in _protoUnlocks, out ArtDecision decision);
            if (skillPress && !bufferedBefore && parser.HasBuffered)
            {
                // ArtKBuffered: 재사용 대기 끝에 나갈 K 를 보관했다(spec 03 10절). A = 대기 끝(입력 시각, int 로 포화)
                var kb = new SimEvent
                {
                    Type = SimEventType.ArtKBuffered, Slot = input.Slot, Tick = _tick, A = FixMath.SaturateToInt(parser.CooldownEnd),
                    V = parser.CooldownEnd,
                };
                sink.Emit(in kb);
            }
            if (r.Accepted && r.HasDirection && parser.InStance)
            {
                // CmdDirInput: A = CmdDir, B = 앞 입력과 이어짐(0/1)
                var d = new SimEvent
                {
                    Type = SimEventType.CmdDirInput, Slot = input.Slot, Tick = _tick, A = (int)r.Direction, B = parser.LastDirChained ? 1 : 0,
                };
                sink.Emit(in d);
            }
            if (skillPress)
            {
                // PracticeGapReport: A = 간격(입력 시각 단위, int 로 포화, 없으면 −1), B = 판정한 기술(보관·무시면 0), C = 판정함(0/1), V = 간격(long)
                var g = new SimEvent
                {
                    Type = SimEventType.PracticeGapReport, Slot = input.Slot, Tick = _tick,
                    A = FixMath.SaturateToInt(gap), B = decided ? (int)decision.Art : 0, C = decided ? 1 : 0, V = gap,
                };
                sink.Emit(in g);
            }
            if (!decided)
            {
                return;
            }
            int n = _protoArtPendingCounts[i];
            if (n >= ProtoTuning.ArtDecisionsPerTick)
            {
                throw new SimInvariantException("proto: more art decisions than input events in a tick");
            }
            _protoArtPending[i * ProtoTuning.ArtDecisionsPerTick + n] = decision;
            _protoArtPendingCounts[i] = n + 1;
        }

        // ── 5단계

        partial void Stage05_Arts(ISimEventSink sink)
        {
            if (!ProtoOn)
            {
                return;
            }
            EnsureProto(sink);
            long tickStart = SimTime.InputTime(_tick, 0);
            for (int i = 0; i < PlayerCount; i++)
            {
                int n = _protoArtPendingCounts[i];
                _protoArtPendingCounts[i] = 0;
                for (int k = 0; k < n; k++)
                {
                    ApplyProtoArt(i, in _protoArtPending[i * ProtoTuning.ArtDecisionsPerTick + k], sink);
                }
                if (_config.SkillEnabled && _protoParsers[i].TryTakeBuffered(tickStart, out ArtDecision buffered))
                {
                    ApplyProtoArt(i, in buffered, sink);
                }
            }
        }

        private void ApplyProtoArt(int i, in ArtDecision d, ISimEventSink sink)
        {
            PlayerSlot slot = SlotAt(i);
            if (!d.IsFire)
            {
                // ArtFizzleNoCrystal·ArtFizzleLate: A = 기술, B = 늦은 입력 번호, V = 늦은 양(입력 시각 단위)
                SimEventType t = d.Reason == FizzleReason.NoCrystal ? SimEventType.ArtFizzleNoCrystal : SimEventType.ArtFizzleLate;
                var f = new SimEvent { Type = t, Slot = slot, Tick = _tick, A = (int)d.Art, B = d.LateIndex, V = d.LateByUnits };
                sink.Emit(in f);
                return;
            }
            // ArtCast: A = 기술, B = 쓴 결정, C = 섬(0/1), V = 판정 시각(입력 시각)
            var c = new SimEvent
            {
                Type = SimEventType.ArtCast, Slot = slot, Tick = _tick, A = (int)d.Art, B = d.CrystalsPaid, C = d.IsJust ? 1 : 0, V = d.JudgeTime,
            };
            sink.Emit(in c);
            ref readonly PlayerState p = ref PlayerAt(i);
            switch (d.Art)
            {
                case ArtId.Burst:
                {
                    int made = SpawnProtoTree(i, ProtoTuning.BurstMulPermille, false, 0, sink);
                    EmitProtoArt(sink, SimEventType.ArtManaReleaseFire, slot, made, in p);
                    break;
                }
                case ArtId.Recoil:
                {
                    int made = SpawnProtoTree(i, ProtoTuning.RecoilMulPermille, true, 0, sink);
                    EmitProtoArt(sink, SimEventType.ArtRecoilShotFire, slot, made, in p);
                    break;
                }
                case ArtId.Spear:
                    FireProtoSpear(i, in p, sink);
                    break;
                default:
                    throw new SimInvariantException("proto: the prototype trie produced an art it has no effect for");
            }
        }

        // 꿰뚫는 창: 판정 위치(p.Pos)에서 시야 방향으로 길이 12의 선분. 슬라임의 중심이 선분에서 (반폭 + 몸 반지름) 안이면 맞는다(id 순).
        // 즉발이라 탄을 만들지 않는다. 전장 밖에는 적이 없으므로 벽에서 자르지 않는다
        private void FireProtoSpear(int i, in PlayerState p, ISimEventSink sink)
        {
            PlayerSlot slot = SlotAt(i);
            ProtoLoadout l = _protoLoadouts[i];
            Element element = _db.Block(l.Core).Element;
            Fix64 damage = Fix64.MulPermille(ProtoStandardT(i), ProtoTuning.SpearMulPermille);
            Angle facing = Angle.FromDir8(PlayerFireDirection(i));
            FixVec2 dir = FixVec2.FromAngle(facing);
            Fix64 length = ProtoMilli(ProtoTuning.SpearRangeMilli);
            Fix64 reach = ProtoMilli(ProtoTuning.SpearHalfWidthMilli) + ProtoMilli(ProtoTuning.SlimeBodyMilli);
            int hits = 0;
            for (int e = 0; e < _protoEnemyCount; e++)
            {
                ref ProtoEnemy en = ref _protoEnemies[e];
                if (en.Kind != ProtoEnemyKind.Slime)
                {
                    continue;
                }
                FixVec2 rel = en.Pos - p.Pos;
                Fix64 along = FixVec2.Dot(rel, dir);
                if (along < Fix64.Zero || along > length)
                {
                    continue;
                }
                FixVec2 off = rel - dir * along;
                if (off.LengthSq() <= reach * reach)
                {
                    AddProtoDamage(ref en, damage, element, slot, EntityId.None, sink);
                    hits++;
                }
            }
            // ArtPiercingLanceFire: A = 맞은 적 수, B = 방향(센티도), V = 적 하나당 피해(속성 배율 전, Raw), X·Y = 시작점
            var e2 = new SimEvent
            {
                Type = SimEventType.ArtPiercingLanceFire, Slot = slot, Tick = _tick, A = hits, B = facing.Centi, V = damage.Raw,
                X = p.Pos.X.Raw, Y = p.Pos.Y.Raw,
            };
            sink.Emit(in e2);
        }

        // ArtManaReleaseFire·ArtRecoilShotFire: A = 낸 탄 수, X·Y = 시전 위치
        private void EmitProtoArt(ISimEventSink sink, SimEventType type, PlayerSlot slot, int made, in PlayerState p)
        {
            var e = new SimEvent { Type = type, Slot = slot, Tick = _tick, A = made, X = p.Pos.X.Raw, Y = p.Pos.Y.Raw };
            sink.Emit(in e);
        }

        // ── 상태 훅: Arts(7, 스칼라) = 시제품의 준비 표시와 조립(ProtoWriteLoadout), 그 뒤에 판정기 상태(커서, 방향 시각, 자세, 재사용 대기, 보관된 K).
        //    준비 표시와 조립은 M2-SPL-02 전에는 절 6(SpellTable)에 있었다. 그 절은 이제 실제 표의 것이다(DECISIONS-TECH T-57)

        partial void Snapshot_Save_Arts(SimWorld target)
        {
            ProtoSaveLoadout(target);
            for (int i = 0; i < PlayerCount; i++)
            {
                target._protoParsers[i].CopyFrom(_protoParsers[i]);
            }
        }

        partial void Snapshot_Load_Arts(SimWorld source)
        {
            source.ProtoSaveLoadout(this);
            for (int i = 0; i < PlayerCount; i++)
            {
                _protoParsers[i].CopyFrom(source._protoParsers[i]);
            }
            Array.Clear(_protoArtPendingCounts, 0, _protoArtPendingCounts.Length);   // 다른 시간선의 임시 값을 남기지 않는다
        }

        // 내용(시제품이 켜져 있을 때만): 준비 표시와 조립, 플레이어마다(슬롯 순) ICommandParser.WriteState
        partial void State_Write_Arts(ref StateWriter w)
        {
            if (!ProtoOn)
            {
                return;
            }
            ProtoWriteLoadout(ref w);
            for (int i = 0; i < PlayerCount; i++)
            {
                _protoParsers[i].WriteState(ref w);
            }
        }

        partial void State_Read_Arts(ref StateReader r)
        {
            if (!ProtoOn)
            {
                return;
            }
            ProtoReadLoadout(ref r);
            Array.Clear(_protoArtPendingCounts, 0, _protoArtPendingCounts.Length);
            for (int i = 0; i < PlayerCount; i++)
            {
                _protoParsers[i].ReadState(ref r);
            }
        }
    }
}

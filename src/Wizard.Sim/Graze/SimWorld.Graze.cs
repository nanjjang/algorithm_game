using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Graze;
using Wizard.Sim.Player;

namespace Wizard.Sim
{
    // Graze 모듈(절 3). spec 01 8절(스쳐 피하기), 12.2(결투 스침), 3.9(필드).
    // 훅: Stage09_Graze(이번 틱의 후보 판정과 보상, 가속 끝). 상태 훅 넷.
    //
    // 흐름(spec 01 3.8, CONTRACTS 3.2 의 9단계 "피격 판정 뒤 스침 판정"):
    //   1. 9단계의 충돌 모듈(적 탄·돌진·예고 구역을 가진 쪽)이 같은 단계에서 피격 후보는 QueuePlayerHit 로,
    //      스침 후보는 OfferGrazeThreat 로 넣는다(플레이어마다, 개체 id 순이 아니어도 된다).
    //   2. Stage09_Graze 가 플레이어마다(슬롯 순) 후보를 위협 id 순으로 판정한다. 이번 틱에 피격 후보가 있거나
    //      피격 후 무적 중이면 아무것도 인정하지 않는다(8.3 [임시], T-G21·T-G22). 이미 인정한 시전 묶음은 다시 인정하지 않는다.
    //      이 훅은 9단계의 맨 뒤에 불린다(CONTRACTS 3.2 "단계 안 순서의 예외", T-50). 그래서 Spell·Enemies·Duel 의
    //      9단계 훅이 넣은 후보를 전부 본 뒤에 판정한다.
    //   3. 인정된 스침마다 보상(8.4, 12.2): 시전 묶음 기록, (결투) 스침 간격, 가속 기한 갱신, 그리고 뼈대의 RaiseGrazeAwarded.
    //      열(임계 시작으로 올림·잠금 해제·열 면제), 콤보(+2, 결투 +1), 피뢰침은 그 상태를 가진 모듈이 이음 훅
    //      (OnGrazeAwarded_Heat·Combo·Characters)에서 그 순간에 반영한다. 이 모듈은 남의 상태를 쓰지 않고 남의 메서드를 부르지 않는다.
    //      마지막에 GrazeAwarded 를 낸다. 가속과 이음 훅은 서로 다른 상태라 8.4 의 순서(열 → 가속 → 콤보 → 피뢰침)와 결과가 같다.
    //
    // 다른 모듈이 쓰는 문(CONTRACTS 3.2 "모듈 사이의 이음"):
    //   연산 — OfferGrazeThreat(9단계의 충돌 모듈), ReleaseGrazeCast(시전을 가진 쪽), AwardSkillGraze(비기, 5단계), EndGrazeBuff(웨이브 끝)
    //   읽기 — GrazeBuffActive(Player 의 4단계 이동), GrazeAwardsThisTick(점수처럼 순서를 따지지 않는 값), GrazeHitRadius
    //
    // 팩에 characters 절이 없으면(뼈대 시험의 빈 팩) Player 모듈과 같이 꺼진다: 훅은 아무것도 하지 않고 절의 내용은 비어 있다.
    public sealed partial class SimWorld
    {
        // 자료구조 크기. 이미 인정한 시전 묶음 목록(플레이어마다). 넘치면 가장 오래된(가장 작은) id 를 지운다 [임시: spec 01 8.2 는
        // "시전의 모든 탄이 사라지면 지운다"만 정한다. 적 탄 상한 150(GDD 4) 보다 넉넉하다]
        internal const int GrazedCastCapacity = 256;

        // 자료구조 크기. 한 틱에 한 플레이어에게 들어오는 스침 후보의 최대 수(적 탄 상한 150 + 돌진 + 구역보다 넉넉하다)
        internal const int GrazeOfferCapacity = 512;

        // 상태(절 3). 색인 = 슬롯
        private readonly GrazeState[] _graze = new GrazeState[MaxPlayers];
        private readonly SortedIntList[] _grazedCasts = NewGrazedCastLists();
        private readonly GrazeThreat[] _grazeOffers = new GrazeThreat[MaxPlayers * GrazeOfferCapacity];   // 슬롯 × 용량, 앞에서부터
        private readonly int[] _grazeOfferCounts = new int[MaxPlayers];

        private static SortedIntList[] NewGrazedCastLists()
        {
            var lists = new SortedIntList[MaxPlayers];
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = new SortedIntList(GrazedCastCapacity);
            }
            return lists;
        }

        internal bool GrazeModuleOn => PlayerModuleOn;

        // ── 충돌 모듈이 부르는 것(같은 어셈블리 안)

        // 9단계: 이번 틱의 스침 후보 하나를 넣는다. 그 플레이어에게 Grazeable 인 위협만 넣으면 되지만, 아닌 것을 넣어도 판정이 거른다.
        // 구역은 터지는 틱에만 스침이 되므로 터지지 않는 틱에는 넣지 않아도 된다(넣어도 거른다)
        internal void OfferGrazeThreat(PlayerSlot slot, in GrazeThreat threat)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("graze: threat for a slot that is not in this world");
            }
            if (!threat.IsWellFormed)
            {
                throw new SimInvariantException("graze: threat needs a kind, an entity id, a cast id and a non-negative radius");
            }
            if (!GrazeModuleOn)
            {
                return;
            }
            int i = slot.Value;
            int n = _grazeOfferCounts[i];
            if (n >= GrazeOfferCapacity)
            {
                throw new SimInvariantException("graze: too many threats offered to one player in a tick");
            }
            _grazeOffers[i * GrazeOfferCapacity + n] = threat;
            _grazeOfferCounts[i] = n + 1;
        }

        // 시전의 모든 탄·구역·돌진이 사라졌을 때 그 시전을 가진 쪽이 부른다(spec 01 8.2 [임시]). 모든 플레이어의 목록에서 지운다
        internal void ReleaseGrazeCast(int castId)
        {
            if (!GrazeModuleOn)
            {
                return;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                _grazedCasts[i].Remove(castId);
            }
        }

        // 비기가 직접 인정하는 스침 1회(spec 01 8.1 10번: 벼락 질주, 잔상 걸음의 GrazeSystem.AwardOnce(p, source: Skill)).
        // 피격·무적 검사와 시전 묶음 기록이 없다. 결투의 스침 간격과 보상 종류는 같다
        internal void AwardSkillGraze(PlayerSlot slot, ISimEventSink sink)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("graze: skill graze for a slot that is not in this world");
            }
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }
            if (!GrazeModuleOn)
            {
                return;
            }
            if (!PlayersSpawned)
            {
                throw new SimInvariantException("graze: skill graze before the players exist");
            }
            int i = slot.Value;
            AwardGraze(i, 0, GrazeKind.Skill, EntityId.None, PlayerAt(i).Pos, sink);
        }

        // 웨이브 끝의 가속 초기화(spec 01 10.6 OnWaveEnd 의 "p.GrazeBuffUntil = 0" [임시]). 켜져 있던 가속이면 GrazeBuffEnded 를 한 번 낸다.
        // 인정한 시전 묶음과 결투의 스침 간격은 그대로 둔다(명세가 가속만 적었다)
        internal void EndGrazeBuff(PlayerSlot slot, ISimEventSink sink)
        {
            if (!IsValidSlot(slot))
            {
                throw new SimInvariantException("graze: buff end for a slot that is not in this world");
            }
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }
            if (!GrazeModuleOn)
            {
                return;
            }
            int i = slot.Value;
            ref GrazeState s = ref _graze[i];
            s.BuffUntil = 0U;
            if (s.BuffActive)
            {
                s.BuffActive = false;
                EmitGraze(sink, SimEventType.GrazeBuffEnded, i, EntityId.None, 0, GrazeKind.Bullet, FixVec2.Zero);
            }
        }

        // ── 다른 모듈과 시험이 읽는 값

        // 이번 틱에 인정된(보상이 있는) 스침 수. 시전 묶음마다 1, 비기 스침 포함. 결투에서 간격에 걸려 연출만 난 것은 세지 않는다.
        // 순서를 따지지 않는 값(점수의 스침 횟수, spec 01 8.1 의 11번)이 같은 틱의 뒤 단계에서 읽는다.
        // 열·콤보·피뢰침은 이 값을 읽지 않고 이음 훅으로 그 순간에 받는다(T-50)
        internal int GrazeAwardsThisTick(int index)
        {
            ref readonly GrazeState s = ref GrazeAt(index);
            return s.AwardTick == _tick ? s.AwardCount : 0;
        }

        // 스침 가속 중인가(spec 01 5.6, 8.4). 이동(4단계)이 Movement.MovePermille 에 넘긴다
        internal bool GrazeBuffActive(int index) => _tick < GrazeAt(index).BuffUntil;

        internal ref readonly GrazeState GrazeAt(int index)
        {
            RequireGrazeIndex(index);
            return ref _graze[index];
        }

        internal bool GrazeCastRecorded(int index, int castId)
        {
            RequireGrazeIndex(index);
            return _grazedCasts[index].Contains(castId);
        }

        internal int GrazeCastCount(int index)
        {
            RequireGrazeIndex(index);
            return _grazedCasts[index].Count;
        }

        internal int GrazeOfferCount(int index)
        {
            RequireGrazeIndex(index);
            return _grazeOfferCounts[index];
        }

        // 플레이어의 피격 반지름. 결투는 SimConfig 의 값(0.30), 그 밖은 캐릭터 값(0.15)(spec 01 12.1)
        internal Fix64 GrazeHitRadius(int index)
        {
            CharacterDef def = _db.Character(PlayerAt(index).Character);
            return _config.HitRadiusOverrideOn ? _config.HitRadiusOverride : def.HitRadius;
        }

        // ── 단계 훅

        // 9단계. 플레이어는 슬롯 순, 후보는 위협 id 순
        partial void Stage09_Graze(ISimEventSink sink)
        {
            if (!GrazeModuleOn)
            {
                return;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                int n = _grazeOfferCounts[i];
                _grazeOfferCounts[i] = 0;                               // 후보는 이 틱에만 뜻이 있다
                if (!PlayersSpawned)
                {
                    continue;
                }
                ExpireGrazeBuff(i, sink);
                if (n == 0 || !CanGrazeNow(i))
                {
                    continue;
                }
                var offers = new Span<GrazeThreat>(_grazeOffers, i * GrazeOfferCapacity, n);
                DetSort.InsertionStable(offers, new GrazeThreatById());
                ref readonly PlayerState p = ref PlayerAt(i);
                Fix64 grazeRadius = _db.Character(p.Character).GrazeRadius;
                Fix64 hitRadius = GrazeHitRadius(i);
                SortedIntList casts = _grazedCasts[i];
                for (int k = 0; k < n; k++)
                {
                    ref readonly GrazeThreat t = ref offers[k];
                    if (!t.Grazeable || t.Blocked || casts.Contains(t.CastId))
                    {
                        continue;
                    }
                    bool grazed = t.Kind == GrazeKind.Zone
                        ? t.ZoneDetonatesNow && GrazeRules.ZoneEdge(t.ZoneContainsPlayer, t.ZoneEdgeDistance, grazeRadius)
                        : GrazeRules.InBand(p.Pos, t.Pos, t.Radius, grazeRadius, hitRadius);
                    if (grazed)
                    {
                        AwardGraze(i, t.CastId, t.Kind, t.Id, t.Pos, sink);
                    }
                }
            }
        }

        // spec 01 8.3 의 앞 두 줄. 피격 후 무적 중이면 인정하지 않는다 [임시]. 이번 틱에 맞았으면(피격 후보가 있으면) 인정하지 않는다 [임시].
        // 쓰러진 뒤(체력 0)에도 인정하지 않는다 [임시: 명세가 정하지 않았다]. 비기의 스침 창(InSkillGrazeWindow)은 비기 모듈이 생기면 더한다
        private bool CanGrazeNow(int i)
        {
            ref readonly PlayerState p = ref PlayerAt(i);
            if (p.HpPoints <= 0)
            {
                return false;
            }
            if (_tick < p.InvulnUntil)
            {
                return false;
            }
            return !p.Hit.Active;
        }

        // spec 01 8.4 Award, 12.2 DuelGraze. 같은 틱에 여러 시전이 인정되면 시전마다 부른다(8.3)
        private void AwardGraze(int i, int castId, GrazeKind kind, EntityId source, FixVec2 pos, ISimEventSink sink)
        {
            if (castId > 0)
            {
                RecordGrazedCast(i, castId);
            }
            ref GrazeState s = ref _graze[i];
            // 결투: 상대 1명당 1.0초에 1회(12.2). 간격 안이면 연출만 내고 보상은 없다. 시전 묶음은 이미 기록했다(묶음당 1회)
            int cooldown = _config.GrazeCooldownTicks;
            if (cooldown > 0)
            {
                if (_tick < s.CooldownReadyTick)
                {
                    EmitGraze(sink, SimEventType.GrazeCosmeticOnly, i, source, castId, kind, pos);
                    return;
                }
                s.CooldownReadyTick = unchecked(_tick + (uint)cooldown);
            }
            if (s.AwardTick != _tick)
            {
                s.AwardTick = _tick;
                s.AwardCount = 0;
            }
            s.AwardCount = unchecked(s.AwardCount + 1);
            // 가속은 전부 보상(무한 웨이브)에만 있다(12.2 "가속은 없다")
            if (_config.GrazeFullReward)
            {
                RefreshGrazeBuff(i, sink);
            }
            // 열·콤보·피뢰침: 그 상태를 가진 모듈이 지금 반영한다(순서는 뼈대의 한 곳. 전부 보상인지는 받는 모듈이 SimConfig 로 가른다)
            RaiseGrazeAwarded(SlotAt(i), sink);
            EmitGraze(sink, SimEventType.GrazeAwarded, i, source, castId, kind, pos);
        }

        private void RecordGrazedCast(int i, int castId)
        {
            SortedIntList casts = _grazedCasts[i];
            if (casts.Contains(castId))
            {
                return;
            }
            if (casts.Count >= GrazedCastCapacity)
            {
                casts.Remove(casts[0]);                                 // 가장 오래된 시전 [임시]. 목록은 자라지 않는다(할당 없음)
            }
            casts.Add(castId);
        }

        // 가속 기한 갱신(중첩 없음, T-G08). 꺼져 있던 가속이 켜질 때만 GrazeBuffStarted
        private void RefreshGrazeBuff(int i, ISimEventSink sink)
        {
            ExpireGrazeBuff(i, sink);
            ref GrazeState s = ref _graze[i];
            int buffTicks = _db.Character(PlayerAt(i).Character).GrazeBuffTicks;
            uint until = GrazeRules.BuffUntil(_tick, buffTicks);
            if (until <= _tick)
            {
                return;
            }
            s.BuffUntil = until;
            if (!s.BuffActive)
            {
                s.BuffActive = true;
                EmitGraze(sink, SimEventType.GrazeBuffStarted, i, EntityId.None, 0, GrazeKind.Bullet, FixVec2.Zero);
            }
        }

        // 기한이 지난 가속을 끈다(GrazeBuffEnded 는 한 번만). 이동은 4단계에서 이미 t < BuffUntil 로 읽었다
        private void ExpireGrazeBuff(int i, ISimEventSink sink)
        {
            ref GrazeState s = ref _graze[i];
            if (s.BuffActive && _tick >= s.BuffUntil)
            {
                s.BuffActive = false;
                EmitGraze(sink, SimEventType.GrazeBuffEnded, i, EntityId.None, 0, GrazeKind.Bullet, FixVec2.Zero);
            }
        }

        // 칸은 CONTRACTS 3.3 "스침 이벤트의 칸".
        // GrazeAwarded·GrazeCosmeticOnly: Source = 위협 id(비기는 없음), A = 시전 묶음(비기는 0), B = GrazeKind, X·Y = 위협 위치(Raw).
        // C(chainIndex)는 0이다: 연속 스침의 음높이는 표시 계층이 센다(CONTRACTS 0.3). GrazeBuffStarted·GrazeBuffEnded: 인자 없음
        private void EmitGraze(ISimEventSink sink, SimEventType type, int i, EntityId source, int castId, GrazeKind kind, FixVec2 pos)
        {
            bool hasArgs = type == SimEventType.GrazeAwarded || type == SimEventType.GrazeCosmeticOnly;
            var e = new SimEvent
            {
                Type = type,
                Slot = SlotAt(i),
                Tick = _tick,
                Source = source,
                A = castId,
                B = hasArgs ? (int)kind : 0,
                X = pos.X.Raw,
                Y = pos.Y.Raw,
            };
            sink.Emit(in e);
        }

        private void RequireGrazeIndex(int index)
        {
            if ((uint)index >= (uint)PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        // ── 뷰 훅(CONTRACTS 3.3, T-54). PlayerView 의 Graze 칸: 스침 가속 중인가(spec 01 17.1 GrazeBuffActive). 상태를 바꾸지 않는다
        partial void View_Player_Graze(int index, ref PlayerViewDraft draft)
        {
            draft.GrazeBuffActive = GrazeBuffActive(index);
        }

        // ── 상태 훅

        partial void Snapshot_Save_Graze(SimWorld target) => CopyGrazeState(this, target);

        partial void Snapshot_Load_Graze(SimWorld source) => CopyGrazeState(source, this);

        // 값 복사. 할당 없음(목록 용량이 같다). 후보는 쓰는 칸만 옮긴다
        private static void CopyGrazeState(SimWorld from, SimWorld to)
        {
            new ReadOnlySpan<GrazeState>(from._graze).CopyTo(to._graze);
            for (int i = 0; i < from.PlayerCount; i++)
            {
                to._grazedCasts[i].CopyFrom(from._grazedCasts[i]);
                int n = from._grazeOfferCounts[i];
                new ReadOnlySpan<GrazeThreat>(from._grazeOffers, i * GrazeOfferCapacity, n)
                    .CopyTo(new Span<GrazeThreat>(to._grazeOffers, i * GrazeOfferCapacity, n));
                to._grazeOfferCounts[i] = n;
            }
        }

        // 내용(모듈이 켜져 있을 때만): 플레이어마다(슬롯 순) GrazeState, 인정한 시전 묶음(개수, 오름차순 id), 이번 틱의 후보(개수, 넣은 순서).
        // 후보는 틱 경계에서 보통 비어 있다. Step 밖에서 넣은 후보도 되감기·해시가 어긋나지 않게 같이 쓴다(PendingHit 과 같다)
        partial void State_Write_Graze(ref StateWriter w)
        {
            if (!GrazeModuleOn)
            {
                return;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                _graze[i].Write(ref w);
                SortedIntList casts = _grazedCasts[i];
                w.Count(casts.Count);
                for (int k = 0; k < casts.Count; k++)
                {
                    w.I32(casts[k]);
                }
                int n = _grazeOfferCounts[i];
                w.Count(n);
                for (int k = 0; k < n; k++)
                {
                    _grazeOffers[i * GrazeOfferCapacity + k].Write(ref w);
                }
            }
        }

        partial void State_Read_Graze(ref StateReader r)
        {
            if (!GrazeModuleOn)
            {
                return;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                _graze[i].Read(ref r);
                int c = r.Count();
                if (c > GrazedCastCapacity)
                {
                    throw new SimInvariantException("graze: too many grazed casts in state");
                }
                SortedIntList casts = _grazedCasts[i];
                casts.Clear();
                int prev = 0;
                for (int k = 0; k < c; k++)
                {
                    int v = r.I32();
                    if (v <= prev)
                    {
                        throw new SimInvariantException("graze: grazed casts are not ascending positive ids");
                    }
                    casts.Add(v);
                    prev = v;
                }
                int n = r.Count();
                if (n > GrazeOfferCapacity)
                {
                    throw new SimInvariantException("graze: too many threats in state");
                }
                for (int k = 0; k < n; k++)
                {
                    _grazeOffers[i * GrazeOfferCapacity + k].Read(ref r);
                }
                _grazeOfferCounts[i] = n;
            }
        }
    }
}

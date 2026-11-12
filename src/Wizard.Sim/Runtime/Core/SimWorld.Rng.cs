using System;
using Wizard.Core;

namespace Wizard.Sim
{
    // CONTRACTS 3.2 훅 표의 Rng(16) 절과 CONTRACTS 1.3 "세계가 보관"인 난수 흐름. 훅이 없는 Runtime/Core 의 절이다.
    // 이 절에는 어느 모듈에도 속하지 않는 세계 공통 스칼라(모드, 플레이어 수, 씨앗, 틱, 끝남, 개체 id 카운터)도 같이 든다.
    //
    // 보관 흐름: WaveSpawn, HpShard, Event, EnemyAi, Mutation, Weather, BossAi, DuelWild(RngStream 의 주석).
    // 웨이브(결투는 라운드) 시작 때 모듈이 DeriveHeldRng 로 다시 만든다. 앞 웨이브의 소비가 뒤로 번지지 않는다(spec 07 14.2).
    // 세계 생성 때는 wave = 0 으로 만들어 둔다 [임시: 첫 웨이브·라운드 시작 때 그 번호로 다시 파생하므로 결과에 닿지 않는다].
    // "호출 때 파생"인 흐름(WaveComp, Modifier, Loot, ArtUnlock)은 상태가 없다. 모듈이 RngStreams.Derive(RunSeed, …)로 그 자리에서 만든다.
    public sealed partial class SimWorld
    {
        private const int RngSlots = (int)RngStream.DuelWild + 1;        // RngStream 값으로 바로 찾는 표의 크기(0은 비어 있다)
        private const int InitialRngWave = 0;

        private readonly Pcg32[] _rng = new Pcg32[RngSlots];

        // 보관 흐름 하나. 모듈은 이 참조로 뽑는다(값 복사로 뽑으면 소비가 남지 않는다)
        internal ref Pcg32 HeldRng(RngStream stream)
        {
            if (!IsHeld(stream))
            {
                throw new SimInvariantException("rng: stream is not held by the world");
            }
            return ref _rng[(int)stream];
        }

        // 보관 흐름 하나를 (씨앗, 흐름, wave, sub)로 다시 만든다. DuelWild 는 wave = 라운드 번호
        internal void DeriveHeldRng(RngStream stream, int wave, int sub = 0)
        {
            if (!IsHeld(stream))
            {
                throw new SimInvariantException("rng: stream is not held by the world");
            }
            _rng[(int)stream] = RngStreams.Derive(_seed, stream, wave, sub);
        }

        // 보관 흐름 전부를 wave 로 다시 만든다(웨이브·라운드 시작)
        internal void DeriveAllHeldRng(int wave)
        {
            for (int s = 1; s < RngSlots; s++)
            {
                if (IsHeld((RngStream)s))
                {
                    _rng[s] = RngStreams.Derive(_seed, (RngStream)s, wave);
                }
            }
        }

        private static bool IsHeld(RngStream stream)
        {
            switch (stream)
            {
                case RngStream.WaveSpawn:
                case RngStream.HpShard:
                case RngStream.Event:
                case RngStream.EnemyAi:
                case RngStream.Mutation:
                case RngStream.Weather:
                case RngStream.BossAi:
                case RngStream.DuelWild:
                    return true;
                default:
                    return false;
            }
        }

        private void InitRng() => DeriveAllHeldRng(InitialRngWave);

        // 내용: mode u8, player_count u8, seed u64, tick u32, is_over bool, last_entity_id i32,
        //       보관 흐름마다(RngStream 오름차순) state u64, inc u64
        private void State_Write_Rng(ref StateWriter w)
        {
            w.U8((byte)_mode);
            w.U8((byte)_playerSlots.Length);
            w.U64(_seed);
            w.U32(_tick);
            w.Bool(_isOver);
            w.I32(_lastEntityId);
            for (int s = 1; s < RngSlots; s++)
            {
                if (IsHeld((RngStream)s))
                {
                    w.U64(_rng[s].State);
                    w.U64(_rng[s].Inc);
                }
            }
        }

        // 모양(모드, 플레이어 수, 씨앗)은 세계를 만들 때 정해진다. 다르면 다른 판의 상태다
        private void State_Read_Rng(ref StateReader r)
        {
            if (r.U8() != (byte)_mode || r.U8() != (byte)_playerSlots.Length || r.U64() != _seed)
            {
                throw new SimInvariantException("state: world shape (mode, players, seed) differs");
            }
            _tick = r.U32();
            _isOver = r.Bool();
            _lastEntityId = r.I32();
            for (int s = 1; s < RngSlots; s++)
            {
                if (IsHeld((RngStream)s))
                {
                    _rng[s].State = r.U64();
                    _rng[s].Inc = r.U64();
                }
            }
        }

        // 스냅숏의 세계 공통 부분(값 복사, 할당 없음)
        private void CopyCoreTo(SimWorld target)
        {
            target._tick = _tick;
            target._isOver = _isOver;
            target._lastEntityId = _lastEntityId;
            new ReadOnlySpan<Pcg32>(_rng).CopyTo(target._rng);
        }
    }
}

using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Duel;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Wizard.Sim.Waves;

namespace Wizard.Sim
{
    // CONTRACTS 3.2, spec 01 3절·12절·13절, DECISIONS-TECH T-05·T-14·T-33.
    // 세계 하나 = 판 하나(무한 웨이브, 결투, 연습). 모드 차이는 SimConfig 값이고, 플레이어는 언제나 배열이다(무한 웨이브 1, 결투 2).
    //
    // 이 파일들(Runtime/Core)이 뼈대다: 틱 파이프라인(SimWorld.Step.cs), 훅 선언(SimWorld.Hooks.cs),
    // 상태 절의 순서·해시·직렬화·스냅숏(SimWorld.State.cs), 세계 공통 스칼라와 난수 흐름(SimWorld.Rng.cs).
    // 모듈의 상태와 규칙은 각 모듈 폴더의 SimWorld.<모듈>.cs 에 있다.
    //
    // 스레드: 세계 하나는 스레드 하나에서만 부른다. 세계끼리 나누는 것은 불변인 ContentDb 뿐이다.
    // 가변 정적 필드가 없다. 임시 버퍼는 세계가 가진다(서버는 여러 세계를 여러 스레드에서 동시에 돌린다, T-42).
    public sealed partial class SimWorld
    {
        // PlayerSlot 은 0..3(CONTRACTS 1.5). 플레이어 배열의 길이 상한
        internal const int MaxPlayers = 4;

        private const int EndlessPlayers = 1;
        private const int DuelPlayers = 2;
        private const int PracticePlayers = 1;

        private readonly ContentDb _db;
        private readonly SimConfig _config;
        private readonly SimMode _mode;
        private readonly PlayerSlot[] _playerSlots;       // 길이 = 플레이어 수. _playerSlots[i].Value == i
        private readonly ulong _seed;

        // 시작 조건. 세계 생성 때의 값이고 상태가 아니다(해시에 들어가지 않는다). 생성 뒤 호출자가 바꾸지 않는다.
        // 셋 가운데 모드에 맞는 하나만 null 이 아니다.
        private readonly EndlessSetup? _endlessSetup;
        private readonly DuelSetup? _duelSetup;
        private readonly PracticeSetup? _practiceSetup;

        // 세계 공통 스칼라(Rng 절에 들어간다)
        private uint _tick;
        private bool _isOver;
        private int _lastEntityId;                        // 마지막으로 준 개체 id. 0이면 아직 없음(EntityId 는 1부터)

        // 세계가 가진 임시 버퍼(상태 아님)
        private readonly int[] _inputCountScratch;        // Step 전제 검사: 플레이어별 이벤트 수
        private readonly NullEventSink _nullSink;         // CanApply 의 확인 단계용(이벤트를 내지 않는다)
        private readonly SimWorldView _view;

        private SimWorld(ContentDb db, SimConfig config, SimMode mode, int playerCount, ulong seed,
                         EndlessSetup? endless, DuelSetup? duel, PracticeSetup? practice)
        {
            _db = db;
            _config = config;
            _mode = mode;
            _seed = seed;
            _endlessSetup = endless;
            _duelSetup = duel;
            _practiceSetup = practice;

            _playerSlots = new PlayerSlot[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                _playerSlots[i] = new PlayerSlot((byte)i);
            }
            _inputCountScratch = new int[playerCount];
            _nullSink = new NullEventSink();
            _view = new SimWorldView(this);

            _tick = 0;
            _isOver = false;
            _lastEntityId = 0;
            InitRng();
        }

        public static SimWorld CreateEndless(ContentDb db, SimConfig config, EndlessSetup setup)
        {
            RequireArgs(db, config, SimMode.Endless);
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            return new SimWorld(db, config, SimMode.Endless, EndlessPlayers, setup.Seed, setup, null, null);
        }

        // 결투: 플레이어 배열은 setup.Players 의 순서(슬롯 0 = 왼쪽, 1 = 오른쪽)
        public static SimWorld CreateDuel(ContentDb db, SimConfig config, DuelSetup setup)
        {
            RequireArgs(db, config, SimMode.Duel);
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            if (setup.Players == null || setup.Players.Length != DuelPlayers)
            {
                throw new ArgumentException("DuelSetup.Players must have exactly two entries", nameof(setup));
            }
            for (int i = 0; i < DuelPlayers; i++)
            {
                DuelPlayerSetup? p = setup.Players[i];
                if (p == null || p.Slot.Value != i)
                {
                    throw new ArgumentException("DuelSetup.Players[i] must be non-null with Slot == i", nameof(setup));
                }
            }
            return new SimWorld(db, config, SimMode.Duel, DuelPlayers, setup.Seed, null, setup, null);
        }

        // 연습(허수아비·봇). PracticeSetup 의 칸은 연습 영역의 작업이 채운다. 씨앗이 정해질 때까지 0이다
        public static SimWorld CreatePractice(ContentDb db, SimConfig config, PracticeSetup setup)
        {
            RequireArgs(db, config, SimMode.Practice);
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }
            return new SimWorld(db, config, SimMode.Practice, PracticePlayers, 0UL, null, null, setup);
        }

        // 다음에 처리할 틱
        public uint Tick => _tick;

        public SimMode Mode => _mode;

        public int PlayerCount => _playerSlots.Length;

        public bool IsOver => _isOver;

        public ISimView View => _view;

        // 지금 상태에서 이 명령이 유효한가. 상태를 바꾸지 않는다. RunSession 이 기록 전에 부른다(CONTRACTS 3.1)
        public bool CanApply(in RunCommand command) => IsCommandValid(in command, _nullSink);

        // 무한 웨이브가 끝난 뒤의 결과(CONTRACTS 3.7). 세계가 아는 칸(끝 틱, 최종 해시, 시작 웨이브)만 여기서 채운다.
        // 웨이브·점수·드랍 칸은 Waves 모듈이 채울 자리다(결과 보고의 계약 변경 요청: 결과 훅이 계약에 없다).
        public RunOutcome GetOutcome()
        {
            if (_mode != SimMode.Endless)
            {
                throw new InvalidOperationException("GetOutcome is for endless worlds");
            }
            if (!_isOver)
            {
                throw new InvalidOperationException("GetOutcome before IsOver");
            }
            return new RunOutcome
            {
                EndTick = _tick,
                FinalStateHash = ComputeStateHash(),
                StartWave = _endlessSetup!.StartWave,
            };
        }

        // 결투가 끝난 뒤의 결과(CONTRACTS 3.9). 승자와 라운드 칸은 Duel 모듈이 채울 자리다(GetOutcome 과 같은 사정)
        public DuelOutcome GetDuelOutcome()
        {
            if (_mode != SimMode.Duel)
            {
                throw new InvalidOperationException("GetDuelOutcome is for duel worlds");
            }
            if (!_isOver)
            {
                throw new InvalidOperationException("GetDuelOutcome before IsOver");
            }
            return new DuelOutcome
            {
                EndTick = _tick,
                FinalStateHash = ComputeStateHash(),
            };
        }

        // ── 모듈이 쓰는 세계 공통 값(같은 부분 클래스 안에서만)

        internal ContentDb Db => _db;

        internal SimConfig Config => _config;

        internal ulong RunSeed => _seed;

        internal EndlessSetup? EndlessSetupOrNull => _endlessSetup;

        internal DuelSetup? DuelSetupOrNull => _duelSetup;

        internal PracticeSetup? PracticeSetupOrNull => _practiceSetup;

        // 슬롯 i 의 플레이어(i = 0..PlayerCount-1). 처리는 언제나 이 순서다
        internal PlayerSlot SlotAt(int index) => _playerSlots[index];

        internal bool IsValidSlot(PlayerSlot slot) => slot.Value < _playerSlots.Length;

        // 판을 끝낸다. 끝내는 규칙은 Waves·Duel 모듈의 것이다
        internal void MarkOver() => _isOver = true;

        // 세계 하나에 카운터 하나. 생성 순서대로 1씩(CONTRACTS 1.5). 같은 틱의 여러 생성은 부른 순서대로
        internal EntityId NewEntityId()
        {
            _lastEntityId = unchecked(_lastEntityId + 1);
            return new EntityId(_lastEntityId);
        }

        private static void RequireArgs(ContentDb db, SimConfig config, SimMode mode)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (config.Mode != mode)
            {
                throw new ArgumentException("SimConfig.Mode does not match the world kind", nameof(config));
            }
        }

        // 스냅숏 버퍼 안의 세계: 같은 시작 조건으로 같은 모양을 만든다(모듈 배열의 크기가 같다). Step 하지 않는다
        private SimWorld CreateShadow() =>
            new SimWorld(_db, _config, _mode, _playerSlots.Length, _seed, _endlessSetup, _duelSetup, _practiceSetup);
    }
}

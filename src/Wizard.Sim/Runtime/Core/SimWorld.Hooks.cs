using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 3.2 "부분 클래스와 훅"(T-33). 훅 선언은 여기 한 곳이다. 모듈은 자기 폴더의 SimWorld.<모듈>.cs 에서 구현만 더한다.
    // C# 의 부분 메서드는 구현이 하나뿐이어야 하므로 (단계, 모듈)마다 따로 선언한다. 구현이 없으면 호출이 컴파일에서 사라진다.
    // 부르는 순서는 SimWorld.Step.cs(틱 단계)와 SimWorld.State.cs(상태 절) 두 곳에만 있다. 훅을 더하거나 순서를 바꾸는 것은 계약 변경이다.
    //
    // 훅의 약속:
    //   - 단계 훅은 Step 안에서 불린다. 관리 힙에 할당하지 않는다(Stage00 의 Depart 컴파일만 예외. CONTRACTS 3.2).
    //     다른 모듈을 직접 부르지 않는다. 개체는 id 오름차순, 플레이어는 슬롯 순(0..PlayerCount-1)으로 처리한다.
    //   - 표시 이벤트는 받은 sink 에만 낸다.
    //   - Stage00 훅은 한 명령에 두 번 불린다. 먼저 apply == false 로(확인), 명령이 유효하면 apply == true 로(적용).
    //       확인: 그 op 이 자기 것이고 지금 상태에서 받을 수 있으면 valid = true. 상태를 바꾸지 않고 이벤트를 내지 않는다
    //             (CanApply 도 이 길로 묻는다). 한 모듈이라도 true 로 하면 유효하다.
    //       적용: 확인을 통과한 명령만 온다(valid == true). 자기 op 이면 적용한다.
    //     어느 모듈도 받지 않은 명령은 적용하지 않고 DebugCommandRejected 만 낸다(CONTRACTS 3.1).
    //   - Stage02 훅은 입력 이벤트 하나마다 InputOrder 순으로 불린다(한 이벤트 안에서 Player → Arts).
    //   - Snapshot_Save_<모듈>(target): 자기 상태를 target 세계의 같은 필드로 값 복사한다. Snapshot_Load_<모듈>(source): 그 반대.
    //     target·source 는 같은 모양(같은 ContentDb, 모드, 플레이어 수)의 세계이고 스냅숏 버퍼 안에만 있다. 할당하지 않는다.
    //   - State_Write_<모듈>(ref w): 절의 내용만 정규 직렬화로 쓴다(CONTRACTS 1.4). 같은 함수가 해시도 센다(해시 모드).
    //     State_Read_<모듈>(ref r): 같은 순서로 읽어 상태 전체를 덮어쓴다.
    //     풀 절(Spell, Enemies, Telegraph, Bosses)은 틀까지 모듈이 쓴다: 쓸 때 TryBeginPoolSection(ref w, 절, 개수)가 true 일 때만 내용을 쓰고,
    //     읽을 때 TryBeginPoolSection(ref r, 절)이 false 이면 풀을 비운다. 그래서 빈 풀은 바이트를 남기지 않는다.
    public sealed partial class SimWorld
    {
        // ── 0. RunCommand 적용(순번 순)
        partial void Stage00_Spell(in RunCommand command, bool apply, ref bool valid, ISimEventSink sink);
        partial void Stage00_Camp(in RunCommand command, bool apply, ref bool valid, ISimEventSink sink);
        partial void Stage00_Debug(in RunCommand command, bool apply, ref bool valid, ISimEventSink sink);

        // ── 1. 기한 처리
        partial void Stage01_Heat(ISimEventSink sink);
        partial void Stage01_Characters(ISimEventSink sink);
        partial void Stage01_Duel(ISimEventSink sink);

        // ── 2. 입력 이벤트 적용(이벤트마다)
        partial void Stage02_Player(in PlayerInput input, ISimEventSink sink);
        partial void Stage02_Arts(in PlayerInput input, ISimEventSink sink);

        // ── 3. 시야의 대각 유지 창 만료
        partial void Stage03_Player(ISimEventSink sink);

        // ── 4. 플레이어 이동과 벽
        partial void Stage04_Player(ISimEventSink sink);

        // ── 5. 비기 발동
        partial void Stage05_Arts(ISimEventSink sink);

        // ── 6. J 발사
        partial void Stage06_Heat(ISimEventSink sink);
        partial void Stage06_Spell(ISimEventSink sink);
        partial void Stage06_Characters(ISimEventSink sink);

        // ── 7. 웨이브 디렉터, 적·보스 AI
        partial void Stage07_Enemies(ISimEventSink sink);
        partial void Stage07_Bosses(ISimEventSink sink);
        partial void Stage07_Waves(ISimEventSink sink);

        // ── 8. 예고 대기 줄
        partial void Stage08_Telegraph(ISimEventSink sink);

        // ── 9. 이동과 충돌
        partial void Stage09_Graze(ISimEventSink sink);
        partial void Stage09_Spell(ISimEventSink sink);
        partial void Stage09_Enemies(ISimEventSink sink);
        partial void Stage09_Duel(ISimEventSink sink);

        // ── 10. 피해 확정과 사망 처리
        partial void Stage10_Player(ISimEventSink sink);
        partial void Stage10_Combo(ISimEventSink sink);
        partial void Stage10_Spell(ISimEventSink sink);
        partial void Stage10_Characters(ISimEventSink sink);
        partial void Stage10_Enemies(ISimEventSink sink);
        partial void Stage10_Bosses(ISimEventSink sink);
        partial void Stage10_Waves(ISimEventSink sink);

        // ── 11. 틱 끝 갱신
        partial void Stage11_Heat(ISimEventSink sink);
        partial void Stage11_Combo(ISimEventSink sink);
        partial void Stage11_Waves(ISimEventSink sink);
        partial void Stage11_Duel(ISimEventSink sink);

        // ── 상태 훅(모듈마다 넷). Rng(16)는 Runtime/Core 의 것이라 SimWorld.Rng.cs 에 구현이 있다
        partial void Snapshot_Save_Player(SimWorld target);
        partial void Snapshot_Load_Player(SimWorld source);
        partial void State_Write_Player(ref StateWriter w);
        partial void State_Read_Player(ref StateReader r);

        partial void Snapshot_Save_Heat(SimWorld target);
        partial void Snapshot_Load_Heat(SimWorld source);
        partial void State_Write_Heat(ref StateWriter w);
        partial void State_Read_Heat(ref StateReader r);

        partial void Snapshot_Save_Graze(SimWorld target);
        partial void Snapshot_Load_Graze(SimWorld source);
        partial void State_Write_Graze(ref StateWriter w);
        partial void State_Read_Graze(ref StateReader r);

        partial void Snapshot_Save_Combo(SimWorld target);
        partial void Snapshot_Load_Combo(SimWorld source);
        partial void State_Write_Combo(ref StateWriter w);
        partial void State_Read_Combo(ref StateReader r);

        partial void Snapshot_Save_Spell(SimWorld target);
        partial void Snapshot_Load_Spell(SimWorld source);
        partial void State_Write_Spell(ref StateWriter w);
        partial void State_Read_Spell(ref StateReader r);

        partial void Snapshot_Save_SpellTable(SimWorld target);
        partial void Snapshot_Load_SpellTable(SimWorld source);
        partial void State_Write_SpellTable(ref StateWriter w);
        partial void State_Read_SpellTable(ref StateReader r);

        partial void Snapshot_Save_Arts(SimWorld target);
        partial void Snapshot_Load_Arts(SimWorld source);
        partial void State_Write_Arts(ref StateWriter w);
        partial void State_Read_Arts(ref StateReader r);

        partial void Snapshot_Save_Characters(SimWorld target);
        partial void Snapshot_Load_Characters(SimWorld source);
        partial void State_Write_Characters(ref StateWriter w);
        partial void State_Read_Characters(ref StateReader r);

        partial void Snapshot_Save_Enemies(SimWorld target);
        partial void Snapshot_Load_Enemies(SimWorld source);
        partial void State_Write_Enemies(ref StateWriter w);
        partial void State_Read_Enemies(ref StateReader r);

        partial void Snapshot_Save_Telegraph(SimWorld target);
        partial void Snapshot_Load_Telegraph(SimWorld source);
        partial void State_Write_Telegraph(ref StateWriter w);
        partial void State_Read_Telegraph(ref StateReader r);

        partial void Snapshot_Save_Bosses(SimWorld target);
        partial void Snapshot_Load_Bosses(SimWorld source);
        partial void State_Write_Bosses(ref StateWriter w);
        partial void State_Read_Bosses(ref StateReader r);

        partial void Snapshot_Save_Waves(SimWorld target);
        partial void Snapshot_Load_Waves(SimWorld source);
        partial void State_Write_Waves(ref StateWriter w);
        partial void State_Read_Waves(ref StateReader r);

        partial void Snapshot_Save_Camp(SimWorld target);
        partial void Snapshot_Load_Camp(SimWorld source);
        partial void State_Write_Camp(ref StateWriter w);
        partial void State_Read_Camp(ref StateReader r);

        partial void Snapshot_Save_Outcome(SimWorld target);
        partial void Snapshot_Load_Outcome(SimWorld source);
        partial void State_Write_Outcome(ref StateWriter w);
        partial void State_Read_Outcome(ref StateReader r);

        partial void Snapshot_Save_Duel(SimWorld target);
        partial void Snapshot_Load_Duel(SimWorld source);
        partial void State_Write_Duel(ref StateWriter w);
        partial void State_Read_Duel(ref StateReader r);

        partial void Snapshot_Save_Debug(SimWorld target);
        partial void Snapshot_Load_Debug(SimWorld source);
        partial void State_Write_Debug(ref StateWriter w);
        partial void State_Read_Debug(ref StateReader r);
    }
}

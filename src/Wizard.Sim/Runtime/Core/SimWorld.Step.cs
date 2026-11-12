using System;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.Sim.Input;

namespace Wizard.Sim
{
    // CONTRACTS 3.2 "Step 의 처리 순서". 틱 안의 순서는 여기 한 곳뿐이다(0~12단계).
    // 한 단계 안의 모듈 순서는 CONTRACTS 3.2 훅 표의 위에서 아래 순이다. 훅을 더하거나 순서를 바꾸는 것은 계약 변경이다.
    public sealed partial class SimWorld
    {
        // 틱 하나를 진행한다.
        // 전제(모든 빌드에서 검사, 어기면 SimInvariantException. 검사는 상태를 바꾸기 전에 끝난다):
        //   inputs   — Tick == this.Tick, 슬롯이 이 세계에 있음, 아는 키, InputOrder 순(같은 것 둘 없음), 플레이어당 8개 이하
        //   commands — Tick == this.Tick, 슬롯이 이 세계에 있음, Seq 오름차순(같은 것 둘 없음)
        // 신뢰할 수 없는 기록은 InputRecordCodec.TryRead 가 먼저 거른다(CONTRACTS 3.8). 관리 힙에 할당하지 않는다.
        public void Step(ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands, ISimEventSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }
            CheckCommands(commands);
            CheckInputs(inputs);

            // 0. RunCommand 적용(순번 순). 앞 명령이 바꾼 상태에서 다음 명령을 확인한다
            for (int i = 0; i < commands.Length; i++)
            {
                ApplyCommand(in commands[i], sink);
            }

            // 1. 기한 처리: 잠금 풀림, 지연 효과 큐
            Stage01_Heat(sink);
            Stage01_Characters(sink);
            Stage01_Duel(sink);

            // 2. 입력 이벤트 적용(InputOrder 순): KeyState → 자세 → 시야 → 판정기 → 일시정지
            for (int i = 0; i < inputs.Length; i++)
            {
                Stage02_Player(in inputs[i], sink);
                Stage02_Arts(in inputs[i], sink);
            }

            // 3. 시야의 대각 유지 창 만료
            Stage03_Player(sink);

            // 4. 플레이어 이동과 벽
            Stage04_Player(sink);

            // 5. 비기 발동
            Stage05_Arts(sink);

            // 6. J 발사: 과열 판정 → Execute → 열 반영
            Stage06_Heat(sink);
            Stage06_Spell(sink);
            Stage06_Characters(sink);

            // 7. 웨이브 디렉터 → 적·보스 AI(모듈 순서는 훅 표의 순서)
            Stage07_Enemies(sink);
            Stage07_Bosses(sink);
            Stage07_Waves(sink);

            // 8. 예고 대기 줄
            Stage08_Telegraph(sink);

            // 9. 이동과 충돌, 예고 판정, 피격 뒤 스침, 줍기(모듈 순서는 훅 표의 순서)
            Stage09_Graze(sink);
            Stage09_Spell(sink);
            Stage09_Enemies(sink);
            Stage09_Duel(sink);

            // 10. 피해 확정과 사망 처리, 상태 효과·지대 만료
            Stage10_Player(sink);
            Stage10_Combo(sink);
            Stage10_Spell(sink);
            Stage10_Characters(sink);
            Stage10_Enemies(sink);
            Stage10_Bosses(sink);
            Stage10_Waves(sink);

            // 11. 틱 끝 갱신: 냉각, 콤보 끊김, 별똥, 웨이브·야영·라운드 상태 기계
            Stage11_Heat(sink);
            Stage11_Combo(sink);
            Stage11_Waves(sink);
            Stage11_Duel(sink);

            // 12. Tick += 1 (넘침은 래핑)
            _tick = unchecked(_tick + 1U);
        }

        // 0단계 한 명령: 확인 → (유효하면) 적용, 아니면 디버그 이벤트만
        private void ApplyCommand(in RunCommand command, ISimEventSink sink)
        {
            if (!IsCommandValid(in command, sink))
            {
                var e = new SimEvent
                {
                    Type = SimEventType.DebugCommandRejected,
                    Slot = command.Slot,
                    Tick = _tick,
                    A = (int)command.Op,
                    B = unchecked((int)command.Seq),
                };
                sink.Emit(in e);
                return;
            }
            bool valid = true;
            Stage00_Spell(in command, true, ref valid, sink);
            Stage00_Camp(in command, true, ref valid, sink);
            Stage00_Debug(in command, true, ref valid, sink);
        }

        // 확인 단계(상태를 바꾸지 않는다). CanApply 와 0단계가 같은 길을 쓴다
        private bool IsCommandValid(in RunCommand command, ISimEventSink sink)
        {
            if (!IsValidSlot(command.Slot))
            {
                return false;
            }
            // 디버그 op 은 AllowDebugOps 가 꺼져 있으면 무효다(CONTRACTS 3.1). 모듈에 묻기 전에 거른다
            if (command.IsDebug && !_config.AllowDebugOps)
            {
                return false;
            }
            bool valid = false;
            Stage00_Spell(in command, false, ref valid, sink);
            Stage00_Camp(in command, false, ref valid, sink);
            Stage00_Debug(in command, false, ref valid, sink);
            return valid;
        }

        private void CheckCommands(ReadOnlySpan<RunCommand> commands)
        {
            for (int i = 0; i < commands.Length; i++)
            {
                ref readonly RunCommand c = ref commands[i];
                if (c.Tick != _tick)
                {
                    throw new SimInvariantException("Step: command tick differs from world tick");
                }
                if (!IsValidSlot(c.Slot))
                {
                    throw new SimInvariantException("Step: command slot is not in this world");
                }
                if (i > 0 && c.Seq <= commands[i - 1].Seq)
                {
                    throw new SimInvariantException("Step: commands are not in ascending Seq order");
                }
            }
        }

        private void CheckInputs(ReadOnlySpan<PlayerInput> inputs)
        {
            int[] counts = _inputCountScratch;
            for (int p = 0; p < counts.Length; p++)
            {
                counts[p] = 0;
            }
            for (int i = 0; i < inputs.Length; i++)
            {
                ref readonly PlayerInput input = ref inputs[i];
                if (input.Event.Tick != _tick)
                {
                    throw new SimInvariantException("Step: input tick differs from world tick");
                }
                if (!IsValidSlot(input.Slot))
                {
                    throw new SimInvariantException("Step: input slot is not in this world");
                }
                InputOrder.KeyClass(input.Event.Key);     // 모르는 키면 SimInvariantException
                if (i > 0 && InputOrder.Compare(in inputs[i - 1], in input) >= 0)
                {
                    throw new SimInvariantException("Step: inputs are not in InputOrder");
                }
                int n = counts[input.Slot.Value] + 1;
                if (n > InputOrder.MaxEventsPerTickPerPlayer)
                {
                    throw new SimInvariantException("Step: more than 8 input events for one player in a tick");
                }
                counts[input.Slot.Value] = n;
            }
        }
    }
}

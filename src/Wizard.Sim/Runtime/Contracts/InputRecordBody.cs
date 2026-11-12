using System;
using Wizard.Sim.Waves;

namespace Wizard.Sim
{
    // CONTRACTS 3.8, 11.1, DECISIONS-TECH T-49. InputRecordCodec.TryRead 가 검사를 통과한 .wzrc 에서 뽑아 주는 것.
    // 서버의 1차 검사가 쓰는 값이다(8.3절): 머리말 대조와 본문 자르기(HeaderLength), 디버그 op 거절, 일시정지 한도, 받아 둔 중간 지점과의 대조.
    // 전부 클라이언트가 적은 값이다. 구조만 확인된 것이고 재실행으로 확인된 것이 아니다.
    public sealed class InputRecordBody
    {
        public int HeaderLength;                          // 머리말의 길이. file[..HeaderLength] = 머리말, file[HeaderLength..] = ReplayRunner.Run 의 recordBody
        public uint EndTick;                              // 꼬리말
        public ulong FinalStateHash;                      // 꼬리말
        public bool DebugUsed;                            // 꼬리말 flags 비트 0
        public bool HasDebugOps;                          // 본문에 200~255 op 이 하나라도 있다(꼬리말의 플래그와 무관하게 본다)
        public int CommandCount;                          // 0x02 레코드 수
        public int InputCount;                            // 0x01 레코드 수
        public int PauseCount;                            // 0x03 레코드 수
        public long PauseTotalMs;                         // 0x03 의 wall_ms 합
        public RunCheckpoint[] Checkpoints = Array.Empty<RunCheckpoint>();   // 0x04 레코드, 파일 순서(틱이 줄지 않는다)
    }
}

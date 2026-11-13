#nullable enable
using Wizard.Netcode.Duel;

namespace Wizard.Unity.Presentation.Fx
{
    // 빈 그림 재생기가 디버그 겹침 화면에 찍는 표식 하나. 식별자와 위치(표시 좌표, 1칸 = 1유닛)와 단계를 든다.
    // Label 은 Play 때 한 번 만든다(그리는 프레임마다 글자를 만들지 않는다). 화면 글자가 아니라 식별자라 이름 표를 거치지 않는다
    public readonly struct DebugFxMark
    {
        public DebugFxMark(string fxId, System.Numerics.Vector2 worldPos, EventPhase phase, string label, long startMs, long fadeStartMs)
        {
            FxId = fxId;
            WorldPos = worldPos;
            Phase = phase;
            Label = label;
            StartMs = startMs;
            FadeStartMs = fadeStartMs;
        }

        public string FxId { get; }

        public System.Numerics.Vector2 WorldPos { get; }

        public EventPhase Phase { get; }

        public string Label { get; }

        public long StartMs { get; }

        // 흐려지기 시작한 시각(번복). 흐려지지 않으면 -1
        public long FadeStartMs { get; }

        public bool Fading => FadeStartMs >= 0;

        public DebugFxMark WithFade(long fadeStartMs, EventPhase phase) =>
            new DebugFxMark(FxId, WorldPos, phase, Label, StartMs, fadeStartMs);
    }
}

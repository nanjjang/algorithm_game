#nullable enable
namespace Wizard.Unity.Presentation.Sfx
{
    // 빈 소리 재생기가 디버그 겹침 화면에 찍는 줄 하나. 소리에는 자리가 없어 식별자와 음높이 단·음량만 든다
    public readonly struct DebugSfxMark
    {
        public DebugSfxMark(string sfxId, int pitchStep, float volumeDb, string label, long atMs)
        {
            SfxId = sfxId;
            PitchStep = pitchStep;
            VolumeDb = volumeDb;
            Label = label;
            AtMs = atMs;
        }

        public string SfxId { get; }

        public int PitchStep { get; }

        public float VolumeDb { get; }

        public string Label { get; }

        public long AtMs { get; }
    }
}

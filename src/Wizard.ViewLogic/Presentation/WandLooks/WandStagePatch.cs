using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 덧씌움의 단계 패치(spec 04 12.3절 PerStage). SilhouetteStage 는 패치 그림이 가진 실루엣의 단계이고 Stage 와 같아야 한다
    // (다르면 단계 실루엣을 바꾸는 것이라 등록 검사가 거부한다, 15.12절 151). Palette 는 0xRRGGBB
    public sealed class WandStagePatch
    {
        public WandStagePatch(int stage, int silhouetteStage, IReadOnlyList<int> palette, string? ornament, WandParticleRequest? particles)
        {
            Stage = stage;
            SilhouetteStage = silhouetteStage;
            Palette = palette;
            Ornament = ornament;
            Particles = particles;
        }

        public int Stage { get; }

        public int SilhouetteStage { get; }

        public IReadOnlyList<int> Palette { get; }

        public string? Ornament { get; }

        public WandParticleRequest? Particles { get; }
    }
}

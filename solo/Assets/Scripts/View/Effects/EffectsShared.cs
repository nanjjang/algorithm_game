using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 내 탄 층(ShotLayer)과 이펙트 층(EffectLayer)이 함께 쓰는 것: 데이터, 입자 예산 관리기 하나(10 3.3절 "한 곳에서 발급"),
    /// 누적 가림 넓이 셈, 번쩍 관리기, 그림 정지 예산, 입자 그리기. ViewRegistry.Effects의 AddShots가 만들고 AddEffects가 같은 것을 받는다
    /// (CreateAll 한 번 = 판 하나에 하나. 정적 상태를 두지 않는다).
    /// </summary>
    public sealed class EffectsShared
    {
        public PresentationData Data;
        public ViewContext Vc;
        public ParticleBudget Budget;
        public OcclusionField Occlusion;
        public FlashGovernor Flash;
        public HitStopBudget HitStopBudget;
        public FxParticles Particles;
        public FxSprites Sprites;
        public WorldTextPopups Texts;
        /// <summary>표시 시계(초, 그림 정지 중에는 멈춘다 — 층이 받은 dt의 합).</summary>
        public float Time;
        /// <summary>EffectLayer가 붙어 있으면 그 층이 입자를 움직이고 걷는다. 없으면 ShotLayer가 한다.</summary>
        public bool EffectLayerAttached;
        bool _ready;

        public EffectsShared() { }
        public EffectsShared(PresentationData data) { Data = data; }

        /// <summary>층의 Build에서 부른다(두 층이 다 불러도 한 번만 세운다 — 같은 vc면).</summary>
        public void Ensure(ViewContext vc, SimWorld w)
        {
            if (_ready && Vc == vc) return;
            DisposeRenderers();
            Vc = vc;
            if (Data == null) Data = PresentationData.Load(vc != null && vc.Game != null ? vc.Game.Content : (w != null ? w.Content : null));
            var t = Data.Tuning;
            Budget = new ParticleBudget(Data.Budget);
            Occlusion = OcclusionField.FromTuning(t);
            Flash = FlashGovernor.FromTuning(t);
            HitStopBudget = new HitStopBudget(t.F("hitstop_budget", "budget_seconds"), t.F("hitstop_budget", "window_seconds"));
            Particles = new FxParticles(this);
            Sprites = new FxSprites(this);
            Texts = new WorldTextPopups(this);
            Time = 0f;
            _ready = true;
        }

        public int EffectsLevel => Vc != null ? Vc.EffectsLevel : 0;
        public ViewOptions Options => Vc != null && Vc.Options != null ? Vc.Options : new ViewOptions();

        /// <summary>한 프레임 앞으로(입자, 예산 시계).</summary>
        public void Advance(float dt)
        {
            if (!_ready) return;
            Time += dt;
            Budget.Level = EffectsLevel;
            Particles.Update(dt);
            Sprites.Update(dt);
            Texts.Update(dt);
        }

        void DisposeRenderers()
        {
            if (Particles != null) Particles.DisposeAll();
            if (Sprites != null) Sprites.DisposeAll();
            if (Texts != null) Texts.DisposeAll();
        }

        public void DisposeAll()
        {
            DisposeRenderers();
            if (Budget != null) Budget.Clear();
            if (Occlusion != null) Occlusion.Clear();
            _ready = false;
            Vc = null;
        }
    }
}

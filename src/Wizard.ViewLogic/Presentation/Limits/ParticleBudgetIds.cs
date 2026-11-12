namespace Wizard.ViewLogic.Presentation.Limits
{
    // data/presentation/particle_budget.json 의 열쇠 ↔ 열거형. 열쇠는 ASCII id 이고 화면에 보이지 않는다
    public static class ParticleBudgetIds
    {
        public static bool TryCategory(string id, out ParticleCategory category)
        {
            switch (id)
            {
                case "art": category = ParticleCategory.Art; return true;
                case "hit": category = ParticleCategory.Hit; return true;
                case "kill": category = ParticleCategory.Kill; return true;
                case "shot": category = ParticleCategory.Shot; return true;
                case "wand_idle": category = ParticleCategory.WandIdle; return true;
                case "environment": category = ParticleCategory.Environment; return true;
                case "art_opponent": category = ParticleCategory.ArtOpponent; return true;
                case "wand_idle_opponent": category = ParticleCategory.WandIdleOpponent; return true;
                case "emote": category = ParticleCategory.Emote; return true;
                case "enhance_charge": category = ParticleCategory.EnhanceCharge; return true;
                case "enhance_burst": category = ParticleCategory.EnhanceBurst; return true;
                default: category = ParticleCategory.None; return false;
            }
        }

        public static bool TryScene(string id, out ParticleScene scene)
        {
            switch (id)
            {
                case "battle": scene = ParticleScene.Battle; return true;
                case "duel": scene = ParticleScene.Duel; return true;
                case "plaza": scene = ParticleScene.Plaza; return true;
                case "enhance": scene = ParticleScene.Enhance; return true;
                default: scene = ParticleScene.Battle; return false;
            }
        }

        public static bool TryRecurrence(string id, out ParticleRecurrence recurrence)
        {
            switch (id)
            {
                case "one_shot": recurrence = ParticleRecurrence.OneShot; return true;
                case "recurring": recurrence = ParticleRecurrence.Recurring; return true;
                default: recurrence = ParticleRecurrence.OneShot; return false;
            }
        }

        public static bool TryHitDecor(string id, out HitDecorMode mode)
        {
            switch (id)
            {
                case "full": mode = HitDecorMode.Full; return true;
                case "half": mode = HitDecorMode.Half; return true;
                case "minimal": mode = HitDecorMode.Minimal; return true;
                default: mode = HitDecorMode.Full; return false;
            }
        }
    }
}

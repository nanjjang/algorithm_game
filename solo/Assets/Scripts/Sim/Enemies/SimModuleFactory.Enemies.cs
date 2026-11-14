namespace Wizard.Solo.Sim
{
    public static partial class SimModuleFactory
    {
        static partial void CreateEnemies(ref EnemyModule m) { m = new Enemies.EnemySystem(); }
    }
}

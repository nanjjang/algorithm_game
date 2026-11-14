namespace Wizard.Solo
{
    public static partial class AppFactory
    {
        /// <summary>F2: 그림 창고. 부팅 때는 manifest만 읽는다(텍스처는 처음 요청될 때).</summary>
        static partial void CreateArt(GameContext ctx, ref Wizard.Solo.Art.IArtLibrary art)
        {
            art = new Wizard.Solo.Art.ArtLibrary();
        }
    }
}

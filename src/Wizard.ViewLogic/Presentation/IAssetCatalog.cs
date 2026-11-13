using Wizard.ViewLogic.Common;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13 (크기를 받는 둘은 DECISIONS-TECH T-58의 개정문)
    public interface IAssetCatalog
    {
        // 크기를 주지 않으면 그 열쇠의 가장 작은 그림이다(같은 열쇠를 크기마다 따로 그린 화면 그림, T-58)
        bool TryGetSprite(AssetKey key, int frame, out SpriteRef sprite);
        bool TryGetClip(AssetKey key, out ClipRef clip);
        // frameSize = 프레임 하나의 픽셀 크기. 그 크기의 그림이 없으면 false(다른 크기를 대신 주지 않는다)
        bool TryGetSprite(AssetKey key, PixelSize frameSize, int frame, out SpriteRef sprite);
        bool TryGetClip(AssetKey key, PixelSize frameSize, out ClipRef clip);
        AssetKey Placeholder(AssetKey missing);           // 회청 바둑판 + id 글자. 위험색·금색·연두·청록을 쓰지 않는다
    }
}

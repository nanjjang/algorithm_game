using Wizard.ViewLogic.Presentation;

namespace Wizard.ViewLogic.Assets
{
    // 대체 그림을 쓴 열쇠를 알리는 곳(spec 14 5.7절 "대체를 쓴 Id는 개발 빌드에서 한 번 기록한다").
    // 레지스트리는 열쇠마다 한 번만 부른다. 개발 빌드의 표시 계층이 로그에 잇고, 출시 빌드는 null 을 넘긴다
    public interface IAssetLog
    {
        void PlaceholderUsed(AssetKey missing);
    }
}

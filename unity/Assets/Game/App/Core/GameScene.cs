#nullable enable
namespace Wizard.Unity.App.Core
{
    // 장면 넷(ARCHITECTURE 4.3, T-32). 번호는 더하기만 한다. 장면 파일 이름은 SceneFlow.NameOf 가 정한다(화면 글자가 아니다)
    public enum GameScene : byte
    {
        Boot = 0,
        Menu = 1,
        Battle = 2,     // 무한 웨이브·결투·연습 공용
        Plaza = 3,
    }
}

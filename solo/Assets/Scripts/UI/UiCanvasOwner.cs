using UnityEngine;

namespace Wizard.Solo.UI
{
    /// <summary>화면 캔버스에 붙어 그 캔버스를 만든 관리자를 가리킨다(정적 싱글턴 없이 위젯이 관리자를 찾는 길).</summary>
    public sealed class UiCanvasOwner : MonoBehaviour
    {
        public ScreenManager Manager;
        public ScreenLayer Layer;
    }
}

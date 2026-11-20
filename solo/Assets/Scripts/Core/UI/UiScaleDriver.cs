using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Solo.UI
{
    /// <summary>창 크기가 바뀌면 CanvasScaler의 배율을 UiScale.Compute로 다시 맞춘다(UiCanvasFactory가 붙인다).</summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UiScaleDriver : MonoBehaviour
    {
        CanvasScaler _scaler;
        int _w, _h;

        void Awake() { _scaler = GetComponent<CanvasScaler>(); }

        void Update()
        {
            if (Screen.width == _w && Screen.height == _h) return;
            _w = Screen.width;
            _h = Screen.height;
            if (_scaler != null) _scaler.scaleFactor = UiScale.Compute(_w, _h);
        }
    }
}

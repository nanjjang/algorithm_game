using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 화면 관리자(IScreenManager 구현, 08 요약서 1.1절, 00 문서 8·8.2절). 스택 규칙은 UiScreenStack(순수 클래스)이 갖고 여기는 그리기와 입력만 한다.
    /// - 캔버스: 화면 층마다 하나(UiCanvasFactory.Create(name, UiSortOrders.Screen(layer)) — 100·200·300·400·500). HUD 캔버스(0)는 F5가 같은 함수로 만든다.
    /// - EventSystem: 여기서 하나만 만들고 InputSystemUIInputModule을 붙인다(옛 입력 모듈은 쓰지 않는다).
    /// - 화면 객체는 id마다 하나를 만들어 두고 다시 열 때 쓴다(Build는 한 번). 같은 id가 이미 열려 있으면 새로 하나 만든다(창 위의 창).
    /// - 보이는 화면: 맨 위에서 아래로 첫 Fullscreen까지. 그 아래는 꺼 둔다(층 캔버스 순서 때문에 아래 층이 위에 그려지지 않게).
    /// - Modal은 뒤를 먹색 #12131C 60%로 덮는다(K-10, 마우스도 막는다).
    /// - 메뉴 키: MenuInput → Esc(Back)는 맨 위 화면의 OnBack(false면 Pop), 나머지는 입력을 받는 화면에 위에서부터.
    /// - Top은 Toast·Tooltip 층을 세지 않는다(전투 입력의 문이 Top으로 열리고 닫힌다).
    /// </summary>
    public sealed class ScreenManager : MonoBehaviour, IScreenManager
    {
        public const string RootName = "Wizard.Screens";
        public const string EventSystemName = "Wizard.EventSystem";
        /// <summary>토스트 자리(오른쪽 위 480×96, 08 13.1절).</summary>
        public const float ToastWidth = 480f, ToastHeight = 96f, ToastMargin = 16f, ToastGap = 8f;
        /// <summary>[임시] 토스트가 내려오는 시간(초)의 기본값. 쓰는 값은 Tuning.ToastSlideSeconds.</summary>
        public const float ToastSlideSeconds = 0.2f;

        static readonly ScreenLayer[] Layers =
            { ScreenLayer.Fullscreen, ScreenLayer.Panel, ScreenLayer.HudOverlay, ScreenLayer.Modal, ScreenLayer.Toast };

        GameContext _ctx;
        UiScreenStack _stack;
        MenuInput _menu;
        readonly UiInputModeTracker _mode = new UiInputModeTracker();
        readonly Dictionary<ScreenId, Func<IScreen>> _factories = new Dictionary<ScreenId, Func<IScreen>>();
        readonly Dictionary<ScreenId, IScreen> _cached = new Dictionary<ScreenId, IScreen>();
        readonly Dictionary<IScreen, RectTransform> _slots = new Dictionary<IScreen, RectTransform>();
        readonly HashSet<IScreen> _transient = new HashSet<IScreen>();
        readonly RectTransform[] _layerRoots = new RectTransform[Layers.Length];
        readonly List<MenuKey> _keys = new List<MenuKey>();
        readonly List<IScreen> _receivers = new List<IScreen>();
        GameObject _eventSystem;
        bool _tornDown;
        RectTransform _tooltipAnchor;

        /// <summary>입력 장치 모드(모든 화면이 같이 본다).</summary>
        public UiInputModeTracker InputMode => _mode;
        /// <summary>UI 기반의 솔로 임시 수치(DataSolo/F3.json. 없으면 기본값).</summary>
        public UiTuning Tuning { get; private set; } = UiTuning.Defaults();
        public MenuInput Menu => _menu;
        public UiScreenStack Stack => _stack;
        public GameContext Context => _ctx;
        /// <summary>
        /// true면 메뉴 키를 화면에 보내지 않는다(키 재설정의 "키를 누르세요" 동안 — 그 화면이 키보드를 직접 읽는다. Esc 취소 포함).
        /// </summary>
        public bool CaptureKeys { get; set; }

        /// <summary>
        /// 관리자 GameObject를 만든다(DontDestroyOnLoad). 앞서 만든 관리자(와 그 캔버스·EventSystem)가 있으면 먼저 걷는다
        /// (PlayMode 테스트가 Bootstrap.Boot를 여러 번 부른다).
        /// </summary>
        public static ScreenManager Create(GameContext ctx)
        {
            var old = FindObjectsByType<ScreenManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < old.Length; i++) if (old[i] != null) old[i].Teardown(true);
            var go = new GameObject(RootName);
            DontDestroyOnLoad(go);
            var m = go.AddComponent<ScreenManager>();
            m.Init(ctx);
            return m;
        }

        void Init(GameContext ctx)
        {
            _ctx = ctx;
            _stack = new UiScreenStack(Provide, msg => Debug.LogWarning(msg));
            _stack.Entering += OnEntering;
            _stack.Exited += OnExited;
            _stack.Changed += RefreshVisibility;
            for (int i = 0; i < Layers.Length; i++)
            {
                var root = UiCanvasFactory.Create(RootName + "." + Layers[i], UiSortOrders.Screen(Layers[i]));
                var owner = root.parent.gameObject.AddComponent<UiCanvasOwner>();
                owner.Manager = this;
                owner.Layer = Layers[i];
                _layerRoots[i] = root;
            }
            CreateEventSystem();
            Tuning = UiTuning.FromJson(ctx != null && ctx.Content != null ? ctx.Content.SoloExtra(UiTuning.ExtraName) : null);
            _menu = new MenuInput { RepeatDelay = Tuning.KeyRepeatDelaySeconds, RepeatInterval = Tuning.KeyRepeatIntervalSeconds };
            if (ctx != null && ctx.Settings != null)
            {
                _menu.ApplySettings(ctx.Settings.Current);
                ctx.Settings.Changed += OnSettingsChanged;
            }
        }

        void OnSettingsChanged()
        {
            if (_menu != null && _ctx != null && _ctx.Settings != null) _menu.ApplySettings(_ctx.Settings.Current);
        }

        void CreateEventSystem()
        {
            // 남아 있는 우리 것을 걷는다(다른 곳에서 만든 것은 경고만).
            var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].gameObject.name == EventSystemName) DestroyImmediate(all[i].gameObject);
                else Debug.LogWarning("ScreenManager: another EventSystem exists (" + all[i].gameObject.name + ")");
            }
            _eventSystem = new GameObject(EventSystemName, typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(_eventSystem);
            var es = _eventSystem.GetComponent<EventSystem>();
            // 메뉴의 방향·결정은 MenuInput이 맡는다(uGUI 탐색을 쓰지 않는다).
            es.sendNavigationEvents = false;
        }

        // ------------------------------------------------------------------ IScreenManager

        public void Register(ScreenId id, Func<IScreen> factory)
        {
            if (id == ScreenId.None || factory == null) return;
            if (_factories.ContainsKey(id)) Debug.LogWarning("ScreenManager: " + id + " registered twice (last wins)");
            _factories[id] = factory;
            _cached.Remove(id);
        }

        public bool IsRegistered(ScreenId id) => _factories.ContainsKey(id);

        public void Push(ScreenId id, ScreenArgs args = null) => _stack.Push(id, args);
        public void Replace(ScreenId id, ScreenArgs args = null) => _stack.Replace(id, args);
        public void Pop(object result = null) => _stack.Pop(result);
        public void PopTo(ScreenId id, ScreenArgs args = null) => _stack.PopTo(id, args);
        public ScreenId Top => _stack != null ? _stack.Top : ScreenId.None;
        public bool IsOpen(ScreenId id) => _stack != null && _stack.IsOpen(id);

        /// <summary>그 화면을 닫는다(맨 위면 Pop, 아니면 그 칸만, 토스트 층이면 토스트 목록에서).</summary>
        public void Close(IScreen screen, object result = null) => _stack.Dismiss(screen, result);

        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult)
        {
            var args = ScreenArgs.From(Top)
                .With(ScreenArgKeys.TitleKey, titleKey)
                .With(ScreenArgKeys.BodyKey, bodyKey)
                .With(ScreenArgKeys.Caution, caution);
            args.OnResult = r => onResult?.Invoke(r is bool b && b);
            Push(ScreenId.ConfirmDialog, args);
        }

        /// <summary>Confirm과 같고 본문은 이미 만든 글(틀을 채운 글 — 예: "웨이브 15까지 정산").</summary>
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult)
        {
            var args = ScreenArgs.From(Top)
                .With(ScreenArgKeys.TitleKey, titleKey)
                .With(ScreenArgKeys.Text, bodyText)
                .With(ScreenArgKeys.Caution, caution);
            args.OnResult = r => onResult?.Invoke(r is bool b && b);
            Push(ScreenId.ConfirmDialog, args);
        }

        public void Toast(string text, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = new UiToastScreen(this, seconds);
            BuildInto(t, true);
            _stack.PushScreen(t, ScreenArgs.From(Top).With(ScreenArgKeys.Text, text));
        }

        /// <summary>툴팁을 띄운다(anchor 옆, 최대 폭 400). 이미 떠 있으면 글과 자리만 바꾼다.</summary>
        public void ShowTooltip(string text, RectTransform anchor)
        {
            if (string.IsNullOrEmpty(text)) { HideTooltip(); return; }
            _tooltipAnchor = anchor;
            if (_cached.TryGetValue(ScreenId.Tooltip, out var cached) && _stack.Contains(cached) && cached is UiTooltipScreen open)
            {
                open.Show(text, anchor);
                return;
            }
            if (!_factories.ContainsKey(ScreenId.Tooltip)) return;
            Push(ScreenId.Tooltip, ScreenArgs.From(Top).With(ScreenArgKeys.Text, text));
            if (_cached.TryGetValue(ScreenId.Tooltip, out var s) && s is UiTooltipScreen tip) tip.Show(text, anchor);
            UiSounds.Play(_ctx != null ? _ctx.Sound : null, UiSounds.TooltipShow);
        }

        /// <summary>툴팁을 내린다. anchor를 주면 그 칸의 툴팁일 때만.</summary>
        public void HideTooltip(RectTransform anchor = null)
        {
            if (_stack == null) return;
            if (anchor != null && _tooltipAnchor != anchor) return;
            _tooltipAnchor = null;
            _stack.DismissToast(ScreenId.Tooltip);
        }

        // ------------------------------------------------------------------ 화면 객체

        IScreen Provide(ScreenId id)
        {
            if (!_factories.TryGetValue(id, out var factory)) return null;
            if (_cached.TryGetValue(id, out var cached) && cached != null && !_stack.Contains(cached)) return cached;
            IScreen s;
            try { s = factory(); }
            catch (Exception ex) { Debug.LogException(ex); return null; }
            if (s == null) return null;
            if (s.Id != id) Debug.LogWarning("ScreenManager: factory for " + id + " returned " + s.Id);
            bool transient = cached != null; // 이미 열린 것이 있으면 이것은 한 번 쓰고 버린다
            if (!BuildInto(s, transient)) return null;
            if (!transient) _cached[id] = s;
            return s;
        }

        RectTransform LayerRoot(ScreenLayer layer)
        {
            for (int i = 0; i < Layers.Length; i++) if (Layers[i] == layer) return _layerRoots[i];
            return _layerRoots[0];
        }

        bool BuildInto(IScreen s, bool transient)
        {
            var parent = LayerRoot(s.Layer);
            var slot = UiKit.Node(parent, "Screen." + (s.Id != ScreenId.None ? s.Id.ToString() : s.GetType().Name));
            UiKit.Stretch(slot);
            if (s.Layer == ScreenLayer.Modal)
            {
                // 창 뒤 덮개(K-10): 창 크기 밖(먹색 띠)까지 덮도록 넉넉히.
                var dim = UiKit.Rect(slot, 0f, 0f, 0f, 0f, UiTheme.ModalDim, true, "Dim");
                UiKit.Center(dim.rectTransform, UiScale.RefWidth * 4f, UiScale.RefHeight * 4f);
            }
            var root = UiKit.Node(slot, "Root");
            UiKit.Stretch(root);
            AttachTransition(s.Layer, slot, root);
            slot.gameObject.SetActive(false);
            try { s.Build(_ctx, root); }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Destroy(slot.gameObject);
                return false;
            }
            _slots[s] = slot;
            if (transient) _transient.Add(s);
            return true;
        }

        /// <summary>
        /// 화면 전환 연출(표시만): 켜질 때마다 슬롯이 페이드로 들어오고, 전체 화면은 아래에서 살짝 밀려 올라오고,
        /// 창(Modal)·판(Panel)·HUD 위 판은 살짝 작게 시작해 커진다. 토스트는 자기 연출이 있어 붙이지 않는다.
        /// </summary>
        static void AttachTransition(ScreenLayer layer, RectTransform slot, RectTransform root)
        {
            if (layer == ScreenLayer.Toast) return;
            UiMotion.Entrance(slot, 0f, UiTheme.ScreenFadeSeconds, Vector2.zero, 1f);
            switch (layer)
            {
                case ScreenLayer.Fullscreen:
                    UiMotion.Entrance(root, 0f, UiTheme.ScreenFadeSeconds + 0.06f, new Vector2(0f, -UiTheme.ScreenSlidePx), 1f);
                    break;
                default:
                    UiMotion.Entrance(root, 0f, UiTheme.PanelPopSeconds, new Vector2(0f, -UiTheme.ScreenSlidePx * 0.5f), UiTheme.PanelPopFromScale + 0.04f);
                    break;
            }
        }

        void OnEntering(UiScreenEntry e)
        {
            if (_slots.TryGetValue(e.Screen, out var slot) && slot != null)
            {
                slot.SetAsLastSibling();
                slot.gameObject.SetActive(_stack.IsVisible(e.Screen));
            }
            if (e.Layer != ScreenLayer.Toast) UiSounds.Play(_ctx != null ? _ctx.Sound : null, UiSounds.ScreenOpen);
            LayoutToasts();
        }

        void OnExited(UiScreenEntry e)
        {
            if (_slots.TryGetValue(e.Screen, out var slot) && slot != null) slot.gameObject.SetActive(false);
            if (_transient.Remove(e.Screen))
            {
                if (slot != null) Destroy(slot.gameObject);
                _slots.Remove(e.Screen);
            }
            if (e.Layer != ScreenLayer.Toast) UiSounds.Play(_ctx != null ? _ctx.Sound : null, UiSounds.ScreenClose);
            LayoutToasts();
        }

        void RefreshVisibility()
        {
            var entries = _stack.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (_slots.TryGetValue(entries[i].Screen, out var slot) && slot != null)
                    slot.gameObject.SetActive(_stack.IsVisibleAt(i));
            }
            var toasts = _stack.Toasts;
            for (int i = 0; i < toasts.Count; i++)
                if (_slots.TryGetValue(toasts[i].Screen, out var slot) && slot != null) slot.gameObject.SetActive(true);
        }

        void LayoutToasts()
        {
            if (_stack == null) return;
            var toasts = _stack.Toasts;
            int k = 0;
            for (int i = 0; i < toasts.Count; i++)
                if (toasts[i].Screen is UiToastScreen t) t.SetSlotIndex(k++);
        }

        // ------------------------------------------------------------------ 프레임

        void Update()
        {
            if (_tornDown || _stack == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector2 md = Vector2.zero;
            try { md = _menu.Reader.MouseDelta; } catch (Exception) { md = Vector2.zero; }
            _mode.OnMouseMove(md.x, md.y);
            _keys.Clear();
            _menu.Update(dt, _keys);
            if (_menu.AnyKeyPressed) _mode.OnKeyAny();
            if (!CaptureKeys)
                for (int i = 0; i < _keys.Count; i++) Dispatch(_keys[i]);

            _stack.GetTickReceivers(_receivers);
            for (int i = 0; i < _receivers.Count; i++)
            {
                var s = _receivers[i];
                if (!_stack.Contains(s)) continue;
                try { s.Tick(dt); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>메뉴 키 하나를 화면에 보낸다(테스트·봇이 직접 불러도 된다).</summary>
        public void Dispatch(MenuKey key)
        {
            if (key == MenuKey.Back)
            {
                try { _stack.Back(); } catch (Exception ex) { Debug.LogException(ex); }
                return;
            }
            var list = new List<IScreen>();
            _stack.GetInputReceivers(list);
            for (int i = 0; i < list.Count; i++)
            {
                bool used;
                try { used = list[i].OnMenuKey(key); }
                catch (Exception ex) { Debug.LogException(ex); used = true; }
                if (used) return;
            }
        }

        // ------------------------------------------------------------------ 걷기

        /// <summary>열린 화면을 모두 닫고(OnExit) 캔버스·EventSystem·자신을 없앤다. immediate면 바로(다시 부팅할 때).</summary>
        public void Teardown(bool immediate)
        {
            if (_tornDown) return;
            _tornDown = true;
            try { _stack?.Clear(); } catch (Exception ex) { Debug.LogException(ex); }
            if (_ctx != null && _ctx.Settings != null) _ctx.Settings.Changed -= OnSettingsChanged;
            for (int i = 0; i < _layerRoots.Length; i++)
            {
                if (_layerRoots[i] == null) continue;
                var canvasGo = _layerRoots[i].parent != null ? _layerRoots[i].parent.gameObject : _layerRoots[i].gameObject;
                Kill(canvasGo, immediate);
                _layerRoots[i] = null;
            }
            if (_eventSystem != null) Kill(_eventSystem, immediate);
            _eventSystem = null;
            _slots.Clear();
            _cached.Clear();
            _transient.Clear();
            if (_ctx != null && ReferenceEquals(_ctx.Screens, this)) _ctx.Screens = null;
            if (this != null) Kill(gameObject, immediate);
        }

        static void Kill(GameObject go, bool immediate)
        {
            if (go == null) return;
            if (immediate) DestroyImmediate(go);
            else Destroy(go);
        }

        void OnDestroy()
        {
            if (!_tornDown) Teardown(false);
        }
    }
}

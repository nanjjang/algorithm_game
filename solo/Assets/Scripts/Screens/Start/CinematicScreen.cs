using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 시네마틱 영상 화면(ScreenId.Cinematic). StreamingAssets/Cinematic/&lt;영상&gt;.mp4를 화면 가득 튼다.
    /// 첫 실행에는 Bootstrap이 연출 감도 물음 위에 인트로를 얹는다(물음보다 먼저 나온다. 아직 감도를 고르지 않았으므로 전체 판).
    /// 영상은 solo/Tools/cinematic이 굽는다(그림은 저장소의 원본 그림, 자막은 영상에 구워져 있다).
    /// - 인자: ArgVideo(string, 기본 intro), ArgNext(ScreenId, 기본 None). 끝나면 Next가 있으면 그 화면으로 Replace
    ///   (ProfileRestored·PendingSettled는 StartFlow.Forward로 그대로 넘긴다), 없으면 이 화면만 닫는다.
    /// - 연출 감도가 전체가 아니면 번쩍임·흔들림을 줄여 구운 &lt;영상&gt;_calm.mp4를 튼다(첫 실행 물음의 선택을 따른다).
    /// - Enter·Esc 한 번 = 건너뛰기 안내, 안내가 떠 있는 3초 안에 한 번 더 = 건너뛴다(첫 실행에 실수로 넘기지 않게).
    /// - 파일이 없거나 재생 오류면 바로 다음 화면으로 간다.
    /// </summary>
    public sealed class CinematicScreen : ScreenBase
    {
        public const string ArgVideo = "cinematic_video";
        public const string ArgNext = "cinematic_next";
        public const string IntroVideo = "intro";
        public const string StoryVideo = "story";
        public const string SkipHintKey = "str.cinematic.skip_hint";

        const string Folder = "Cinematic";
        const float HintSeconds = 3f;
        const float PrepareTimeout = 8f;

        VideoPlayer _player;
        RenderTexture _target;
        RawImage _frame;
        RectTransform _hint;
        float _hintLeft;
        float _sincePrepare;
        bool _started;
        bool _done;
        bool _finishPending;

        public override ScreenId Id => ScreenId.Cinematic;
        protected override bool HasFrame => false;

        /// <summary>영상 파일 경로(연출 감도에 맞는 쪽, 없으면 다른 쪽). 둘 다 없으면 null. 설정이 없으면 감도 0(전체)으로 본다.</summary>
        public static string PathFor(string video, int effectsLevel) =>
            PathFor(Path.Combine(Application.streamingAssetsPath, Folder), video, effectsLevel);

        /// <summary>dir 안에서 고른다(테스트용).</summary>
        public static string PathFor(string dir, string video, int effectsLevel)
        {
            string calm = Path.Combine(dir, video + "_calm.mp4");
            string full = Path.Combine(dir, video + ".mp4");
            string first = effectsLevel > 0 ? calm : full, second = effectsLevel > 0 ? full : calm;
            if (File.Exists(first)) return first;
            // 줄임·최소를 골랐는데 calm이 없으면 번쩍이는 쪽을 틀지 않는다.
            if (effectsLevel > 0) return null;
            return File.Exists(second) ? second : null;
        }

        public static bool Available(string video) => PathFor(video, 0) != null || PathFor(video, 1) != null;

        /// <summary>영상을 틀고 next로 가는 인자. forward의 값(PendingSettled 등)은 그대로 싣는다.</summary>
        public static ScreenArgs ArgsFor(string video, ScreenId next, ScreenArgs forward)
        {
            var a = ScreenArgs.From(forward != null ? forward.Entry : ScreenId.None);
            if (forward != null)
                foreach (var kv in forward.Values) a.Values[kv.Key] = kv.Value;
            return a.With(ArgVideo, video).With(ArgNext, next);
        }

        protected override void BuildContent(RectTransform content)
        {
            // 검은 바탕은 창 끝까지, 영상(16:9)은 창 안에 잘리지 않게 가장 크게(UiFullBleed.Contain) — 남는 곳만 검은 띠.
            UiFullBleed.Fill(UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, Color.black, true, "Background").rectTransform);

            var go = new GameObject("CinematicFrame", typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            UiKit.Place(rt, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.Contain(rt, (float)UiScale.RefWidth / UiScale.RefHeight);
            _frame = go.GetComponent<RawImage>();
            _frame.color = Color.white;
            _frame.raycastTarget = false;
            _frame.enabled = false;

            _hint = UiKit.Node(content, "SkipHint", UiScale.RefWidth - 520f, UiScale.RefHeight - 64f, 480f, 40f);
            UiKit.Rect(_hint, 0f, 0f, 480f, 40f, new Color(0f, 0f, 0f, 0.55f), false, "HintBack");
            UiKit.Label(Ctx, _hint, T(SkipHintKey), UiFontId.F16, 12f, 0f, 456f, 40f, UiTheme.Chalk, TextAnchor.MiddleRight);
            _hint.gameObject.SetActive(false);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _done = false;
            _finishPending = false;
            _started = false;
            _sincePrepare = 0f;
            _hintLeft = 0f;
            if (_hint != null) _hint.gameObject.SetActive(false);

            var settings = Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
            int level = settings != null ? SettingsOptions.EffectsLevel(settings) : 0;
            string path = PathFor(Args.Get(ArgVideo, IntroVideo), level);
            // 화면 관리자가 이 화면을 여는 중이므로 다음 화면으로는 다음 Tick에 간다.
            if (path == null) { _finishPending = true; return; }

            _target = new RenderTexture(1920, 1080, 0) { name = "CinematicTarget" };
            var host = new GameObject("CinematicPlayer");
            host.transform.SetParent(Root, false);
            _player = host.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.Url;
            _player.url = path;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            float vol = settings != null
                ? SettingsOptions.Volume(settings, SoundBus.Master) * SettingsOptions.Volume(settings, SoundBus.Bgm)
                : 1f;
            _player.SetDirectAudioVolume(0, Mathf.Clamp01(vol));
            _player.prepareCompleted += OnPrepared;
            _player.loopPointReached += _ => Finish();
            _player.errorReceived += (_, msg) => { Debug.LogWarning("Cinematic: " + msg); Finish(); };
            _frame.texture = _target;
            _player.Prepare();
        }

        void OnPrepared(VideoPlayer p)
        {
            if (_done) return;
            _started = true;
            _frame.enabled = true;
            p.Play();
        }

        public override void Tick(float dt)
        {
            if (_done) return;
            if (_finishPending) { Finish(); return; }
            if (!_started)
            {
                _sincePrepare += dt;
                if (_sincePrepare > PrepareTimeout) { Finish(); return; }
            }
            if (_hintLeft > 0f)
            {
                _hintLeft -= dt;
                if (_hintLeft <= 0f && _hint != null) _hint.gameObject.SetActive(false);
            }
        }

        public override bool OnBack()
        {
            RequestSkip();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Confirm || key == MenuKey.Cancel || key == MenuKey.Back)
            {
                RequestSkip();
                return true;
            }
            return true; // 영상 중에는 다른 키를 먹는다
        }

        void RequestSkip()
        {
            if (_done) return;
            if (_hintLeft > 0f) { Finish(); return; }
            _hintLeft = HintSeconds;
            if (_hint != null) _hint.gameObject.SetActive(true);
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            Release();
            var next = Args != null ? Args.Get(ArgNext, ScreenId.None) : ScreenId.None;
            if (next != ScreenId.None)
                StartNav.Replace(next, StartFlow.Forward(Args, ScreenId.Cinematic, true)).Apply(Screens, this);
            else
                StartNav.Close().Apply(Screens, this);
        }

        void Release()
        {
            if (_player != null)
            {
                _player.Stop();
                Object.Destroy(_player.gameObject);
                _player = null;
            }
            if (_frame != null)
            {
                _frame.texture = null;
                _frame.enabled = false;
            }
            if (_target != null)
            {
                _target.Release();
                Object.Destroy(_target);
                _target = null;
            }
        }

        public override void OnExit()
        {
            Release();
            base.OnExit();
        }
    }
}

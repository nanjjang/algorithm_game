using System;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 타이틀(08 요약서 3.2절, 01 요약서 2.1절)의 규칙.
    /// - 지금 프로필 표지는 항상 보인다. 솔로는 오프라인 프로필뿐이라 str.title.profile.offline.
    /// - 들어올 때 ProfileRestored가 참이면 SaveRestored 창을 한 번(TakeRestored로 지운다).
    /// - J: (offline_guide_seen = false면 OfflineGuide) → MainMenu. PendingSettled는 그대로 넘긴다.
    /// - [프로필 바꾸기] → ProfileSelect(PendingSettled를 실어 보낸다 — 오프라인 카드로 MainMenu에 닿을 때 알림이 빠지지 않게).
    /// - 시작 안내: 2초 주기로 밝기만 바뀐다(깜박이지 않는다). 주기가 돌 때마다 ui.title.start_prompt_pulse.
    /// </summary>
    public sealed class TitleModel
    {
        readonly StartTuning _tuning;
        float _pulseClock;
        bool _started;

        public TitleModel(StartTuning tuning) { _tuning = tuning ?? new StartTuning(); }

        /// <summary>프로필 표지 열쇠. 화면은 GameContext.Kind로만 프로필 종류를 가른다(솔로는 항상 Offline).</summary>
        public static string ProfileKey(ProfileKind kind) =>
            kind == ProfileKind.Server ? StartNameKeys.TitleProfileServer : StartNameKeys.TitleProfileOffline;

        /// <summary>프로필 표지 그림(I-F12 변형).</summary>
        /// <summary>표지 그림(160×40)에서 글자 자리: 왼쪽 문서 아이콘(16~33px) 뒤 38px부터, 오른쪽 접힌 귀퉁이 앞 6px까지(09 6절 I-F12 그림 기준).</summary>
        public const float BadgeTextInset = 38f, BadgeTextRight = 6f;

        public static string ProfileBadgeVariant(ProfileKind kind) => kind == ProfileKind.Server ? "profile_server" : "profile_offline";

        public static string BuildVersionText(INameTable names, string version)
        {
            string v = version ?? string.Empty;
            return names != null ? names.Format(StartNameKeys.TitleBuildVersion, "version", v) : v;
        }

        /// <summary>들어올 때: 저장 되돌림 창을 띄울까(한 번만).</summary>
        public bool OnEnter(ScreenArgs args)
        {
            _started = false;
            _pulseClock = 0f;
            return StartFlow.TakeRestored(args);
        }

        /// <summary>J. 두 번 눌러도 한 번만 넘어간다.</summary>
        public StartNav Start(ProfileData profile, ScreenArgs args)
        {
            if (_started) return StartNav.None;
            _started = true;
            return StartFlow.EnterOffline(profile, args, ScreenId.Title);
        }

        /// <summary>J가 다시 먹게 한다(OfflineGuide에서 Esc로 돌아왔을 때).</summary>
        public void Rearm() { _started = false; }

        public StartNav ChangeProfile(ScreenArgs args) =>
            StartNav.Push(ScreenId.ProfileSelect, StartFlow.Forward(args, ScreenId.Title, false));

        /// <summary>시작 안내 밝기(0~1): 주기 시작에 1, 반 주기에 가장 어둡다. 끊김 없이 바뀐다.</summary>
        public static float PulseBrightness(float seconds, float period, float minBrightness)
        {
            if (period <= 0f) return 1f;
            double phase = (seconds % period) / period;
            double wave = (Math.Cos(phase * 2.0 * Math.PI) + 1.0) * 0.5; // 1 → 0 → 1
            return (float)(minBrightness + (1.0 - minBrightness) * wave);
        }

        /// <summary>시간을 보낸다. 주기 하나가 돌았으면 true(소리 사건 시점).</summary>
        public bool Advance(float dt, out float brightness)
        {
            float period = _tuning.TitlePulseSeconds;
            float before = _pulseClock;
            _pulseClock += dt;
            bool wrapped = period > 0f && Math.Floor(_pulseClock / period) > Math.Floor(before / period);
            if (period > 0f) _pulseClock %= period;
            brightness = PulseBrightness(_pulseClock, period, _tuning.TitlePulseMinBrightness);
            return wrapped;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>StoryLayer가 이번 프레임에 그릴 것(순수 값).</summary>
    public struct StoryFrame
    {
        // 보스 등장 이름 띠
        public bool IntroVisible;
        public string IntroBossId;
        /// <summary>BossIntroBegin의 A = 1(복수 꼬리표). 판 상태로 다시 셈하지 않는다.</summary>
        public bool IntroRevengeTag;

        /// <summary>대사 판 (480,920) 960×96에 띄울 열쇠(없으면 null).</summary>
        public string DialogKey;

        /// <summary>거울 금 0.4초의 진행(−1 = 없음).</summary>
        public float MirrorCrackT;
        public Vector2 MirrorCrackPos;

        public ReflectionFrame Reflection;
        public Vector2 ReflectionPos;
        /// <summary>되비침·쓰러짐 중 입력 무시 표시(플레이어 InputLocked).</summary>
        public bool InputLockedMark;

        public CollapseFrame Collapse;
        public Vector2 CollapsePos;

        public NoteFrame Note;

        public RevengeFrame Revenge;
        public Vector2 RevengePos;

        /// <summary>최종 격노 화면 가장자리 자홍 8px(BossFinalRage ~ w.Boss == null).</summary>
        public bool FinalRageEdge;

        /// <summary>전체 화면 흰 번쩍의 지금 불투명도, 테두리 강조.</summary>
        public float FlashAlpha;
        public bool FlashBorder;
    }

    /// <summary>한 번 낼 소리.</summary>
    public struct StoryCue
    {
        public string Id;
        public SoundBus Bus;
        public StoryCue(string id, SoundBus bus) { Id = id; Bus = bus; }
    }

    /// <summary>
    /// 첫 만남·복수 연출의 상태(순수 — uGUI 없음). 이벤트(OnEvent)로 일회 연출의 기준 틱을 잡고, 매 프레임 Compute(w)가
    /// 시간표(StoryTimeline)로 보일 것을 정한다. 시뮬레이션 상태를 읽기만 하고 고치지 않는다. 게임 틱을 멈추지 않고
    /// 연출 길이로 판을 붙잡지 않는다(복수는 WaveClearing을 벗어나면 접힌다).
    /// Reset(Build)은 진행 중이던 일회 연출을 다시 틀지 않는다 — 상태로 정해지는 것(최종 격노 테, 입력 무시 표시)만 남는다.
    /// </summary>
    public sealed class StoryDirector
    {
        public const string NameKeyPhantomLine = "phantom_first.master_line";
        public const string NameKeyRevengeLine = "revenge.master_line";

        public const string SoundNoteDrop = "ui.revenge.note_drop";
        public const string SoundNoteLift = "ui.revenge.note_lift";
        public const string SoundPhantomLine = "ui.hud.phantom_line";
        public const string SoundReflectionBurst = "phantom_first.reflection_burst";
        public const string SoundHeartsBreak = "phantom_first.reflection_hearts_break";
        public const string SoundDissolve = "phantom_first.collapse_phantom_dissolve";
        public const string SoundRevengeIntroTag = "revenge.intro_tag";
        public const string SoundOrnament = "revenge.ornament_attach";
        public const string SoundTitleBand = "revenge.title_band";
        public const string SoundSkippable = "revenge.skippable";

        readonly StoryTuning _t;
        readonly StoryFlashGate _gate;

        /// <summary>이번 Compute에서 생긴 소리(부르는 쪽이 내고 비운다).</summary>
        public readonly List<StoryCue> Cues = new List<StoryCue>();
        /// <summary>이번 Compute에서 생긴 흔들림 대 요청 수.</summary>
        public int PendingShakes;

        int _introTick = -1;
        string _introBoss;
        bool _introRevenge;
        int _crackTick = -1;
        Vector2 _crackPos;
        int _reflTick = -1, _reflPrev = -1;
        Vector2 _reflPos;
        int _collapseTick = -1, _collapsePrev = -1;
        Vector2 _collapsePos;
        bool _masterLine;
        int _noteTick = -1;
        bool _noteShown, _noteLifted;
        int _revengeTick = -1, _revengePrev = -1;
        Vector2 _revengePos;
        bool _finalRage;
        StoryFlash _flash;

        public StoryDirector(StoryTuning t)
        {
            _t = t ?? StoryTuning.Defaults();
            _gate = new StoryFlashGate(_t);
        }

        public StoryTuning Tuning => _t;
        /// <summary>시험용: 복수 연출의 기준 틱(−1 = 없음·접힘).</summary>
        public int RevengeBaseTick => _revengeTick;
        public int NoteBaseTick => _noteTick;
        public StoryFlash LastFlash => _flash;

        /// <summary>층을 다시 세울 때(Build). 일회 연출은 버린다.</summary>
        public void Reset()
        {
            _introTick = -1; _introBoss = null; _introRevenge = false;
            _crackTick = -1;
            _reflTick = -1; _reflPrev = -1;
            _collapseTick = -1; _collapsePrev = -1;
            _masterLine = false;
            _noteTick = -1; _noteShown = false; _noteLifted = false;
            _revengeTick = -1; _revengePrev = -1;
            _finalRage = false;
            _flash = new StoryFlash();
            _gate.Reset();
            Cues.Clear();
            PendingShakes = 0;
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.BossIntroBegin:
                    _introTick = e.Tick;
                    _introBoss = e.Id;
                    _introRevenge = e.A == 1;
                    if (_introRevenge) Cue(SoundRevengeIntroTag, SoundBus.Sfx);
                    break;
                case SimEventType.PhantomMirrorCrack:
                    _crackTick = e.Tick;
                    _crackPos = e.Pos;
                    break;
                case SimEventType.PhantomReflection:
                    _reflTick = e.Tick;
                    _reflPrev = -1;
                    _reflPos = e.Pos;
                    break;
                case SimEventType.PhantomCollapse:
                    _reflTick = -1;
                    _collapseTick = e.Tick;
                    _collapsePrev = -1;
                    _collapsePos = e.Pos;
                    break;
                case SimEventType.PhantomMasterLine:
                    _masterLine = true;
                    Cue(SoundPhantomLine, SoundBus.Ui);
                    break;
                case SimEventType.PlayerRose:
                    _masterLine = false;
                    _collapseTick = -1;
                    // 처음 쪽지: 조건은 판 안의 복수 중 여부(프로필의 phantom_met은 정산 때 적히므로 아직 거짓이다).
                    if (!_noteShown && w != null && w.Run != null && w.Run.RevengeActive)
                    {
                        _noteShown = true;
                        _noteLifted = false;
                        _noteTick = e.Tick;
                        Cue(SoundNoteDrop, SoundBus.Ui);
                    }
                    break;
                case SimEventType.RevengeKill:
                    _revengeTick = e.Tick;
                    _revengePrev = -1;
                    _revengePos = e.Pos;
                    break;
                case SimEventType.BossFinalRage:
                    _finalRage = true;
                    break;
            }
        }

        void Cue(string id, SoundBus bus) => Cues.Add(new StoryCue(id, bus));

        /// <param name="effectsLevel">0 전체 / 1 줄임 / 2 최소(번쩍 단계 상한).</param>
        /// <param name="flashScale">설정 flash_scale(0~1).</param>
        public StoryFrame Compute(SimWorld w, int effectsLevel, float flashScale)
        {
            var f = new StoryFrame { MirrorCrackT = -1f };
            f.Reflection = new ReflectionFrame { Burst0 = -1f, Burst1 = -1f, Burst2 = -1f };
            f.Collapse = new CollapseFrame { DissolveT = -1f };
            if (w == null) return f;
            int now = w.Tick;
            var phase = w.Run != null ? w.Run.Phase : RunPhase.NotStarted;

            // 보스 등장 이름 띠(+ 복수 꼬리표면 첫 만남 한마디 1.2초).
            string introLine = null;
            if (_introTick >= 0)
            {
                int e = now - _introTick;
                if (StoryTimeline.IntroBand(_t, e))
                {
                    f.IntroVisible = true;
                    f.IntroBossId = _introBoss;
                    f.IntroRevengeTag = _introRevenge;
                }
                if (_introRevenge && StoryTimeline.IntroRevengeLine(_t, e)) introLine = NameKeyPhantomLine;
                if (!f.IntroVisible && introLine == null) _introTick = -1;
            }

            // 거울 금(허상 체력 바닥).
            if (_crackTick >= 0)
            {
                f.MirrorCrackT = StoryTimeline.MirrorCrack(_t, now - _crackTick);
                f.MirrorCrackPos = _crackPos;
                if (f.MirrorCrackT < 0f) _crackTick = -1;
            }

            // 되비침: 만개 연출 24/54/84틱, +114틱 번쩍·흔들림 대.
            if (_reflTick >= 0)
            {
                int e = now - _reflTick;
                if (phase != RunPhase.PhantomFinale && phase != RunPhase.PhantomDowned) { _reflTick = -1; }
                else
                {
                    f.Reflection = StoryTimeline.Reflection(_t, e);
                    f.ReflectionPos = _reflPos;
                    var b = _t.ReflectionBurstTicks;
                    for (int i = 0; b != null && i < b.Length; i++)
                        if (_reflPrev < b[i] && e >= b[i] && e < b[i] + _t.ReflectionBurstLifeTicks) Cue(SoundReflectionBurst, SoundBus.Sfx);
                    if (StoryTimeline.CrossedReflectionFlash(_t, _reflPrev, e))
                    {
                        RequestFlash(_t.ReflectionFlashOpacity, now, effectsLevel, flashScale);
                        PendingShakes++;
                        Cue(SoundHeartsBreak, SoundBus.Sfx);
                    }
                    _reflPrev = e;
                }
            }

            // 쓰러짐: 무채색 덮개, 2.60초 은색 가루.
            if (_collapseTick >= 0)
            {
                int e = now - _collapseTick;
                f.Collapse = StoryTimeline.Collapse(_t, e);
                f.CollapsePos = _collapsePos;
                if (_collapsePrev < _t.CollapseDissolveTick && e >= _t.CollapseDissolveTick && e < _t.CollapseTicks) Cue(SoundDissolve, SoundBus.Sfx);
                _collapsePrev = e;
                if (!f.Collapse.Visible) _collapseTick = -1;
            }

            // 입력 무시 표시(되비침·쓰러짐 — 상태로 정해진다).
            if (phase == RunPhase.PhantomFinale || phase == RunPhase.PhantomDowned)
            {
                for (int i = 0; i < w.Players.Length; i++)
                {
                    var p = w.Players[i];
                    if (p != null && p.Active && p.InputLocked) { f.InputLockedMark = true; break; }
                }
            }

            // 첫 만남 대사 판: PhantomMasterLine(쓰러짐 +72틱) ~ 일어남.
            if (_masterLine && phase != RunPhase.PhantomDowned) _masterLine = false;

            // 처음 쪽지: 내려오고 240틱(4초) 뒤 올라감.
            if (_noteTick >= 0)
            {
                int e = now - _noteTick;
                f.Note = StoryTimeline.FirstNote(_t, e);
                if (f.Note.Lifting && !_noteLifted) { _noteLifted = true; Cue(SoundNoteLift, SoundBus.Ui); }
                if (!f.Note.Visible && e >= _t.FirstNoteTicks)
                {
                    if (!_noteLifted) { _noteLifted = true; Cue(SoundNoteLift, SoundBus.Ui); }
                    _noteTick = -1;
                }
            }

            // 복수 처치: 처치 틱 기준. WaveClearing을 벗어나면(넘김·264틱) 남은 연출을 접는다.
            if (_revengeTick >= 0)
            {
                int e = now - _revengeTick;
                f.Revenge = StoryTimeline.Revenge(_t, e, phase == RunPhase.WaveClearing);
                f.RevengePos = _revengePos;
                if (!f.Revenge.Visible) { _revengeTick = -1; }
                else
                {
                    if (StoryTimeline.CrossedRevengeFlash(_t, _revengePrev, e))
                    {
                        RequestFlash(_t.RevengeFlashOpacity, now, effectsLevel, flashScale);
                        PendingShakes++;
                    }
                    if (_revengePrev < _t.RevengeOrnamentStart && e >= _t.RevengeOrnamentStart) Cue(SoundOrnament, SoundBus.Sfx);
                    if (_revengePrev < _t.RevengeSkipHint && e >= _t.RevengeSkipHint) Cue(SoundSkippable, SoundBus.Sfx);
                    if (_revengePrev < _t.RevengeBandStart && e >= _t.RevengeBandStart) Cue(SoundTitleBand, SoundBus.Sfx);
                    _revengePrev = e;
                }
            }

            // 대사 판: 복수 한마디 > 첫 만남 한마디(쓰러짐) > 등장 때의 첫 만남 한마디.
            if (f.Revenge.Visible && f.Revenge.MasterLine) f.DialogKey = NameKeyRevengeLine;
            else if (_masterLine) f.DialogKey = NameKeyPhantomLine;
            else f.DialogKey = introLine;

            // 최종 격노 테: 보스가 걷힐 때까지.
            if (w.Boss == null) _finalRage = false;
            f.FinalRageEdge = w.Boss != null && (w.Boss.FinalRage || _finalRage);

            // 번쩍.
            f.FlashAlpha = StoryFlashGate.AlphaAt(_flash, now, _t.FlashOnTicks, _t.FlashOffTicks);
            f.FlashBorder = StoryFlashGate.BorderAt(_flash, now, _t.FlashBorderTicks);
            return f;
        }

        void RequestFlash(float opacity, int tick, int effectsLevel, float flashScale)
        {
            _flash = _gate.Request(opacity, tick, effectsLevel, flashScale);
        }
    }
}

using System.Collections.Generic;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 허수아비 연습의 세션 다루기(엔진 없음, 00 문서 4.1절·5.5절, D-17·D-56). 진입이 어디든 같은 화면(TV-082)이고 세션 호출만 다르다:
    /// MainMenu = StartRun / EndRun, Camp = PushSubRun / PopSubRun(본판은 멈춘다). 설정은 IWandService.BuildPracticeConfig만 쓴다
    /// (BuildRunConfig는 출발 횟수를 올리므로 쓰지 않는다). 들어갈 때 전투 입력의 문(InputScreen)을 DummyPractice로 돌리고 나갈 때 되돌린다.
    /// </summary>
    public sealed class PracticeSession
    {
        readonly GameContext _ctx;
        ScreenId _prevInput;
        bool _inputTaken;

        public PracticeEntry Entry { get; private set; }
        public int Character { get; private set; }
        public long WandId { get; private set; }
        /// <summary>방을 열었다(StartRun 또는 PushSubRun을 불렀다).</summary>
        public bool Open { get; private set; }
        /// <summary>방을 못 열었을 때의 문구 열쇠(CoreEmpty → error.core_empty 등).</summary>
        public string ErrorKey { get; private set; }
        /// <summary>마지막으로 넘긴 설정(테스트·표시용).</summary>
        public RunConfig Config { get; private set; }

        public PracticeSession(GameContext ctx) { _ctx = ctx; }

        IGameSession Session => _ctx != null ? _ctx.Session : null;

        /// <summary>들어간다. character 0이면 프로필의 last_character(D-56). Camp면 인자를 보지 않고 지금 판의 조립을 읽는다.</summary>
        public bool Enter(PracticeEntry entry, int character, ulong seed)
        {
            Entry = entry;
            ErrorKey = null;
            Open = false;
            var s = Session;
            if (s != null && !_inputTaken)
            {
                _prevInput = s.InputScreen;
                s.InputScreen = ScreenId.DummyPractice;
                _inputTaken = true;
            }
            return entry == PracticeEntry.Camp ? OpenFromCamp(seed) : OpenFromMenu(character, seed);
        }

        bool OpenFromMenu(int character, ulong seed)
        {
            var profile = _ctx != null ? _ctx.Profile : null;
            if (character < 1 || character > Ids.Char.Count) character = profile != null ? profile.LastCharacter : 1;
            if (character < 1 || character > Ids.Char.Count) character = 1;
            Character = character;
            var rec = profile != null ? profile.Character(character) : null;
            WandId = rec != null ? rec.EquippedWandId : 0;
            var wands = _ctx != null ? _ctx.Wands : null;
            if (wands == null || Session == null) { ErrorKey = CampFormat.ErrorKey(ErrorCode.NotFound); return false; }
            var res = wands.BuildPracticeConfig(character, WandId, RunMode.Practice, null, seed);
            if (!res.Ok) { ErrorKey = CampFormat.ErrorKey(res.Error); return false; }
            Config = res.Value;
            Session.StartRun(Config);
            Open = true;
            return true;
        }

        bool OpenFromCamp(ulong seed)
        {
            var s = Session;
            var main = s != null ? s.MainWorld : null;
            var p = FirstActive(main);
            var wands = _ctx != null ? _ctx.Wands : null;
            if (p == null || wands == null) { ErrorKey = CampFormat.ErrorKey(ErrorCode.NotFound); return false; }
            Character = Ids.Char.NumberOf(p.CharId);
            WandId = p.Wand != null ? p.Wand.WandId : 0;
            var spec = CurrentLoadout(p);
            var res = wands.BuildPracticeConfig(Character, WandId, RunMode.Practice, spec, seed);
            if (!res.Ok) { ErrorKey = CampFormat.ErrorKey(res.Error); return false; }
            Config = res.Value;
            s.PushSubRun(Config);
            Open = true;
            return true;
        }

        /// <summary>지금 판의 조립(코어·연결 칸 id, 야생 블록 포함)을 LoadoutSpec으로.</summary>
        public static LoadoutSpec CurrentLoadout(PlayerState p)
        {
            var spec = new LoadoutSpec();
            if (p == null || p.Loadout == null) return spec;
            spec.Core = string.IsNullOrEmpty(p.Loadout.Core.BlockId) ? null : p.Loadout.Core.BlockId;
            var ids = p.Loadout.LinkIds();
            for (int i = 0; i < ids.Length; i++) spec.Links.Add(string.IsNullOrEmpty(ids[i]) ? null : ids[i]);
            // 판 한정 별(★)도 연습에 그대로 가져간다(허수아비 수치가 야영 수치판과 같게).
            spec.Stars = new List<int>(p.Loadout.SlotStars());
            return spec;
        }

        public static PlayerState FirstActive(SimWorld w)
        {
            if (w == null) return null;
            for (int i = 0; i < w.Players.Length; i++) if (w.Players[i].Active) return w.Players[i];
            return null;
        }

        /// <summary>메인 메뉴 진입에서 Q/E로 캐릭터를 바꾼다: 지금 방을 걷고(EndRun) 그 캐릭터의 든 지팡이로 다시 연다.</summary>
        public bool SwitchCharacter(int delta, ulong seed)
        {
            if (Entry != PracticeEntry.MainMenu) return false;
            int n = Ids.Char.Count;
            int c = ((Character - 1 + delta) % n + n) % n + 1;
            if (Open && Session != null) Session.EndRun();
            Open = false;
            ErrorKey = null;
            return OpenFromMenu(c, seed);
        }

        /// <summary>나간다: 방을 걷고(MainMenu = EndRun, Camp = PopSubRun) 전투 입력의 문을 앞의 값으로 되돌린다.</summary>
        public void Exit()
        {
            var s = Session;
            if (s != null && Open)
            {
                if (Entry == PracticeEntry.Camp) { if (s.HasSubRun) s.PopSubRun(); }
                else s.EndRun();
            }
            Open = false;
            if (s != null && _inputTaken) s.InputScreen = _prevInput;
            _inputTaken = false;
        }
    }
}

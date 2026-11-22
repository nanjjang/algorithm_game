using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 비기 트라이(ScreenId.ArtTrie, 08 요약서 8.4절). 트라이 그림 = 커맨드 목록 = 해금 화면(ctx.Rules.Arts.DescribeTrie).
    /// 인자: ArtPick(bool, 해금 차례 고르기 — 판 중 야영), Character(int, 판 밖에서 열 때). 판 중이면 그 판의 해금, 판 밖이면 프로필의 해금.
    /// 허수아비 연습 위에서 열리면 방금 넣은 입력까지의 길(지금 위치)을 밝힌다.
    /// </summary>
    public sealed class ArtTrieScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.ArtTrie;
        protected override string TitleKey => "str.art_trie.title";

        ArtTrieView _view;
        bool _pickMode;
        int _character;
        string _sig;
        readonly List<RecentInput> _recent = new List<RecentInput>();

        protected override void BuildContent(RectTransform content)
        {
            _view = new ArtTrieView(Ctx, content, (UiScale.RefWidth - ArtTrieView.W) * 0.5f, 48f, Focus);
            _view.Picked = OnPicked;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _pickMode = Args.Get(ScreenArgKeys.ArtPick, false);
            var profile = Ctx != null ? Ctx.Profile : null;
            _character = Args.Get(ScreenArgKeys.Character, 0);
            var p = Player();
            if (_character < 1 || _character > Ids.Char.Count) _character = p != null ? Ids.Char.NumberOf(p.CharId) : (profile != null ? profile.LastCharacter : 1);
            _sig = null;
            Rebuild();
            if (_pickMode && _view.PickShown) PlayUi("ui.trie.unlock_candidate");
        }

        /// <summary>해금 차례는 본판의 것(판 중 야영). 지금 위치는 지금 도는 세계(연습 방)의 것.</summary>
        SimWorld MainWorld => Ctx != null && Ctx.Session != null ? Ctx.Session.MainWorld : null;
        SimWorld LiveWorld => Ctx != null && Ctx.Session != null ? Ctx.Session.World : null;

        PlayerState Player()
        {
            var p = CampRules.MeOf(LiveWorld);
            return p != null && (_character == 0 || Ids.Char.NumberOf(p.CharId) == _character) ? p : null;
        }

        /// <summary>이 캐릭터의 해금: 판 중이면 그 판(p.UnlockedArts), 판 밖이면 프로필 + 시작 3종(04 11.1절 U-R2).</summary>
        public static List<string> UnlockedFor(GameContext ctx, PlayerState p, int character)
        {
            var list = new List<string>();
            if (p != null) { list.AddRange(p.UnlockedArts); return list; }
            list.Add(Ids.Art.Burst);
            list.Add(Ids.Art.Recoil);
            var ch = ctx != null && ctx.Content != null ? ctx.Content.Character(Ids.Char.Of(character)) : null;
            if (ch != null && ch.UniqueArt != null && !list.Contains(ch.UniqueArt)) list.Add(ch.UniqueArt);
            var rec = ctx != null && ctx.Profile != null ? ctx.Profile.Character(character) : null;
            if (rec != null) foreach (var a in rec.UnlockedArts) if (!list.Contains(a)) list.Add(a);
            return list;
        }

        void Rebuild()
        {
            var p = Player();
            var main = MainWorld;
            ArtOffer offer = _pickMode && main != null ? CampRules.RewardOf(main, CampRules.MeOf(main)).ArtOffer : null;
            var cands = offer != null ? offer.Candidates : null;
            string charId = Ids.Char.Of(_character);
            var m = ArtTrieModel.Build(Ctx.Content, Ctx.Rules != null ? Ctx.Rules.Arts : null, charId, UnlockedFor(Ctx, p, _character), cands);
            if (p != null && LiveWorld != null && LiveWorld.Run.Phase == RunPhase.Practice)
            {
                _recent.Clear();
                LiveWorld.Mods.Arts.GetRecentInputs(p, _recent);
                m.MarkCurrent(_recent);
            }
            _view.Rebuild(m, cands != null && cands.Count > 0);
        }

        void OnPicked(string artId)
        {
            var main = MainWorld;
            var p = CampRules.MeOf(main);
            var offer = main != null && p != null ? CampRules.RewardOf(main, p).ArtOffer : null;
            if (p == null || offer == null || !offer.Candidates.Contains(artId)) return;
            main.Enqueue(new RunCommand(RunCommandKind.ArtPick, 0, 0, artId, p.Slot));
            bool online = Ctx.Session is IOnlineSession os && os.IsOnline; // 온라인 판은 오프라인 프로필에 남기지 않는다
            if (Ctx.Settlement != null && !online) Ctx.Settlement.RecordArtUnlock(Ids.Char.NumberOf(p.CharId), artId);
            PlayUi("ui.trie.unlock");
        }

        public override void Tick(float dt)
        {
            // 해금 차례가 바뀌거나(다음 차례·끝) 연습 중 입력이 바뀌면 다시 그린다.
            var main = MainWorld;
            var live = LiveWorld;
            var sb = new System.Text.StringBuilder();
            var mine = _pickMode && main != null ? CampRules.RewardOf(main, CampRules.MeOf(main)) : null;
            if (mine != null) sb.Append(mine.ArtOffer != null ? mine.ArtOffer.TurnWave : -1).Append(mine.ArtTurns.Count);
            var p = Player();
            if (p != null && live != null && live.Run.Phase == RunPhase.Practice)
            {
                _recent.Clear();
                live.Mods.Arts.GetRecentInputs(p, _recent);
                for (int i = 0; i < _recent.Count; i++) sb.Append((int)_recent[i].Dir).Append(_recent[i].TimeU);
            }
            if (p != null) sb.Append('|').Append(p.UnlockedArts.Count);
            string sig = sb.ToString();
            if (_sig == null) { _sig = sig; return; }
            if (sig == _sig) return;
            _sig = sig;
            Rebuild();
            // 고를 차례가 다 끝났으면 닫는다(야영 화면이 연 고르기).
            if (_pickMode && main != null && !CampRules.ArtPickPending(CampRules.RewardOf(main, CampRules.MeOf(main)))) Close();
        }

        public override bool OnMenuKey(MenuKey key)
        {
            bool used = base.OnMenuKey(key);
            var n = _view.NodeOf(Focus.Current);
            if (n != null) _view.Select(n);
            if (!used && key == MenuKey.Cancel) { Close(); return true; }
            return used;
        }
    }
}

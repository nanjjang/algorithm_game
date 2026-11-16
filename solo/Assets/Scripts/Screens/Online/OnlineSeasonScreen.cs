using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UInputField = UnityEngine.UI.InputField;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>시즌 화면 글자 열쇠(Resources/NamesSolo/OnlineSeason.ko.json).</summary>
    public static class SeasonKeys
    {
        public const string Title = "str.season.title";
        public const string Tab = "str.season.tab.";
        public const string SeasonLine = "str.season.line.season";
        public const string SettlingLine = "str.season.line.settling";
        public const string LeagueLine = "str.season.line.league";
        public const string League = "str.season.league.";
        public const string IlLine = "str.season.line.il";
        public const string RatingLine = "str.season.line.rating";
        public const string WaveLine = "str.season.line.wave";
        public const string HeirLine = "str.season.line.heir";
        public const string HeirNone = "str.season.line.heir_none";
        public const string PlayLine = "str.season.line.play";
        public const string TitlesLine = "str.season.line.titles";
        public const string TitleHeir = "str.season.title.heir";
        public const string TitleDisciple = "str.season.title.disciple";
        public const string OptUp = "str.season.opt_up";
        public const string LeagueResult = "str.season.league_result.";
        public const string QueueJoin = "str.season.queue.join";
        public const string QueueLeave = "str.season.queue.leave";
        public const string QueueWaiting = "str.season.queue.waiting";
        public const string QueueCheck = "str.season.queue.check.";
        public const string QueueDropped = "str.season.queue.dropped";
        public const string WatchHint = "str.season.watch.hint";
        public const string Watch = "str.season.watch.button";
        public const string WatchNote = "str.season.watch.note";
        public const string HallHead = "str.season.hall.head";
        public const string HallRow = "str.season.hall.row";
        public const string HallRowNone = "str.season.hall.row_none";
        public const string HallEmpty = "str.season.hall.empty";
        public const string Board = "str.season.board.";
        public const string BoardChar = "str.season.board.char";
        public const string BoardRow = "str.season.board.row";
        public const string BoardRowRated = "str.season.board.row_rated";
        public const string BoardRowHeir = "str.season.board.row_heir";
        public const string BoardEmpty = "str.season.board.empty";
        public const string MailHead = "str.season.mail.head";
        public const string MailReason = "str.season.mail.reason.";
        public const string MailGold = "str.season.mail.gold";
        public const string MailClaim = "str.season.mail.claim";
        public const string MailClaimAll = "str.season.mail.claim_all";
        public const string MailEmpty = "str.season.mail.empty";
        public const string MailResult = "str.season.mail.result.";
        public const string SafeNameHint = "str.season.safe.name_hint";
        public const string SafeBlock = "str.season.safe.block";
        public const string SafeUnblock = "str.season.safe.unblock";
        public const string SafeReport = "str.season.safe.report.";
        public const string SafeBlockedHead = "str.season.safe.blocked_head";
        public const string SafeBlockedEmpty = "str.season.safe.blocked_empty";
        public const string SafeNote = "str.season.safe.note";
        public const string BlockResult = "str.season.block_result.";
        public const string ReportResult = "str.season.report_result.";
        public const string Muted = "str.season.muted";
    }

    /// <summary>
    /// 시즌(ScreenId.OnlineSeason, 11-online.md 6.14절). 탭 넷:
    ///   시즌 — 시즌 번호·남은 날, 리그와 보유 IL, 결투 점수, 후계 점수, [위 리그로], [자동 매치 대기], 관전 코드, 역대 후계자.
    ///   순위표 — 시즌 종합·주간·주간(+7 이하)·역대·친구·리그 결투 점수·후계 점수·캐릭터별.
    ///   우편함 — 장터 대금·돌아온 물건·시즌 보상 받기.
    ///   차단·신고 — 이름으로 차단·신고(사유 5종), 차단 목록.
    /// 자동 매치가 잡히면 "join:코드", 관전을 누르면 "watch:코드"를 결과로 닫는다(온라인 방 화면이 그 방으로 들어간다).
    /// </summary>
    public sealed class OnlineSeasonScreen : ScreenBase
    {
        const float X0 = 48f, TabY = 104f, BodyY = 176f, BodyW = 1824f, BodyH = 800f, P = 28f;
        const int BoardRows = 15, MailRows = 8, BlockRows = 8, HallRows = 8;

        OnlineService _svc;
        UiLabel _status;
        readonly RectTransform[] _tabRoots = new RectTransform[4];
        readonly UiButton[] _tabButtons = new UiButton[4];
        int _tab;

        // 시즌 탭
        readonly UiLabel[] _lines = new UiLabel[8];
        UiButton _optUp, _queueJoin, _queueLeave, _watch;
        UiLabel _queueLabel, _hall;
        UInputField _watchCode;
        SeasonInfo _info;
        bool _waiting;

        // 순위표 탭
        readonly List<UiButton> _boardButtons = new List<UiButton>();
        readonly UiLabel[] _boardLines = new UiLabel[BoardRows];
        UiLabel _boardEmpty;
        int _boardPick;
        static readonly BoardKind[] Kinds =
        {
            BoardKind.SeasonOverall, BoardKind.Weekly, BoardKind.WeeklyLow, BoardKind.AllTime, BoardKind.Friends, BoardKind.RankedLeague, BoardKind.Heir,
            BoardKind.Character, BoardKind.Character, BoardKind.Character, BoardKind.Character, BoardKind.Character,
        };

        // 우편함 탭
        sealed class MailRow { public RectTransform Root; public UiLabel Text; public UiButton Claim; }
        readonly List<MailRow> _mailRows = new List<MailRow>();
        UiLabel _mailHead, _mailEmpty;
        List<MailItem> _mail = new List<MailItem>();

        // 차단·신고 탭
        sealed class BlockRow { public RectTransform Root; public UiLabel Name; public UiButton Remove; }
        readonly List<BlockRow> _blockRows = new List<BlockRow>();
        UInputField _safeName;
        UiLabel _blockedHead, _blockedEmpty;
        List<FriendInfo> _blocked = new List<FriendInfo>();

        public override ScreenId Id => ScreenId.OnlineSeason;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "S-0501", "camp_background", 0.6f);
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "square", T(SeasonKeys.Title), out var band);
            UiKit.Button(Ctx, band, T(OnlineKeys.Leave), UiScale.RefWidth - OnlineUi.Edge - 176f, 16f, 176f, 48f, () => Close(), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _tabButtons[i] = UiKit.Button(Ctx, content, T(SeasonKeys.Tab + i), X0 + i * 232f, TabY, 220f, 56f, () => ShowTab(k), UiButtonStyle.Medium, Focus, null, UiFontId.F24);
                _tabRoots[i] = UiKit.Node(content, "Tab" + i, X0, BodyY, BodyW, BodyH);
            }
            BuildSeason(_tabRoots[0]);
            BuildBoard(_tabRoots[1]);
            BuildMail(_tabRoots[2]);
            BuildSafety(_tabRoots[3]);
            _status = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, X0, 992f, BodyW, 40f, UiTheme.Amber);
            UiKit.DropShadow(_status);
        }

        void BuildSeason(RectTransform root)
        {
            float lw = 880f, rw = BodyW - lw - 32f;
            var left = UiKit.Panel(Ctx, root, UiPanelKind.Board, 0f, 0f, lw, BodyH).rectTransform;
            UiKit.Bevel(left);
            float y = 24f, inner = lw - 2f * P;
            for (int i = 0; i < _lines.Length; i++)
            {
                _lines[i] = UiKit.Label(Ctx, left, string.Empty, UiFontId.F24, P, y, inner, 40f, i == 0 ? UiTheme.SelectYellow : UiTheme.Chalk);
                y += 48f;
            }
            y += 16f;
            UiKit.Divider(Ctx, left, P, y, inner);
            y += 24f;
            _optUp = UiKit.Button(Ctx, left, T(SeasonKeys.OptUp), P, y, inner, 56f, OnOptUp, UiButtonStyle.Medium, Focus, null, UiFontId.F24);
            y += 72f;
            _queueJoin = UiKit.Button(Ctx, left, T(SeasonKeys.QueueJoin), P, y, inner, 64f, OnQueueJoin, UiButtonStyle.Primary, Focus, null, UiFontId.F24);
            _queueLeave = UiKit.Button(Ctx, left, T(SeasonKeys.QueueLeave), P, y, inner, 64f, OnQueueLeave, UiButtonStyle.Medium, Focus, null, UiFontId.F24);
            y += 76f;
            _queueLabel = UiKit.Label(Ctx, left, string.Empty, UiFontId.F16, P, y, inner, 72f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);

            var right = UiKit.Panel(Ctx, root, UiPanelKind.Board, lw + 32f, 0f, rw, BodyH).rectTransform;
            UiKit.Bevel(right);
            float rin = rw - 2f * P;
            _watchCode = OnlineUi.Field(Ctx, Focus, right, null, T(SeasonKeys.WatchHint), P, 24f, rin - 196f, RoomServer.CodeLength + 2);
            _watch = UiKit.Button(Ctx, right, T(SeasonKeys.Watch), P + rin - 184f, 24f, 184f, 48f, OnWatch, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            UiKit.Label(Ctx, right, T(SeasonKeys.WatchNote), UiFontId.F16, P, 84f, rin, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            UiKit.Divider(Ctx, right, P, 144f, rin);
            UiKit.Label(Ctx, right, T(SeasonKeys.HallHead), UiFontId.F24, P, 160f, rin, 40f, UiTheme.SelectYellow);
            _hall = UiKit.Label(Ctx, right, string.Empty, UiFontId.F24, P, 208f, rin, HallRows * 40f, UiTheme.Chalk, TextAnchor.UpperLeft, wrap: true);
        }

        void BuildBoard(RectTransform root)
        {
            var panel = UiKit.Panel(Ctx, root, UiPanelKind.Board, 0f, 0f, BodyW, BodyH).rectTransform;
            UiKit.Bevel(panel);
            float bw = 140f;
            for (int i = 0; i < Kinds.Length; i++)
            {
                int k = i;
                string label = i < 7 ? T(SeasonKeys.Board + Kinds[i]) : F(SeasonKeys.BoardChar, "n", i - 6);
                _boardButtons.Add(UiKit.Button(Ctx, panel, label, P + i * (bw + 8f), 20f, bw, 44f, () => PickBoard(k), UiButtonStyle.Small, Focus, null, UiFontId.F16));
            }
            for (int i = 0; i < BoardRows; i++)
                _boardLines[i] = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, P, 88f + i * 46f, BodyW - 2f * P, 40f, i < 3 ? UiTheme.SelectYellow : UiTheme.Chalk);
            _boardEmpty = UiKit.Label(Ctx, panel, T(SeasonKeys.BoardEmpty), UiFontId.F24, P, 120f, BodyW - 2f * P, 40f, UiTheme.SlateBlue);
        }

        void BuildMail(RectTransform root)
        {
            var panel = UiKit.Panel(Ctx, root, UiPanelKind.Board, 0f, 0f, BodyW, BodyH).rectTransform;
            UiKit.Bevel(panel);
            float inner = BodyW - 2f * P;
            _mailHead = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, P, 24f, inner - 260f, 40f, UiTheme.SelectYellow);
            UiKit.Button(Ctx, panel, T(SeasonKeys.MailClaimAll), P + inner - 240f, 20f, 240f, 48f, () => Claim(0), UiButtonStyle.Primary, Focus, null, UiFontId.F16);
            for (int i = 0; i < MailRows; i++)
            {
                int k = i;
                var r = new MailRow { Root = UiKit.Node(panel, "Mail" + i, P, 88f + i * 76f, inner, 68f) };
                UiKit.Panel(Ctx, r.Root, UiPanelKind.Recess, 0f, 0f, inner, 68f);
                r.Text = UiKit.Label(Ctx, r.Root, string.Empty, UiFontId.F24, 16f, 14f, inner - 180f, 40f, UiTheme.Chalk);
                r.Claim = UiKit.Button(Ctx, r.Root, T(SeasonKeys.MailClaim), inner - 148f, 12f, 136f, 44f, () => ClaimRow(k), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
                _mailRows.Add(r);
            }
            _mailEmpty = UiKit.Label(Ctx, panel, T(SeasonKeys.MailEmpty), UiFontId.F24, P, 120f, inner, 40f, UiTheme.SlateBlue);
        }

        void BuildSafety(RectTransform root)
        {
            var panel = UiKit.Panel(Ctx, root, UiPanelKind.Board, 0f, 0f, BodyW, BodyH).rectTransform;
            UiKit.Bevel(panel);
            float inner = BodyW - 2f * P;
            _safeName = OnlineUi.Field(Ctx, Focus, panel, null, T(SeasonKeys.SafeNameHint), P, 24f, 420f, AccountStore.NameMax);
            UiKit.Button(Ctx, panel, T(SeasonKeys.SafeBlock), P + 436f, 24f, 160f, 48f, OnBlock, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            for (int i = 1; i <= 5; i++)
            {
                int reason = i;
                UiKit.Button(Ctx, panel, T(SeasonKeys.SafeReport + i), P + 436f + 168f + (i - 1) * 208f, 24f, 200f, 48f, () => OnReport(reason), UiButtonStyle.Small, Focus, null, UiFontId.F16);
            }
            UiKit.Label(Ctx, panel, T(SeasonKeys.SafeNote), UiFontId.F16, P, 84f, inner, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            UiKit.Divider(Ctx, panel, P, 140f, inner);
            _blockedHead = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, P, 156f, inner, 40f, UiTheme.SelectYellow);
            for (int i = 0; i < BlockRows; i++)
            {
                int k = i;
                var r = new BlockRow { Root = UiKit.Node(panel, "Block" + i, P, 208f + i * 68f, 800f, 60f) };
                UiKit.Panel(Ctx, r.Root, UiPanelKind.Recess, 0f, 0f, 800f, 60f);
                r.Name = UiKit.Label(Ctx, r.Root, string.Empty, UiFontId.F24, 16f, 10f, 560f, 40f, UiTheme.Chalk);
                r.Remove = UiKit.Button(Ctx, r.Root, T(SeasonKeys.SafeUnblock), 800f - 148f, 10f, 136f, 40f, () => Unblock(k), UiButtonStyle.Small, Focus, null, UiFontId.F16);
                _blockRows.Add(r);
            }
            _blockedEmpty = UiKit.Label(Ctx, panel, T(SeasonKeys.SafeBlockedEmpty), UiFontId.F24, P, 216f, inner, 40f, UiTheme.SlateBlue);
        }

        // ------------------------------------------------------------ 수명

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _waiting = false;
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null) Say(T(OnlineKeys.ErrorServer), false);
            ShowTab(0);
            Focus.SetCursor(0, false);
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            _waiting = false;
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        public override void Tick(float dt)
        {
            if (Screens != null) Screens.CaptureKeys = (_watchCode != null && _watchCode.isFocused) || (_safeName != null && _safeName.isFocused);
            if (!_waiting || _svc == null) return;
            string code = _svc.PollMatch(out bool still);
            if (code != null)
            {
                _waiting = false;
                Close("join:" + code);
                return;
            }
            if (!still)
            {
                _waiting = false;
                Say(T(SeasonKeys.QueueDropped), false);
                RefreshSeason();
            }
        }

        void Say(string text, bool good)
        {
            _status.Text.color = good ? UiTheme.Good : UiTheme.Amber;
            _status.SetText(text ?? string.Empty);
        }

        void ShowTab(int tab)
        {
            _tab = tab;
            for (int i = 0; i < 4; i++)
            {
                _tabRoots[i].gameObject.SetActive(i == tab);
                _tabButtons[i].Picked = i == tab;
            }
            if (tab == 0) RefreshSeason();
            else if (tab == 1) PickBoard(_boardPick);
            else if (tab == 2) RefreshMail();
            else RefreshSafety();
        }

        // ------------------------------------------------------------ 시즌 탭

        string TitleText(string id)
        {
            // "title.heir:3" → "제3대 후계자"
            int colon = id != null ? id.IndexOf(':') : -1;
            string n = colon >= 0 ? id.Substring(colon + 1) : string.Empty;
            bool heir = id != null && id.StartsWith("title.heir", StringComparison.Ordinal);
            return F(heir ? SeasonKeys.TitleHeir : SeasonKeys.TitleDisciple, "n", n);
        }

        void RefreshSeason()
        {
            _info = _svc != null && !_waiting ? _svc.Season() ?? _info : _info;
            var s = _info;
            bool has = s != null && _svc != null && !_svc.IsGuest;
            for (int i = 0; i < _lines.Length; i++) _lines[i].SetText(string.Empty);
            if (s != null)
            {
                long left = Math.Max(0, (s.Phase == 0 ? s.EndTicks : s.SettleUntilTicks) - s.NowTicks);
                long days = left / TimeSpan.TicksPerDay, hours = left % TimeSpan.TicksPerDay / TimeSpan.TicksPerHour;
                _lines[0].SetText(F(s.Phase == 0 ? SeasonKeys.SeasonLine : SeasonKeys.SettlingLine, "n", s.SeasonId, "d", days, "h", hours));
            }
            if (has)
            {
                int l = Mathf.Clamp(s.League, 0, OnlineRules.LeagueCount - 1);
                _lines[1].SetText(F(SeasonKeys.LeagueLine, "league", T(SeasonKeys.League + l)));
                _lines[2].SetText(F(SeasonKeys.IlLine, "il", s.HoldingIl));
                _lines[3].SetText(F(SeasonKeys.RatingLine, "rating", s.Rating[l], "w", s.RatedWins[l], "l", s.RatedLosses[l], "games", s.SeasonRanked));
                _lines[4].SetText(F(SeasonKeys.WaveLine, "score", N(s.SeasonScore), "runs", s.SeasonRuns));
                _lines[5].SetText(s.Rank > 0 ? F(SeasonKeys.HeirLine, "total", s.Total, "e", s.E, "p", s.P, "rank", s.Rank, "n", s.Population) : T(SeasonKeys.HeirNone));
                _lines[6].SetText(F(SeasonKeys.PlayLine, "h", s.PlaySeconds / 3600, "m", s.PlaySeconds % 3600 / 60, "mail", s.MailCount));
                if (s.Titles != null && s.Titles.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var t in s.Titles) names.Add(TitleText(t));
                    _lines[7].SetText(F(SeasonKeys.TitlesLine, "titles", string.Join(", ", names.ToArray())));
                }
                else if (s.ChatMutedSeconds > 0) _lines[7].SetText(F(SeasonKeys.Muted, "m", (s.ChatMutedSeconds + 59) / 60));
            }
            _optUp.Enabled = has && !_waiting && !s.OptedUp && s.League < OnlineRules.LeagueTop;
            _queueJoin.gameObject.SetActive(!_waiting);
            _queueLeave.gameObject.SetActive(_waiting);
            _queueJoin.Enabled = has;
            _watch.Enabled = _svc != null && !_waiting;
            if (_waiting) _queueLabel.SetText(T(SeasonKeys.QueueWaiting));
            else if (has && s.Queue != (byte)AccountStore.QueueCheck.Ok)
                _queueLabel.SetText(F(SeasonKeys.QueueCheck + (AccountStore.QueueCheck)s.Queue, "m", (s.QueueWaitSeconds + 59) / 60));
            else _queueLabel.SetText(string.Empty);
            RefreshHall();
        }

        void RefreshHall()
        {
            if (_waiting) return; // 대기 중에는 같은 연결로 다른 것을 묻지 않는다(MatchFound를 놓치지 않게)
            var hall = _svc != null ? _svc.Hall() : null;
            if (hall == null || hall.Count == 0) { _hall.SetText(T(SeasonKeys.HallEmpty)); return; }
            var sb = new System.Text.StringBuilder();
            for (int i = hall.Count - 1, shown = 0; i >= 0 && shown < HallRows; i--, shown++)
            {
                var h = hall[i];
                sb.Append(string.IsNullOrEmpty(h.HeirName) ? F(SeasonKeys.HallRowNone, "n", h.SeasonId) : F(SeasonKeys.HallRow, "n", h.SeasonId, "name", h.HeirName, "total", h.Total));
                sb.Append('\n');
            }
            _hall.SetText(sb.ToString());
        }

        void OnOptUp()
        {
            if (_svc == null || _waiting) return;
            var r = _svc.OptUpLeague(out _);
            Say(r == null ? T(OnlineKeys.ErrorServer) : T(SeasonKeys.LeagueResult + r.Value), r == AccountStore.LeagueResult.Ok);
            RefreshSeason();
        }

        void OnQueueJoin()
        {
            if (_svc == null || _waiting) return;
            var r = _svc.QueueJoin();
            if (r == null) { Say(T(OnlineKeys.ErrorServer), false); return; }
            _waiting = r.Value.Waiting;
            if (_waiting) Say(string.Empty, true);
            else Say(F(SeasonKeys.QueueCheck + r.Value.Check, "m", (r.Value.WaitSeconds + 59) / 60), false);
            RefreshSeason();
        }

        void OnQueueLeave()
        {
            if (_svc == null || !_waiting) return;
            // 나가기 직전에 잡혔을 수 있다: 먼저 받은 것을 본다.
            string code = _svc.PollMatch(out _);
            if (code != null) { _waiting = false; Close("join:" + code); return; }
            _svc.QueueLeave();
            _waiting = false;
            RefreshSeason();
        }

        void OnWatch()
        {
            if (_waiting) return;
            string code = RoomServer.Normalize(_watchCode.text);
            if (string.IsNullOrEmpty(code) || code.Length != RoomServer.CodeLength) { Say(T(OnlineKeys.ErrorCode), false); return; }
            Close("watch:" + code);
        }

        // ------------------------------------------------------------ 순위표 탭

        void PickBoard(int pick)
        {
            _boardPick = pick;
            for (int i = 0; i < _boardButtons.Count; i++) _boardButtons[i].Picked = i == pick;
            var kind = Kinds[pick];
            int league = _info != null ? _info.League : 0;
            int arg = kind == BoardKind.Character ? pick - 6 : (kind == BoardKind.RankedLeague || kind == BoardKind.Heir ? league : 0);
            var rows = _svc != null && !_waiting ? _svc.Board(kind, arg) : null;
            int n = rows != null ? rows.Count : 0;
            _boardEmpty.gameObject.SetActive(n == 0);
            for (int i = 0; i < BoardRows; i++)
            {
                if (i >= n) { _boardLines[i].SetText(string.Empty); continue; }
                var b = rows[i];
                string text;
                if (kind == BoardKind.RankedLeague) text = F(SeasonKeys.BoardRowRated, "rank", b.Rank, "name", b.Name, "score", N(b.Score), "w", b.Wave, "l", b.Stage);
                else if (kind == BoardKind.Heir) text = F(SeasonKeys.BoardRowHeir, "rank", b.Rank, "name", b.Name, "score", N(b.Score), "runs", b.Stage, "games", b.Wave);
                else text = F(SeasonKeys.BoardRow, "rank", b.Rank, "name", b.Name, "score", N(b.Score), "wave", b.Wave, "stage", b.Stage);
                _boardLines[i].SetText(text);
            }
        }

        // ------------------------------------------------------------ 우편함 탭

        string MailText(MailItem m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(T(SeasonKeys.MailReason + (m.Reason ?? AccountStore.MailSystem)));
            if (m.Gold > 0) sb.Append("  ·  ").Append(F(SeasonKeys.MailGold, "gold", N(m.Gold)));
            if (m.Items != null)
                foreach (var kv in m.Items) sb.Append("  ·  ").Append(T(kv.Key)).Append(" ×").Append(kv.Value);
            return sb.ToString();
        }

        void RefreshMail()
        {
            _mail = _svc != null && !_waiting ? _svc.Mail() ?? new List<MailItem>() : new List<MailItem>();
            _mailHead.SetText(F(SeasonKeys.MailHead, "n", _mail.Count));
            _mailEmpty.gameObject.SetActive(_mail.Count == 0);
            for (int i = 0; i < _mailRows.Count; i++)
            {
                bool on = i < _mail.Count;
                _mailRows[i].Root.gameObject.SetActive(on);
                if (on) _mailRows[i].Text.SetText(MailText(_mail[i]));
            }
        }

        void ClaimRow(int k)
        {
            if (k < _mail.Count) Claim(_mail[k].Id);
        }

        void Claim(long mailId)
        {
            if (_svc == null || _waiting) return;
            var r = _svc.ClaimMail(mailId);
            Say(r == null ? T(OnlineKeys.ErrorServer) : T(SeasonKeys.MailResult + r.Value), r == AccountStore.MailResult.Ok);
            RefreshMail();
        }

        // ------------------------------------------------------------ 차단·신고 탭

        void RefreshSafety()
        {
            _blocked = _svc != null && !_waiting ? _svc.Blocked() ?? new List<FriendInfo>() : new List<FriendInfo>();
            _blockedHead.SetText(F(SeasonKeys.SafeBlockedHead, "n", _blocked.Count));
            _blockedEmpty.gameObject.SetActive(_blocked.Count == 0);
            for (int i = 0; i < _blockRows.Count; i++)
            {
                bool on = i < _blocked.Count;
                _blockRows[i].Root.gameObject.SetActive(on);
                if (on) _blockRows[i].Name.SetText(_blocked[i].Name);
            }
        }

        void OnBlock()
        {
            if (_svc == null || _waiting) return;
            var r = _svc.Block(_safeName.text);
            Say(r == null ? T(OnlineKeys.ErrorServer) : T(SeasonKeys.BlockResult + r.Value), r == AccountStore.BlockResult.Ok);
            if (r == AccountStore.BlockResult.Ok) _safeName.text = string.Empty;
            RefreshSafety();
        }

        void Unblock(int k)
        {
            if (_svc == null || _waiting || k >= _blocked.Count) return;
            var r = _svc.Unblock(_blocked[k].Id);
            Say(r == null ? T(OnlineKeys.ErrorServer) : T(SeasonKeys.BlockResult + r.Value), r == AccountStore.BlockResult.Ok);
            RefreshSafety();
        }

        void OnReport(int reason)
        {
            if (_svc == null || _waiting) return;
            var r = _svc.Report(_safeName.text, reason);
            Say(r == null ? T(OnlineKeys.ErrorServer) : T(SeasonKeys.ReportResult + r.Value), r == AccountStore.ReportResult.Ok);
        }
    }
}

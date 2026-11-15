using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Net;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 내 온라인 기록(ScreenId.OnlineProfile, 11-online.md 6.8~6.9절, docs/ui-assets.md 4.12·4.25절). 탑 벽(S-2506) 장면 위 세 칸:
    ///  - 왼쪽: 제자 5명(얼굴 + 이름 + 서버 지팡이 단계). 누르면 가운데가 그 지팡이로 바뀐다.
    ///  - 가운데 "강화대": 큰 캐릭터 그림과 단계, 확률 띠(대성공·성공·변이·실패를 색 칸으로), 비용(골드·강화석 아이콘), 떨어짐 안내,
    ///    변이 꼬리표, 제물 칸 5개(K-60)와 서버 창고 격자(블록 아이콘 + 수량), 부적, [조립] [강화]. 변이가 제시되면 쪽지 판에 [받기]/[거절].
    ///  - 오른쪽: 결투 순위 석판(S-2503 줄, 1~3위 메달 I-L8), 내 줄은 아래에 고정.
    /// 서버가 굴린다(OnlineService). 오프라인 프로필과 따로다.
    /// </summary>
    public sealed class OnlineProfileScreen : ScreenBase
    {
        const float RosterX = 48f, RosterY = 112f, RosterW = 352f, RosterRowH = 152f;
        const float AnvilX = 424f, AnvilY = 112f, AnvilW = 928f, AnvilH = 872f;
        const float RankX = 1376f, RankY = 112f, RankW = 496f, RankH = 872f, RankRowH = 56f;
        const int RankRows = 12, StorageCells = 16, SacSlots = 5;

        OnlineService _svc;
        int _char = 1;
        bool _useCharm;
        readonly List<string> _sacrifices = new List<string>();
        readonly List<string> _blockIds = new List<string>();

        UiLabel _gold, _low, _mid, _high, _charms;
        readonly List<UiButton> _roster = new List<UiButton>();
        readonly List<UiLabel> _rosterStage = new List<UiLabel>();
        readonly List<UImage> _rosterFace = new List<UImage>();

        UImage _bigArt, _wandIcon;
        UiLabel _bigName, _bigStage, _oddsLabel, _dropLine, _costGold, _costStone, _mutHead, _sacHead, _storageHead, _storageEmpty, _status;
        RectTransform _oddsBar, _mutTags;
        UImage _segGreat, _segSuccess, _segMut, _segFail;
        readonly List<UImage> _sacIcons = new List<UImage>();
        readonly List<UiButton> _cells = new List<UiButton>();
        readonly List<UImage> _cellIcons = new List<UImage>();
        readonly List<UiLabel> _cellQty = new List<UiLabel>();
        UiButton _charm, _assembly, _enhance, _sacClear;
        RectTransform _offer;
        UiLabel _offerText;
        UiButton _accept, _decline;

        readonly List<RectTransform> _rankRows = new List<RectTransform>();
        readonly List<UImage> _rankBg = new List<UImage>(), _rankMedal = new List<UImage>();
        readonly List<UiLabel> _rankNo = new List<UiLabel>(), _rankName = new List<UiLabel>(), _rankRating = new List<UiLabel>(), _rankWl = new List<UiLabel>();
        UiLabel _rankEmpty, _youName, _youRating;
        UImage _rankEmptyArt;

        public override ScreenId Id => ScreenId.OnlineProfile;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            OnlineUi.Scene(Ctx, content, "S-2506", "tower_wall", 0.5f, "bg_records_hall");
            OnlineUi.HeaderBand(Ctx, content, "I-F1", "record", T(OnlineKeys.ProfileTitle), out var band);
            float hx = 560f;
            _gold = OnlineUi.IconText(Ctx, band, "I-C2", "gold", string.Empty, hx, 0f, 170f, 80f, UiFontId.F24, UiTheme.Gold);
            _low = ItemChip(band, Wizard.Solo.Ids.Item.StoneLow, hx + 180f);
            _mid = ItemChip(band, Wizard.Solo.Ids.Item.StoneMid, hx + 280f);
            _high = ItemChip(band, Wizard.Solo.Ids.Item.StoneHigh, hx + 380f);
            _charms = ItemChip(band, Wizard.Solo.Ids.Item.Charm, hx + 480f);
            float bw = 176f, bx = UiScale.RefWidth - OnlineUi.Edge - 3f * bw - 16f;
            UiKit.Button(Ctx, band, T(OnlineKeys.MarketButton), bx, 16f, bw, 48f, () => OpenChild(ScreenId.OnlineMarket), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "market"));
            UiKit.Button(Ctx, band, T(OnlineKeys.FriendsButton), bx + bw + 8f, 16f, bw, 48f, () => OpenChild(ScreenId.OnlineFriends), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F1", "friends"));
            UiKit.Button(Ctx, band, T(OnlineKeys.Leave), bx + 2f * (bw + 8f), 16f, bw, 48f, () => Close(), UiButtonStyle.Medium, Focus, null, UiFontId.F16, OnlineUi.Sprite(Ctx, "I-F2", "back"));

            BuildRoster(content);
            BuildAnvil(content);
            BuildRanking(content);
        }

        UiLabel ItemChip(RectTransform parent, string itemId, float x)
        {
            UiKit.Icon(Ctx, parent, itemId, 32, x, 24f);
            return UiKit.Label(Ctx, parent, string.Empty, UiFontId.F24, x + 38f, 0f, 56f, 80f, UiTheme.Chalk, TextAnchor.MiddleLeft);
        }

        void BuildRoster(RectTransform content)
        {
            for (int c = 1; c <= Wizard.Solo.Ids.Char.Count; c++)
            {
                int ch = c;
                float y = RosterY + (c - 1) * (RosterRowH + 28f);
                var b = UiKit.Button(Ctx, content, null, RosterX, y, RosterW, RosterRowH, () => PickChar(ch), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.Panel(Ctx, r, UiPanelKind.Board, 0f, 0f, RosterW, RosterRowH).raycastTarget = false;
                string charId = Wizard.Solo.Ids.Char.Of(ch);
                var face = UiKit.Image(r, OnlineUi.Portrait(Ctx, charId), 16f, 12f, 128f, 128f, Color.white, false);
                _rosterFace.Add(face);
                UiKit.Label(Ctx, r, T(charId), UiFontId.F24, 160f, 28f, RosterW - 176f, 36f, UiTheme.Chalk);
                _rosterStage.Add(UiKit.Label(Ctx, r, string.Empty, UiFontId.F48, 160f, 68f, RosterW - 176f, 60f, UiTheme.Chalk));
                _roster.Add(b);
            }
        }

        void BuildAnvil(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Board, AnvilX, AnvilY, AnvilW, AnvilH).rectTransform;
            UiKit.Bevel(panel);
            const float P = 32f;
            // 왼쪽 위: 캐릭터 큰 그림 + 받침 + 지팡이 아이콘.
            OnlineUi.Img(Ctx, panel, "S-0503", "dirt_pedestal", P, 196f, 224f, 67f);
            _bigArt = UiKit.Image(panel, null, P + 16f, 24f, 192f, 192f, Color.white, false);
            _wandIcon = UiKit.Image(panel, null, P + 168f, 152f, 64f, 64f, Color.white, true);
            float tx = P + 256f, tw = AnvilW - tx - P;
            _bigName = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F32, tx, 24f, tw - 200f, 44f, UiTheme.Chalk);
            _bigStage = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F64, tx + tw - 200f, 12f, 200f, 72f, UiTheme.Chalk, TextAnchor.MiddleRight);
            UiKit.DropShadow(_bigStage, 3f);
            // 확률 띠(10000bp = 폭 tw).
            _oddsLabel = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, tx, 84f, tw, 32f, UiTheme.Chalk);
            _oddsBar = UiKit.Panel(Ctx, panel, UiPanelKind.Recess, tx, 120f, tw, 36f).rectTransform;
            _segGreat = UiKit.Rect(_oddsBar, 0f, 4f, 0f, 28f, UiTheme.Gold, false, "Great");
            _segSuccess = UiKit.Rect(_oddsBar, 0f, 4f, 0f, 28f, UiTheme.Good, false, "Success");
            _segMut = UiKit.Rect(_oddsBar, 0f, 4f, 0f, 28f, UiTheme.Mutation, false, "Mutation");
            _segFail = UiKit.Rect(_oddsBar, 0f, 4f, 0f, 28f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.35f), false, "Fail");
            float lx = tx;
            foreach (var (col, key) in new[] { (UiTheme.Gold, OnlineKeys.ProfOddsGreat), (UiTheme.Good, OnlineKeys.ProfOddsSuccess), (UiTheme.Mutation, OnlineKeys.ProfOddsMutation), (UiTheme.WithAlpha(UiTheme.SlateBlue, 0.6f), OnlineKeys.ProfOddsFail) })
            {
                UiKit.Rect(panel, lx, 168f, 12f, 12f, col, false, "Legend");
                string txt = key == OnlineKeys.ProfOddsSuccess ? T(OnlineKeys.ProfOddsSuccess).Replace(" {pct}%", string.Empty) : T(key);
                UiKit.Label(Ctx, panel, txt, UiFontId.F16, lx + 18f, 160f, 110f, 24f, UiTheme.SlateBlue);
                lx += 128f;
            }
            _dropLine = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F16, tx, 192f, tw, 24f, UiTheme.SlateBlue);
            // 비용.
            UiKit.Label(Ctx, panel, T(OnlineKeys.ProfCost), UiFontId.F16, tx, 226f, 80f, 32f, UiTheme.SlateBlue);
            _costGold = OnlineUi.IconText(Ctx, panel, "I-C2", "gold", string.Empty, tx + 80f, 226f, 160f, 32f, UiFontId.F24, UiTheme.Gold);
            _costStone = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, tx + 290f, 226f, 300f, 32f, UiTheme.Chalk);
            _costStoneIcon = UiKit.Image(panel, null, tx + 250f, 226f, 32f, 32f, Color.white, true);

            // 변이 꼬리표.
            _mutHead = UiKit.Label(Ctx, panel, T(OnlineKeys.ProfMutations), UiFontId.F16, P, 288f, 120f, 28f, UiTheme.SlateBlue);
            _mutTags = UiKit.Node(panel, "MutTags", P + 80f, 290f, AnvilW - 2f * P - 80f, 28f);

            // 제물 칸 5개.
            UiKit.Divider(Ctx, panel, P, 332f, AnvilW - 2f * P);
            _sacHead = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, P, 348f, 260f, 36f, UiTheme.Chalk);
            UiKit.Label(Ctx, panel, T(OnlineKeys.ProfSacrificeHint), UiFontId.F16, P, 384f, 480f, 40f, UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            for (int i = 0; i < SacSlots; i++)
            {
                float sx = P + 520f + i * 72f;
                OnlineUi.Img(Ctx, panel, "K-60", "item_slot_empty", sx, 344f, 64f, 64f);
                var ic = UiKit.Image(panel, null, sx + 8f, 352f, 48f, 48f, Color.white, true);
                ic.gameObject.SetActive(false);
                _sacIcons.Add(ic);
            }
            _sacClear = UiKit.IconButton(Ctx, panel, OnlineUi.Sprite(Ctx, "I-F2", "trash"), AnvilW - P - 32f, 360f, 32f, 32f, () => { _sacrifices.Clear(); Refresh(); });

            // 서버 창고 격자(8열 × 2줄).
            _storageHead = UiKit.Label(Ctx, panel, T(OnlineKeys.ProfStorage), UiFontId.F16, P, 428f, 300f, 28f, UiTheme.SlateBlue);
            _storageEmpty = UiKit.Label(Ctx, panel, T(OnlineKeys.ProfStorageEmpty), UiFontId.F16, P, 470f, AnvilW - 2f * P, 28f, UiTheme.SlateBlue);
            for (int i = 0; i < StorageCells; i++)
            {
                int k = i;
                float cx = P + (i % 8) * 108f, cy = 460f + (i / 8) * 96f;
                var b = UiKit.Button(Ctx, panel, null, cx, cy, 96f, 88f, () => AddSacrifice(k), UiButtonStyle.Plain, Focus);
                var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                OnlineUi.Img(Ctx, r, "K-60", "item_slot_filled", 16f, 0f, 64f, 64f);
                var ic = UiKit.Image(r, null, 24f, 8f, 48f, 48f, Color.white, true);
                _cellIcons.Add(ic);
                _cellQty.Add(UiKit.Label(Ctx, r, string.Empty, UiFontId.F12, 0f, 64f, 96f, 22f, UiTheme.Chalk, TextAnchor.MiddleCenter));
                UiKit.AttachTooltip(b.gameObject, () => k < _blockIds.Count ? T(_blockIds[k]) : string.Empty);
                _cells.Add(b);
            }

            // 아래: 부적 · 조립 · 강화.
            float by = AnvilH - P - 64f;
            _charm = UiKit.Button(Ctx, panel, string.Empty, P, by + 8f, 240f, 48f, ToggleCharm, UiButtonStyle.Medium, Focus, null, UiFontId.F16, null);
            _assembly = UiKit.Button(Ctx, panel, T(OnlineKeys.AssemblyButton), P + 256f, by + 8f, 200f, 48f, () => OpenAssembly(_char), UiButtonStyle.Medium, Focus, null, UiFontId.F24, OnlineUi.Sprite(Ctx, "I-F1", "library"));
            _enhance = UiKit.Button(Ctx, panel, T(OnlineKeys.ProfileEnhance), AnvilW - P - 320f, by, 320f, 64f, () => OnEnhance(_char), UiButtonStyle.Primary, Focus, null, UiFontId.F32, OnlineUi.Sprite(Ctx, "I-F1", "enhance"));
            _status = UiKit.Label(Ctx, panel, string.Empty, UiFontId.F24, P, by - 52f, AnvilW - 2f * P, 40f, UiTheme.Chalk, TextAnchor.MiddleCenter);

            // 변이 제시(쪽지 판, 강화 버튼 줄을 덮는다).
            _offer = UiKit.Panel(Ctx, panel, UiPanelKind.Paper, P, by - 72f, AnvilW - 2f * P, 136f).rectTransform;
            _offerText = UiKit.Label(Ctx, _offer, string.Empty, UiFontId.F24, 24f, 16f, AnvilW - 2f * P - 48f, 48f, UiTheme.PaperText, TextAnchor.MiddleLeft, wrap: true);
            _accept = UiKit.Button(Ctx, _offer, T(OnlineKeys.MutationAccept), AnvilW - 2f * P - 24f - 2f * 200f - 12f, 72f, 200f, 48f, () => OnResolve(_char, true), UiButtonStyle.Primary, Focus);
            _decline = UiKit.Button(Ctx, _offer, T(OnlineKeys.MutationDecline), AnvilW - 2f * P - 24f - 200f, 72f, 200f, 48f, () => OnResolve(_char, false), UiButtonStyle.Medium, Focus);
        }

        UImage _costStoneIcon;

        void BuildRanking(RectTransform content)
        {
            var tablet = OnlineUi.OnlineImg(content, "frame_tablet", RankX, RankY, RankW, RankH);
            var panel = (tablet ?? UiKit.Panel(Ctx, content, UiPanelKind.Board, RankX, RankY, RankW, RankH)).rectTransform;
            if (tablet == null) UiKit.Bevel(panel);
            OnlineUi.Img(Ctx, panel, "I-F1", "ranking", 24f, 24f, 32f, 32f);
            UiKit.Label(Ctx, panel, T(OnlineKeys.ProfileTop), UiFontId.F32, 68f, 18f, RankW - 92f, 44f, UiTheme.Chalk);
            for (int i = 0; i < RankRows; i++)
            {
                float y = 76f + i * (RankRowH + 4f);
                var row = UiKit.Node(panel, "Rank" + i, 16f, y, RankW - 32f, RankRowH);
                _rankBg.Add(OnlineUi.Nine(Ctx, row, "S-2503", i < 3 ? "leaderboard_row_top_three" : (i % 2 == 1 ? "leaderboard_row_alternate" : "leaderboard_row"), 0f, 0f, RankW - 32f, RankRowH,
                    i < 3 ? null : (i % 2 == 1 ? null : "normal")));
                _rankMedal.Add(OnlineUi.Img(Ctx, row, "I-L8", "place_" + Mathf.Min(i + 1, 3), 16f, 12f, 32f, 32f));
                _rankNo.Add(UiKit.Label(Ctx, row, (i + 1).ToString(), UiFontId.F24, 12f, 0f, 40f, RankRowH, UiTheme.Chalk, TextAnchor.MiddleCenter));
                _rankName.Add(UiKit.Label(Ctx, row, string.Empty, UiFontId.F24, 64f, 0f, 220f, RankRowH, UiTheme.Chalk));
                _rankRating.Add(UiKit.Label(Ctx, row, string.Empty, UiFontId.F24, 280f, 0f, 80f, RankRowH, UiTheme.Chalk, TextAnchor.MiddleRight));
                _rankWl.Add(UiKit.Label(Ctx, row, string.Empty, UiFontId.F12, 368f, 0f, 92f, RankRowH, UiTheme.SlateBlue, TextAnchor.MiddleRight));
                _rankRows.Add(row);
            }
            _rankEmptyArt = OnlineUi.OnlineImg(panel, "empty_ranking", (RankW - 256f) * 0.5f, 220f, 256f, 256f);
            _rankEmpty = UiKit.Label(Ctx, panel, T(OnlineKeys.ProfUnranked), UiFontId.F24, 32f, 500f, RankW - 64f, 72f, UiTheme.SlateBlue, TextAnchor.MiddleCenter, wrap: true);
            // 내 줄(아래 고정).
            var you = UiKit.Node(panel, "You", 16f, RankH - 16f - RankRowH, RankW - 32f, RankRowH);
            OnlineUi.Nine(Ctx, you, "S-2503", "leaderboard_row_you", 0f, 0f, RankW - 32f, RankRowH);
            UiKit.Label(Ctx, you, T(OnlineKeys.ProfYouRow), UiFontId.F16, 12f, 0f, 80f, RankRowH, UiTheme.SelectYellow);
            _youName = UiKit.Label(Ctx, you, string.Empty, UiFontId.F24, 92f, 0f, 200f, RankRowH, UiTheme.Chalk);
            _youRating = UiKit.Label(Ctx, you, string.Empty, UiFontId.F24, 290f, 0f, 170f, RankRowH, UiTheme.Chalk, TextAnchor.MiddleRight);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _status.SetText(string.Empty);
            _sacrifices.Clear();
            Reconnect();
            LoadRanking();
            Refresh();
            Focus.SetCursor(0, false);
        }

        void Reconnect()
        {
            _svc?.Dispose();
            _svc = OnlineService.Open(Ctx != null ? Ctx.Content : null);
            if (_svc == null) _status.SetText(T(OnlineKeys.ErrorServer));
        }

        void LoadRanking()
        {
            var top = _svc != null ? _svc.Leaderboard() : null;
            int n = top != null ? Mathf.Min(top.Count, RankRows) : 0;
            _rankEmpty.gameObject.SetActive(n == 0);
            if (_rankEmptyArt != null) _rankEmptyArt.gameObject.SetActive(n == 0);
            for (int i = 0; i < RankRows; i++)
            {
                bool on = i < n;
                _rankRows[i].gameObject.SetActive(on);
                if (!on) continue;
                var p = top[i];
                _rankMedal[i].gameObject.SetActive(i < 3);
                _rankNo[i].gameObject.SetActive(i >= 3);
                _rankName[i].SetText(p.Name);
                _rankRating[i].SetText(p.Rating.ToString());
                _rankWl[i].SetText(F(OnlineKeys.ProfWl, "w", p.DuelWins, "l", p.DuelLosses));
            }
        }

        public override void OnExit()
        {
            _svc?.Dispose();
            _svc = null;
            base.OnExit();
        }

        void OpenChild(ScreenId id)
        {
            var args = ScreenArgs.From(ScreenId.OnlineProfile);
            args.OnResult = _ => { Reconnect(); Refresh(); };
            Screens?.Push(id, args);
        }

        void OpenAssembly(int ch)
        {
            var args = ScreenArgs.From(ScreenId.OnlineProfile).With(ScreenArgKeys.Character, ch);
            // 조립 화면이 닫히면 서버 프로필을 다시 받아 보인다(조립은 다른 연결로 저장했다).
            args.OnResult = _ => { Reconnect(); Refresh(); };
            Screens?.Push(ScreenId.OnlineAssembly, args);
        }

        void PickChar(int ch) { _char = ch; _sacrifices.Clear(); _status.SetText(string.Empty); Refresh(); }
        void ToggleCharm() { _useCharm = !_useCharm; Refresh(); }

        int MaxSacrifices => Ctx != null && Ctx.Content != null && Ctx.Content.Enhance != null ? Ctx.Content.Enhance.Sacrifice.MaxBlocks : 0;

        /// <summary>창고에 남은 그 블록 수(고른 제물을 뺀 것).</summary>
        int FreeQty(ServerProfile p, string id)
        {
            int used = 0;
            foreach (var s in _sacrifices) if (s == id) used++;
            return p.Qty(id) - used;
        }

        void AddSacrifice(int cell)
        {
            var p = _svc != null ? _svc.Profile : null;
            if (p == null || cell >= _blockIds.Count || _sacrifices.Count >= MaxSacrifices) return;
            string id = _blockIds[cell];
            if (FreeQty(p, id) <= 0) return;
            _sacrifices.Add(id);
            Refresh();
        }

        Sprite Icon(string id, int size) { try { return Ctx != null && Ctx.Art != null ? Ctx.Art.Icon(id, size) : null; } catch (System.Exception) { return null; } }

        void Refresh()
        {
            var p = _svc != null ? _svc.Profile : null;
            var db = Ctx != null ? Ctx.Content : null;
            var cfg = db != null ? db.Enhance : null;
            int charms = p != null ? p.Qty(Wizard.Solo.Ids.Item.Charm) : 0;
            if (charms <= 0) _useCharm = false;
            _gold.SetText(p != null ? N(p.Gold) : "-");
            _low.SetText(p != null ? p.Qty(Wizard.Solo.Ids.Item.StoneLow).ToString() : "-");
            _mid.SetText(p != null ? p.Qty(Wizard.Solo.Ids.Item.StoneMid).ToString() : "-");
            _high.SetText(p != null ? p.Qty(Wizard.Solo.Ids.Item.StoneHigh).ToString() : "-");
            _charms.SetText(charms.ToString());
            _youName.SetText(p != null ? p.Name : "-");
            _youRating.SetText(p != null ? p.Rating + "  " + F(OnlineKeys.ProfWl, "w", p.DuelWins, "l", p.DuelLosses) : "-");

            for (int i = 0; i < _roster.Count; i++)
            {
                int st = p != null && p.WandStages != null && i < p.WandStages.Length ? p.WandStages[i] : 0;
                _rosterStage[i].SetText(F(OnlineKeys.ProfStage, "stage", st));
                _rosterStage[i].Text.color = cfg != null && st >= cfg.MaxStage ? UiTheme.Gold : UiTheme.Chalk;
                _roster[i].Picked = i + 1 == _char;
                _roster[i].On = i + 1 == _char;
            }

            int c = _char, idx = c - 1;
            string charId = Wizard.Solo.Ids.Char.Of(c);
            int stage = p != null && p.WandStages != null && idx < p.WandStages.Length ? p.WandStages[idx] : 0;
            int aura = p != null && p.Auras != null && idx < p.Auras.Length ? p.Auras[idx] : 0;
            int peak = p != null && p.PeakStages != null && idx < p.PeakStages.Length ? Mathf.Max(stage, p.PeakStages[idx]) : stage;
            bool max = cfg != null && stage >= cfg.MaxStage;
            var def = db != null ? db.Character(charId) : null;
            var bd = cfg != null && !max
                ? EnhanceRules.Breakdown(cfg, stage, peak, aura, EnhanceRules.SacrificeBonus(cfg, db, _sacrifices, def != null ? def.Element : null))
                : default(Wizard.Solo.Meta.ProbBreakdown);

            _bigArt.sprite = OnlineUi.CharacterArt(Ctx, charId);
            Sprite wi = null;
            try { wi = Ctx != null && Ctx.Art != null ? Ctx.Art.WandIcon(charId, stage) : null; } catch (System.Exception) { }
            _wandIcon.sprite = wi;
            _wandIcon.gameObject.SetActive(wi != null);
            _bigName.SetText(T(charId));
            _bigStage.SetText(F(OnlineKeys.ProfStage, "stage", stage));
            _bigStage.Text.color = max ? UiTheme.Gold : UiTheme.Chalk;

            // 확률 띠.
            float w = ((RectTransform)_oddsBar).rect.width > 0f ? ((RectTransform)_oddsBar).rect.width : 600f;
            float inner = w - 8f;
            float g = max ? 0f : bd.GreatBp / 10000f, s = max ? 0f : bd.SuccessBp / 10000f, m = max ? 0f : bd.MutationEndBp / 10000f;
            s = Mathf.Max(s, g);
            m = Mathf.Max(m, s);
            UiKit.Place(_segGreat.rectTransform, 4f, 4f, inner * g, 28f);
            UiKit.Place(_segSuccess.rectTransform, 4f + inner * g, 4f, inner * (s - g), 28f);
            UiKit.Place(_segMut.rectTransform, 4f + inner * s, 4f, inner * (m - s), 28f);
            UiKit.Place(_segFail.rectTransform, 4f + inner * m, 4f, inner * (1f - m), 28f);
            _oddsLabel.SetText(max ? T(OnlineKeys.EnhanceMax) : F(OnlineKeys.ProfOddsSuccess, "pct", bd.SuccessPct));
            _dropLine.SetText(max ? string.Empty : bd.DropPossible && !_useCharm ? F(OnlineKeys.ProfDrop, "floor", bd.FloorStage) : T(OnlineKeys.ProfNoDrop));
            _dropLine.Text.color = bd.DropPossible && !_useCharm ? UiTheme.Amber : UiTheme.SlateBlue;
            _costGold.SetText(max ? "-" : N(bd.CostGold));
            _costGold.Text.color = p != null && p.Gold >= bd.CostGold ? UiTheme.Gold : UiTheme.Amber;
            bool hasStones = p != null && (bd.StoneId == null || p.Qty(bd.StoneId) >= bd.StoneCount);
            _costStoneIcon.sprite = bd.StoneId != null ? Icon(bd.StoneId, 32) : null;
            _costStoneIcon.gameObject.SetActive(!max && bd.StoneId != null && _costStoneIcon.sprite != null);
            _costStone.SetText(!max && bd.StoneId != null ? "×" + bd.StoneCount + "  (" + (p != null ? p.Qty(bd.StoneId) : 0) + ")" : string.Empty);
            _costStone.Text.color = hasStones ? UiTheme.Chalk : UiTheme.Amber;

            // 변이 꼬리표.
            for (int i = _mutTags.childCount - 1; i >= 0; i--) Object.Destroy(_mutTags.GetChild(i).gameObject);
            float tx = 0f;
            var muts = p != null ? p.MutationsOf(c) : null;
            if (muts == null || muts.Count == 0) UiKit.Label(Ctx, _mutTags, T(OnlineKeys.ProfNoMutations), UiFontId.F16, 0f, 0f, 300f, 28f, UiTheme.SlateBlue);
            else foreach (var mu in muts) { var lab = OnlineUi.Tag(Ctx, _mutTags, T(mu.Id), tx, 2f, UiTheme.Mutation); tx += lab.Rect.rect.width + 8f; }

            // 제물.
            _sacHead.SetText(F(OnlineKeys.ProfSacrificeHead, "n", _sacrifices.Count, "max", MaxSacrifices));
            for (int i = 0; i < _sacIcons.Count; i++)
            {
                bool on = i < _sacrifices.Count && i < MaxSacrifices;
                _sacIcons[i].gameObject.SetActive(on);
                if (on) _sacIcons[i].sprite = Icon(_sacrifices[i], 48);
            }
            _sacClear.Enabled = _sacrifices.Count > 0;

            _blockIds.Clear();
            if (p != null && p.Items != null && db != null)
                foreach (var kv in p.Items) if (kv.Value > 0 && db.Block(kv.Key) != null) _blockIds.Add(kv.Key);
            _blockIds.Sort(System.StringComparer.Ordinal);
            _storageEmpty.gameObject.SetActive(_blockIds.Count == 0);
            for (int i = 0; i < _cells.Count; i++)
            {
                bool on = i < _blockIds.Count;
                _cells[i].gameObject.SetActive(on);
                if (!on) continue;
                string id = _blockIds[i];
                _cellIcons[i].sprite = Icon(id, 48);
                _cellQty[i].SetText(T(id) + " ×" + FreeQty(p, id));
                _cells[i].Enabled = FreeQty(p, id) > 0 && _sacrifices.Count < MaxSacrifices;
            }

            // 부적·버튼·변이 제시.
            _charm.SetText(T(_useCharm ? OnlineKeys.CharmOn : OnlineKeys.CharmOff) + "  (" + charms + ")");
            _charm.On = _useCharm;
            _charm.Enabled = charms > 0;
            var offer = p != null ? p.OfferOf(c) : null;
            _offer.gameObject.SetActive(offer != null);
            if (offer != null)
                _offerText.SetText(F(OnlineKeys.MutationOfferLine, "who", T(charId), "m", T(offer.Candidate))
                    + (offer.WouldPushOut != null ? "  " + F(OnlineKeys.MutationPush, "m", T(offer.WouldPushOut)) : string.Empty));
            _enhance.gameObject.SetActive(offer == null);
            _enhance.Enabled = p != null && !max && p.Gold >= bd.CostGold && hasStones;
            _assembly.Enabled = p != null;
        }

        void OnEnhance(int character)
        {
            if (_svc == null) return;
            _char = character;
            var rep = _svc.Enhance(character, _useCharm, _sacrifices);
            Show(rep, character);
            if (rep != null && rep.Value.Result == AccountStore.EnhanceResult.Ok) _sacrifices.Clear();
            Refresh();
        }

        void OnResolve(int character, bool accept)
        {
            if (_svc == null) return;
            Show(_svc.ResolveMutation(character, accept), character);
            Refresh();
        }

        void Show(OnlineService.EnhanceReply? rep, int character)
        {
            _status.Text.color = UiTheme.Amber;
            if (rep == null) { _status.SetText(T(OnlineKeys.ErrorServer)); return; }
            var r = rep.Value;
            if (r.Result == AccountStore.EnhanceResult.NotEnoughGold) _status.SetText(T(OnlineKeys.EnhanceGold));
            else if (r.Result == AccountStore.EnhanceResult.NotEnoughStones) _status.SetText(T(OnlineKeys.EnhanceStones));
            else if (r.Result == AccountStore.EnhanceResult.BadSacrifice) _status.SetText(T(OnlineKeys.SacrificeBad));
            else if (r.Result != AccountStore.EnhanceResult.Ok) _status.SetText(T(OnlineKeys.ErrorServer));
            else
            {
                int after = r.Profile.WandStages[character - 1];
                _status.Text.color = after > r.StageBefore ? UiTheme.Good : UiTheme.Chalk;
                _status.SetText("+" + r.StageBefore + " → +" + after + "   " + T(OnlineKeys.Outcome + r.Outcome));
                var pop = _bigStage.GetComponent<UiPop>() ?? _bigStage.gameObject.AddComponent<UiPop>();
                if (after > r.StageBefore) pop.Pop(1.25f, 0.35f); else pop.Shake(4f, 0.3f);
            }
        }
    }
}

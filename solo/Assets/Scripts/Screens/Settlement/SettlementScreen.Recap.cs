using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Records;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.UI;
using Wizard.Solo.View.Stats;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Settlement
{
    // 판 끝 요약("이번 판 돌아보기", 1쪽 왼쪽 칸)과 축하 연출(최고 기록·새 해금 — 공통 그림 success_burst, level_wave_pillar, shockwave).
    // 규칙은 RunRecap(엔진 없음). 여기는 그리기만 한다.
    public sealed partial class SettlementScreen
    {
        // 판 끝 요약 칸(내용 좌표): 격자 왼쪽 0~5열, 머리 밑(BodyY)부터. 높이는 내용에 맞춘다(최대 RecapH).
        // 속성으로 둔다: 다른 부분 파일의 정적 값(LX·HalfW)은 초기화 순서가 정해져 있지 않다.
        static float RecapX => LX;
        static float RecapW => HalfW;
        const float RecapY = BodyY, RecapH = 688f;
        const float TileGap = 8f, TileH = 104f, MasterPx = 96f, CharPx = 72f;
        static float TileW => (HalfW - 2f * UiLayout.SurfacePad - 2f * TileGap) / 3f;
        RectTransform _recapBg;

        readonly RunStatsCollector _collector;
        readonly RecapTuning _recapTuning;
        RectTransform _recapBox, _fxRoot;
        readonly List<GameObject> _recapObjects = new List<GameObject>();
        readonly List<GameObject> _artObjects = new List<GameObject>();
        EnhanceFxPlayer _fx;
        float _fxClock;
        float _celebrateAt = -1f;
        bool _recordCelebrated, _takeCelebrated;

        public SettlementScreen() : this(null, null) { }

        public SettlementScreen(RunStatsCollector collector, RecapTuning tuning)
        {
            _collector = collector;
            _recapTuning = tuning;
        }

        void BuildRecapBox(RectTransform page)
        {
            _recapBox = UiKit.Node(page, "RunRecap", RecapX, RecapY, RecapW, RecapH);
            _recapBox.name = "RunRecap";
            _recapBox.gameObject.SetActive(false);
        }

        void BuildFxRoot(RectTransform content)
        {
            _fxRoot = UiKit.Node(content, "CelebrationFx", 0f, 0f, UiScale.RefWidth, ContentHeight);
            // 축하 그림은 쪽(점수 판·가져가는 것 줄) 뒤, 배경 위에 둔다: 폭발·빛기둥이 줄의 글자를 덮어 읽지 못하게 되지 않게.
            if (_pages[0] != null) _fxRoot.SetSiblingIndex(_pages[0].GetSiblingIndex());
            else _fxRoot.SetAsLastSibling();
            _fx = new EnhanceFxPlayer(_fxRoot);
        }

        /// <summary>판 끝 요약을 만든다(OnEnter, Begin 뒤 한 번).</summary>
        void PrepareRecap(Wizard.Solo.Sim.RunSummary summary)
        {
            RunStats stats = _collector != null && summary != null ? _collector.For(summary.RunId) : null;
            _model.BuildRecap(stats, _recapTuning ?? RecapTuning.From(Ctx != null ? Ctx.Content : null));
            _model.AfterCommit = SaveHighlights;
            _recordCelebrated = _takeCelebrated = false;
        }

        /// <summary>가방 확정 뒤: 판 끝 요약의 개인 최고(한 방·스침 연속·콤보)를 캐릭터 기록에 적는다(보여 주기만 하는 값).</summary>
        void SaveHighlights()
        {
            RunStats st = _model.Stats;
            if (st == null || Ctx == null || Ctx.Profiles == null || Ctx.Profile == null) return;
            CharacterRecord rec = Ctx.Profile.Character(_model.CharacterNumber);
            if (RunHighlightBook.Apply(rec, st.MaxHitDamage, st.MaxGrazeChain, st.MaxCombo)) Ctx.Profiles.Save();
        }

        // ------------------------------------------------------------ 1쪽 왼쪽: 이번 판 돌아보기

        void FillRecap()
        {
            ClearList(_recapObjects);
            RunRecap r = _model.Recap;
            bool show = _showRecordDetails && r != null && _recapBox != null;
            if (_recapBox != null) _recapBox.gameObject.SetActive(show);
            for (int i = 0; i < _artObjects.Count; i++) if (_artObjects[i] != null) _artObjects[i].SetActive(!show);
            if (!show) return;

            INameTable names = Ctx != null ? Ctx.Names : null;
            RectTransform box = _recapBox;
            float pad = UiLayout.SurfacePad, w = RecapW - pad * 2f;
            float lh16 = UiTheme.LineHeight(UiFontId.F16), lh24 = UiTheme.LineHeight(UiFontId.F24), lh32 = UiTheme.LineHeight(UiFontId.F32);
            float y = pad;

            // 머리: 제목 + 오른쪽 위 캐릭터 얼굴.
            Add(UiKit.Label(Ctx, box, T(RunRecap.TitleKey), UiFontId.F16, pad, y, w - CharPx, lh16, UiTheme.Brass));
            string charId = _model.Summary != null ? _model.Summary.CharId : null;
            Sprite face = Ctx != null && Ctx.Art != null && !string.IsNullOrEmpty(charId) ? Ctx.Art.CharacterSelection(charId) : null;
            if (face != null) Add(UiKit.Image(box, face, RecapW - pad - CharPx, pad - 8f, CharPx, CharPx));
            y += lh16 + 12f;

            // 사인.
            var cause = UiKit.Label(Ctx, box, r.CauseText(names), UiFontId.F32, pad, y, w - CharPx * 0.5f, lh32, UiTheme.Chalk);
            UiKit.DropShadow(cause, 2f);
            Add(cause);
            UiMotion.Entrance(cause, 0.05f, UiTheme.PanelPopSeconds, new Vector2(-24f, 0f), 1f);
            y += lh32 + 4f;
            if (r.HasCause)
            {
                float tagW = w * 0.75f, tagH = lh16 + 8f;
                Color tc = r.CauseTelegraphed ? UiTheme.Amber : UiTheme.SlateBlue;
                Add(UiKit.Rect(box, pad, y, tagW, tagH, UiTheme.WithAlpha(tc, 0.18f)));
                Add(UiKit.Rect(box, pad, y, 4f, tagH, tc));
                Add(UiKit.Label(Ctx, box, T(r.CauseTagKey), UiFontId.F16, pad + 12f, y + 4f, tagW - 16f, lh16, tc));
                y += tagH + 8f;
            }
            if (r.HasStats)
            {
                Add(UiKit.Label(Ctx, box, F(RunRecap.CauseHitsKey, "n", r.HitsTaken), UiFontId.F24, pad, y, w, lh24, UiTheme.Chalk));
                y += lh24;
                string most = r.MostHitText(names);
                if (!string.IsNullOrEmpty(most)) Add(UiKit.Label(Ctx, box, most, UiFontId.F24, pad, y, w, lh24, UiTheme.Chalk));
                y += lh24 + 8f;
            }
            Add(UiKit.Rect(box, pad, y, w, 1f, UiTheme.WithAlpha(UiTheme.Brass, 0.5f), false, "RecapRule"));
            y += 16f;

            // 이번 판 최고 순간(3 × 2 칸).
            Add(UiKit.Label(Ctx, box, T(RunRecap.HighlightTitleKey), UiFontId.F16, pad, y, w, lh16, UiTheme.Gold));
            y += lh16 + 6f;
            int highlights = 0;
            foreach (var highlight in r.Highlights)
            {
                if (highlight.Unknown || (highlight.IsWave && !highlight.NewBest)) continue;
                float tx = pad + (highlights % 3) * (TileW + TileGap);
                float ty = y + (highlights / 3) * (TileH + TileGap);
                Tile(box, highlight, tx, ty, 0.25f + highlights * 0.06f);
                highlights++;
            }
            y += Mathf.CeilToInt(highlights / 3f) * (TileH + TileGap) + 4f;

            // 다음 판에 해 볼 것.
            Add(UiKit.Label(Ctx, box, T(RunRecap.TipTitleKey), UiFontId.F16, pad, y, w, lh16, UiTheme.Good));
            y += lh16 + 2f;
            var tip = UiKit.Label(Ctx, box, r.TipText(names), UiFontId.F24, pad, y, w, lh24 * 2f, UiTheme.Chalk, TextAnchor.UpperLeft, true);
            Add(tip);
            y += lh24 * 2f + 8f;

            // 스승의 한마디(사인·성과별 몇 갈래).
            Sprite master = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "master_peek");
            // 스승 줄은 내용 바로 밑에 붙이고, 판은 그 높이에 맞춘다(아래 빈 칸을 남기지 않는다).
            float my = y + 4f;
            float boxH = Mathf.Min(RecapH, UiLayout.Snap(my + MasterPx + pad));
            UiKit.Place(_recapBox, RecapX, RecapY, RecapW, boxH);
            Backing(ref _recapBg, _recapBox, RecapW, boxH, 1);
            if (master != null) Add(UiKit.Image(box, master, pad, my, MasterPx, MasterPx));
            else Add(UiLayout.Surface(Ctx, box, pad, my, MasterPx, MasterPx, 0));
            float qx = pad + MasterPx + 16f, qw = RecapW - qx - pad;
            Add(UiLayout.Surface(Ctx, box, qx, my, qw, MasterPx, 0));
            Add(UiKit.Label(Ctx, box, T(RunRecap.MasterNameKey), UiFontId.F16, qx + 12f, my + 6f, qw - 24f, lh16, UiTheme.SlateBlue));
            var quote = UiKit.Label(Ctx, box, T(r.MasterLineKey), UiFontId.F24, qx + 12f, my + 8f + lh16, qw - 24f, MasterPx - lh16 - 14f,
                UiTheme.Paper, TextAnchor.UpperLeft, true);
            Add(quote);
            UiMotion.Entrance(quote, 0.9f, UiTheme.PanelPopSeconds, new Vector2(0f, 12f), 1f);
        }

        void Tile(RectTransform box, RecapHighlight h, float x, float y, float delay)
        {
            var root = UiKit.Node(box, "Highlight", x, y, TileW, TileH);
            Add(root.gameObject);
            UiKit.Rect(root, 10f, 0f, TileW - 20f, h.NewBest ? 2f : 1f,
                h.NewBest ? UiTheme.Gold : UiTheme.WithAlpha(UiTheme.SlateBlue, 0.4f), false, "HighlightRule");
            float lh16 = UiTheme.LineHeight(UiFontId.F16), lh32 = UiTheme.LineHeight(UiFontId.F32);
            float labelW = h.NewBest ? (TileW - 20f) * 0.6f : TileW - 20f;
            UiKit.Label(Ctx, root, T(h.LabelKey), UiFontId.F16, 10f, 6f, labelW, lh16, UiTheme.SlateBlue);
            if (h.NewBest)
                UiKit.Label(Ctx, root, T(RunRecap.HighlightNewKey), UiFontId.F16, 10f + labelW, 6f, TileW - 20f - labelW, lh16, UiTheme.Gold, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(h.IconId))
            {
                const int IconPx = 40;
                UiKit.Icon(Ctx, root, h.IconId, IconPx, 10f, 8f + lh16 + 6f);
                UiKit.Label(Ctx, root, T(h.IconId), UiFontId.F16, 10f + IconPx + 8f, 8f + lh16 + 4f, TileW - IconPx - 28f, UiTheme.LineHeight(UiFontId.F16), UiTheme.Chalk);
                UiKit.Label(Ctx, root, F(RunRecap.HighlightShotsKey, "n", N(h.Value)), UiFontId.F16, 10f + IconPx + 8f, 8f + lh16 + 28f, TileW - IconPx - 28f, lh16, UiTheme.SlateBlue);
            }
            else
            {
                Color c = h.NewBest ? UiTheme.Gold : UiTheme.Chalk;
                var v = UiKit.Label(Ctx, root, string.Empty, UiFontId.F32, 10f, 8f + lh16 + 4f, TileW - 20f, lh32, c, TextAnchor.MiddleCenter);
                if (h.IsWave)
                    v.SetText(F(RunRecap.WaveFormatKey, "n", h.Value));
                else UiCountUp.Run(v, h.Value, N, delay, UiTheme.CountUpSeconds * 0.8f, 0, h.NewBest);
                UiKit.DropShadow(v, 2f);
            }
            UiMotion.Entrance(root, delay, UiTheme.PanelPopSeconds, new Vector2(0f, 16f), 1f);
        }

        void Add(Component c) { if (c != null) _recapObjects.Add(c.gameObject); }
        void Add(GameObject g) { if (g != null) _recapObjects.Add(g); }

        static void ClearList(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null) Object.Destroy(list[i]);
            list.Clear();
        }

        // ------------------------------------------------------------ 3쪽: 새로 이룬 도전 과제

        void ChallengeRows()
        {
            List<string> ids = _model.NewChallenges;
            if (ids.Count == 0) return;
            List<ChallengeDef> defs = ChallengeBook.Load(Ctx != null ? Ctx.Content : null);
            var lines = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                ChallengeDef d = ChallengeBook.Find(defs, ids[i]);
                string name = T(ChallengeBook.NameKey(ids[i]));
                string reward = d != null ? ChallengeText.Reward(Ctx != null ? Ctx.Names : null, d) : string.Empty;
                lines.Add(F(SettlementNameKeys.TakeChallengeLine, "name", name, "reward", reward));
            }
            ListRow(T(SettlementNameKeys.TakeChallenges), lines, UiTheme.Gold);
        }

        // ------------------------------------------------------------ 축하 연출

        int EffectsLevel => Ctx != null && Ctx.Settings != null ? SettingsOptions.EffectsLevel(Ctx.Settings.Current) : 0;

        /// <summary>쪽을 보일 때: 그 쪽의 축하를 한 번 예약한다(숫자가 다 오른 뒤 터진다).</summary>
        void ScheduleCelebration()
        {
            if (_model.Page == SettlementPage.Record && !_recordCelebrated && _model.CelebrateRecord)
            {
                _recordCelebrated = true;
                _celebrateAt = _fxClock + 0.15f + _model.ScoreRows.FindAll(ShowScoreRow).Count * RowRevealSeconds + UiTheme.CountUpSeconds * 1.4f;
            }
            else if (_model.Page == SettlementPage.Takeaways && !_takeCelebrated && _model.CelebrateTakeaways)
            {
                _takeCelebrated = true;
                _celebrateAt = _fxClock + UiTheme.PanelPopSeconds;
            }
        }

        void TickCelebration(float dt)
        {
            _fxClock += dt;
            if (_celebrateAt >= 0f && _fxClock >= _celebrateAt)
            {
                _celebrateAt = -1f;
                Celebrate();
            }
            _fx?.Update(dt);
        }

        /// <summary>큰 축하: 가운데 성공 폭발 + 충격파 고리, 양옆 빛기둥. 연출 감도 최소면 하지 않는다(줄임이면 폭발만 작게). 화면 전체 번쩍임은 쓰지 않는다.</summary>
        void Celebrate()
        {
            if (_fx == null || _fxRoot == null) return;
            int level = EffectsLevel;
            if (level >= 2) return;
            IArtLibrary art = Ctx != null ? Ctx.Art : null;
            bool record = _model.Page == SettlementPage.Record;
            float cx = record ? ScoreX + ScoreW * 0.5f : UiScale.RefWidth * 0.5f;
            float cy = record ? ScoreY + ScoreH * 0.72f : TakeY + 140f;
            float bs = level == 1 ? 384f : 640f;
            Color gold = Color.Lerp(UiTheme.Gold, Color.white, 0.35f);
            _fx.Play(EnhanceCommonArt.Anim(EnhanceCommonArt.Enh(art, EnhanceCommonArt.SuccessBurst), EnhanceCommonArt.BurstFps, false, "success_burst"),
                cx - bs * 0.5f, cy - bs * 0.5f, bs, bs, gold);
            if (level == 1) return;
            _fx.Play(EnhanceCommonArt.Anim(EnhanceCommonArt.Combat(art, EnhanceCommonArt.Shockwave), EnhanceCommonArt.ShockFps, false, "shockwave"),
                cx - 256f, cy - 256f, 512f, 512f, gold);
            var pillar = EnhanceCommonArt.Anim(EnhanceCommonArt.Combat(art, EnhanceCommonArt.Pillar), EnhanceCommonArt.PillarFps, false, "pillar");
            if (pillar == null) pillar = EnhanceCommonArt.Anim(EnhanceCommonArt.Enh(art, EnhanceCommonArt.Pillar), EnhanceCommonArt.PillarFps, false, "pillar");
            float pw = 96f, ph = 384f;
            float left = record ? ScoreX - pw * 0.5f : TakeX - pw;
            float right = record ? ScoreX + ScoreW - pw * 0.5f : TakeX + TakeW;
            float top = Mathf.Max(0f, cy - ph * 0.5f);
            _fx.Play(pillar, left, top, pw, ph, gold, pillar != null ? pillar.Duration * 2f : 0f);
            _fx.Play(pillar, right, top, pw, ph, gold, pillar != null ? pillar.Duration * 2f : 0f);
        }
    }
}

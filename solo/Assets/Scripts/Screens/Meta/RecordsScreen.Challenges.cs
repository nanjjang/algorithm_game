using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Records;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    // 기록 화면의 "도전 과제" 탭(WP4): 다섯 제자 모두 + 제자 1~5, 여섯 세로 칸. 칸마다 과제 카드(이름, 설명, 진행 막대, 보상, 이룸 표시).
    // 진행 값은 ChallengeBook(캐릭터 기록에서 계산)만 쓴다. 보상은 선택지·표시 해금(칭호, 꾸미기, 서고 칸)뿐이다.
    public sealed partial class RecordsScreen
    {
        const float BoardX = 48f, BoardY = 168f - TopBarHeight, BoardW = 1824f, BoardH = 760f;
        const float ColGap = 12f, ColHeadH = 40f, CardH = 108f, CardGap = 10f;

        RectTransform _listBoard, _challengeBoard;

        void BuildChallengeBoard(RectTransform content)
        {
            _challengeBoard = UiKit.Node(content, "ChallengeBoard", BoardX, BoardY, BoardW, BoardH);
            _challengeBoard.gameObject.SetActive(false);
        }

        void FillChallenges(ProfileData p)
        {
            MetaUi.ClearChildren(_challengeBoard);
            var defs = ChallengeBook.Load(Ctx != null ? Ctx.Content : null);
            int cols = 1 + Ids.Char.Count;
            float colW = (BoardW - ColGap * (cols - 1)) / cols;
            for (int c = 0; c < cols; c++)
            {
                float x = c * (colW + ColGap);
                var list = ChallengeBook.For(defs, c);
                int done = 0;
                for (int i = 0; i < list.Count; i++) if (ChallengeBook.Progress(p, list[i]).Done) done++;
                var col = UiKit.Node(_challengeBoard, "Col" + c, x, 0f, colW, BoardH);
                UiKit.Plate(Ctx, col, UiPanelKind.Heading, 0f, 0f, colW, ColHeadH);
                float lh24 = UiTheme.LineHeight(UiFontId.F24), lh16 = UiTheme.LineHeight(UiFontId.F16);
                string head = c == 0 ? T(MetaKeys.RecordsChallengeAll) : T(MetaKeys.Character(c));
                UiKit.Label(Ctx, col, head, UiFontId.F24, 12f, (ColHeadH - lh24) * 0.5f, colW * 0.6f, lh24, UiTheme.Chalk);
                UiKit.Label(Ctx, col, F(MetaKeys.RecordsChallengeDoneCount, "n", done, "max", list.Count), UiFontId.F16, colW * 0.5f,
                    (ColHeadH - lh16) * 0.5f, colW * 0.5f - 12f, lh16, done == list.Count && list.Count > 0 ? UiTheme.Gold : UiTheme.SlateBlue,
                    TextAnchor.MiddleRight);
                for (int i = 0; i < list.Count; i++)
                    ChallengeCard(col, ChallengeBook.Progress(p, list[i]), 0f, ColHeadH + CardGap + i * (CardH + CardGap), colW, i);
            }
        }

        void ChallengeCard(RectTransform parent, ChallengeProgress pr, float x, float y, float w, int index)
        {
            var names = Ctx != null ? Ctx.Names : null;
            var root = UiKit.Node(parent, "Challenge." + pr.Def.Id, x, y, w, CardH);
            UiKit.Panel(Ctx, root, pr.Done ? UiPanelKind.Board : UiPanelKind.Recess, 0f, 0f, w, CardH);
            if (pr.Done) UiKit.Rect(root, 0f, 0f, 4f, CardH, UiTheme.Gold);
            float pad = 12f, lh16 = UiTheme.LineHeight(UiFontId.F16), lh12 = UiTheme.LineHeight(UiFontId.F12);
            float iw = w - pad * 2f;
            UiKit.Label(Ctx, root, T(ChallengeBook.NameKey(pr.Def.Id)), UiFontId.F16, pad, 8f, iw, lh16, pr.Done ? UiTheme.Gold : UiTheme.Chalk);
            UiKit.Label(Ctx, root, ChallengeText.Describe(names, pr.Def), UiFontId.F12, pad, 8f + lh16 + 2f, iw, lh12, UiTheme.SlateBlue);
            float by = 8f + lh16 + lh12 + 10f;
            UiKit.ProgressBar(Ctx, root, pad, by + (lh12 - 8f) * 0.5f, iw * 0.55f, 8f, pr.Ratio);
            UiKit.Label(Ctx, root, ChallengeText.ProgressText(names, pr), UiFontId.F12, pad + iw * 0.58f, by, iw * 0.42f, lh12,
                pr.Done ? UiTheme.Good : UiTheme.Chalk, TextAnchor.MiddleRight);
            UiKit.Label(Ctx, root, ChallengeText.Reward(names, pr.Def), UiFontId.F12, pad, by + lh12 + 8f, iw, lh12,
                pr.Done ? UiTheme.Good : UiTheme.Paper);
            UiMotion.Entrance(root, 0.02f * index, UiTheme.PanelPopSeconds, new Vector2(0f, 12f), 1f);
        }
    }
}

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    /// <summary>튜토리얼 단계의 종류(DataSolo/Tutorial.json steps의 id). 화면 글 열쇠 str.tutorial.gate.&lt;id&gt;.*.</summary>
    public static class TutorialStepIds
    {
        public const string Intro = "intro", Move = "move", Fire = "fire", Overheat = "overheat", Crit = "crit", Graze = "graze",
            Combo = "combo", Art = "art", Stance = "stance", Reverse = "reverse", Command = "command", Block = "block";
    }

    /// <summary>
    /// 튜토리얼 진행 알림. Run이 PracticeReport 이벤트(Id = 아래 상수)로 낸다 — 표시·화면 전용(규칙 연결에 쓰지 않는다).
    /// Progress: A = 단계, B = 지금 진척, F = 목표. Setback: A = 단계, B = TutorialSetback.
    /// </summary>
    public static class TutorialReportKeys
    {
        public const string Progress = "tutorial.progress";
        public const string Setback = "tutorial.setback";
    }

    /// <summary>관문에서 되돌아간 까닭(화면이 바로 힌트를 띄운다).</summary>
    public enum TutorialSetback
    {
        None = 0,
        /// <summary>임계 관문에서 열이 넘쳐 잠겼다(꾹 눌렀다) — 진척 0.</summary>
        Overflow = 1,
        /// <summary>스침 관문에서 탄에 맞았다 — 그 스침은 세지 않는다.</summary>
        Hit = 2,
        /// <summary>결정이 모자라 K가 불발했다.</summary>
        Fizzle = 3
    }

    /// <summary>
    /// 튜토리얼 판의 단계 표와 관문 수치(DataSolo/Tutorial.json). 시뮬레이션(Run의 완료 판정)과 화면(풍선)이 같은 표를 읽는다.
    /// 실전 규칙은 바꾸지 않는다: 이 표는 연습 방(RunPhase.Practice, Mode = Tutorial)에서만 쓴다.
    /// </summary>
    public sealed class TutorialPlan
    {
        public const string ExtraName = "Tutorial";

        public string[] Steps = new string[0];
        public Vector2 MoveTargetOffset;
        public float MoveTargetRadius;
        public int FireHits;
        public int CritExtraShots;
        public int GrazeGoal;
        public float GrazeConfirmSeconds;
        public float StanceCells;
        public int ReverseShots;
        public int CommandGrantCrystals;
        public string CommandArtId;
        public int IntroLines;
        public float AdvanceDelaySeconds;
        public float HintAfterSeconds;
        public float SetbackHintSeconds;
        public float PraiseSeconds;
        public float BalloonX, BalloonY, BalloonW, BalloonH;
        public float DialogX, DialogY, DialogW, DialogH;

        public int Count => Steps.Length;

        public static TutorialPlan From(IContentDb db)
        {
            var t = new TutorialPlan();
            JObject jo = db != null ? db.SoloExtra(ExtraName) : null;
            if (jo == null) return t;
            var arr = jo["steps"] as JArray;
            if (arr != null)
            {
                var list = new List<string>();
                foreach (var s in arr) if (s != null && s.Type == JTokenType.String) list.Add((string)s);
                t.Steps = list.ToArray();
            }
            t.MoveTargetOffset = new Vector2(F(jo, "move_target_offset_x"), F(jo, "move_target_offset_y"));
            t.MoveTargetRadius = F(jo, "move_target_radius");
            t.FireHits = (int)F(jo, "fire_hits");
            t.CritExtraShots = (int)F(jo, "crit_extra_shots");
            t.GrazeGoal = (int)F(jo, "graze_goal");
            t.GrazeConfirmSeconds = F(jo, "graze_confirm_seconds");
            t.StanceCells = F(jo, "stance_cells");
            t.ReverseShots = (int)F(jo, "reverse_shots");
            t.CommandGrantCrystals = (int)F(jo, "command_grant_crystals");
            t.CommandArtId = jo["command_art_id"] != null ? (string)jo["command_art_id"] : null;
            t.IntroLines = (int)F(jo, "intro_lines");
            t.AdvanceDelaySeconds = F(jo, "advance_delay_seconds");
            t.HintAfterSeconds = F(jo, "hint_after_seconds");
            t.SetbackHintSeconds = F(jo, "setback_hint_seconds");
            t.PraiseSeconds = F(jo, "praise_seconds");
            var b = jo["balloon"] as JObject;
            if (b != null) { t.BalloonX = F(b, "x"); t.BalloonY = F(b, "y"); t.BalloonW = F(b, "w"); t.BalloonH = F(b, "h"); }
            var d = jo["dialog"] as JObject;
            if (d != null) { t.DialogX = F(d, "x"); t.DialogY = F(d, "y"); t.DialogW = F(d, "w"); t.DialogH = F(d, "h"); }
            return t;
        }

        static float F(JObject jo, string key)
        {
            JToken v = jo[key];
            return v == null || (v.Type != JTokenType.Integer && v.Type != JTokenType.Float) ? 0f : v.Value<float>();
        }

        /// <summary>단계 번호(1부터)의 id. 범위 밖이면 null.</summary>
        public string IdOf(int step) => step >= 1 && step <= Steps.Length ? Steps[step - 1] : null;

        /// <summary>id의 단계 번호(1부터). 없으면 0.</summary>
        public int StepOf(string id)
        {
            for (int i = 0; i < Steps.Length; i++) if (Steps[i] == id) return i + 1;
            return 0;
        }

        /// <summary>이동 관문의 목표 지점(시뮬레이션 좌표, 칸). 전장 가운데 + 오프셋을 전장 안으로 자른다.</summary>
        public Vector2 MoveTarget(SimWorld w)
        {
            float aw = w != null ? w.ArenaWidth : 0f, ah = w != null ? w.ArenaHeight : 0f;
            var c = new Vector2(aw * 0.5f, ah * 0.5f) + MoveTargetOffset;
            float m = Mathf.Max(MoveTargetRadius, 1f);
            c.x = Mathf.Clamp(c.x, m, Mathf.Max(m, aw - m));
            c.y = Mathf.Clamp(c.y, m, Mathf.Max(m, ah - m));
            return c;
        }

        /// <summary>열 한 번이 차는 동안 꾹 눌러 나오는 "잠기지 않는 임계 사격" 수(02 6.6절 임계 사격 가능 횟수와 같은 식).</summary>
        public static int CritShotsPerFill(PlayerState p)
        {
            if (p == null || p.Spell == null) return 0;
            int shot = p.Spell.HeatPerShotHu;
            int crit = p.Derived.CritStartHu;
            if (shot <= 0) return 0;
            int span = Player.PlayerTuning.HeatMaxHu - crit;
            if (span < 0) return 0;
            return span / shot + 1;
        }

        /// <summary>임계 관문의 목표: 꾹 눌러 나오는 수 + 덧. 꾹 누르면 넘쳐 진척이 0이 되므로 끊어 쏘아야만 닿는다.</summary>
        public int CritGoal(PlayerState p) => Mathf.Max(1, CritShotsPerFill(p) + CritExtraShots);

        /// <summary>단계의 목표 수(진척 막대의 분모). 0이면 막대 없음.</summary>
        public int GoalOf(string id, PlayerState p)
        {
            switch (id)
            {
                case TutorialStepIds.Fire: return FireHits;
                case TutorialStepIds.Crit: return CritGoal(p);
                case TutorialStepIds.Graze: return GrazeGoal;
                case TutorialStepIds.Reverse: return ReverseShots;
                case TutorialStepIds.Combo: return p != null && p.Def != null ? p.Def.ComboPerCrystal : 0;
                default: return 0;
            }
        }
    }
}

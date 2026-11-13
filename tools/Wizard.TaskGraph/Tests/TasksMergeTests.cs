using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // FIX-7, DECISIONS-TECH T-65 9번: tasks.json 의 구조 병합. 세 판을 직접 주는 순수 함수 시험이다.
    public sealed class TasksMergeTests
    {
        private static JsonObject BaseDoc()
        {
            return Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "tools/" }),
                Fx.Task("M2-A-01", milestone: "M2", group: "M2-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "src/a/" }, vectors: new[] { "S01:V01", "S01:V02" }),
                Fx.Task("M2-A-02", milestone: "M2", group: "M2-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "src/b/" }),
                Fx.Task("M2-A-03", milestone: "M2", group: "M2-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "src/c/" }));
        }

        private static string Text(JsonObject d) => Fx.Text(d);

        private static string Edit(string text, Action<JsonObject> f)
        {
            var d = (JsonObject)JsonNode.Parse(text)!;
            f(d);
            return Text(d);
        }

        private static JsonObject Card(JsonObject d, string id) => ((JsonArray)d["tasks"]!).OfType<JsonObject>().Single(t => (string)t["id"]! == id);

        private static JsonArray Arr(JsonObject card, string key) => (JsonArray)card[key]!;

        // taskgraph add 와 같은 길로 카드를 더한다(마일스톤 집계도 그쪽에서 고친다: 두 브랜치가 각각 14 → 15 로 고치는 꼴)
        private static string Add(string text, string id, string group, string milestone = "M2")
        {
            JsonObject t = Fx.Task(id, milestone: milestone, group: group, deps: new[] { "M0-BT-01" }, paths: new[] { "fix/" + id + "/", Ledger.TasksPath });
            AddResult r = TaskAdder.Prepare(text, "t", t.ToJsonString(), Fx.SchemaPath, null);
            Assert.True(r.Report.Ok, string.Join("\n", r.Report.Errors));
            return r.NewText;
        }

        private static string Merged(string b, string m, string t)
        {
            TasksMerge.Outcome o = TasksMerge.Merge(b, m, t);
            Assert.True(o.Ok, string.Join("\n", o.Conflicts));
            string text = o.Text!;
            Assert.Equal(TaskDoc.Parse(text, "merged").Serialize(), text);   // 파일의 꼴 그대로
            return text;
        }

        private static IReadOnlyList<string> Conflicts(string b, string m, string t)
        {
            TasksMerge.Outcome o = TasksMerge.Merge(b, m, t);
            Assert.False(o.Ok);
            Assert.Null(o.Text);
            return o.Conflicts;
        }

        // 카드 하나의 줄 묶음(그 글자 그대로)
        private static string CardBlock(string text, string id)
        {
            var lines = text.Split('\n').ToList();
            int at = lines.FindIndex(l => l == $"   \"id\": \"{id}\",") - 1;
            int end = lines.FindIndex(at, l => l == "  }," || l == "  }");
            return string.Join("\n", lines.Skip(at).Take(end - at + 1));
        }

        private static string[] Ids(string text) => TaskDoc.Parse(text, "x").Tasks.Select(t => t.Id).ToArray();

        private static JsonObject Milestone(string text, string id) =>
            ((JsonArray)TaskDoc.Parse(text, "x").Root["milestones"]!).OfType<JsonObject>().Single(m => (string)m["id"]! == id);

        private static void AssertUntouched(string baseText, string merged, params string[] ids)
        {
            foreach (string id in ids)
            {
                Assert.Contains(CardBlock(baseText, id), merged);
            }
        }

        // ---------- 풀리는 경우 ----------

        [Fact]
        public void A_BothAddDifferentCardsToTheSameMilestone_KeepsBoth_AndRecounts()
        {
            string b = Text(BaseDoc());
            string m = Add(b, "FIX-1", "M2-W11");
            string t = Add(b, "FIX-2", "M2-W12");
            Assert.Equal(4, (int)Milestone(m, "M2")["task_count"]!);
            Assert.Equal(4, (int)Milestone(t, "M2")["task_count"]!);   // 둘 다 3 → 4 (git 이면 충돌 없이 4)
            string r = Merged(b, m, t);
            Assert.Equal(new[] { "M0-BT-01", "M2-A-01", "M2-A-02", "M2-A-03", "FIX-1", "FIX-2" }, Ids(r));
            JsonObject m2 = Milestone(r, "M2");
            Assert.Equal(5, (int)m2["task_count"]!);
            Assert.Equal("M2-W01,M2-W02,M2-W11,M2-W12", string.Join(",", ((JsonArray)m2["parallel_groups"]!).Select(x => (string)x!)));
            AssertUntouched(b, r, "M0-BT-01", "M2-A-01", "M2-A-02", "M2-A-03");
            Assert.Contains(CardBlock(m, "FIX-1"), r);
            Assert.True(GraphValidator.Validate(TaskDoc.Parse(r, "r"), Fx.SchemaPath, null).Ok);
        }

        [Fact]
        public void B_BothEditDifferentCards()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => Card(d, "M2-A-01")["title"] = "main 이 고친 제목");
            string t = Edit(b, d => Card(d, "M2-A-02")["module"] = "sim");
            string r = Merged(b, m, t);
            TaskDoc doc = TaskDoc.Parse(r, "r");
            Assert.Equal("main 이 고친 제목", doc.Get("M2-A-01").Title);
            Assert.Equal("sim", doc.Get("M2-A-02").Module);
            AssertUntouched(b, r, "M0-BT-01", "M2-A-03");
        }

        [Fact]
        public void C_SameCard_OneAddsOwnedPath_OtherAddsAcceptance()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => Arr(Card(d, "M2-A-01"), "owned_paths").Add(Ledger.DecisionsPath));
            string t = Edit(b, d => Arr(Card(d, "M2-A-01"), "acceptance").Add("옮기는 쪽의 새 수락 조건"));
            TaskDef c = TaskDoc.Parse(Merged(b, m, t), "r").Get("M2-A-01");
            Assert.Equal(new[] { "src/a/", Ledger.DecisionsPath }, c.OwnedPaths.ToArray());
            Assert.Equal(new[] { "M2-A-01 의 수락 조건", "옮기는 쪽의 새 수락 조건" }, c.Acceptance.ToArray());
        }

        [Fact]
        public void D_SameListField_DifferentItemsBothKeptMainFirst_SameItemOnce()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => { Arr(Card(d, "M2-A-02"), "acceptance").Add("main 의 것"); Arr(Card(d, "M2-A-03"), "spec_refs").Add("docs/dev/PIPELINE.md §3.8"); });
            string t = Edit(b, d => { Arr(Card(d, "M2-A-02"), "acceptance").Add("옮기는 쪽의 것"); Arr(Card(d, "M2-A-03"), "spec_refs").Add("docs/dev/PIPELINE.md §3.8"); });
            TaskDoc doc = TaskDoc.Parse(Merged(b, m, t), "r");
            Assert.Equal(new[] { "M2-A-02 의 수락 조건", "main 의 것", "옮기는 쪽의 것" }, doc.Get("M2-A-02").Acceptance.ToArray());
            Assert.Equal(new[] { "docs/dev/PIPELINE.md §3", "docs/dev/PIPELINE.md §3.8" }, doc.Get("M2-A-03").SpecRefs.ToArray());
        }

        [Fact]
        public void E_OneSideMovesAVectorSelectorBetweenCards_OtherSideUntouched()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => Card(d, "M0-BT-01")["title"] = "다른 카드만 고쳤다");
            string t = Edit(b, d => { Arr(Card(d, "M2-A-01"), "vectors").RemoveAt(1); Arr(Card(d, "M2-A-02"), "vectors").Add("S01:V02"); });
            TaskDoc doc = TaskDoc.Parse(Merged(b, m, t), "r");
            Assert.Equal(new[] { "S01:V01" }, doc.Get("M2-A-01").Vectors.Select(v => v.Value).ToArray());
            Assert.Equal(new[] { "S01:V02" }, doc.Get("M2-A-02").Vectors.Select(v => v.Value).ToArray());
        }

        [Fact]
        public void F_OneSideChangesParallelGroup_OtherAddsCard_GroupsRecounted()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d =>
            {
                Card(d, "M2-A-03")["parallel_group"] = "M2-W07";
                Recount.Apply(d);
            });
            string t = Add(b, "FIX-2", "M2-W12");
            string r = Merged(b, m, t);
            JsonObject m2 = Milestone(r, "M2");
            Assert.Equal("M2-W01,M2-W02,M2-W07,M2-W12", string.Join(",", ((JsonArray)m2["parallel_groups"]!).Select(x => (string)x!)));
            Assert.Equal(4, (int)m2["task_count"]!);
            Assert.True(GraphValidator.Validate(TaskDoc.Parse(r, "r"), Fx.SchemaPath, null).Ok);

            // parallel_groups 의 같은 줄을 양쪽이 고쳤어도(git 이면 충돌) 다시 센다
            string t2 = Edit(b, d => { Card(d, "M2-A-02")["parallel_group"] = "M2-W08"; Recount.Apply(d); });
            JsonObject m2b = Milestone(Merged(b, m, t2), "M2");
            Assert.Equal("M2-W01,M2-W07,M2-W08", string.Join(",", ((JsonArray)m2b["parallel_groups"]!).Select(x => (string)x!)));   // M2-W02 는 비었다
        }

        [Fact]
        public void G_BothSidesSameValue()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => Card(d, "M2-A-01")["reviewer_model"] = "fable");
            string t = Edit(b, d => Card(d, "M2-A-01")["reviewer_model"] = "fable");
            Assert.Equal("fable", TaskDoc.Parse(Merged(b, m, t), "r").Get("M2-A-01").ReviewerModel);
            Assert.Equal(m, Merged(b, m, t));
        }

        [Fact]
        public void H_OneSideRewritesANotesLine_OtherAddsAgentOrHumanCheck()
        {
            JsonObject bd = BaseDoc();
            ((JsonArray)bd["notes"]!).Add("둘째 줄");
            string b = Text(bd);
            string m = Edit(b, d => ((JsonArray)d["notes"]!)[0] = "고쳐 적은 첫 줄");
            string t = Edit(b, d =>
            {
                ((JsonArray)d["agents"]!).Add(new JsonObject { ["name"] = "tech-lead", ["default_model"] = "fable", ["default_reviewer_model"] = "fable" });
                Card(d, "M2-A-02")["human_check"] = "사람이 본다";
            });
            JsonObject r = TaskDoc.Parse(Merged(b, m, t), "r").Root;
            Assert.Equal(new[] { "고쳐 적은 첫 줄", "둘째 줄" }, ((JsonArray)r["notes"]!).Select(x => (string)x!).ToArray());
            Assert.Equal(3, ((JsonArray)r["agents"]!).Count);
            Assert.Equal("사람이 본다", TaskDoc.Parse(Merged(b, m, t), "r").Get("M2-A-02").HumanCheck);

            // 뒤집어도(main 이 더하고 옮기는 쪽이 고쳐 적었다) 같다
            JsonObject r2 = TaskDoc.Parse(Merged(b, t, m), "r").Root;
            Assert.Equal(new[] { "고쳐 적은 첫 줄", "둘째 줄" }.OrderBy(x => x, StringComparer.Ordinal), ((JsonArray)r2["notes"]!).Select(x => (string)x!).OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void MovingSideCardGoesAfterItsPredecessor_AfterMainAdditionsThere()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => ((JsonArray)d["tasks"]!).Insert(2, Fx.Task("M2-M-01", milestone: "M2", group: "M2-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "src/m/" })));
            string t = Edit(b, d => ((JsonArray)d["tasks"]!).Insert(2, Fx.Task("M2-T-01", milestone: "M2", group: "M2-W01", deps: new[] { "M0-BT-01" }, paths: new[] { "src/t/" })));
            Assert.Equal(new[] { "M0-BT-01", "M2-A-01", "M2-M-01", "M2-T-01", "M2-A-02", "M2-A-03" }, Ids(Merged(b, m, t)));
        }

        // ---------- 진짜 충돌(풀지 않는다) ----------

        [Theory]
        [InlineData("title", "main 제목", "옮기는 쪽 제목")]
        [InlineData("parallel_group", "M2-W05", "M2-W06")]
        public void X_A_SameValueFieldChangedDifferently(string key, string mv, string tv)
        {
            string b = Text(BaseDoc());
            var c = Conflicts(b, Edit(b, d => Card(d, "M2-A-01")[key] = mv), Edit(b, d => Card(d, "M2-A-01")[key] = tv));
            Assert.Single(c);
            Assert.StartsWith($"tasks.json: 카드 M2-A-01의 '{key}'를 양쪽이 다르게 고쳤다", c[0]);
        }

        [Fact]
        public void X_B_SameIdAddedWithDifferentContent()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => ((JsonArray)d["tasks"]!).Add(Fx.Task("FIX-9", milestone: "M2", group: "M2-W11", paths: new[] { "fix/a/" })));
            string t = Edit(b, d => ((JsonArray)d["tasks"]!).Add(Fx.Task("FIX-9", milestone: "M2", group: "M2-W12", paths: new[] { "fix/b/" })));
            Assert.Equal(new[] { "tasks.json: 카드 FIX-9을(를) 양쪽이 같은 id로 다르게 더했다(번호 겹침)" }, Conflicts(b, m, t).ToArray());
        }

        [Fact]
        public void X_C_DeletedOnOneSide_EditedOnTheOther()
        {
            string b = Text(BaseDoc());
            string del = Edit(b, d => ((JsonArray)d["tasks"]!).Remove(Card(d, "M2-A-03")));
            string ed = Edit(b, d => Arr(Card(d, "M2-A-03"), "acceptance").Add("더한 조건"));
            Assert.Equal(new[] { "tasks.json: 카드 M2-A-03을(를) 옮기는 쪽이 지웠는데 main 쪽이 고쳤다" }, Conflicts(b, ed, del).ToArray());
            Assert.Equal(new[] { "tasks.json: 카드 M2-A-03을(를) main 쪽이 지웠는데 옮기는 쪽이 고쳤다" }, Conflicts(b, del, ed).ToArray());
        }

        [Fact]
        public void X_D_SameAcceptanceItemRewrittenDifferently()
        {
            string b = Text(BaseDoc());
            string m = Edit(b, d => Card(d, "M2-A-01")["acceptance"] = Fx.Arr(new[] { "main 이 고쳐 쓴 조건" }));
            string t = Edit(b, d => Card(d, "M2-A-01")["acceptance"] = Fx.Arr(new[] { "옮기는 쪽이 고쳐 쓴 조건" }));
            var c = Conflicts(b, m, t);
            Assert.Single(c);
            Assert.StartsWith("tasks.json: 카드 M2-A-01의 'acceptance'에서 양쪽이 같은 항목을 빼고 서로 다른 항목을 더했다", c[0]);
            Assert.Contains("M2-A-01 의 수락 조건", c[0]);
        }

        [Fact]
        public void X_E_AgentModelOrMilestoneTitleOrGateChangedDifferently()
        {
            string b = Text(BaseDoc());
            JsonObject Agent(JsonObject d) => ((JsonArray)d["agents"]!).OfType<JsonObject>().First();
            JsonObject Ms(JsonObject d) => ((JsonArray)d["milestones"]!).OfType<JsonObject>().Single(x => (string)x["id"]! == "M2");
            Assert.StartsWith("tasks.json: 에이전트 build-tools-engineer의 'default_model'를 양쪽이 다르게 고쳤다",
                Conflicts(b, Edit(b, d => Agent(d)["default_model"] = "fable"), Edit(b, d => Agent(d)["default_model"] = "sonnet")).Single());
            Assert.StartsWith("tasks.json: 마일스톤 M2의 'title'를 양쪽이 다르게 고쳤다",
                Conflicts(b, Edit(b, d => Ms(d)["title"] = "가"), Edit(b, d => Ms(d)["title"] = "나")).Single());
            Assert.StartsWith("tasks.json: 마일스톤 M2의 'gate'를 양쪽이 다르게 고쳤다",
                Conflicts(b, Edit(b, d => Ms(d)["gate"] = "M2-A-01"), Edit(b, d => Ms(d)["gate"] = "M2-A-02")).Single());
        }

        // (바) 구조로는 합쳐지지만 합친 그래프가 validate 에 실패한다(LedgerResolver 가 스키마·그래프·벡터를 다 본다)
        private static LedgerResolver.Result Resolve(string b, string m, string t, string? specsDir, MergeHistory? history = null)
        {
            var conflicted = new Dictionary<string, LedgerResolver.Stages> { [Ledger.TasksPath] = new LedgerResolver.Stages(b, m, t) };
            using TempRepo work = TempRepo.Create(git: false);
            return LedgerResolver.Resolve(conflicted, rel => rel == Ledger.TasksPath ? null : "", Fx.SchemaPath, specsDir,
                _ => history ?? MergeHistory.NotRead(), work.Dir);
        }

        [Fact]
        public void X_F_MergedGraphFailsValidation()
        {
            using TempRepo specs = TempRepo.Create(git: false);
            specs.Write("01-test.md", "# 명세\n\n## 5. 테스트 벡터\n\n### 5.1 묶음\n\n| ID | 내용 |\n|---|---|\n| V01 | 하나 |\n| V02 | 둘 |\n| V03 | 셋 |\n");
            JsonObject bd = BaseDoc();
            Card(bd, "M0-BT-01")["vectors"] = Fx.Arr(new[] { "S01:V03" });
            string b = Text(bd);
            Assert.True(Resolve(b, b, b, specs.Dir).Ok);

            // 같은 명세 벡터를 양쪽이 서로 다른 카드에 배정했다
            string m = Edit(b, d => Arr(Card(d, "M2-A-02"), "vectors").Add("S01:V03"));
            string t = Edit(b, d => Arr(Card(d, "M2-A-03"), "vectors").Add("S01:V03"));
            Assert.True(TasksMerge.Merge(b, m, t).Ok);   // 구조로는 합쳐진다
            LedgerResolver.Result r = Resolve(b, m, t, specs.Dir);
            Assert.False(r.Ok);
            Assert.Empty(r.Resolved);
            Assert.Contains(r.Conflicts, c => c.StartsWith("docs/dev/tasks.json: 합친 그래프가 validate 에 실패한다 — 벡터 S01:V03(§5.1)가 두 작업에 배정되어 있다", StringComparison.Ordinal));
            // --no-vectors 는 벡터만 뺀다(이 경우는 통과)
            Assert.True(Resolve(b, m, t, null).Ok);

            // deps 가 순환한다
            m = Edit(b, d => Arr(Card(d, "M2-A-02"), "deps").Add("M2-A-03"));
            t = Edit(b, d => Arr(Card(d, "M2-A-03"), "deps").Add("M2-A-02"));
            Assert.Contains(Resolve(b, m, t, null).Conflicts, c => c.Contains("deps에 순환이 있다", StringComparison.Ordinal));

            // 병합 전인 두 카드의 경로가 겹친다(한쪽이 병합됐으면 통과)
            m = Edit(b, d => Arr(Card(d, "M2-A-02"), "owned_paths").Add("src/shared/"));
            t = Edit(b, d => Arr(Card(d, "M2-A-03"), "owned_paths").Add("src/shared/x.cs"));
            Assert.Contains(Resolve(b, m, t, null).Conflicts, c => c.Contains("M2-A-02와 M2-A-03의 owned_paths가 겹친다", StringComparison.Ordinal));
            Assert.True(Resolve(b, m, t, null, MergeHistory.Of("main", new[] { "M2-A-02" })).Ok);

            // 스키마도 본다
            m = Edit(b, d => Card(d, "M2-A-02")["owner_model"] = "gpt");
            Assert.Contains(Resolve(b, m, b, null).Conflicts, c => c.Contains("합친 그래프가 validate 에 실패한다", StringComparison.Ordinal));
        }
    }
}

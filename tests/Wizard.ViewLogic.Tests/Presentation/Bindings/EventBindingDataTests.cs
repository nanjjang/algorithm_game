using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Wizard.Sim.Events;
using Wizard.ViewLogic.Presentation;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Bindings
{
    // 저장소의 data/presentation/event_bindings.json·event_bindings.none.json 을 수락 조건으로 검사한다(CONTRACTS 13절)
    public sealed class EventBindingDataTests
    {
        // effects.md 20절 비고: 그림의 주인이 다른 문서라 이 절이 장수를 세지 않는 줄
        private const string NotCountedHere = "이 표에서는 세지 않는다";

        [Fact]
        public void RealFilesLoadWithoutErrors()
        {
            EventBindingTable t = RepoFiles.LoadRealTable();
            Assert.NotEmpty(t.Bindings);
            Assert.NotEmpty(t.NoneKeys);
        }

        [Fact]
        public void EverySimEventTypeAndUiKeyIsBoundOrIntentionallyNone()
        {
            // 수락 1: 빠짐 검사, 두 카탈로그에 같은 열쇠 없음, 지어낸 열쇠 없음
            List<string> problems = EventBindingCoverage.FindProblems(RepoFiles.LoadRealTable());
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void CoverageCountsMatchBothCatalogs()
        {
            EventBindingTable t = RepoFiles.LoadRealTable();
            int sim = EventBindingCoverage.AllSimEventTypes().Count;
            int ui = UiEventKeys.All.Count;
            Assert.Equal(sim + ui, t.Bindings.Count + t.NoneKeys.Count);
        }

        [Fact]
        public void ContractExampleLineIsAsWritten()
        {
            // CONTRACTS 13절의 한 줄 그대로
            EventBindingTable t = RepoFiles.LoadRealTable();
            Assert.True(t.TryGet(SimEventDocs.BindingKey(SimEventType.HeatLockEntered), out EventBinding? b));
            Assert.Equal(new[] { "fx_lock_x", "fx_lock_steam" }, b!.Fx);
            Assert.Equal(new[] { "sfx_c07" }, b.Sfx);
            Assert.Equal(BindingShake.None, b.Shake);
            Assert.Equal(0, b.HitstopMs);
            Assert.True(b.ConfirmedOnly);
        }

        [Fact]
        public void GrazeChainWindowAndStepsAreData()
        {
            // 수락 6: 2초와 5단은 바인딩 데이터에 있다(spec 01 17.2 chainIndex 0~5, effects.md 11.1)
            EventBindingTable t = RepoFiles.LoadRealTable();
            Assert.Equal(2 * Wizard.Sim.SimVersion.TickRate, t.GrazeChain.WindowTicks);
            Assert.Equal(5, t.GrazeChain.MaxStep);
            string raw = RepoFiles.Read(EventBindingTable.BindingsFile);
            Assert.Matches(new Regex("\"graze_chain\": \\{ \"window_seconds\": 2(\\.0)?, \"max_step\": 5 \\}"), raw);
        }

        [Fact]
        public void IntentionallyNoneListIsSmallAndReasoned()
        {
            // "의도적 없음"은 연출이 정말 없는 것만이다. 에셋이 아직 없는 사건은 빈 fx·sfx 의 줄(자리)로 둔다
            EventBindingTable t = RepoFiles.LoadRealTable();
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "not_an_event", "debug_only", "spec_no_presentation", "phase_of_core_event", "camera_output" };
            Assert.All(t.NoneKeys, k => Assert.Contains(t.NoneReason(k)!, allowed));
            Assert.Contains(SimEventDocs.BindingKey(SimEventType.None), t.NoneKeys);
            Assert.Contains(SimEventDocs.BindingKey(SimEventType.DebugCommandRejected), t.NoneKeys);
            Assert.Contains(SimEventDocs.BindingKey(SimEventType.CrystalGainBlocked), t.NoneKeys);   // spec 01 17.2 "(연출 없음, 디버그)"
            Assert.True(t.NoneKeys.Count <= 16, "의도적 없음이 너무 많다: " + t.NoneKeys.Count);
        }

        [Fact]
        public void Spec10ConfirmRequiredRowsAreConfirmedOnly()
        {
            // spec 10 23.1~23.6의 "확정 필요" 칸이 "예"인 사건은 줄이 있고 confirmed_only 다(소리·피격 연출은 확정에서만. E 17, O 4.11).
            // "문양: C1, 소리: 예"(duel.art_activated)는 문양을 예측에서 그리므로 confirmed_only 가 아니다(소리는 라우터가 늘 확정에서만 낸다)
            EventBindingTable t = RepoFiles.LoadRealTable();
            string[] lines = RepoFiles.Lines("docs/spec/10-duel.md");
            int start = Array.FindIndex(lines, l => l.StartsWith("## 23.", StringComparison.Ordinal));
            int checkedRows = 0;
            for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("### 23.7", StringComparison.Ordinal); i++)
            {
                if (!lines[i].StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }
                string[] cells = lines[i].Split('|').Skip(1).Select(c => c.Trim()).ToArray();
                if (cells.Length < 4)
                {
                    continue;
                }
                foreach (Match m in Regex.Matches(cells[0], "`([^`]+)`"))
                {
                    string key = m.Groups[1].Value;
                    if (t.IsIntentionallyNone(key))
                    {
                        continue;                                // duel.hit_confirmed 등: 코어 이벤트의 단계로 그린다
                    }
                    Assert.True(t.TryGet(key, out EventBinding? b), key + " 에 바인딩 줄이 없다");
                    if (cells[3] == "예")
                    {
                        Assert.True(b!.ConfirmedOnly, key + " 은 확정 필요인데 confirmed_only 가 아니다");
                        checkedRows++;
                    }
                    if (key == "duel.art_activated")
                    {
                        Assert.False(b!.ConfirmedOnly);
                    }
                }
            }
            Assert.True(checkedRows >= 25, "확정 필요 줄을 너무 적게 읽었다: " + checkedRows);
        }

        [Fact]
        public void DuelPredictedHitIsDrawnByCoreHitRowsBeforeConfirmation()
        {
            // spec 10 23.3: duel.hit_predicted(적중 불꽃, 피해 숫자)는 확정 필요가 아니고 duel.hit_rollback 은 0.1초 흐려짐이다.
            // 둘은 "의도적 없음"(phase_of_core_event)이라 코어 적중 줄이 Predicted 에서 그려야 한다(effects.md 17절, CONTRACTS 6.4).
            // 피격 연출(duel.hit_confirmed → player.hit)만 확정 필요다
            EventBindingTable t = RepoFiles.LoadRealTable();
            Assert.Equal("phase_of_core_event", t.NoneReason("duel.hit_predicted"));
            Assert.Equal("phase_of_core_event", t.NoneReason("duel.hit_rollback"));

            Assert.True(t.TryGet(SimEventDocs.BindingKey(SimEventType.ProjectileHit), out EventBinding? projectile));
            Assert.False(projectile!.ConfirmedOnly);
            Assert.Contains("fx_element_hit", projectile.Fx);        // 적중 불꽃
            Assert.Contains("ui_damage_digits", projectile.Fx);      // 피해 숫자(spec 02 projectile.hit 연결점)

            Assert.True(t.TryGet(SimEventDocs.BindingKey(SimEventType.HitEnemy), out EventBinding? enemy));
            Assert.False(enemy!.ConfirmedOnly);
            Assert.Contains("fx_element_hit", enemy.Fx);

            Assert.True(t.TryGet(SimEventDocs.BindingKey(SimEventType.PlayerHit), out EventBinding? player));
            Assert.True(player!.ConfirmedOnly);

            // 적중 불꽃을 그리는 줄은 모두 예측에서 그린다(확정에서만 그리는 줄이 불꽃을 갖고 있으면 예측 적중이 빠진다)
            Assert.All(t.Bindings.Where(b => b.Fx.Contains("fx_element_hit")), b => Assert.False(b.ConfirmedOnly, b.EventKey));
        }

        [Fact]
        public void SfxCatalogIsExactlyEffects19()
        {
            // 수락 2: 소리 식별자는 effects.md 19절의 사건 번호(E-01 → sfx_e01, T-09a → sfx_t09a, "T-01 ~ T-08" → t01~t08)
            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string row in EffectsTableRows("## 19.", "## 20."))
            {
                string first = row.Split('|')[1].Trim();
                Match range = Regex.Match(first, "^([ECTHN])-(\\d{2}) ~ ([ECTHN])-(\\d{2})$");
                if (range.Success)
                {
                    for (int n = int.Parse(range.Groups[2].Value); n <= int.Parse(range.Groups[4].Value); n++)
                    {
                        expected.Add("sfx_" + range.Groups[1].Value.ToLowerInvariant() + n.ToString("00"));
                    }
                    continue;
                }
                Match one = Regex.Match(first, "^([ECTHN])-(\\d{2}[a-c]?)$");
                if (one.Success)
                {
                    expected.Add("sfx_" + one.Groups[1].Value.ToLowerInvariant() + one.Groups[2].Value);
                }
            }
            Assert.True(expected.Count > 100, "19절에서 번호를 너무 적게 읽었다: " + expected.Count);
            EventBindingTable t = RepoFiles.LoadRealTable();
            Assert.Equal(expected, new SortedSet<string>(t.SfxCatalog, StringComparer.Ordinal));
        }

        [Fact]
        public void FxCatalogPointsAtEffects20RowsAndCoversThem()
        {
            // 수락 2: 그림 식별자는 effects.md 20절의 표 줄에 붙인 이름이다. src 가 가리키는 줄이 있어야 하고,
            // 이 절이 장수를 세는 줄(비고에 "이 표에서는 세지 않는다"가 없는 줄)은 모두 식별자가 있다
            Dictionary<string, string> rows = Effects20Rows();
            EventBindingTable t = RepoFiles.LoadRealTable();
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (FxCatalogEntry e in t.FxCatalog)
            {
                Assert.True(rows.ContainsKey(e.Src), e.Fx + ": effects.md 에 " + e.Src + " 줄이 없다");
                Assert.DoesNotContain(NotCountedHere, rows[e.Src]);
                covered.Add(e.Src);
            }
            foreach (KeyValuePair<string, string> r in rows)
            {
                if (r.Value.Contains(NotCountedHere, StringComparison.Ordinal))
                {
                    continue;                                    // 그림의 주인이 다른 문서다
                }
                Assert.True(covered.Contains(r.Key), "effects.md " + r.Key + " (" + r.Value.Split('|')[1].Trim() + ") 에 fx 식별자가 없다");
            }
        }

        [Fact]
        public void Spec01AssetIdsAreInTheCatalogAtTheirRows()
        {
            // spec 01 17.2 끝의 에셋 식별자(effects.md 20.3절의 이름을 따른다)가 카탈로그에 그 줄로 있다
            EventBindingTable t = RepoFiles.LoadRealTable();
            Dictionary<string, string> rows = Effects20Rows();
            var expect = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fx_heat_ring"] = "열 링", ["fx_crit_wedge_ring"] = "임계 링의 도는 쐐기", ["fx_brink_triangle"] = "넘치기 직전 경고 삼각",
                ["fx_lock_x"] = "잠금 X", ["fx_lock_steam"] = "잠금 김", ["fx_ring_shard"] = "링 깨짐 조각", ["fx_crit_muzzle"] = "임계 총구 고리",
                ["fx_crit_wedge"] = "임계 쐐기", ["fx_crit_hit"] = "임계 적중 불꽃", ["fx_boilover_arcs"] = "끓어넘침 증기 호",
                ["fx_graze_ring"] = "스침 고리", ["fx_afterimage"] = "잔상", ["fx_lightning_rod"] = "피뢰침 벼락 줄기",
                ["ui_combo_digits"] = "콤보 숫자 글꼴", ["ui_crystal_empty"] = "결정(", ["ui_crystal_full"] = "결정(", ["ui_crystal_debt"] = "결정(",
                ["ui_crystal_crack"] = "결정(", ["fx_crystal_break"] = "결정 깨짐", ["fx_crystal_shard"] = "결정 조각", ["fx_frost_shield"] = "서리 방패",
                ["fx_starfall_fall"] = "별똥", ["fx_starfall_rest"] = "별똥", ["ui_hp_cell_full"] = "체력 칸", ["ui_hp_cell_empty"] = "체력 칸",
                ["ui_hp_cell_break"] = "체력 칸", ["fx_hit_edge"] = "피격 가장자리", ["fx_invuln_outline"] = "무적 테두리",
            };
            Dictionary<string, string> srcOf = t.FxCatalog.ToDictionary(e => e.Fx, e => e.Src, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kv in expect)
            {
                Assert.True(srcOf.TryGetValue(kv.Key, out string? src), kv.Key + " 이 fx_catalog 에 없다");
                Assert.StartsWith("20.3#", src);
                Assert.StartsWith(kv.Value, rows[src!].Split('|')[1].Trim());
            }
        }

        [Fact]
        public void SoundsFromLinkTablesAreBound()
        {
            // 연결점 표가 소리 번호를 적은 사건 몇 개(spec 01 17.2, spec 13 21.5)
            EventBindingTable t = RepoFiles.LoadRealTable();
            AssertSfx(t, SimEventDocs.BindingKey(SimEventType.GrazeAwarded), "sfx_c10");
            AssertSfx(t, SimEventDocs.BindingKey(SimEventType.CrystalGained), "sfx_c13");
            AssertSfx(t, SimEventDocs.BindingKey(SimEventType.BoiloverTriggered), "sfx_c20");
            AssertSfx(t, UiEventKeys.UiEnhanceOpen, "sfx_e01");
            AssertSfx(t, UiEventKeys.UiEnhanceWandDrop, "sfx_e15");
            AssertSfx(t, UiEventKeys.NoticeEnhancePlus1012, "sfx_e34");
        }

        private static void AssertSfx(EventBindingTable t, string key, string sfx)
        {
            Assert.True(t.TryGet(key, out EventBinding? b), key);
            Assert.Contains(sfx, b!.Sfx);
        }

        // "20.3#4" → 그 줄 전체. 머리 두 줄(머리, 구분) 뒤의 몸통 줄을 1부터 센다
        internal static Dictionary<string, string> Effects20Rows()
        {
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = RepoFiles.Lines("docs/effects.md");
            string? sec = null;
            int row = 0;
            int header = 0;
            foreach (string l in lines)
            {
                Match m = Regex.Match(l, "^### (20\\.\\d)");
                if (m.Success)
                {
                    sec = m.Groups[1].Value;
                    row = 0;
                    header = 0;
                    continue;
                }
                if (l.StartsWith("## 21", StringComparison.Ordinal))
                {
                    break;
                }
                if (sec == null)
                {
                    continue;
                }
                if (!l.StartsWith("|", StringComparison.Ordinal))
                {
                    header = 0;
                    continue;
                }
                header++;
                if (header <= 2)
                {
                    continue;
                }
                row++;
                rows[sec + "#" + row] = l;
            }
            Assert.True(rows.Count > 100, "effects.md 20절에서 줄을 너무 적게 읽었다: " + rows.Count);
            return rows;
        }

        private static IEnumerable<string> EffectsTableRows(string start, string end)
        {
            string[] lines = RepoFiles.Lines("docs/effects.md");
            int s = Array.FindIndex(lines, l => l.StartsWith(start, StringComparison.Ordinal));
            Assert.True(s >= 0);
            for (int i = s + 1; i < lines.Length && !lines[i].StartsWith(end, StringComparison.Ordinal); i++)
            {
                if (lines[i].StartsWith("|", StringComparison.Ordinal))
                {
                    yield return lines[i];
                }
            }
        }
    }
}

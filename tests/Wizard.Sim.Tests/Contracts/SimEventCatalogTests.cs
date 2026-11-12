using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Wizard.Sim.Events;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.3: "400번대 이후의 이벤트는 각 명세의 연결점 절에 있는 이름을 그 구간에 그대로 옮긴다."
    // 명세 파일을 직접 읽어 연결점 절의 표에 나온 이벤트 이름을 모으고, SimEventType 과 양쪽으로 대조한다.
    //   - 표의 줄마다 역따옴표가 든 첫 칸의 이름을 모은다(종별 표처럼 첫 칸이 종 이름이면 다음 칸).
    //   - 줄임 표기: `a.b_c` / `_d` → a.b_d, `a.b.c` / `.d` → a.b.d, `a.*` 다음 칸의 "x / y" → a.x, a.y
    //   - 점 이름은 BindingKey 로, PascalCase 이름은 열거형 이름으로 찾는다
    //   - 코어 밖에서 생기는 사건(CONTRACTS 13절 UiEventKeys 의 구역)과 같은 사건의 다른 이름은 아래 표에 이유와 함께 적는다
    public sealed class SimEventCatalogTests
    {
        private sealed class Section
        {
            public string Spec = "";
            public string File = "";
            public string Start = "";
            public string? End;                       // null 이면 다음 "## " 머리
            public string[] SkipSubsections = Array.Empty<string>();
            public int RangeLo, RangeHi;
        }

        private static readonly Section[] Sections =
        {
            new Section { Spec = "01", File = "01-sim-core.md", Start = "### 17.2", End = "## 18.", RangeLo = 100, RangeHi = 399 },
            new Section { Spec = "02", File = "02-spell-compiler.md", Start = "## 19.", End = "## 20.", RangeLo = 400, RangeHi = 499 },
            new Section { Spec = "03", File = "03-command-parser.md", Start = "## 21.", End = "## 22.", RangeLo = 500, RangeHi = 599 },
            // 18.3 강화 화면, 18.4 남이 보는 화면: 코어 밖(ui.*, notice.*). 강화는 전용 화면에서만 한다(CLAUDE.md 2.5)
            new Section { Spec = "04", File = "04-characters-wands.md", Start = "## 18.", End = null,
                          SkipSubsections = new[] { "### 18.3", "### 18.4" }, RangeLo = 500, RangeHi = 599 },
            new Section { Spec = "05", File = "05-enemies.md", Start = "### 14.1", End = "### 14.4", RangeLo = 600, RangeHi = 699 },   // 14.4는 소리 식별자
            new Section { Spec = "06", File = "06-bosses.md", Start = "## 17.", End = "### 17.5", RangeLo = 700, RangeHi = 799 },      // 17.5는 그림 장수
            new Section { Spec = "07", File = "07-waves-drops.md", Start = "## 20.", End = "## 21.", RangeLo = 800, RangeHi = 899 },
            // 23.7 매치·리그(메뉴): 코어 밖
            new Section { Spec = "10", File = "10-duel.md", Start = "## 23.", End = null,
                          SkipSubsections = new[] { "### 23.7" }, RangeLo = 900, RangeHi = 999 },
        };

        // 코어(Wizard.Sim)가 내지 않는 사건. CONTRACTS 13절: 코어 밖 사건은 UiEventKeys(M9-PRS-01)에 둔다
        private static readonly Dictionary<string, string> OutsideCore = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01:InputDroppedByNetwork"] = "넷코드가 아는 사건(duel.input_dropped, CONTRACTS 13절 UiEventKeys)",
            ["04:WandIdle"] = "상태 흐름(뷰)이지 사건이 아니다",
            ["04:PlazaEmoteRaiseWand"] = "광장 감정표현(plaza.*, 코어 밖)",
            ["06:revenge.settlement_row"] = "판이 끝난 뒤의 정산 화면",
            ["06:revenge.tracker_complete"] = "판이 끝난 뒤의 정산 화면",
            ["06:revenge.tracker_update"] = "추적기 화면 진입",
            ["07:revenge_tracker.update"] = "추적기 화면(U4.26)",
            ["07:settle.bag_screen"] = "판이 끝난 뒤의 정산 화면(가방은 코어 밖, CONTRACTS 3.7)",
            ["07:settle.bundle_open"] = "판이 끝난 뒤의 정산 화면",
            ["10:duel.match_found"] = "매치(서버)",
            ["10:duel.walk_to_arena"] = "광장 연출",
            ["10:duel.intro_shown"] = "결투 시작 화면",
            ["10:duel.void"] = "무효는 호스트가 정한다(CONTRACTS 3.9: 이탈·기권·몰수·무효는 코어 밖)",
            ["10:duel.opponent_disconnected"] = "연결(호스트)",
            ["10:duel.opponent_reconnected"] = "연결(호스트)",
            ["10:duel.forfeit"] = "기권은 코어 밖(CONTRACTS 3.9)",
            ["10:duel.aim_line_update"] = "내 화면의 매 프레임 표시",
            ["10:duel.opponent_projectile_spawn"] = "보는 쪽에 따른 표시(코어는 projectile.spawned 를 슬롯과 함께 낸다)",
            ["10:duel.opponent_projectile_impact"] = "보는 쪽에 따른 표시(코어는 projectile.hit 를 낸다)",
            ["10:duel.hit_predicted"] = "예측·확정 단계는 DuelClientCore 의 PresentationEvent(CONTRACTS 6.4)",
            ["10:duel.hit_confirmed"] = "예측·확정 단계는 DuelClientCore 의 PresentationEvent(CONTRACTS 6.4)",
            ["10:duel.hit_rollback"] = "예측·확정 단계는 DuelClientCore 의 PresentationEvent(CONTRACTS 6.4)",
            ["10:duel.margin_band_changed"] = "넷코드(CONTRACTS 13절 UiEventKeys 의 예)",
            ["10:duel.input_dropped"] = "넷코드(CONTRACTS 13절 UiEventKeys 의 예)",
            ["10:duel.rollback_stall_begin"] = "넷코드",
            ["10:duel.rollback_stall_end"] = "넷코드",
            ["10:duel.state_resync"] = "넷코드",
            ["10:duel.applied_stage_label"] = "경기 시작 이름표(화면)",
            ["10:duel.spectate_start"] = "관전(넷코드)",
        };

        // 같은 사건을 가리키는 다른 표기 → 열거형 이름
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01:SeongeCrackChanged"] = "CrackChanged",          // 작업용 이름을 코드에 쓰지 않는다(CLAUDE.md 5절). CONTRACTS 3.3의 이름
            ["05:graze"] = "GrazeAwarded",                       // "플레이어 쪽 연출(E 11.1)" = spec 01 17.2의 GrazeAwarded
            ["06:star_serpent.bite_bite"] = "StarSerpentBite",   // `star_serpent.bite_telegraph`/`_bite` 의 뒤쪽은 물기 자체(OPEN-QUESTIONS)
            ["07:star_crystal.telegraph"] = "StarfallTelegraph", // "(돌발 사건, 다른 명세가 정함)" = spec 01 10.5·17.2의 별똥
            ["07:star_crystal.fall"] = "StarfallLanded",
            ["07:star_crystal.picked"] = "StarfallPicked",
        };

        // 명세 연결점 절 밖에서 CONTRACTS 가 직접 정한 값
        private static readonly HashSet<SimEventType> ContractOnly = new HashSet<SimEventType>
        {
            SimEventType.PlayerRose,          // CONTRACTS 3.3(spec 01 6.3의 일어남)
            SimEventType.SpellNodeExecuted,   // CONTRACTS 3.3(GDD 13 회로 점등)
        };

        private static readonly Regex Tick = new Regex("`([^`]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex EventToken = new Regex("^[A-Za-z][A-Za-z0-9_]*(\\.[a-z0-9_]+)*(\\.\\*)?$", RegexOptions.CultureInvariant);

        [Fact]
        public void EveryLinkSectionEventIsInSimEventTypeInItsRange()
        {
            Dictionary<string, SimEventType> byKey = SimEventContractTests.AllTypes().ToDictionary(t => SimEventDocs.BindingKey(t), StringComparer.Ordinal);
            var resolvedBy = new Dictionary<SimEventType, string>();
            var usedOutside = new HashSet<string>(StringComparer.Ordinal);
            var usedAliases = new HashSet<string>(StringComparer.Ordinal);
            var problems = new List<string>();

            foreach (Section s in Sections)
            {
                foreach (string token in Collect(s))
                {
                    string id = s.Spec + ":" + token;
                    if (OutsideCore.ContainsKey(id))
                    {
                        usedOutside.Add(id);
                        continue;
                    }
                    SimEventType type;
                    if (Aliases.TryGetValue(id, out string? alias))
                    {
                        usedAliases.Add(id);
                        if (!Enum.TryParse(alias, false, out type))
                        {
                            problems.Add(id + ": 별칭 " + alias + " 이 열거형에 없다");
                            continue;
                        }
                    }
                    else if (token.Contains('.'))
                    {
                        if (!byKey.TryGetValue(token, out type))
                        {
                            problems.Add(id + ": 열쇠가 SimEventDocs 에 없다");
                            continue;
                        }
                    }
                    else if (!Enum.TryParse(token, false, out type) || !Enum.IsDefined(typeof(SimEventType), type))
                    {
                        problems.Add(id + ": 이름이 SimEventType 에 없다");
                        continue;
                    }

                    int v = (int)type;
                    if (resolvedBy.ContainsKey(type))
                    {
                        continue;                               // 앞 명세에 같은 사건이 있다(값을 같이 쓴다)
                    }
                    if (v < s.RangeLo || v > s.RangeHi)
                    {
                        problems.Add(id + ": " + type + "=" + v + " 가 spec " + s.Spec + " 의 구간 " + s.RangeLo + "~" + s.RangeHi + " 밖이다");
                    }
                    resolvedBy[type] = id;
                }
            }

            foreach (string stale in OutsideCore.Keys.Where(k => !usedOutside.Contains(k)))
            {
                problems.Add("코어 밖 표의 " + stale + " 가 명세에 없다(표를 고쳐라)");
            }
            foreach (string stale in Aliases.Keys.Where(k => !usedAliases.Contains(k)))
            {
                problems.Add("별칭 표의 " + stale + " 가 명세에 없다(표를 고쳐라)");
            }

            // 거꾸로: 100~999의 모든 값은 명세의 이름에서 왔거나 CONTRACTS 가 직접 정한 것이다(지어낸 사건이 없다)
            foreach (SimEventType t in SimEventContractTests.AllTypes().Where(t => (int)t >= 100 && (int)t <= 999))
            {
                if (!resolvedBy.ContainsKey(t) && !ContractOnly.Contains(t))
                {
                    problems.Add(t + "=" + (int)t + " 는 어느 연결점 절에도 없다");
                }
            }

            Assert.True(problems.Count == 0, string.Join("\n", problems));
            Assert.True(resolvedBy.Count > 400, "모은 이름이 너무 적다: " + resolvedBy.Count);   // 파서가 절을 놓치지 않았는지
        }

        [Fact]
        public void SectionsAreFoundInSpecFiles()
        {
            foreach (Section s in Sections)
            {
                int n = Collect(s).Count();
                Assert.True(n > 0, "spec " + s.Spec + " " + s.Start + " 에서 이벤트를 하나도 찾지 못했다");
            }
        }

        [Fact]
        public void ShorthandExpansion()
        {
            Assert.Equal(new[] { "boar_kiln.wall_stun_begin", "boar_kiln.wall_stun_end" },
                         ExpandCell("`boar_kiln.wall_stun_begin`/`_end`(기절 별)", null));
            Assert.Equal(new[] { "status.slow.applied", "status.slow.refreshed", "status.slow.expired" },
                         ExpandCell("`status.slow.applied` / `.refreshed` / `.expired`", null));
            Assert.Equal(new[] { "phantom_first.intro", "phantom_first.crack" },
                         ExpandCell("`phantom_first.*`", " intro / crack "));
            Assert.Equal(new[] { "star_serpent.spit_aim", "star_serpent.spit_lock", "star_serpent.spit_fire" },
                         ExpandCell("`star_serpent.spit_aim`/`_lock`/`_fire`", null));
        }

        private static IEnumerable<string> Collect(Section s)
        {
            string path = Path.Combine(RepoRoot(), "docs", "spec", s.File);
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int start = Array.FindIndex(lines, l => l.StartsWith(s.Start, StringComparison.Ordinal));
            Assert.True(start >= 0, s.File + " 에 '" + s.Start + "' 가 없다");
            bool skipping = false;
            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (s.End != null ? line.StartsWith(s.End, StringComparison.Ordinal) : line.StartsWith("## ", StringComparison.Ordinal))
                {
                    yield break;
                }
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    skipping = s.SkipSubsections.Any(x => line.StartsWith(x, StringComparison.Ordinal));
                    continue;
                }
                if (skipping || !line.StartsWith("|", StringComparison.Ordinal))
                {
                    continue;
                }
                List<string> cells = SplitRow(line);
                int c = cells.FindIndex(x => x.Contains('`'));
                if (c < 0)
                {
                    continue;
                }
                string? next = c + 1 < cells.Count ? cells[c + 1] : null;
                foreach (string token in ExpandCell(cells[c], next))
                {
                    yield return token;
                }
            }
        }

        private static List<string> ExpandCell(string cell, string? nextCell)
        {
            var result = new List<string>();
            string? prev = null;
            foreach (Match m in Tick.Matches(cell))
            {
                string raw = m.Groups[1].Value.Trim();
                string token;
                if (raw.StartsWith("_", StringComparison.Ordinal) && prev != null)
                {
                    int dot = prev.LastIndexOf('.');
                    int us = prev.LastIndexOf('_');
                    token = (us > dot ? prev.Substring(0, us) : prev) + raw;
                }
                else if (raw.StartsWith(".", StringComparison.Ordinal) && prev != null)
                {
                    token = prev.Substring(0, prev.LastIndexOf('.')) + raw;
                }
                else
                {
                    token = raw;
                }
                if (!EventToken.IsMatch(token))
                {
                    continue;                                   // 에셋 열쇠 틀, 색, 문자열 id 등
                }
                if (token.EndsWith(".*", StringComparison.Ordinal))
                {
                    string stem = token.Substring(0, token.Length - 1);
                    foreach (string part in (nextCell ?? "").Split('/'))
                    {
                        string p = part.Trim();
                        if (p.Length > 0)
                        {
                            result.Add(stem + p);
                        }
                    }
                    continue;
                }
                result.Add(token);
                prev = token;
            }
            return result;
        }

        // 표의 칸 나누기. 역따옴표 안의 | 는 칸 구분이 아니다(`voice.{char}.{a|b}` 같은 틀)
        private static List<string> SplitRow(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool code = false;
            for (int i = 1; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '`')
                {
                    code = !code;
                }
                if (ch == '|' && !code)
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(ch);
            }
            if (sb.Length > 0)
            {
                cells.Add(sb.ToString());
            }
            return cells;
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Wizard.sln")))
            {
                dir = dir.Parent;
            }
            Assert.True(dir != null, "Wizard.sln 이 든 폴더를 찾지 못했다");
            return dir!.FullName;
        }
    }
}

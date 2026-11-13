using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Wizard.Sim.Events;
using Wizard.ViewLogic.Presentation;
using Wizard.ViewLogic.Tests.Presentation.Bindings;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.UiEvents
{
    // CONTRACTS 13절: "각 명세의 연결점 절(spec 01 17, 02 19, 03 21, 04 18, 05 14, 06 17, 07 20, 08, 09 22, 10 23, 11 17, 12 17, 13 21)에
    // 나오는 사건 이름은 두 카탈로그 가운데 하나에 있어야 한다." 명세 파일을 직접 읽어 이름을 모으고 SimEventType·UiEventKeys 와 양쪽으로 대조한다.
    //   모으기 — 표의 줄마다 역따옴표가 든 첫 칸의 이름(spec 12 17절은 목록 줄의 이름 전부). 이름 뒤의 "(인자)"는 뗀다.
    //            줄임 표기: `a.b_c` / `_d` → a.b_d, `a.b.c` / `.d` → a.b.d, `a.b.c` / `d` → a.b.d, `a.*` 다음 칸의 "x / y" → a.x, a.y
    //   코어 — 점 이름은 SimEventDocs.BindingKey 로, PascalCase 이름은 열거형 이름으로 찾는다(SimEventCatalogTests 와 같은 규칙과 별칭)
    //   코어 밖 — 아래 규칙으로 "구역.snake_case" 열쇠를 만들어 UiEventKeys.All 에서 찾는다:
    //            1. 별칭 표에 있으면 그 열쇠
    //            2. 점 이름: 앞 마디가 구역이면 그대로, 아니면 명세별 표(DotZone)의 구역을 앞에 붙인다(hud.x → ui.hud.x)
    //            3. 점 없는 snake 이름: 앞 마디가 구역이면 그 마디를 구역으로 떼어 낸다(plaza_enter → plaza.enter),
    //               아니면 명세별 표(SnakeZone)의 구역을 앞에 붙인다(verify_passed → run.verify_passed)
    //            4. PascalCase: 앞 낱말이 구역이면 구역.snake(나머지)(PlazaEmoteRaiseWand → plaza.emote_raise_wand),
    //               spec 04 18.3(강화 화면, 코어 밖)은 ui.wand.snake(이름)
    //   거꾸로 — UiEventKeys.All 의 모든 열쇠는 어느 명세 이름에서 왔다(지어낸 열쇠가 없다). 표의 줄도 모두 쓰였다(낡은 줄이 없다)
    public sealed class UiEventCatalogSpecTests
    {
        private sealed class Section
        {
            public string Spec = "";
            public string File = "";
            public string Start = "";
            public string? End;                       // null 이면 다음 "## " 머리
            public bool Bullets;                      // 목록 줄("- `…`")에서도 모은다
        }

        private static readonly Section[] Sections =
        {
            new Section { Spec = "01", File = "01-sim-core.md", Start = "### 17.2", End = "## 18." },
            new Section { Spec = "02", File = "02-spell-compiler.md", Start = "## 19.", End = "## 20." },
            new Section { Spec = "03", File = "03-command-parser.md", Start = "## 21.", End = "## 22." },
            new Section { Spec = "04", File = "04-characters-wands.md", Start = "## 18.", End = null },
            new Section { Spec = "05", File = "05-enemies.md", Start = "### 14.1", End = "### 14.4" },       // 14.4는 소리 식별자
            new Section { Spec = "06", File = "06-bosses.md", Start = "## 17.", End = "### 17.5" },          // 17.5는 그림 장수
            new Section { Spec = "07", File = "07-waves-drops.md", Start = "## 20.", End = "## 21." },
            new Section { Spec = "08", File = "08-enhance-economy-profile.md", Start = "## 16.", End = "## 17." },
            new Section { Spec = "09", File = "09-run-verify-ranking-season.md", Start = "## 22.", End = "## 23." },
            new Section { Spec = "10", File = "10-duel.md", Start = "## 23.", End = null },
            new Section { Spec = "11", File = "11-plaza-social-trade.md", Start = "## 17.", End = "### 17.5" },   // 17.5 입자 예산, 17.6 에셋 식별자
            new Section { Spec = "12", File = "12-server-infra.md", Start = "## 17.", End = null, Bullets = true },
            new Section { Spec = "13", File = "13-ui-flows.md", Start = "## 21.", End = null },
        };

        // 수락 조건이 이름을 콕 집은 절: 이 절들의 이름은 하나도 빠짐없이 풀려야 하고, 모은 수가 0이면 파서가 절을 놓친 것이다
        private static readonly string[] AcceptanceSpecs = { "09", "10", "11", "12", "13" };

        // 사건이 아니다(뷰·상태 흐름). 열쇠를 만들지 않는다
        private static readonly Dictionary<string, string> NotEvents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["04:WandIdle"] = "상태 흐름(WandView 를 매 프레임 읽는다)",
            ["04:InspectWandView"] = "18.4 남이 보는 화면의 뷰",
            ["04:TradeWandView"] = "18.4 남이 보는 화면의 뷰",
            ["04:DuelOpponentWandView"] = "18.4 남이 보는 화면의 뷰",
            ["04:TitleBadge"] = "18.4 남이 보는 화면의 뷰",
            ["04:WildDyeAppearance"] = "18.4 남이 보는 화면의 뷰",
        };

        // 코어 이벤트의 다른 표기(SimEventCatalogTests 의 별칭과 같다)
        private static readonly Dictionary<string, string> SimAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01:SeongeCrackChanged"] = nameof(SimEventType.CrackChanged),
            ["05:graze"] = nameof(SimEventType.GrazeAwarded),
            ["06:star_serpent.bite_bite"] = nameof(SimEventType.StarSerpentBite),
            ["07:star_crystal.telegraph"] = nameof(SimEventType.StarfallTelegraph),
            ["07:star_crystal.fall"] = nameof(SimEventType.StarfallLanded),
            ["07:star_crystal.picked"] = nameof(SimEventType.StarfallPicked),
        };

        // 코어 밖 사건의 별칭: 같은 사건을 다른 명세가 다르게 적은 것, 작업용 이름이 든 것(CLAUDE.md 5절: 열쇠에 쓰지 않는다)
        private static readonly Dictionary<string, string> UiAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01:InputDroppedByNetwork"] = UiEventKeys.DuelInputDropped,            // spec 10 23.6과 같은 사건(CONTRACTS 13절의 예)
            ["07:revenge_tracker.update"] = UiEventKeys.UiRevengeTrackerUpdate,     // spec 06 17절 revenge.tracker_update 와 같은 화면
            ["07:settle.bundle_open"] = UiEventKeys.UiSettlementBundleOpen,         // spec 13 21.4 ui.settlement.bundle_open
            ["07:settle.bag_screen"] = UiEventKeys.UiSettlementBagOpen,             // spec 13 21.4 ui.settlement.bag_open
            ["09:run_submit_sent"] = UiEventKeys.RunSubmitSent,                     // spec 12 17절 run.submit.sent 와 같은 사건
            ["09:run_submit_ack"] = UiEventKeys.RunSubmitAck,                       // 위와 같은 꼴로 둔다
            ["11:notice_nuru_eye_open"] = UiEventKeys.NoticeMasterEyeOpen,          // 흔들의자의 스승 한쪽 눈(작업용 이름 대신)
            ["11:notice_nuru_eye_close"] = UiEventKeys.NoticeMasterEyeClose,
        };

        // 점 이름의 앞 마디가 구역이 아닐 때 붙일 구역(명세별)
        private static readonly Dictionary<string, Dictionary<string, string>> DotZone = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["06"] = Map(("revenge", "ui")),                                         // 판이 끝난 뒤의 정산·추적기 화면
            ["07"] = Map(("revenge_tracker", "ui"), ("settle", "ui")),
            ["08"] = Map(("enhance", "ui"), ("wand", "ui"), ("shop", "ui"), ("profile", "ui")),
            ["10"] = Map(("league", "duel")),                                        // 23.7 매치·리그(메뉴)
            ["12"] = Map(("session", "net"), ("auth", "net"), ("security", "net"), ("enhance", "ui"), ("market", "trade"),
                         ("system", "notice"), ("store", "ui"), ("purchase", "ui"), ("currency", "ui"), ("admin", "ui")),
            ["13"] = Map(("hud", "ui"), ("fx", "ui"), ("chat", "ui")),
        };

        // 점 없는 snake 이름의 앞 마디가 구역이 아닐 때 붙일 구역(명세별). 이름 전체를 둔다
        private static readonly Dictionary<string, Dictionary<string, string>> SnakeZone = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["09"] = Map(("settle", "run"), ("reward", "run"), ("verify", "run"), ("debt", "run"), ("wand", "run"), ("tidy", "run"),
                         ("personal", "run"), ("phantom", "run"), ("revenge", "run"), ("account", "run"), ("replay", "run"),
                         ("leaderboard", "season"), ("weekly", "season"), ("heir", "season"), ("disciple", "season"), ("league", "season"),
                         ("sanction", "season")),
            ["11"] = Map(("chat", "plaza"), ("request", "plaza"), ("friend", "plaza"), ("block", "plaza"), ("report", "plaza"),
                         ("inspect", "plaza"), ("mail", "plaza"), ("shop", "plaza"), ("market", "trade")),
        };

        private static readonly Regex Tick = new Regex("`([^`]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex EventToken = new Regex("^[A-Za-z][A-Za-z0-9_]*(\\.[a-z0-9_]+)*(\\.\\*)?$", RegexOptions.CultureInvariant);

        [Fact]
        public void EveryLinkSectionNameIsInSimEventTypeOrUiEventKeys()
        {
            Dictionary<string, SimEventType> simByKey = EventBindingCoverage.AllSimEventTypes().ToDictionary(t => SimEventDocs.BindingKey(t), StringComparer.Ordinal);
            var simNames = new HashSet<string>(Enum.GetNames(typeof(SimEventType)), StringComparer.Ordinal);
            var uiKeys = new HashSet<string>(UiEventKeys.All, StringComparer.Ordinal);
            var producedUi = new HashSet<string>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var perSpec = new Dictionary<string, int>(StringComparer.Ordinal);
            var problems = new List<string>();

            foreach (Section s in Sections)
            {
                foreach ((string sub, string token) in Collect(s))
                {
                    string id = s.Spec + ":" + token;
                    perSpec[s.Spec] = (perSpec.TryGetValue(s.Spec, out int n) ? n : 0) + 1;
                    if (NotEvents.ContainsKey(id))
                    {
                        used.Add(id);
                        continue;
                    }
                    if (SimAliases.TryGetValue(id, out string? alias))
                    {
                        used.Add(id);
                        if (!simNames.Contains(alias))
                        {
                            problems.Add(id + ": 별칭 " + alias + " 이 SimEventType 에 없다");
                        }
                        continue;
                    }
                    if (token.Contains('.') && simByKey.ContainsKey(token))
                    {
                        continue;
                    }
                    if (char.IsUpper(token[0]) && simNames.Contains(token))
                    {
                        continue;
                    }
                    if (UiAliases.ContainsKey(id))
                    {
                        used.Add(id);
                    }
                    string? key = ToKey(s.Spec, sub, token);
                    if (key == null)
                    {
                        problems.Add(id + " (" + sub + "): SimEventType 에도 없고 코어 밖 열쇠로 옮기는 규칙도 없다");
                        continue;
                    }
                    if (simByKey.ContainsKey(key))
                    {
                        continue;
                    }
                    if (!uiKeys.Contains(key))
                    {
                        problems.Add(id + " → '" + key + "': UiEventKeys.All 에 없다");
                        continue;
                    }
                    producedUi.Add(key);
                }
            }

            foreach (string k in UiEventKeys.All.Where(k => !producedUi.Contains(k)))
            {
                problems.Add("UiEventKeys '" + k + "' 는 어느 명세 연결점 절의 이름에서도 오지 않았다(지어낸 열쇠)");
            }
            foreach (string stale in NotEvents.Keys.Concat(SimAliases.Keys).Concat(UiAliases.Keys).Where(k => !used.Contains(k)))
            {
                problems.Add("표의 " + stale + " 가 명세에 없다(표를 고쳐라)");
            }
            foreach (string spec in AcceptanceSpecs)
            {
                if (!perSpec.ContainsKey(spec))
                {
                    problems.Add("spec " + spec + " 의 연결점 절에서 이름을 하나도 모으지 못했다");
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void AcceptanceSectionsAreParsed()
        {
            // spec 09 22, 10 23, 11 17, 12 17, 13 21절의 이름 수(파서가 표나 목록을 놓치면 줄어든다). 명세가 이름을 더하면 늘어나도 된다
            var counts = Sections.ToDictionary(s => s.Spec, s => Collect(s).Count(), StringComparer.Ordinal);
            Assert.True(counts["09"] >= 40, "spec 09: " + counts["09"]);
            Assert.True(counts["10"] >= 60, "spec 10: " + counts["10"]);
            Assert.True(counts["11"] >= 60, "spec 11: " + counts["11"]);
            Assert.True(counts["12"] >= 60, "spec 12: " + counts["12"]);
            Assert.True(counts["13"] >= 170, "spec 13: " + counts["13"]);
        }

        [Fact]
        public void KeyRules()
        {
            Assert.Equal("ui.hud.boss_show", ToKey("13", "21.3", "hud.boss_show"));
            Assert.Equal("ui.enhance.attempt.charge.begin", ToKey("08", "16.2", "enhance.attempt.charge.begin"));
            Assert.Equal("notice.enhance.plus13", ToKey("08", "16.4", "notice.enhance.plus13"));
            Assert.Equal("plaza.interact_prompt_hide", ToKey("11", "17.1", "plaza_interact_prompt_hide"));
            Assert.Equal("trade.market_listed", ToKey("11", "17.4", "market_listed"));
            Assert.Equal("run.verify_passed", ToKey("09", "", "verify_passed"));
            Assert.Equal("season.deadline_soon", ToKey("09", "", "season_deadline_soon"));
            Assert.Equal("net.auth.login.started", ToKey("12", "", "auth.login.started"));
            Assert.Equal("duel.league.promoted", ToKey("10", "23.7", "league.promoted"));
            Assert.Equal("plaza.emote_raise_wand", ToKey("04", "18.2", "PlazaEmoteRaiseWand"));
            Assert.Equal("ui.wand.stage13_achieved", ToKey("04", "18.3", "Stage13Achieved"));
            Assert.Equal("notice.master_eye_open", ToKey("11", "17.2", "notice_nuru_eye_open"));
            Assert.Null(ToKey("05", "14.1", "hazard_unknown"));
        }

        [Fact]
        public void ShorthandExpansion()
        {
            Assert.Equal(new[] { "ui.common.hover_enter", "ui.common.hover_exit" }, ExpandCell("`ui.common.hover_enter` / `hover_exit`", null));
            Assert.Equal(new[] { "enhance.attempt.charge.begin", "enhance.attempt.charge.end" },
                         ExpandCell("`enhance.attempt.charge.begin(intensity)` / `.end`", null));
            Assert.Equal(new[] { "plaza_interact_prompt_show", "plaza_interact_prompt_hide" },
                         ExpandCell("`plaza_interact_prompt_show(target)` / `_hide`", null));
            Assert.Equal(new[] { "notice_band_show" }, ExpandCell("`notice_band_show(kind = enhance10, wand, nickname?)`", null));
            Assert.Equal(new[] { "run_submit_sent", "run_submit_ack" }, ExpandCell("`run_submit_sent` / `run_submit_ack`", null));
            Assert.Equal(new[] { "phantom_first.intro", "phantom_first.crack" }, ExpandCell("`phantom_first.*`", " intro / crack "));
        }

        // ── 열쇠 만들기 ──

        private static string? ToKey(string spec, string sub, string token)
        {
            if (UiAliases.TryGetValue(spec + ":" + token, out string? alias))
            {
                return alias;
            }
            if (char.IsUpper(token[0]))
            {
                string snake = Snake(token);
                int us = snake.IndexOf('_');
                string first = us < 0 ? snake : snake.Substring(0, us);
                if (us > 0 && UiEventZones.IsZone(first))
                {
                    return first + "." + snake.Substring(us + 1);
                }
                if (spec == "04" && sub == "18.3")
                {
                    return UiEventZones.Ui + ".wand." + snake;
                }
                return null;
            }
            int dot = token.IndexOf('.');
            if (dot > 0)
            {
                string first = token.Substring(0, dot);
                if (UiEventZones.IsZone(first))
                {
                    return token;
                }
                return DotZone.TryGetValue(spec, out Dictionary<string, string>? m) && m.TryGetValue(first, out string? z) ? z + "." + token : null;
            }
            int u = token.IndexOf('_');
            string head = u < 0 ? token : token.Substring(0, u);
            if (u > 0 && UiEventZones.IsZone(head))
            {
                return head + "." + token.Substring(u + 1);
            }
            return SnakeZone.TryGetValue(spec, out Dictionary<string, string>? sm) && sm.TryGetValue(head, out string? sz) ? sz + "." + token : null;
        }

        // PascalCase → snake_case(숫자는 앞 글자에 붙는다: Stage13Achieved → stage13_achieved)
        private static string Snake(string pascal)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static Dictionary<string, string> Map(params (string Key, string Zone)[] pairs)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string k, string z) in pairs)
            {
                d.Add(k, z);
            }
            return d;
        }

        // ── 명세 읽기 ──

        private static List<(string Sub, string Token)> Collect(Section s)
        {
            string[] lines = RepoFiles.Lines("docs/spec/" + s.File);
            int start = Array.FindIndex(lines, l => l.StartsWith(s.Start, StringComparison.Ordinal));
            Assert.True(start >= 0, s.File + " 에 '" + s.Start + "' 가 없다");
            var result = new List<(string, string)>();
            string sub = "";
            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (s.End != null ? line.StartsWith(s.End, StringComparison.Ordinal) : line.StartsWith("## ", StringComparison.Ordinal))
                {
                    break;
                }
                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    string rest = line.Substring(4);
                    int sp = rest.IndexOf(' ');
                    sub = sp < 0 ? rest : rest.Substring(0, sp);
                    continue;
                }
                if (line.StartsWith("|", StringComparison.Ordinal))
                {
                    List<string> cells = SplitRow(line);
                    int c = cells.FindIndex(x => x.Contains('`'));
                    if (c < 0)
                    {
                        continue;
                    }
                    string? next = c + 1 < cells.Count ? cells[c + 1] : null;
                    foreach (string t in ExpandCell(cells[c], next))
                    {
                        result.Add((sub, t));
                    }
                }
                else if (s.Bullets && line.StartsWith("- ", StringComparison.Ordinal))
                {
                    foreach (string t in ExpandCell(line, null))
                    {
                        result.Add((sub, t));
                    }
                }
            }
            return result;
        }

        private static List<string> ExpandCell(string cell, string? nextCell)
        {
            var result = new List<string>();
            string? prev = null;
            foreach (Match m in Tick.Matches(cell))
            {
                string raw = m.Groups[1].Value.Trim();
                int paren = raw.IndexOf('(');
                if (paren > 0)
                {
                    raw = raw.Substring(0, paren).Trim();
                }
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
                else if (prev != null && prev.Contains('.') && !raw.Contains('.') && raw.Length > 0 && raw[0] >= 'a' && raw[0] <= 'z')
                {
                    token = prev.Substring(0, prev.LastIndexOf('.') + 1) + raw;     // `ui.common.hover_enter` / `hover_exit`
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

        // 표의 칸 나누기. 역따옴표 안의 | 는 칸 구분이 아니다
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
    }
}

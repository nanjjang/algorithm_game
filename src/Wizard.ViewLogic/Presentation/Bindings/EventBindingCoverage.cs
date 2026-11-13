using System;
using System.Collections.Generic;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13절의 빠짐 검사. 에셋을 나중에 꽂을 식별자가 빠지지 않게 하는 장치다.
    //   1. SimEventType 의 모든 값(SimEventDocs.BindingKey)과 UiEventKeys.All 의 모든 열쇠는 바인딩 줄이 있거나 "의도적 없음" 목록에 있다
    //   2. 두 카탈로그에 같은 열쇠가 없다(결투처럼 코어 이벤트와 코어 밖 사건이 같은 구역 이름을 쓰므로 열쇠 단위로 막는다)
    //   3. 바인딩 줄과 "의도적 없음" 목록의 열쇠는 두 카탈로그 가운데 하나에 있다(지어낸 열쇠가 없다)
    //   4. UiEventKeys 의 열쇠는 UiEventZones 의 구역으로 시작하는 꼴이고 서로 다르다
    // 문제를 한 줄씩 돌려준다(SimEventType 값 순, UiEventKeys.All 순, 파일 순). 비었으면 통과
    public static class EventBindingCoverage
    {
        public static List<string> FindProblems(EventBindingTable table)
        {
            var problems = new List<string>();
            var simKeys = new Dictionary<string, SimEventType>(StringComparer.Ordinal);
            foreach (SimEventType t in AllSimEventTypes())
            {
                string key = SimEventDocs.BindingKey(t);
                if (simKeys.TryGetValue(key, out SimEventType other))
                {
                    problems.Add("SimEventDocs: " + t + " and " + other + " share the key '" + key + "'");
                    continue;
                }
                simKeys.Add(key, t);
                if (!table.TryGet(key, out _) && !table.IsIntentionallyNone(key))
                {
                    problems.Add("SimEventType." + t + " ('" + key + "'): no binding line and not in " + EventBindingTable.NoneFile);
                }
            }

            var uiKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in UiEventKeys.All)
            {
                if (!uiKeys.Add(key))
                {
                    problems.Add("UiEventKeys: '" + key + "' appears twice in All");
                    continue;
                }
                if (!UiEventZones.IsValidKey(key))
                {
                    problems.Add("UiEventKeys: '" + key + "' is not <zone>.<snake_case> with a CONTRACTS 13 zone");
                }
                if (simKeys.ContainsKey(key))
                {
                    problems.Add("'" + key + "' is in both SimEventDocs and UiEventKeys");
                }
                if (!table.TryGet(key, out _) && !table.IsIntentionallyNone(key))
                {
                    problems.Add("UiEventKeys '" + key + "': no binding line and not in " + EventBindingTable.NoneFile);
                }
            }

            foreach (EventBinding b in table.Bindings)
            {
                if (!simKeys.ContainsKey(b.EventKey) && !uiKeys.Contains(b.EventKey))
                {
                    problems.Add(EventBindingTable.BindingsFile + ": '" + b.EventKey + "' is neither a SimEventDocs key nor a UiEventKeys key");
                }
            }
            foreach (string key in table.NoneKeys)
            {
                if (!simKeys.ContainsKey(key) && !uiKeys.Contains(key))
                {
                    problems.Add(EventBindingTable.NoneFile + ": '" + key + "' is neither a SimEventDocs key nor a UiEventKeys key");
                }
            }
            return problems;
        }

        // 값 오름차순. Enum.GetValues 는 값의 부호 없는 크기 순으로 돌려준다(선언 순서와 무관)
        public static List<SimEventType> AllSimEventTypes()
        {
            var list = new List<SimEventType>();
            foreach (object v in Enum.GetValues(typeof(SimEventType)))
            {
                list.Add((SimEventType)v);
            }
            return list;
        }
    }
}

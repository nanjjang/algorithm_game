using System;
using System.Collections.Generic;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Affinity
{
    // 상성 분배 검사기(spec 05 5.2 AffinityBalanceValidator, ARCHITECTURE 8.2 3단계 교차 검사). databuild check·build 에서 돈다.
    // 원본(IAffinitySource)이 모은 줄에 규칙(IAffinityRule)을 건다. 원본과 규칙은 이 어셈블리에서 찾는다(등록하지 않는다).
    public sealed class AffinityValidator : IDataValidator
    {
        public string Name => "affinity";

        public void Validate(DataSet data, Diagnostics diagnostics)
        {
            var table = new AffinityTable();
            foreach (IAffinitySource s in Find<IAffinitySource>())
            {
                s.Collect(data, table, diagnostics);
            }
            if (table.Rows.Count == 0)
            {
                return;
            }
            Check(table, diagnostics);
        }

        public static void Check(AffinityTable table, Diagnostics diagnostics)
        {
            foreach (IAffinityRule r in Find<IAffinityRule>())
            {
                r.Check(table, diagnostics);
            }
        }

        // 이 어셈블리에서 T 를 구현한 형식(형식 이름의 서수 오름차순)
        public static IReadOnlyList<T> Find<T>()
            where T : class
        {
            var types = new List<Type>(typeof(AffinityValidator).Assembly.GetTypes());
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            var list = new List<T>();
            foreach (Type t in types)
            {
                if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition || !typeof(T).IsAssignableFrom(t))
                {
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new DataBuildException(t.FullName + ": 상성 검사의 원본·규칙에는 공개 매개변수 없는 생성자가 있어야 한다");
                }
                list.Add((T)Activator.CreateInstance(t)!);
            }
            return list;
        }
    }
}

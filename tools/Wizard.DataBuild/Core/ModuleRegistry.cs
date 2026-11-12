using System;
using System.Collections.Generic;
using System.Reflection;
using Wizard.Content;

namespace Wizard.DataBuild.Core
{
    // 변환기와 교차 검사를 어셈블리에서 찾는다(T-33: DataBuild 의 변환기는 모듈 인터페이스를 구현한 클래스를 찾아 등록한다).
    // 결과는 정해진 순서다: 변환기는 절 id 오름차순, 검사는 이름의 서수 오름차순. 같은 절의 변환기가 둘이면 오류다.
    public sealed class ModuleRegistry
    {
        public ModuleRegistry(IReadOnlyList<ISectionConverter> converters, IReadOnlyList<IDataValidator> validators)
        {
            var c = new List<ISectionConverter>(converters);
            c.Sort((a, b) => ((ushort)a.Section).CompareTo((ushort)b.Section));
            for (int i = 0; i < c.Count; i++)
            {
                if (!ContentSections.IsKnown(c[i].Section))
                {
                    throw new DataBuildException(c[i].GetType().FullName + ": 절 목차에 없는 절 " + ((ushort)c[i].Section).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                if (i > 0 && c[i].Section == c[i - 1].Section)
                {
                    throw new DataBuildException("절 " + ContentSections.Name(c[i].Section) + " 의 변환기가 둘이다: "
                        + c[i - 1].GetType().FullName + ", " + c[i].GetType().FullName);
                }
            }
            var v = new List<IDataValidator>(validators);
            v.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            for (int i = 1; i < v.Count; i++)
            {
                if (v[i].Name == v[i - 1].Name)
                {
                    throw new DataBuildException("교차 검사의 이름 '" + v[i].Name + "' 이 둘이다");
                }
            }
            Converters = c;
            Validators = v;
        }

        public IReadOnlyList<ISectionConverter> Converters { get; }

        public IReadOnlyList<IDataValidator> Validators { get; }

        public static ModuleRegistry Discover() => Discover(typeof(ModuleRegistry).Assembly);

        public static ModuleRegistry Discover(Assembly assembly)
        {
            var converters = new List<ISectionConverter>();
            var validators = new List<IDataValidator>();
            var types = new List<Type>(assembly.GetTypes());
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            foreach (Type t in types)
            {
                if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition)
                {
                    continue;
                }
                bool isConverter = typeof(ISectionConverter).IsAssignableFrom(t);
                bool isValidator = typeof(IDataValidator).IsAssignableFrom(t);
                if (!isConverter && !isValidator)
                {
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new DataBuildException(t.FullName + ": 변환기·교차 검사에는 공개 매개변수 없는 생성자가 있어야 한다");
                }
                object o = Activator.CreateInstance(t)!;
                if (isConverter)
                {
                    converters.Add((ISectionConverter)o);
                }
                if (isValidator)
                {
                    validators.Add((IDataValidator)o);
                }
            }
            return new ModuleRegistry(converters, validators);
        }

        public ISectionConverter? ConverterFor(ContentSection section)
        {
            foreach (ISectionConverter c in Converters)
            {
                if (c.Section == section)
                {
                    return c;
                }
            }
            return null;
        }
    }
}

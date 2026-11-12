using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // 계약의 공개 칸(이름, 형식, 선언 순서)을 대조하는 도우미. 기대값은 CONTRACTS.md 의 코드 조각에서 옮긴다
    internal static class ContractShape
    {
        public static void Fields(Type type, params (string Name, Type FieldType)[] expected)
        {
            FieldInfo[] actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(f => f.MetadataToken)
                .ToArray();
            Assert.Equal(expected.Select(e => e.Name + ":" + e.FieldType.FullName), actual.Select(f => f.Name + ":" + f.FieldType.FullName));
        }

        public static void Enum<T>(params (string Name, long Value)[] expected) where T : struct, System.Enum
        {
            var actual = System.Enum.GetValues(typeof(T)).Cast<T>()
                .Select(v => (Name: v.ToString(), Value: Convert.ToInt64(v)))
                .OrderBy(x => x.Value)
                .ToArray();
            Assert.Equal(expected.OrderBy(x => x.Value).Select(x => x.Name + "=" + x.Value), actual.Select(x => x.Name + "=" + x.Value));
        }

        public static void HasPublicInstanceMethod(Type type, string name, Type returnType, params Type[] parameters)
        {
            MethodInfo? m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameters, null);
            Assert.True(m != null, type.Name + "." + name + " 이 없다");
            Assert.Equal(returnType, m!.ReturnType);
        }

        public static void HasPublicStaticMethod(Type type, string name, Type returnType, params Type[] parameters)
        {
            MethodInfo? m = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, parameters, null);
            Assert.True(m != null, type.Name + "." + name + " 이 없다");
            Assert.Equal(returnType, m!.ReturnType);
        }
    }
}

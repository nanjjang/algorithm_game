using System;
using System.Collections.Generic;
using Wizard.DataBuild.Core;

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 검산기(spec 05 10절 EvasionValidator, ARCHITECTURE 8.2 3단계 교차 검사). databuild check·build 에서 돈다.
    // R1~R4 를 어긴 공격과 R2 를 어긴 몸마다 한 줄: "<파일>:<JSON 위치>: <누구> 의 공격 <열쇠>: R<n> 위반: <값과 식>"
    // 지키지 못하는 공격은 넣지 않는다(spec 05 1.2 "데이터 로드 시 실패 처리"). R5 는 런타임 검사(spec 05 3.5)라 여기서 보지 않는다.
    public sealed class EvasionValidator : IDataValidator
    {
        public string Name => "evasion";

        public void Validate(DataSet data, Diagnostics diagnostics)
        {
            var input = new EvasionInput();
            foreach (IEvasionSource s in Sources())
            {
                s.Collect(data, input, diagnostics);
            }
            if (input.Attacks.Count == 0 && input.Movers.Count == 0)
            {
                return;
            }
            if (EvasionBasis.HasCharactersFile(data) && EvasionBasis.Read(data) == null)
            {
                diagnostics.Error(DataLoader.DataDir + "/" + EvasionBasis.CharactersFile,
                    "회피 검산(spec 05 10절)의 기준 속도 V(가장 느린 자세 속도)와 피격 반지름을 구할 캐릭터 줄(characters 또는 baseline 의 stance_speed·hit_radius)이 없다");
                return;
            }
            Report(input, EvasionBasis.Read(data), diagnostics);
        }

        // basis 가 null 이면(data/ 에 캐릭터 파일이 없다: 적 절만 둔 변환기 시험의 임시 저장소) V 에 기대는 R1·R2 는 볼 수 없어
        // V 에 기대지 않는 줄(R3·R4·면제 표지)만 본다. 캐릭터 파일이 있는데 V 를 구할 줄이 없으면 실패한다.
        // 저장소의 data/ 에는 캐릭터 파일이 늘 있다(시험 RepoData_HasEvasionBasis 가 지킨다).
        public static void Report(EvasionInput input, EvasionBasis? basis, Diagnostics diagnostics)
        {
            foreach (EvasionAttack a in input.Attacks)
            {
                List<EvasionViolation> found = basis == null
                    ? EvasionCheck.CheckAttackWithoutBasis(a)
                    : EvasionCheck.CheckAttack(a, basis.SpeedMilli, basis.HitMilli);
                foreach (EvasionViolation v in found)
                {
                    diagnostics.Error(a.File, a.Pointer, a.Label + ": " + v);
                }
            }
            if (basis == null)
            {
                return;
            }
            foreach (EvasionMover m in input.Movers)
            {
                foreach (EvasionViolation v in EvasionCheck.CheckMover(m, basis.SpeedMilli))
                {
                    diagnostics.Error(m.File, m.Pointer, m.Owner + ": " + v);
                }
            }
        }

        // 이 어셈블리의 IEvasionSource(형식 이름의 서수 오름차순)
        public static IReadOnlyList<IEvasionSource> Sources()
        {
            var types = new List<Type>(typeof(EvasionValidator).Assembly.GetTypes());
            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            var list = new List<IEvasionSource>();
            foreach (Type t in types)
            {
                if (t.IsAbstract || t.IsInterface || t.IsGenericTypeDefinition || !typeof(IEvasionSource).IsAssignableFrom(t))
                {
                    continue;
                }
                if (t.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new DataBuildException(t.FullName + ": 회피 검산 원본에는 공개 매개변수 없는 생성자가 있어야 한다");
                }
                list.Add((IEvasionSource)Activator.CreateInstance(t)!);
            }
            return list;
        }
    }
}

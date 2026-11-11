using System;
using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Vectors
{
    // 명세 묶음을 훑은 결과: 벡터 목록(명세 번호, 줄 순서)과 문제 목록.
    public sealed class SpecVectorScan
    {
        public SpecVectorScan(IReadOnlyList<SpecVector> vectors, IReadOnlyList<string> problems)
        {
            Vectors = vectors;
            Problems = problems;
        }

        public IReadOnlyList<SpecVector> Vectors { get; }

        // 알아볼 수 없는 벡터 줄, 꼴이 틀린 id, 중복, 절 밖의 벡터, 벡터가 없는 명세 …
        public IReadOnlyList<string> Problems { get; }

        public List<SpecVector> InSpec(string spec)
        {
            var list = new List<SpecVector>();
            foreach (SpecVector v in Vectors)
            {
                if (string.Equals(v.Spec, spec, StringComparison.Ordinal))
                {
                    list.Add(v);
                }
            }
            return list;
        }

        public SpecVector? Find(string spec, string id)
        {
            foreach (SpecVector v in Vectors)
            {
                if (string.Equals(v.Spec, spec, StringComparison.Ordinal) && string.Equals(v.Id, id, StringComparison.Ordinal))
                {
                    return v;
                }
            }
            return null;
        }
    }
}

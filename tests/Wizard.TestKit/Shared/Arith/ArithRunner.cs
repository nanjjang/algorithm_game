using System;
using System.Collections.Generic;

namespace Wizard.TestKit.Shared.Arith
{
    // 산술 묶음을 지금 런타임에서 돌려 결과를 모은다. 판정(기대와 같은가, 조합끼리 같은가)은 따로 한다.
    public static class ArithRunner
    {
        public static List<ArithCaseResult> Run(ArithBundle bundle, IArithOps ops)
        {
            var results = new List<ArithCaseResult>(bundle.Cases.Count);
            foreach (ArithCase c in bundle.Cases)
            {
                results.Add(RunOne(bundle.Name, c, ops));
            }
            return results;
        }

        public static ArithCaseResult RunOne(string bundleName, ArithCase c, IArithOps ops)
        {
            var values = new List<ArithValue>();
            try
            {
                if (!ops.TryEvaluate(c.Op, c.Args, values))
                {
                    return new ArithCaseResult(bundleName, c.Id, null, ArithFaults.UnknownOp);
                }
            }
            catch (Exception ex)
            {
                return new ArithCaseResult(bundleName, c.Id, null, ArithFaults.Classify(ex));
            }
            return new ArithCaseResult(bundleName, c.Id, values.ToArray(), null);
        }
    }
}

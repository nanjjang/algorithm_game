using System;
using System.Security.Cryptography;
using Wizard.BalanceLab.Core;
using Wizard.Content;
using Wizard.Sim;

namespace Wizard.BalanceLab.Feel
{
    // .wzrc 바이트 → 재실행 → 손맛 세 축의 세어 놓은 값. 명령(FeelCli)과 자체 점검(FeelSelfCheck)이 같은 길을 쓴다
    internal static class FeelRecordAnalysis
    {
        public static bool TryAnalyze(ContentDb db, byte[] file, string fileName, FeelOptions options, bool skipHashCheck,
                                      out FeelRecordInfo info, out FeelResult result, out string error)
        {
            info = new FeelRecordInfo();
            result = new FeelResult();
            if (!WzrcTrace.TryLoad(file, out WzrcTrace? trace, out error) || trace == null)
            {
                return false;
            }
            if (trace.Header.Setup.DataVersion != db.DataVersion && !skipHashCheck)
            {
                error = "the record was made with data_version " + trace.Header.Setup.DataVersion
                        + " but this checkout builds " + db.DataVersion + " (pass --skip-hash-check to analyze anyway)";
                return false;
            }
            ReplayRun run = trace.Replay(db);
            info = new FeelRecordInfo
            {
                FileName = fileName,
                Bytes = file.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(),
                EndTick = trace.EndTick,
                InputCount = trace.Inputs.Count,
                CommandCount = trace.Commands.Count,
                PauseCount = trace.PauseCount,
                HashMatches = run.HashMatches,
                HashCheckSkipped = skipHashCheck,
                RejectedCommands = run.RejectedCommands,
                DataVersion = trace.Header.Setup.DataVersion,
                CoreVersion = trace.Header.Setup.CoreVersion,
            };
            if (!run.HashMatches && !skipHashCheck)
            {
                error = "replaying the record in a practice world did not reproduce its final_state_hash: the record was not made by a "
                        + "practice world of this core and pack, so the measurements would not describe the play (pass --skip-hash-check to analyze anyway)";
                return false;
            }
            result = FeelAnalyzer.Analyze(run.Log.Events, trace.Inputs, trace.EndTick, options);
            error = string.Empty;
            return true;
        }
    }
}

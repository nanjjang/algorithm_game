#nullable enable
using System;
using System.IO;
using UnityEngine;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.Unity.App.Headless
{
    // 플레이어의 머리 없는 골든 실행 입구(ARCHITECTURE 4.3 Headless, 11절, T-30, T-39).
    //   Wizard.app/Contents/MacOS/<실행 파일> -batchmode -nographics -golden <tests/golden> [-goldenPack …] [-goldenOut …] [-goldenCombo …] -logFile <로그>
    // 첫 장면을 읽기 전에 골든을 전부 돌리고 결과 파일을 쓰고 Application.Quit(종료 코드)로 끝낸다(코드는 HeadlessGoldenRun).
    // -golden 이 없으면 아무것도 하지 않는다(보통의 실행). 편집기에서는 돌지 않는다(편집기의 열은 EditMode 시험이 낸다).
    // 이 입구는 출시 빌드에도 들어간다: 배포하는 조합은 그 빌드에서 골든을 통과해야 배포할 수 있다(T-39).
    public static class HeadlessGolden
    {
        public const int ExitCrashed = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunIfRequested()
        {
            if (Application.isEditor)
            {
                return;
            }
            string[] argv = Environment.GetCommandLineArgs();
            if (!HeadlessArgs.IsRequested(argv))
            {
                return;
            }
            int code;
            var log = new StringWriter();
            try
            {
                if (HeadlessArgs.TryParse(argv, out HeadlessArgs? args, out string error) && args != null)
                {
                    code = HeadlessGoldenRun.Execute(args, RuntimeCombo.Detect(), log);
                }
                else
                {
                    log.WriteLine("golden: " + error);
                    code = HeadlessGoldenRun.ExitUsage;
                }
            }
            catch (Exception ex)
            {
                log.WriteLine("golden: crashed: " + ex);
                code = ExitCrashed;
            }
            string text = log.ToString();
            if (code == HeadlessGoldenRun.ExitOk)
            {
                Debug.Log(text);
            }
            else
            {
                Debug.LogError(text);
            }
            Debug.Log("golden: exit " + code);
            Application.Quit(code);
        }
    }
}

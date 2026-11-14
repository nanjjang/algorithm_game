using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wizard.Tools.Common
{
    // tools/ 의 명령 줄 도구의 표준 출력·표준 오류를 BOM 없는 UTF-8, 줄 끝 '\n' 으로 고정한다(DECISIONS-TECH T-67 5번).
    // Windows 에서 .NET 의 Console.WriteLine 은 "\r\n" 을 내고 출력 인코딩은 콘솔 코드 페이지(949)라서, 스크립트와 실행기가 줄로 나눠 읽으면
    // 값 끝에 '\r' 이 붙고 한글이 깨진다. 맥에서는 바뀌는 것이 없다.
    // 이 파일은 Directory.Build.targets 가 도구 묶음(WizardGroup=tools)의 실행 파일 프로젝트에 끼워 넣는다. 도구 폴더를 고치지 않고 전부 걸린다.
    // 모듈 초기화기는 그 어셈블리가 프로세스의 진입 어셈블리일 때만 콘솔을 바꾼다(테스트 호스트나 다른 프로그램이 이 어셈블리를 읽을 때는 아무것도 하지 않는다).
    internal static class ConsoleSetup
    {
        [ModuleInitializer]
        internal static void Init()
        {
            if (System.Reflection.Assembly.GetEntryAssembly() != typeof(ConsoleSetup).Assembly)
            {
                return;
            }
            Apply();
        }

        internal static void Apply()
        {
            var utf8 = new UTF8Encoding(false);
            try
            {
                // 콘솔 창에 붙어 있을 때 창의 코드 페이지도 맞춘다. 관으로 받을 때는 아래의 쓰개가 정한다
                Console.OutputEncoding = utf8;
            }
            catch (IOException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true, NewLine = "\n" });
        }
    }
}

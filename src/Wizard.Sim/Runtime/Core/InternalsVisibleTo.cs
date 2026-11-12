using System.Runtime.CompilerServices;

// 뼈대의 내부 도우미(StateSection, 풀 절의 틀, 보관 난수 흐름)를 모듈 테스트가 직접 시험한다.
// 그 밖의 어셈블리에는 열지 않는다(모듈은 같은 어셈블리의 부분 클래스다)
[assembly: InternalsVisibleTo("Wizard.Sim.Tests")]

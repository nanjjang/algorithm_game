namespace Wizard.Ci
{
    // 판정이 OS 에 따라 갈리는 곳(Requirements)이 받는 OS. 판정 함수는 이 값을 인자로 받아 시험이 한 기기에서 두 OS 의 가지를 다 본다(T-67 1·7번).
    public enum HostOs
    {
        MacOS = 0,
        Windows = 1,
        Linux = 2,
    }
}

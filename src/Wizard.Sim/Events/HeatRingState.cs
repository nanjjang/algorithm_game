namespace Wizard.Sim.Events
{
    // spec 01 17.1 열 링 상태(ExemptArmed 는 PlayerView 의 따로 있는 칸)
    public enum HeatRingState : byte
    {
        Cold = 0,
        Rising = 1,
        Imminent = 2,
        Critical = 3,
        Brink = 4,
        Locked = 5,
    }
}

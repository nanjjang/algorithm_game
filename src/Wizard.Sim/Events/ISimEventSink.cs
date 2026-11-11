namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3
    public interface ISimEventSink
    {
        void Emit(in SimEvent e);
    }
}

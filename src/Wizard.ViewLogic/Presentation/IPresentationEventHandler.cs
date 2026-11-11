using Wizard.Netcode.Duel;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13
    public interface IPresentationEventHandler
    {
        void Handle(in PresentationEvent e, ISimView view);
    }
}

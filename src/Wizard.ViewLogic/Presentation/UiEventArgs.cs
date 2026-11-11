using Wizard.Content;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13
    public readonly struct UiEventArgs
    {
        public readonly int A, B;
        public readonly string? Id;
        public readonly Element Element;
        
        public UiEventArgs(int a, int b, string? id = null, Element element = Element.None)
        {
        A = a;
        B = b;
        Id = id;
        Element = element;
        }
    }
}

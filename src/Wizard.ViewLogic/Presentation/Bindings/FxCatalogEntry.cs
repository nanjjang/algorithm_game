namespace Wizard.ViewLogic.Presentation
{
    // event_bindings.json 의 fx_catalog 한 항목. Src = "<effects.md 20절의 소절>#<그 표의 몸통 줄 번호>"(예: "20.3#4" = 잠금 X)
    public readonly struct FxCatalogEntry
    {
        public FxCatalogEntry(string fx, string src)
        {
            Fx = fx;
            Src = src;
        }

        public string Fx { get; }

        public string Src { get; }
    }
}

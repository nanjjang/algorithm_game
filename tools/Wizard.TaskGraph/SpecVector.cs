namespace Wizard.TaskGraph
{
    public sealed class SpecVector
    {
        public SpecVector(string spec, string section, string id)
        {
            Spec = spec;
            Section = section;
            Id = id;
        }

        public string Spec { get; }
        public string Section { get; }
        public string Id { get; }

        public string Key => "S" + Spec + ":" + Id;
    }
}

namespace Wizard.DataBuild.Validate.Evasion
{
    // 회피 규칙 하나를 어긴 것. Rule 은 "R1" … "R4", "exempt"
    public sealed class EvasionViolation
    {
        public EvasionViolation(string rule, string message)
        {
            Rule = rule;
            Message = message;
        }

        public string Rule { get; }

        public string Message { get; }

        public override string ToString() => Rule + " 위반: " + Message;
    }
}

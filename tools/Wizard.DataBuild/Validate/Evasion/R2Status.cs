namespace Wizard.DataBuild.Validate.Evasion
{
    // R2 판정(spec 05 10.5)
    public enum R2Status
    {
        Pass = 0,
        Exempt = 1,     // 몸 접촉이 없다(늑대: 예고 돌진으로만 공격, spec 05 1.2 보충 6)
        Fail = 2,
    }
}

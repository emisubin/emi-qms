namespace Emi.Qms.Api.Workflow;

public sealed class DepartmentHeadRequiredException(string departmentCode)
    : InvalidOperationException($"{DepartmentLabel(departmentCode)} 부서에 활성 부서장이 없어 업무를 생성할 수 없습니다. 사용자 관리에서 해당 부서장을 먼저 지정해 주세요.")
{
    public string DepartmentCode { get; } = departmentCode;

    private static string DepartmentLabel(string code)
    {
        return code switch
        {
            "sales" => "영업",
            "design" => "설계",
            "production-planning" => "생산관리",
            "procurement" => "구매",
            "materials" => "자재",
            "manufacturing" => "제조",
            "quality" => "품질",
            "logistics" => "물류",
            _ => code
        };
    }
}

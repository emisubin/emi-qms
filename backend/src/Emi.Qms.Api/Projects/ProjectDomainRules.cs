namespace Emi.Qms.Api.Projects;

public static class ProjectDomainRules
{
    public const int MaxPanelsPerProject = 500;

    public const string PanelCountConcurrencyMessage =
        "다른 사용자가 프로젝트 면수를 변경했습니다. 화면을 새로고침한 후 다시 시도해 주세요.";
}

namespace Emi.Qms.Api.Identity;

public static class QmsPermissions
{
    public const string ProjectRead = "projects.read";
    public const string ProjectManage = "projects.manage";
    // Legacy TASK-002 permission kept for 0001 migration compatibility.
    // New endpoint policies must use ProjectReadAll for full project read scope.
    public const string ProjectAccessAll = "projects.access.all";
    public const string ProjectReadAll = "Project.Read.All";
    public const string ProjectCreate = "Project.Create";
    public const string ProjectUpdate = "Project.Update";
    public const string ProjectHold = "Project.Hold";
    public const string ProjectCancel = "Project.Cancel";
    public const string ProjectDelete = "Project.Delete";
    public const string ProjectDeletedRead = "Project.Deleted.Read";
    public const string ProjectSalesAmountRead = "Project.SalesAmount.Read";
    public const string PanelInfoUpdate = "PanelInfo.Update";
    public const string ProductionPlan = "production.plan";
    public const string ManufacturingUpdate = "manufacturing.update";
    public const string ManufacturingWorkTimeRead = "Manufacturing.WorkTime.Read";
    public const string QualityInspect = "quality.inspect";
    public const string QualityApprove = "quality.approve";
    public const string LogisticsShip = "logistics.ship";
    public const string UsersManage = "users.manage";
}

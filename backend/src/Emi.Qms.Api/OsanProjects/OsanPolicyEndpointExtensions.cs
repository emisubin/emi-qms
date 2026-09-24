using System.Security.Claims;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;

namespace Emi.Qms.Api.OsanProjects;

public sealed record OsanCustomerNameRequest(string? Name, long ExpectedVersion = 0);
public sealed record OsanCustomerAssignmentsRequest(IReadOnlyList<Guid>? CustomerIds, long ExpectedVersion);
public sealed record OsanGatesRequest(IReadOnlyList<OsanGateUpdate>? Gates, long ExpectedVersion);

public static class OsanPolicyEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var api=app.MapGroup("/api/osan").RequireAuthorization();
        api.MapGet("/customers", async (string? query, OsanPolicyStore store,
            DatabaseConnectionStringProvider db, ClaimsPrincipal user,CancellationToken ct) =>
        {
            var denied=Guard(db,user,false);
            return denied ?? Results.Ok(new {items=await store.CustomersAsync(query,ct)});
        }).WithName("ListOsanCustomers");
        api.MapGet("/admin/customers", async (OsanPolicyStore store,
            DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct) =>
        {
            var denied=Guard(db,user,true);
            return denied ?? Results.Ok(new {items=await store.CustomersAsync(null,ct)});
        }).WithName("ListOsanAdminCustomers");
        api.MapPost("/admin/customers", async (OsanCustomerNameRequest request,OsanPolicyStore store,
            DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct) =>
        {
            var denied=Guard(db,user,true);
            return denied ?? Result(await store.CreateCustomerAsync(request.Name,ct));
        }).WithName("CreateOsanCustomer");
        api.MapPut("/admin/customers/{customerId:guid}",async(Guid customerId,OsanCustomerNameRequest request,
            OsanPolicyStore store,DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Result(await store.RenameCustomerAsync(customerId,request.Name,request.ExpectedVersion,ct));
        }).WithName("RenameOsanCustomer");
        api.MapDelete("/admin/customers/{customerId:guid}",async(Guid customerId,long expectedVersion,
            OsanPolicyStore store,DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Result(await store.ArchiveCustomerAsync(customerId,expectedVersion,ct));
        }).WithName("ArchiveOsanCustomer");
        api.MapGet("/admin/customer-assignments",async(OsanPolicyStore store,
            DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            if(denied is not null)return denied;
            var customers=await store.CustomersAsync(null,ct);
            var users=await store.AssignmentUsersAsync(ct);
            return Results.Ok(new {customers,users});
        }).WithName("ListOsanCustomerAssignments");
        api.MapPut("/admin/customer-assignments/{userId:guid}",async(Guid userId,
            OsanCustomerAssignmentsRequest request,OsanPolicyStore store,DatabaseConnectionStringProvider db,
            ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Result(await store.AssignAsync(userId,request.CustomerIds,request.ExpectedVersion,ct));
        }).WithName("SetOsanCustomerAssignments");
        api.MapGet("/admin/gates",async(OsanPolicyStore store,DatabaseConnectionStringProvider db,
            ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Results.Ok(await store.GatesAsync(ct));
        }).WithName("GetOsanGateConfiguration");
        api.MapPut("/admin/gates",async(OsanGatesRequest request,OsanPolicyStore store,
            DatabaseConnectionStringProvider db,ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Result(await store.SetGatesAsync(request.Gates,request.ExpectedVersion,ct));
        }).WithName("SetOsanGateConfiguration");
        api.MapGet("/gate-approvals",async(OsanPolicyStore store,DatabaseConnectionStringProvider db,
            ClaimsPrincipal user,CancellationToken ct)=>
        {
            var denied=Guard(db,user,true);
            return denied ?? Results.Ok(new {items=await store.PendingApprovalsAsync(ct)});
        }).WithName("ListOsanGateApprovals");
        return app;
    }

    private static IResult? Guard(DatabaseConnectionStringProvider db,ClaimsPrincipal user,bool admin)
    {
        if(!string.Equals(db.GetCurrentBusinessUnit()?.Code,BusinessUnitCodes.Osan,StringComparison.Ordinal))
            return Results.Json(new OsanProjectErrorResponse("business_unit_capability_disabled",
                "오산 사업부를 선택해 주세요."),statusCode:403);
        if(ProjectEndpointExtensions.GetCurrentUserId(user) is null)return Results.Unauthorized();
        if(admin)return user.IsInRole(QmsRoles.SystemAdministrator)?null:Results.Forbid();
        return ProjectEndpointExtensions.HasPermission(user,QmsPermissions.ProjectRead)?null:Results.Forbid();
    }

    private static IResult Result(OsanPolicyWriteResult result) => result.Status switch
    {
        200 => Results.Ok(result.Value),
        201 => Results.Json(result.Value,statusCode:201),
        _ => Results.Json(new OsanProjectErrorResponse(result.Code??"osan_policy_rejected",
            result.Message??"요청 내용을 확인해 주세요."),statusCode:result.Status)
    };
}

namespace Emi.Qms.Api.Identity;

public interface IUserAdministrationStore
{
    Task<UserAdministrationSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<UserAdministrationMutationResult> UpdateEntraUserAsync(
        Guid userId,
        UpdateUserAdministrationRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);
}

public sealed record UserAdministrationSnapshot(
    IReadOnlyList<UserAdministrationUser> Users,
    IReadOnlyList<Department> Departments,
    IReadOnlyList<Role> Roles);

public sealed record UserAdministrationUser(
    Guid UserId,
    string DevelopmentUserKey,
    string DisplayName,
    string? Email,
    string AuthProvider,
    bool IsActive,
    bool ApprovalPending,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<string> Roles,
    bool IsReadOnly);

public sealed record UpdateUserAdministrationRequest(
    Guid? DepartmentId,
    IReadOnlyList<string> RoleCodes,
    bool IsActive);

public sealed record UserAdministrationMutationResult(
    bool Succeeded,
    string? ErrorMessage,
    UserAdministrationSnapshot? Snapshot)
{
    public static UserAdministrationMutationResult Success(UserAdministrationSnapshot snapshot)
    {
        return new UserAdministrationMutationResult(true, null, snapshot);
    }

    public static UserAdministrationMutationResult Failure(string errorMessage)
    {
        return new UserAdministrationMutationResult(false, errorMessage, null);
    }
}

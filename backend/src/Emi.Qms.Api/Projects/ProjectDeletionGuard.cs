namespace Emi.Qms.Api.Projects;

using Npgsql;

public interface IProjectDeletionGuard
{
    Task<ProjectDeletionGuardResult> CanDeleteAsync(
        ProjectDeletionContext context,
        CancellationToken cancellationToken);
}

public sealed class ProjectDeletionGuard : IProjectDeletionGuard
{
    public Task<ProjectDeletionGuardResult> CanDeleteAsync(
        ProjectDeletionContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ProjectDeletionGuardResult.Allowed());
    }
}

public sealed record ProjectDeletionContext(
    Guid ProjectId,
    string Status,
    string ProjectTitle,
    string ProjectTitleNormalized,
    string? PackagingMethod,
    NpgsqlConnection Connection,
    NpgsqlTransaction Transaction,
    string CorrelationId);

public sealed record ProjectDeletionGuardResult(bool IsAllowed, string? Message, string? InternalReasonCode = null)
{
    public static ProjectDeletionGuardResult Allowed()
    {
        return new ProjectDeletionGuardResult(true, null, null);
    }

    public static ProjectDeletionGuardResult Blocked(string? internalReasonCode = null)
    {
        return new ProjectDeletionGuardResult(
            false,
            "후속 업무이력이 존재하는 프로젝트는 삭제할 수 없습니다. 프로젝트 취소 기능을 사용해 주세요.",
            internalReasonCode);
    }
}

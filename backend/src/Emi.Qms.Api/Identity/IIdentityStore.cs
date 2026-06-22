namespace Emi.Qms.Api.Identity;

public interface IIdentityStore
{
    Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
        string developmentUserKey,
        CancellationToken cancellationToken);

    Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken);
}

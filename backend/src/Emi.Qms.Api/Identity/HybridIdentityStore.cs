namespace Emi.Qms.Api.Identity;

public sealed class HybridIdentityStore(
    InMemoryIdentityStore developmentStore,
    DbIdentityStore dbStore)
    : IIdentityStore
{
    public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
        string developmentUserKey,
        CancellationToken cancellationToken)
    {
        return developmentStore.GetProfileByDevelopmentUserKeyAsync(developmentUserKey, cancellationToken);
    }

    public async Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await developmentStore.GetProfileByUserIdAsync(userId, cancellationToken)
            ?? await dbStore.GetProfileByUserIdAsync(userId, cancellationToken);
    }

    public async Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken)
    {
        return await developmentStore.GetProjectByKeyAsync(projectKey, cancellationToken)
            ?? await dbStore.GetProjectByKeyAsync(projectKey, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var developmentUsers = await developmentStore.GetUsersAsync(cancellationToken);
        var dbUsers = await dbStore.GetUsersAsync(cancellationToken);
        return developmentUsers.Concat(dbUsers)
            .OrderBy(user => user.AuthProvider, StringComparer.Ordinal)
            .ThenBy(user => user.DisplayName, StringComparer.Ordinal)
            .ToList();
    }
}

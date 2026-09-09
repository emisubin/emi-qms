namespace Emi.Qms.Api.Identity;

public sealed class HybridIdentityStore : IIdentityStore
{
    private readonly InMemoryIdentityStore developmentStore;
    private readonly DbIdentityStore dbStore;
    private readonly bool businessUnitsEnabled;

    public HybridIdentityStore(InMemoryIdentityStore developmentStore, DbIdentityStore dbStore)
        : this(developmentStore, dbStore, null)
    {
    }

    public HybridIdentityStore(
        InMemoryIdentityStore developmentStore,
        DbIdentityStore dbStore,
        IConfiguration? configuration)
    {
        this.developmentStore = developmentStore;
        this.dbStore = dbStore;
        businessUnitsEnabled = configuration is not null
            && BusinessUnits.BusinessUnitConfiguration.Read(configuration).Enabled;
    }

    public Task<UserAuthorizationProfile?> GetProfileByDevelopmentUserKeyAsync(
        string developmentUserKey,
        CancellationToken cancellationToken)
    {
        return businessUnitsEnabled
            ? dbStore.GetProfileByDevelopmentUserKeyAsync(developmentUserKey, cancellationToken)
            : developmentStore.GetProfileByDevelopmentUserKeyAsync(developmentUserKey, cancellationToken);
    }

    public async Task<UserAuthorizationProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return businessUnitsEnabled
            ? await dbStore.GetProfileByUserIdAsync(userId, cancellationToken)
            : await developmentStore.GetProfileByUserIdAsync(userId, cancellationToken)
                ?? await dbStore.GetProfileByUserIdAsync(userId, cancellationToken);
    }

    public async Task<QmsProject?> GetProjectByKeyAsync(string projectKey, CancellationToken cancellationToken)
    {
        return businessUnitsEnabled
            ? await dbStore.GetProjectByKeyAsync(projectKey, cancellationToken)
            : await developmentStore.GetProjectByKeyAsync(projectKey, cancellationToken)
                ?? await dbStore.GetProjectByKeyAsync(projectKey, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var dbUsers = await dbStore.GetUsersAsync(cancellationToken);
        if (businessUnitsEnabled)
        {
            return dbUsers;
        }
        var developmentUsers = await developmentStore.GetUsersAsync(cancellationToken);
        return developmentUsers.Concat(dbUsers)
            .OrderBy(user => user.AuthProvider, StringComparer.Ordinal)
            .ThenBy(user => user.DisplayName, StringComparer.Ordinal)
            .ToList();
    }
}

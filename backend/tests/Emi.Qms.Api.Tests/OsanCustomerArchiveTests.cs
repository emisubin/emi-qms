using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task CustomerArchive_PreservesProjectHistoryAndAssignmentsButRejectsNewBindings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration).ApplyAndVerifyAsync(ct);
        await SeedDefaultCustomerAsync(database, ct);
        await database.ExecuteAsync($"""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values('{UserId:D}','archive-test','Synthetic Archive User',true);
            """, ct);
        var policy = new OsanPolicyStore(provider);
        var customer = Assert.Single(await policy.CustomersAsync(null, ct));
        var store = new OsanProjectStore(provider);
        var created = await store.CreateAsync(Normalize(ValidRequest(projectCode: "ARCHIVE-EXISTING")), UserId, ct);
        Assert.Equal(OsanProjectCreateStatus.Success, created.Status);
        var projectId = created.Value!.Project.ProjectId;
        var before = await store.GetAsync(projectId, ct);
        var events = await database.ReadScalarAsync<long>("select count(*) from osan_project_events", ct);
        Assert.Equal(400, (await policy.ArchiveCustomerAsync(customer.CustomerId, 0, ct)).Status);
        Assert.Equal(409, (await policy.ArchiveCustomerAsync(customer.CustomerId, customer.Version + 1, ct)).Status);
        Assert.Single(await policy.CustomersAsync(null, ct));

        var archived = await policy.ArchiveCustomerAsync(customer.CustomerId, customer.Version, ct);
        Assert.Equal(200, archived.Status);
        Assert.Equal(customer.Version + 1, Assert.IsType<OsanCustomer>(archived.Value).Version);
        Assert.Empty(await policy.CustomersAsync(null, ct));
        Assert.Empty(await policy.CustomersAsync("Customer", ct));
        var after = await store.GetAsync(projectId, ct);
        Assert.NotNull(after);
        Assert.Equal(before!.CustomerId, after.CustomerId);
        Assert.Equal(before.CustomerName, after.CustomerName);
        Assert.Equal(before.Targets.Count, after.Targets.Count);
        Assert.Equal(events, await database.ReadScalarAsync<long>("select count(*) from osan_project_events", ct));
        Assert.Equal(409, (await policy.RenameCustomerAsync(customer.CustomerId, "Changed", customer.Version + 1, ct)).Status);
        Assert.Equal(409, (await policy.ArchiveCustomerAsync(customer.CustomerId, customer.Version + 1, ct)).Status);
        Assert.Equal(OsanProjectCreateStatus.CustomerInvalid,
            (await store.CreateAsync(Normalize(ValidRequest(projectCode: "ARCHIVE-NEW")), UserId, ct)).Status);
        Assert.Equal(1L, await database.ReadScalarAsync<long>("select count(*) from projects where project_profile='Osan'", ct));

        var edited = await store.ManageAsync(projectId, OsanProjectStore.EditToken(after),
            Normalize(ValidRequest(projectCode: "ARCHIVE-EXISTING") with { Title = "Updated retained project" }),
            null, UserId, ct);
        Assert.Equal(200, edited.Status);
        Assert.Equal(customer.CustomerId, (await store.GetAsync(projectId, ct))!.CustomerId);

        var assignment = Assert.Single(await policy.AssignmentUsersAsync(ct), user => user.UserId == UserId);
        Assert.Empty(assignment.CustomerIds);
        Assert.Equal(400, (await policy.AssignAsync(UserId, [customer.CustomerId], assignment.Version, ct)).Status);
        Assert.Equal(200, (await policy.AssignAsync(UserId, [], assignment.Version, ct)).Status);
        Assert.Equal(1L, await database.ReadScalarAsync<long>(
            "select count(*) from osan_customer_assignments where user_id=@user and customer_id=@customer", ct,
            ("user", UserId), ("customer", customer.CustomerId)));
        await database.ExecuteAsync("""
            insert into qms_users(id,development_user_key,display_name,is_active)
            values(uuid_generate_v4(),'after-archive-test','Synthetic New User',true);
            """, ct);
        Assert.Equal(0L, await database.ReadScalarAsync<long>("""
            select count(*) from osan_customer_assignments a join qms_users u on u.id=a.user_id
            where u.development_user_key='after-archive-test'
            """, ct));
    }

    [Fact]
    public async Task CustomerArchive_CompetingRequestsHaveOnlyOneWinner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(ct);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration).ApplyAndVerifyAsync(ct);
        var policy = new OsanPolicyStore(provider);
        var customer = Assert.IsType<OsanCustomer>((await policy.CreateCustomerAsync("Synthetic Race", ct)).Value);
        var results = await Task.WhenAll(
            policy.ArchiveCustomerAsync(customer.CustomerId, customer.Version, ct),
            policy.ArchiveCustomerAsync(customer.CustomerId, customer.Version, ct));
        Assert.Equal([200, 409], results.Select(result => result.Status).Order());
        Assert.Equal(2L, await database.ReadScalarAsync<long>("select version from osan_customers where id=@id", ct,
            ("id", customer.CustomerId)));
    }
}

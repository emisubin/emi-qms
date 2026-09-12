using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Projects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class OsanProjectRegistrationApiTests
{
    [Fact]
    public async Task RelatedPanels_UsesExactNonBlankWorkOrderAndProjectAccessScope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PostgreSqlTestDatabase.CreateAsync(cancellationToken);
        var configuration = database.CreateConfiguration();
        var provider = new DatabaseConnectionStringProvider(configuration);
        await CreateMigrationRunner(database.RepositoryRoot, provider, configuration)
            .ApplyAndVerifyAsync(cancellationToken);
        await database.ExecuteAsync(
            "insert into qms_users(id,development_user_key,display_name,is_active) values(@actor,'related-panels','Related Panels',true);",
            cancellationToken,
            ("actor", UserId));

        var projects = new OsanProjectStore(provider);
        var source = (await projects.CreateAsync(
            Normalize(ValidRequest(
                title: "Source",
                projectCode: "RELATED-SOURCE",
                workOrderNumber: "WO-001",
                quantity: 2)),
            UserId,
            cancellationToken)).Value!.Project;
        var accessibleMatch = (await projects.CreateAsync(
            Normalize(ValidRequest(
                title: "Accessible match",
                projectCode: "RELATED-ACCESSIBLE",
                workOrderNumber: "WO-001",
                quantity: 1)),
            UserId,
            cancellationToken)).Value!.Project;
        var hiddenMatch = (await projects.CreateAsync(
            Normalize(ValidRequest(
                title: "Hidden match",
                projectCode: "RELATED-HIDDEN",
                workOrderNumber: "WO-001",
                quantity: 1)),
            UserId,
            cancellationToken)).Value!.Project;
        var caseMismatch = (await projects.CreateAsync(
            Normalize(ValidRequest(
                title: "Case mismatch",
                projectCode: "RELATED-CASE",
                workOrderNumber: "wo-001",
                quantity: 1)),
            UserId,
            cancellationToken)).Value!.Project;
        var blank = (await projects.CreateAsync(
            Normalize(ValidRequest(
                title: "Blank",
                projectCode: "RELATED-BLANK",
                workOrderNumber: null,
                quantity: 1)),
            UserId,
            cancellationToken)).Value!.Project;

        var store = new OsanProgressStore(provider);
        var scope = new ProjectAccessScope(
            false,
            [
                $"osan-{source.ProjectId:N}",
                $"osan-{accessibleMatch.ProjectId:N}",
                $"osan-{caseMismatch.ProjectId:N}"
            ]);
        var related = await store.ListRelatedPanelsAsync(
            source.ProjectId,
            scope,
            cancellationToken);

        Assert.NotNull(related);
        Assert.Equal("WO-001", related.WorkOrderNumber);
        Assert.Equal(3, related.Panels.Count);
        Assert.Equal(2, related.Panels.Count(panel => panel.ProjectId == source.ProjectId));
        Assert.Single(related.Panels, panel => panel.ProjectId == accessibleMatch.ProjectId);
        Assert.DoesNotContain(related.Panels, panel => panel.ProjectId == hiddenMatch.ProjectId);
        Assert.DoesNotContain(related.Panels, panel => panel.ProjectId == caseMismatch.ProjectId);

        var blankRelated = await store.ListRelatedPanelsAsync(
            blank.ProjectId,
            new ProjectAccessScope(true, []),
            cancellationToken);
        Assert.NotNull(blankRelated);
        Assert.Null(blankRelated.WorkOrderNumber);
        Assert.Empty(blankRelated.Panels);
    }
}

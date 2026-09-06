using Emi.Qms.Api.ReviewSafe;
using Npgsql;

namespace Emi.Qms.Api.BusinessUnits;

public sealed class BusinessUnitResolver(
    DatabaseConnectionStringProvider connectionStringProvider,
    BusinessUnitDirectoryStore directoryStore,
    BusinessUnitDatabaseBoundaryValidator boundaryValidator)
{
    public bool IsEnabled => connectionStringProvider.BusinessUnits.Enabled;

    public async Task<BusinessUnitRequestContext> ResolveAsync(
        HttpContext httpContext,
        string authProvider,
        string externalSubject,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new BusinessUnitRequestContext(
                BusinessUnitAccessStatuses.Selected,
                null,
                connectionStringProvider.BusinessUnits.Businesses.Single(),
                [BusinessUnitCodes.Cheongju],
                false,
                "legacy_single_database");
        }

        var directory = await directoryStore.FindAsync(authProvider, externalSubject, cancellationToken);
        var requested = ReadRequestedBusinessUnit(httpContext.Request);
        if (directory is null || directory.ActiveMemberships.Count == 0)
        {
            return new BusinessUnitRequestContext(
                BusinessUnitAccessStatuses.NoMembership,
                directory?.UserId,
                null,
                [],
                directory?.IsOverallAdministrator == true,
                "directory_membership_required");
        }

        if (requested.Invalid)
        {
            return Denied(directory, "business_unit_selector_invalid");
        }

        if (requested.Code is null)
        {
            if (directory.ActiveMemberships.Count != 1)
            {
                return new BusinessUnitRequestContext(
                    BusinessUnitAccessStatuses.SelectionRequired,
                    directory.UserId,
                    null,
                    directory.ActiveMemberships,
                    directory.IsOverallAdministrator,
                    "business_unit_selection_required");
            }

            return await SelectedAsync(directory, directory.ActiveMemberships[0], cancellationToken);
        }

        if (!directory.ActiveMemberships.Contains(requested.Code, StringComparer.Ordinal))
        {
            return Denied(directory, "business_unit_membership_denied");
        }

        if (directory.ActiveMemberships.Count > 1 && !directory.IsOverallAdministrator)
        {
            return Denied(directory, "business_unit_alternate_selection_denied");
        }

        return await SelectedAsync(directory, requested.Code, cancellationToken);
    }

    private async Task<BusinessUnitRequestContext> SelectedAsync(
        BusinessUnitDirectorySnapshot directory,
        string code,
        CancellationToken cancellationToken)
    {
        var target = connectionStringProvider.BusinessUnits.GetBusiness(code);
        await boundaryValidator.ValidateAsync(target, cancellationToken);
        return new BusinessUnitRequestContext(
            BusinessUnitAccessStatuses.Selected,
            directory.UserId,
            target,
            directory.ActiveMemberships,
            directory.IsOverallAdministrator,
            "business_unit_selected");
    }

    private static BusinessUnitRequestContext Denied(BusinessUnitDirectorySnapshot directory, string reason)
    {
        return new BusinessUnitRequestContext(
            BusinessUnitAccessStatuses.SelectionDenied,
            directory.UserId,
            null,
            directory.ActiveMemberships,
            directory.IsOverallAdministrator,
            reason);
    }

    private static (string? Code, bool Invalid) ReadRequestedBusinessUnit(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(BusinessUnitHeaderNames.Selection, out var values))
        {
            return (null, false);
        }

        if (values.Count != 1)
        {
            return (null, true);
        }

        var value = values.ToString().Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(value) || !BusinessUnitCodes.IsKnown(value)
            ? (null, true)
            : (value, false);
    }
}

public sealed class BusinessUnitDatabaseBoundaryValidator(
    DatabaseConnectionStringProvider connectionStringProvider,
    MigrationLedgerInspector migrationLedgerInspector,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog)
{
    public async Task ValidateAsync(
        BusinessUnitDatabaseTarget target,
        CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString(
            target,
            BusinessUnitConnectionPurpose.Runtime);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, cancellationToken))
        {
            throw new BusinessUnitContextUnavailableException("business_unit_database_identity_mismatch");
        }

        var ledger = target.Kind == BusinessUnitDatabaseKind.Directory
            ? await directoryMigrationCatalog.InspectAsync(connection, cancellationToken)
            : await migrationLedgerInspector.InspectAsync(connection, cancellationToken);
        if (!ledger.MigrationLedgerReady)
        {
            throw new BusinessUnitContextUnavailableException("business_unit_database_ledger_mismatch");
        }
    }
}

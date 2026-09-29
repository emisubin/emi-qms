using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.ReviewSafe;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    [Fact]
    public async Task BusinessSchemaSeparation_RejectsUnexpectedDataAndRollsBackLateDependencyFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var databases = await IsolationDatabaseSet.CreateAsync(ct);
        var catalog = new DatabaseMigrationCatalog(new TestEnvironment(databases.RepositoryRoot));
        await BootstrapTargetsAsync(new DatabaseRoleBootstrapper(databases.Configuration,
            new DatabaseRuntimePrivilegeManager(), NullLogger<DatabaseRoleBootstrapper>.Instance), ct);
        foreach (var code in new[] { BusinessUnitCodes.Cheongju, BusinessUnitCodes.Osan })
        {
            await ApplyCommonSchemaBeforeSeparationAsync(databases, catalog, code, ct);
            await using var connection = await databases.OpenAsync(code, BusinessUnitConnectionPurpose.Migration, ct);
            await BusinessUnitDatabaseIdentity.BindOrVerifyAsync(connection, databases.BusinessUnits.GetBusiness(code), ct);
            var migration = await File.ReadAllTextAsync(catalog.GetBusinessMigrationFiles(code).Single(), ct);
            var opposite = code == BusinessUnitCodes.Cheongju ? BusinessUnitCodes.Osan : BusinessUnitCodes.Cheongju;
            var mutations = new List<(string Sql, string State, string Message)>
            {
                ($"update qms_database_identity set business_unit_code='{opposite}'", "P0001", "business_schema_identity_mismatch"),
                ("create table unexpected_retained_relation(id integer)", "P0001", "business_schema_unexpected_table_set")
            };
            if (code == BusinessUnitCodes.Cheongju)
            {
                mutations.Add(("update osan_gate_configuration set version=version+1", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table qms_users add constraint unexpected_dependency foreign key(id) references osan_customers(id)", "2BP01", ""));
            }
            else
            {
                mutations.Add(("update material_categories set display_name=display_name||' changed'", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table iqc_report_template_items disable trigger trg_guard_iqc_template_items; update iqc_report_template_items set label=label||' changed'; alter table iqc_report_template_items enable trigger trg_guard_iqc_template_items", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table iqc_report_template_items disable trigger trg_guard_iqc_template_items; update iqc_report_template_items set guidance=null; alter table iqc_report_template_items enable trigger trg_guard_iqc_template_items", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table panel_quality_template_items disable trigger trg_guard_panel_quality_template_items; update panel_quality_template_items set label=label||' changed'; alter table panel_quality_template_items enable trigger trg_guard_panel_quality_template_items", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table panel_quality_template_items disable trigger trg_guard_panel_quality_template_items; update panel_quality_template_items set guidance=null; alter table panel_quality_template_items enable trigger trg_guard_panel_quality_template_items", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table pending_issue_type_catalog disable trigger trg_guard_pending_issue_type_catalog; update pending_issue_type_catalog set description=null; alter table pending_issue_type_catalog enable trigger trg_guard_pending_issue_type_catalog", "P0001", "business_schema_unexpected_retired_table_data"));
                mutations.Add(("alter table qms_users add constraint unexpected_dependency foreign key(id) references material_categories(id)", "2BP01", ""));
            }
            foreach (var mutation in mutations)
            {
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "select set_config('emi_qms.business_schema_separation', @business, true)";
                command.Parameters.AddWithValue("business", code);
                await command.ExecuteNonQueryAsync(ct);
                command.CommandText = mutation.Sql;
                await command.ExecuteNonQueryAsync(ct);
                command.CommandText = migration;
                var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(ct));
                Assert.Equal(mutation.State, error.SqlState);
                if (mutation.Message.Length > 0) Assert.Equal(mutation.Message, error.MessageText);
                await transaction.RollbackAsync(ct);
                Assert.Equal(209L, await databases.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    "select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE'", ct));
                Assert.Equal(43L, await databases.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    "select count(*) from information_schema.columns where table_schema='public' and table_name='projects'", ct));
                Assert.Equal(130L, await databases.ReadScalarAsync<long>(code, BusinessUnitConnectionPurpose.Migration,
                    "select count(*) from schema_migrations", ct));
                Assert.Equal(code, await databases.ReadScalarAsync<string>(code, BusinessUnitConnectionPurpose.Migration,
                    "select business_unit_code from qms_database_identity", ct));
            }
        }
    }
}

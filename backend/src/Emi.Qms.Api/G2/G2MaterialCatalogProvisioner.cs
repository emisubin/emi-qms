using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.DeploymentMaintenance;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.G2;

public sealed class G2MaterialCatalogProvisioner(
    IConfiguration configuration,
    DatabaseConnectionStringProvider connections,
    DatabaseMigrationCatalog migrationCatalog)
{
    public const string ReviewSafeCode = "g2_material_catalog_review_safe";
    public const string TargetInvalidCode = "g2_material_catalog_target_invalid";
    public const string ReleaseInvalidCode = "g2_material_catalog_release_invalid";
    public const string TemplateInvalidCode = "g2_material_catalog_template_invalid";
    public const string DatabaseNameMismatchCode = "g2_material_catalog_database_name_mismatch";
    public const string MigrationRoleMismatchCode = "g2_material_catalog_migration_role_mismatch";
    public const string DatabaseIdentityMismatchCode = "g2_material_catalog_database_identity_mismatch";
    public const string MigrationLedgerMismatchCode = "g2_material_catalog_migration_ledger_mismatch";
    public const string MaintenanceStateMismatchCode = "g2_material_catalog_maintenance_state_mismatch";
    public const string CatalogNotEmptyCode = "g2_material_catalog_not_empty";
    public const string DatabaseUnavailableCode = "g2_material_catalog_database_unavailable";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] MetadataColumns = ["A", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L"];
    private static readonly string[] FillColumns = ["A", "B", "C", "D", "E", "F", "G", "H", "N", "O", "P", "Q", "R", "T", "V", "W", "Y", "Z", "AA"];

    public async Task<int> ProvisionAsync(CancellationToken ct)
    {
        if (ReviewSafeMode.IsEnabled(configuration))
            throw Failure(ReviewSafeCode);
        if (!string.Equals(configuration["Database:MigrationTarget"]?.Trim(),
                BusinessUnitCodes.Cheongju, StringComparison.OrdinalIgnoreCase))
            throw Failure(TargetInvalidCode);
        if (!Guid.TryParse(configuration["G2Materials:ProvisionReleaseId"], out var releaseId)
            || releaseId == Guid.Empty)
            throw Failure(ReleaseInvalidCode);
        if (!connections.BusinessUnits.Enabled || !connections.BusinessUnits.IsValid)
            throw Failure(TargetInvalidCode);

        var target = connections.BusinessUnits.Businesses.SingleOrDefault(candidate =>
            string.Equals(candidate.Code, BusinessUnitCodes.Cheongju, StringComparison.Ordinal));
        if (target is null) throw Failure(TargetInvalidCode);
        var catalog = ReadApprovedTemplateCatalog();

        try
        {
            await using var connection = new NpgsqlConnection(connections.GetConnectionString(
                target, BusinessUnitConnectionPurpose.Migration));
            await connection.OpenAsync(ct);
            var (databaseName, roleName) = await ReadSessionIdentityAsync(connection, ct);
            if (!string.Equals(databaseName, target.ExpectedDatabaseName, StringComparison.Ordinal))
                throw Failure(DatabaseNameMismatchCode);
            if (!string.Equals(roleName, target.MigrationRoleName, StringComparison.Ordinal))
                throw Failure(MigrationRoleMismatchCode);
            if (!await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, target, ct))
                throw Failure(DatabaseIdentityMismatchCode);

            var ledger = await new MigrationLedgerInspector(migrationCatalog)
                .InspectAsync(connection, target.Code, ct);
            if (!ledger.MigrationLedgerReady
                || !string.Equals(ledger.Status, MigrationLedgerInspector.ExactStatus, StringComparison.Ordinal))
                throw Failure(MigrationLedgerMismatchCode);

            await DeploymentMaintenanceLease.AcquireExclusiveAsync(connection, ct);
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await EnsureBlockedReleaseAsync(connection, transaction, releaseId, ct);
                await LockAndEnsureEmptyAsync(connection, transaction, ct);
                await InsertAsync(connection, transaction, catalog, ct);
                await transaction.CommitAsync(ct);
                return catalog.Count;
            }
            finally
            {
                await DeploymentMaintenanceLease.ReleaseExclusiveAsync(connection);
            }
        }
        catch (G2MaterialCatalogProvisioningException)
        {
            throw;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or ArgumentException)
        {
            throw Failure(DatabaseUnavailableCode);
        }
    }

    internal static IReadOnlyList<G2MaterialCatalog> ReadApprovedTemplateCatalog()
    {
        try
        {
            using var stream = typeof(G2MaterialCatalogProvisioner).Assembly
                .GetManifestResourceStream("G2MaterialTemplate.xlsx")
                ?? throw new InvalidDataException();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.Single(worksheet => worksheet.Name == "양식");
            var headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["N3"] = "제작중", ["O3"] = "키팅대차", ["P3"] = "적재 랙",
                ["T3"] = "자재실 합계", ["U3"] = "ERP", ["V3"] = "이동중"
            };
            if (headers.Any(pair => Normalize(ReadText(sheet.Cell(pair.Key))) != Normalize(pair.Value)))
                throw new InvalidDataException();

            var images = G2MaterialImages.All.Keys
                .GroupBy(key => int.Parse(key.AsSpan(5, 2), CultureInfo.InvariantCulture))
                .ToDictionary(group => group.Key,
                    group => group.OrderBy(key => key, StringComparer.Ordinal).ToArray());
            var result = new List<G2MaterialCatalog>(92);
            var category = string.Empty;
            for (var row = 4; row <= 95; row++)
            {
                foreach (var column in MetadataColumns)
                    if (sheet.Cell($"{column}{row}").HasFormula) throw new InvalidDataException();
                category = ReadText(sheet.Cell(row, 1)) is { Length: > 0 } current ? current : category;
                if (category is not ("사급" or "도급" or "부자재")) throw new InvalidDataException();
                var code = ReadText(sheet.Cell(row, 5));
                if (code.Length == 0) throw new InvalidDataException();
                if (!decimal.TryParse(ReadText(sheet.Cell(row, 8)), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var setQuantity)
                    || setQuantity <= 0)
                    throw new InvalidDataException();
                var id = row - 3;
                result.Add(new G2MaterialCatalog(
                    id, row, category, ReadText(sheet.Cell(row, 3)), ReadText(sheet.Cell(row, 4)),
                    code, ReadText(sheet.Cell(row, 6)), ReadText(sheet.Cell(row, 7)), setQuantity,
                    ReadText(sheet.Cell(row, 9)), ReadText(sheet.Cell(row, 10)),
                    ReadText(sheet.Cell(row, 11)), ReadText(sheet.Cell(row, 12)),
                    images.GetValueOrDefault(id, []),
                    FillColumns.ToDictionary(column => column,
                        column => ReadFill(sheet.Cell($"{column}{row}")), StringComparer.Ordinal)));
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 95;
            for (var row = 96; row <= lastRow; row++)
                if (ReadText(sheet.Cell(row, 5)).Length > 0) throw new InvalidDataException();
            if (result.Count != 92 || !result.Select(item => item.Id).SequenceEqual(Enumerable.Range(1, 92)))
                throw new InvalidDataException();
            return result;
        }
        catch (G2MaterialCatalogProvisioningException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or InvalidOperationException or ArgumentException or FormatException)
        {
            throw Failure(TemplateInvalidCode);
        }
    }

    private static string ReadText(IXLCell cell) => cell.GetString().Trim();

    private static string Normalize(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static string ReadFill(IXLCell cell)
    {
        var color = cell.Style.Fill.BackgroundColor;
        if (color.ColorType == XLColorType.Color)
            return $"#{color.Color.R:X2}{color.Color.G:X2}{color.Color.B:X2}";
        if (color.ColorType == XLColorType.Theme && color.ThemeTint != 0)
            return "#D9D9D9";
        return "#FFFFFF";
    }

    private static async Task<(string DatabaseName, string RoleName)> ReadSessionIdentityAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select current_database(), current_user";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw Failure(DatabaseUnavailableCode);
        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task EnsureBlockedReleaseAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid releaseId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select release_id,state from deployment_maintenance where id=1 for update";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)
            || reader.IsDBNull(0)
            || reader.GetGuid(0) != releaseId
            || reader.GetString(1) is not ("Active" or "Delayed")
            || await reader.ReadAsync(ct))
            throw Failure(MaintenanceStateMismatchCode);
    }

    private static async Task LockAndEnsureEmptyAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "lock table g2_material_items in exclusive mode";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "select exists(select 1 from g2_material_items)";
        if (await command.ExecuteScalarAsync(ct) is true) throw Failure(CatalogNotEmptyCode);
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        IReadOnlyList<G2MaterialCatalog> catalog, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into g2_material_items(id,source_row,metadata) values(@id,@source_row,@metadata)";
        var id = command.Parameters.Add("id", NpgsqlDbType.Integer);
        var sourceRow = command.Parameters.Add("source_row", NpgsqlDbType.Integer);
        var metadata = command.Parameters.Add("metadata", NpgsqlDbType.Jsonb);
        foreach (var item in catalog)
        {
            id.Value = item.Id;
            sourceRow.Value = item.SourceRow;
            metadata.Value = JsonSerializer.Serialize(item, Json);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static G2MaterialCatalogProvisioningException Failure(string code) => new(code);
}

public sealed class G2MaterialCatalogProvisioningException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

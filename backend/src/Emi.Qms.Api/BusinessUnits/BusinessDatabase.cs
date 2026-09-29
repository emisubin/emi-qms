using Npgsql;

namespace Emi.Qms.Api.BusinessUnits;

/// <summary>A scope can be bound by the worker host before it resolves any business services.</summary>
public sealed class BusinessDatabaseScope
{
    public BusinessUnitDatabaseTarget? Target { get; private set; }

    public void Bind(BusinessUnitDatabaseTarget target)
    {
        if (target.Kind != BusinessUnitDatabaseKind.Business || (Target is not null && Target != target))
            throw new BusinessUnitContextUnavailableException("business_unit_scope_mismatch");
        Target = target;
    }
}

/// <summary>Shared business operations receive one immutable target per request or worker scope.</summary>
public class BusinessDatabase
{
    private readonly Lazy<BusinessUnitDatabaseTarget> target;
    private readonly Func<BusinessUnitDatabaseTarget, string?> readConnection;
    private readonly Func<BusinessUnitDatabaseTarget?> currentRequest;
    private readonly Func<CancellationToken, Task<IReadOnlyList<Guid>>> overallAdministrators;
    private readonly bool enforceRequestTarget;

    public BusinessDatabase(DatabaseConnectionStringProvider provider, BusinessDatabaseScope? scope = null)
        : this(provider, () => scope?.Target ?? provider.GetCurrentBusinessUnit(), enforceRequestTarget: false) { }

    protected BusinessDatabase(DatabaseConnectionStringProvider provider, string businessCode)
        : this(provider, () => provider.BusinessUnits.GetBusiness(businessCode), enforceRequestTarget: true) { }

    private BusinessDatabase(DatabaseConnectionStringProvider provider,
        Func<BusinessUnitDatabaseTarget?> resolveTarget, bool enforceRequestTarget)
    {
        target = new Lazy<BusinessUnitDatabaseTarget>(() => resolveTarget()
            ?? throw new BusinessUnitContextUnavailableException("business_unit_context_missing"));
        currentRequest = provider.GetCurrentBusinessUnit;
        this.enforceRequestTarget = enforceRequestTarget;
        readConnection = selected => selected.IsLegacy
            ? provider.GetConnectionString()
            : provider.GetConnectionString(selected);
        overallAdministrators = provider.ReadOverallAdministratorIdsAsync;
        BusinessUnits = new BoundBusinessConfiguration(provider.BusinessUnits.Enabled, () => target.Value);
    }

    public BoundBusinessConfiguration BusinessUnits { get; }
    public bool IsOsan => GetCurrentBusinessUnit().Code == BusinessUnitCodes.Osan;
    public BusinessUnitDatabaseTarget GetCurrentBusinessUnit() => target.Value;

    public string? GetConnectionString()
    {
        var selected = target.Value;
        var request = currentRequest();
        if (enforceRequestTarget && request is not null && request != selected)
            throw new BusinessUnitContextUnavailableException("business_unit_module_mismatch");
        return readConnection(selected);
    }

    public string GetConnectionString(BusinessUnitDatabaseTarget requested,
        BusinessUnitConnectionPurpose purpose = BusinessUnitConnectionPurpose.Runtime)
    {
        if (purpose != BusinessUnitConnectionPurpose.Runtime || requested != target.Value)
            throw new BusinessUnitContextUnavailableException("business_unit_module_mismatch");
        return GetConnectionString() ?? throw new InvalidOperationException("Database connection is not configured.");
    }

    public bool ExternalNotificationsEnabled(BusinessUnitDatabaseTarget? explicitTarget = null)
    {
        if (explicitTarget is not null && explicitTarget != target.Value)
            throw new BusinessUnitContextUnavailableException("business_unit_module_mismatch");
        return target.Value.ExternalNotificationsEnabled;
    }

    // This exposes an authorized recipient lookup, never a Directory connection or arbitrary query.
    public Task<IReadOnlyList<Guid>> GetOverallAdministratorIdsAsync(CancellationToken ct) => overallAdministrators(ct);

    public static implicit operator BusinessDatabase(DatabaseConnectionStringProvider provider) => new(provider);
}

public sealed class CheongjuDatabase(DatabaseConnectionStringProvider provider)
    : BusinessDatabase(provider, BusinessUnitCodes.Cheongju)
{
    public static implicit operator CheongjuDatabase(DatabaseConnectionStringProvider provider) => new(provider);
}

public sealed class OsanDatabase(DatabaseConnectionStringProvider provider)
    : BusinessDatabase(provider, BusinessUnitCodes.Osan)
{
    public static implicit operator OsanDatabase(DatabaseConnectionStringProvider provider) => new(provider);
}

/// <summary>Only the bound business is visible to business code; privileged configuration stays in the host.</summary>
public sealed class BoundBusinessConfiguration(bool enabled, Func<BusinessUnitDatabaseTarget> target)
{
    public bool Enabled { get; } = enabled;
    public IReadOnlyList<BusinessUnitDatabaseTarget> Businesses => [target()];
    public BusinessUnitDatabaseTarget GetBusiness(string code) => code == target().Code
        ? target() : throw new BusinessUnitContextUnavailableException("business_unit_module_mismatch");
}

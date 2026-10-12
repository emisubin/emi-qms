namespace Emi.Qms.Api.BusinessUnits;

public static class BusinessUnitMenuPolicy
{
    public static bool IsG2OnlyForNonAdministrators(BusinessUnitRequestContext? context) =>
        context is { IsSelected: true, Target.IsLegacy: false }
        && string.Equals(context.Target.Code, BusinessUnitCodes.Cheongju, StringComparison.Ordinal);
}

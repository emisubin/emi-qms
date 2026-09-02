namespace Emi.Qms.Api.G2;

public static class G2InventoryCalculator
{
    public static readonly DateOnly AvailableInventoryStartDate = new(2026, 8, 28);

    public static DateOnly MovementDateFor(DateOnly inventoryDate) =>
        inventoryDate >= AvailableInventoryStartDate ? inventoryDate.AddDays(-1) : inventoryDate;

    public static IReadOnlyDictionary<DateOnly, long?> Calculate(
        DateOnly from,
        DateOnly to,
        long? balanceBeforeFrom,
        IReadOnlyDictionary<DateOnly, int> physicalCounts,
        IReadOnlyDictionary<DateOnly, long> production,
        IReadOnlyDictionary<DateOnly, long> delivery,
        IReadOnlyDictionary<DateOnly, long> defects)
    {
        var result = new Dictionary<DateOnly, long?>();
        long? balance = balanceBeforeFrom;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (physicalCounts.TryGetValue(date, out var counted)) balance = counted;
            else if (balance.HasValue)
            {
                var movementDate = MovementDateFor(date);
                balance += production.GetValueOrDefault(movementDate)
                    - delivery.GetValueOrDefault(movementDate)
                    - defects.GetValueOrDefault(movementDate);
            }
            result[date] = balance;
        }
        return result;
    }
}

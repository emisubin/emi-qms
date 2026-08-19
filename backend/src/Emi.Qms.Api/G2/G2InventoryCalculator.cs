namespace Emi.Qms.Api.G2;

public static class G2InventoryCalculator
{
    public static IReadOnlyDictionary<DateOnly, long?> Calculate(
        DateOnly from,
        DateOnly to,
        long? balanceBeforeFrom,
        IReadOnlyDictionary<DateOnly, int> physicalCounts,
        IReadOnlyDictionary<DateOnly, long> production,
        IReadOnlyDictionary<DateOnly, long> delivery)
    {
        var result = new Dictionary<DateOnly, long?>();
        long? balance = balanceBeforeFrom;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (physicalCounts.TryGetValue(date, out var counted)) balance = counted;
            else if (balance.HasValue) balance += production.GetValueOrDefault(date) - delivery.GetValueOrDefault(date);
            result[date] = balance;
        }
        return result;
    }
}

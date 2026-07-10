using Emi.Qms.Api.ProductionPlanning;

namespace Emi.Qms.Api.ReviewSafe;

public sealed class ReviewSafeKoreanHolidayProvider : IKoreanHolidayProvider
{
    public Task<KoreanHolidayProviderResult> FetchAsync(int year, CancellationToken cancellationToken)
    {
        return Task.FromResult(new KoreanHolidayProviderResult(
            false,
            Array.Empty<SystemHolidayUpsert>(),
            "Review-safe UAT에서는 외부 공휴일 동기화를 사용할 수 없습니다."));
    }
}

namespace Emi.Qms.Api.InteriorBusbar;

public sealed partial class InteriorBusbarStore
{
    // Actor and display name come from the freshly authenticated server profile, never request JSON.
    public Task<Guid> InspectProduct(Guid id, Guid actor, string actorDisplayName) => Transaction(async c =>
    {
        var product = await One(c, "busbar_products", id);
        Require((string)product["status"]! == "Complete", "생산 완료 후 품질 검사를 완료할 수 있습니다.");
        // Retry keeps the original inspector/time, including after a successful shipment.
        if (product["inspectedAtUtc"] is not null) return id;
        Require((await Rows(c, "select product_id from busbar_shipment_products where product_id=@id and released_at_utc is null", ("id", id))).Count == 0,
            "이미 출하된 패널에는 품질 검사 기록을 추가할 수 없습니다.");
        Require((await Rows(c, "select side from busbar_photos where product_id=@id", ("id", id))).Count == 2,
            "앞면과 뒷면 사진을 확인한 뒤 품질 검사를 완료하세요.");
        Require(!string.IsNullOrWhiteSpace(actorDisplayName), "검사자 계정 이름을 확인하세요.");
        var now = timeProvider.GetUtcNow();
        await Exec(c, "update busbar_products set inspected_by=@actor,inspected_by_display_name=@name,inspected_at_utc=@now where id=@id",
            ("id", id), ("actor", actor), ("name", actorDisplayName), ("now", now));
        await Audit(c, "ProductInspection", id, actor, "품질 검사 완료", new { inspectedAtUtc = (DateTimeOffset?)null },
            new { inspectedBy = actor, inspectedByDisplayName = actorDisplayName, inspectedAtUtc = now });
        return id;
    });
}

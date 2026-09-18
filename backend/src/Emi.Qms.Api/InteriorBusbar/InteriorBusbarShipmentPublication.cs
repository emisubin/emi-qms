namespace Emi.Qms.Api.InteriorBusbar;

public sealed partial class InteriorBusbarStore
{
    private async Task PublishShippedPanel(Npgsql.NpgsqlConnection c, Guid id)
    {
        if (publicationSink is null)
            throw new BusbarException("publication_unavailable", "독립 제품 페이지 저장소가 준비되지 않아 출하할 수 없습니다.", 503);
        var p = await One(c, "busbar_products", id);
        var photos = await Rows(c, "select side,content_type,content from busbar_photos where product_id=@id", ("id", id));
        var snapshot = new InteriorBusbarPublicSnapshot(id, (string)p["number"]!, (string)p["workerName"]!,
            new DateTimeOffset((DateTime)p["manufacturedAtUtc"]!, TimeSpan.Zero), (string)p["publicToken"]!,
            Convert.ToInt32(p["revision"]) + 1, false,
            photos.Select(photo => new InteriorBusbarPublicPhoto((string)photo["side"]!, (string)photo["contentType"]!, (byte[])photo["content"]!)).ToArray());
        // Survives shipment rollback/process interruption. The worker restores the
        // committed lifecycle state under the same global mutation lock.
        await using (var recovery = new Npgsql.NpgsqlConnection(provider.GetConnectionString()))
        {
            await recovery.OpenAsync();
            await Exec(recovery, "insert into busbar_publication_recovery(product_id) values(@id) on conflict do nothing", ("id", id));
        }
        byte[] html;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            html = await InteriorBusbarPublicPage.RenderDetachedAsync(snapshot, timeout.Token);
            await publicationSink.PublishAsync(snapshot.Token, html, timeout.Token);
        }
        catch (Exception)
        {
            // Do not commit inventory, shipment or ERP work when the public handoff failed.
            throw new BusbarException("publication_failed", "독립 제품 페이지 저장에 실패해 출하하지 않았습니다. 잠시 후 다시 시도하세요.", 503);
        }
        await Exec(c, "update busbar_products set revision=@revision,published_revision=@revision,publication_state='Published',publication_error=null where id=@id",
            ("revision", snapshot.Revision), ("id", id));
        await Exec(c, "insert into busbar_detached_pages(product_id,html) values(@id,@html) on conflict(product_id) do update set html=excluded.html", ("id", id), ("html", html));
        await Exec(c, "delete from busbar_publication_recovery where product_id=@id", ("id", id));
    }
}

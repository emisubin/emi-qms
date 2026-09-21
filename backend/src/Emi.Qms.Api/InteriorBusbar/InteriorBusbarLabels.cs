using Npgsql;
using System.Text.RegularExpressions;
namespace Emi.Qms.Api.InteriorBusbar;

public sealed record BusbarLabelRequest(Guid RequestId, IReadOnlyList<Guid> ProductIds);

public sealed partial class InteriorBusbarStore
{
    private const string LabelColumns = ", exists(select 1 from busbar_shipment_products sp where sp.product_id=p.id and sp.released_at_utc is null) is_shipped, (select name from busbar_product_families f where f.id=p.product_family_id) product_family_name, (select plan_date from busbar_plans pl where pl.id=p.plan_id) plan_date, (select display_name from qms_users u where u.id=p.label_printed_by) label_printed_by_display_name, (select display_name from qms_users u where u.id=p.label_attached_by) label_attached_by_display_name";
    private const string EligibleLabel = "p.status<>'Cancelled' and not exists(select 1 from busbar_shipment_products sp where sp.product_id=p.id and sp.released_at_utc is null)";

    public static string? NormalizePanelNumber(string code)
    {
        var match = Regex.Match(code.Trim(), @"^(?:IB-)?([0-9]+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var digits = match.Groups[1].Value.TrimStart('0');
        return digits.Length == 0 ? "0" : digits;
    }

    private static Task<List<Dictionary<string, object?>>> FindPanel(NpgsqlConnection c, string code)
    {
        Require(!string.IsNullOrWhiteSpace(code) && code.Length <= 2048, "QR 또는 패널 번호를 입력하세요.");
        code = code.Trim();
        return Rows(c, "select p.*" + LabelColumns + " from busbar_products p where p.number=@code or p.public_token=@code or exists(select 1 from busbar_product_qr q where q.product_id=p.id and q.url=@code) or (@numeric<>'' and p.number ~ '^IB-[0-9]+$' and coalesce(nullif(ltrim(substring(p.number from 4),'0'),''),'0')=@numeric)", ("code",code), ("numeric",NormalizePanelNumber(code) ?? ""));
    }

    public Task<object> ResolveLabel(string code) => ReadSnapshot<object>(async c =>
    {
        var rows = await FindPanel(c,code);
        Require(rows.Count == 1,"등록된 패널 QR 또는 번호를 확인하세요.");
        await RequireLabelEligible(c,Id(rows[0]));
        return rows[0];
    });

    public Task<object> PendingLabels(Guid actor) => ReadSnapshot<object>(async c =>
        await Rows(c,"select p.*" + LabelColumns + " from busbar_products p where " + EligibleLabel + " and p.label_state<>'Attached' and exists(select 1 from busbar_label_events e where e.product_id=p.id and e.actor_id=@actor and e.action='Printed') order by (select plan_date from busbar_plans pl where pl.id=p.plan_id),p.number",("actor",actor)));

    public Task<object> LabelHistory(Guid product) => ReadSnapshot<object>(async c =>
    {
        await One(c,"busbar_products",product);
        return await Rows(c,"select id,action,actor_id,actor_display_name,created_at_utc from busbar_label_events where product_id=@id order by created_at_utc desc,id",("id",product));
    });

    private static async Task RequireLabelEligible(NpgsqlConnection c,Guid id) => Require(
        (await Rows(c,"select p.id from busbar_products p where p.id=@id and " + EligibleLabel,("id",id))).Count == 1,
        "취소되었거나 출하된 패널의 라벨은 처리할 수 없습니다.");

    public Task<Guid> ConfirmLabels(BusbarLabelRequest r, Guid actor, bool attached) => Transaction(async c =>
    {
        Require(r.RequestId != Guid.Empty && r.ProductIds is { Count: >0 and <=200 } && r.ProductIds.All(id=>id!=Guid.Empty),"확인할 패널을 1~200개 선택하세요.");
        Require(r.ProductIds.Distinct().Count()==r.ProductIds.Count,"패널 선택이 중복되었습니다.");
        var action=attached?"Attached":"Printed";
        var fingerprint=Fingerprint(r.ProductIds.OrderBy(id=>id).ToArray());
        var prior=await Rows(c,"select * from busbar_label_requests where id=@id",("id",r.RequestId));
        if(prior.Count>0)
        {
            Require(Equals(prior[0]["actorId"],actor) && Equals(prior[0]["action"],action) && Equals(prior[0]["fingerprint"],fingerprint),"같은 요청 식별자로 다른 내용을 처리할 수 없습니다.");
            return r.RequestId;
        }
        foreach(var id in r.ProductIds)
        {
            await RequireLabelEligible(c,id);
            if (!attached) await ReadPrintableQr(c,id,false);
        }
        var actorRow=await One(c,"qms_users",actor);
        var name=actorRow.GetValueOrDefault("displayName") as string ?? "사용자";
        var now=timeProvider.GetUtcNow();
        await Exec(c,"insert into busbar_label_requests(id,actor_id,action,fingerprint,created_at_utc) values(@id,@actor,@action,@fingerprint,@now)",("id",r.RequestId),("actor",actor),("action",action),("fingerprint",fingerprint),("now",now));
        foreach(var id in r.ProductIds)
        {
            var row=await One(c,"busbar_products",id);
            if(attached && Equals(row["labelState"],"Attached")) continue;
            await Exec(c,attached
                ? "update busbar_products set label_state='Attached',label_attached_at_utc=@now,label_attached_by=@actor where id=@id"
                : "update busbar_products set label_state=case when label_state='Attached' then 'Attached' else 'Printed' end,label_printed_at_utc=@now,label_printed_by=@actor where id=@id",("id",id),("actor",actor),("now",now));
            await Exec(c,"insert into busbar_label_events(id,request_id,product_id,action,actor_id,actor_display_name,created_at_utc) values(@id,@request,@product,@action,@actor,@name,@now)",("id",Guid.NewGuid()),("request",r.RequestId),("product",id),("action",action),("actor",actor),("name",name),("now",now));
        }
        return r.RequestId;
    });
}

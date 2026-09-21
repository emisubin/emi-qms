namespace Emi.Qms.Api.InteriorBusbar;

public sealed partial class InteriorBusbarStore
{
    private static async Task RequireShippable(Npgsql.NpgsqlConnection c, Guid productId, Guid familyId)
    {
        var p = await One(c, "busbar_products", productId);
        Require(Equals(p["productFamilyId"], familyId), "프로젝트와 다른 제품군의 패널입니다.");
        Require((string)p["status"]! == "Complete", "생산 완료 패널만 출하할 수 있습니다.");
        Require((await Rows(c, "select product_id from busbar_shipment_products where product_id=@id and released_at_utc is null", ("id", productId))).Count == 0,
            "이미 출하된 패널입니다. 기존 출하 이력을 확인하세요.");
    }

    public Task<object> ResolveShipmentPanel(Guid projectId, string code) => ReadSnapshot<object>(async c =>
    {
        Require(!string.IsNullOrWhiteSpace(code) && code.Length <= 2048, "QR 또는 제품번호를 입력하세요.");
        code = code.Trim();
        var project = await One(c, "busbar_projects", projectId);
        // Match stored QR URLs locally. Never fetch or follow a scanned URL.
        var matches = await FindPanel(c, code);
        Require(matches.Count == 1, "등록된 패널 QR 또는 제품번호를 확인하세요.");
        var p = matches[0];
        await RequireShippable(c, Id(p), Id(project, "productFamilyId"));
        return p;
    });

    public Task<object> ProjectDetail(Guid id) => ReadSnapshot<object>(async c =>
    {
        var projects = await Rows(c, ProjectQuery + " where p.id=@id", ("id", id));
        if (projects.Count == 0) throw new BusbarException("not_found", "프로젝트를 찾을 수 없습니다.", 404);
        var shipments = await Rows(c, "select s.*,o.created_at_utc,u.display_name shipped_by_name,exists(select 1 from busbar_operations r where r.reverses_id=s.id) reversed from busbar_shipments s join busbar_operations o on o.id=s.id left join qms_users u on u.id=o.created_by where s.project_id=@id order by o.created_at_utc desc,s.id", ("id", id));
        var panels = await Rows(c, "select sp.shipment_id,sp.released_at_utc,p.*,true has_front,true has_back from busbar_shipment_products sp join busbar_products p on p.id=sp.product_id join busbar_shipments s on s.id=sp.shipment_id where s.project_id=@id order by p.number", ("id", id));
        return new { project = projects[0], shipments, panels };
    });
}

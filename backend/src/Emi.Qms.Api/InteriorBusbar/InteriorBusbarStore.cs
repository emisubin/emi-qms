using System.Text.Json;
using System.Security.Cryptography;
using Npgsql;
using QRCoder;
namespace Emi.Qms.Api.InteriorBusbar;

public sealed class InteriorBusbarStore(DatabaseConnectionStringProvider provider, TimeProvider timeProvider, InteriorBusbarPublicationOptions? publicationOptions = null)
{
    // The same transaction lock fences inventory mutations and bounded external publication.
    // Checks, immutable ledger deltas and derived balances must commit together.
    public const long MutationLock = 9070090;

    internal static async Task<List<Dictionary<string, object?>>> Rows(NpgsqlConnection c, string sql, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var parts = reader.GetName(i).Split('_');
                var name = parts[0] + string.Concat(parts.Skip(1).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
                row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    internal static async Task Exec(NpgsqlConnection c, string sql, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<T> Transaction<T>(Func<NpgsqlConnection, Task<T>> action)
    {
        await using var c = new NpgsqlConnection(provider.GetConnectionString());
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        await Exec(c, "select pg_advisory_xact_lock(@key)", ("key", MutationLock));
        var result = await action(c);
        await tx.CommitAsync();
        return result;
    }

    private async Task<T> ReadSnapshot<T>(Func<NpgsqlConnection, Task<T>> action)
    {
        await using var c = new NpgsqlConnection(provider.GetConnectionString());
        await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await Exec(c, "set transaction read only");
        var result = await action(c);
        await tx.CommitAsync();
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new BusbarException("invalid_request", message);
    }

    private static string Text(string? value, string label)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 200, $"{label}을(를) 200자 이내로 입력하세요.");
        return value!.Trim();
    }

    private static Guid Id(Dictionary<string, object?> row, string key = "id") => (Guid)row[key]!;

    private static decimal Num(Dictionary<string, object?> row, string key) => Convert.ToDecimal(row[key]);

    private static async Task<Dictionary<string, object?>> One(NpgsqlConnection c, string table, Guid id)
    {
        var rows = await Rows(c, $"select * from {table} where id=@id", ("id", id));
        if (rows.Count == 0) throw new BusbarException("not_found", "대상을 찾을 수 없습니다.", 404);
        return rows[0];
    }

    private static async Task Active(NpgsqlConnection c, string table, Guid id)
    {
        var row = await One(c, table, id);
        Require((bool)row["isActive"]!, "사용 중인 기준정보를 선택하세요.");
    }

    private static async Task Audit(NpgsqlConnection c, string kind, Guid id, Guid actor, string reason, object before, object after) => await Exec(c, "insert into busbar_audit values(@id,@kind,@entity,@reason,@actor,now(),@before::jsonb,@after::jsonb)", ("id", Guid.NewGuid()), ("kind", kind), ("entity", id), ("reason", reason), ("actor", actor), ("before", JsonSerializer.Serialize(before)), ("after", JsonSerializer.Serialize(after)));

    public Task<object> Workspace(bool canWrite, int page = 1, int pageSize = 100, Guid? planId = null, Guid? productFamilyId = null, DateOnly? planDateFrom = null, DateOnly? planDateTo = null, string? status = null) => ReadSnapshot<object>(async c =>
    {
        Require(page > 0 && pageSize is > 0 and <= 200, "페이지 크기는 1~200이어야 합니다.");
        var offset = ((long)page - 1) * pageSize;
        Require(planDateFrom is null || planDateTo is null || planDateFrom <= planDateTo, "조회 시작일은 종료일보다 늦을 수 없습니다.");
        Require(status is null or "Draft" or "Complete" or "Cancelled", "생산 상태를 확인하세요.");
        var predicates = new List<string>();
        var filterArgs = new List<(string, object?)>();
        if (planId is not null) { predicates.Add("p.plan_id=@plan"); filterArgs.Add(("plan", planId.Value)); }
        if (productFamilyId is not null) { predicates.Add("p.product_family_id=@family"); filterArgs.Add(("family", productFamilyId.Value)); }
        if (status is not null) { predicates.Add("p.status=@status"); filterArgs.Add(("status", status)); }
        if (planDateFrom is not null)
        {
            predicates.Add("exists(select 1 from busbar_plans fp where fp.id=p.plan_id and fp.plan_date>=@dateFrom)");
            filterArgs.Add(("dateFrom", planDateFrom.Value));
        }
        if (planDateTo is not null)
        {
            predicates.Add("exists(select 1 from busbar_plans fp where fp.id=p.plan_id and fp.plan_date<=@dateTo)");
            filterArgs.Add(("dateTo", planDateTo.Value));
        }
        var productFilter = predicates.Count == 0 ? "" : " where " + string.Join(" and ", predicates);
        var parameters = filterArgs.ToArray();
        var productOrder = planId is not null ? "p.plan_sequence,p.id"
            : productFamilyId is not null || planDateFrom is not null || planDateTo is not null
                ? "(select fp.plan_date from busbar_plans fp where fp.id=p.plan_id) desc nulls last,p.product_family_id,p.plan_sequence,p.id"
                : "p.created_at_utc desc,p.id";
        var result = new Dictionary<string, object?>
        {
            ["canWrite"] = canWrite,
            ["settings"] = (await Rows(c, "select common_project_code,ecount_customer_code,ecount_warehouse_code from busbar_settings"))[0]
        }
;
        foreach (var (key, sql) in new (string, string)[]{
            ("productFamilies","select f.*,coalesce(s.balance,0) balance,(select count(*) from busbar_products p where p.product_family_id=f.id and p.status='Complete') produced_quantity,coalesce((select sum(quantity) from busbar_plans p where p.product_family_id=f.id),0) planned_quantity from busbar_product_families f left join busbar_stock s on s.stock_kind='Finished' and s.item_id=f.id order by f.code"),
            ("materials","select m.*,coalesce(s.balance,0) balance from busbar_materials m left join busbar_stock s on s.stock_kind='Material' and s.item_id=m.id order by m.code"),
            ("workers","select * from busbar_workers order by code"),("boms","select * from busbar_boms order by version desc"),("bomLines","select * from busbar_bom_lines"),
            ("projects",ProjectQuery),("plans","select p.*,(select count(*) from busbar_products x where x.plan_id=p.id and x.status='Complete') actual_quantity from busbar_plans p order by plan_date desc"),
            ("purchases","select p.*,coalesce((select sum(r.quantity) from busbar_receipts r where r.purchase_id=p.id and not exists(select 1 from busbar_operations o where o.reverses_id=r.id)),0) received_quantity from busbar_purchases p order by order_date desc"),
            ("products","select p.*,exists(select 1 from busbar_product_qr qr where qr.product_id=p.id) qr_ready,(select display_name from qms_users u where u.id=coalesce(p.photo_registered_by,p.created_by)) registered_by_display_name, exists(select 1 from busbar_photos f where f.product_id=p.id and side='front') has_front,exists(select 1 from busbar_photos f where f.product_id=p.id and side='back') has_back from busbar_products p"+productFilter+" order by "+productOrder+" limit "+pageSize+" offset "+offset),
            ("ledger","select o.*,exists(select 1 from busbar_operations r where r.reverses_id=o.id) reversed from busbar_operations o order by created_at_utc desc,id limit "+pageSize+" offset "+offset),("ledgerLines","select l.* from busbar_ledger l where operation_id in (select id from busbar_operations order by created_at_utc desc,id limit "+pageSize+" offset "+offset+")"),("shipments","select s.*,o.created_at_utc,exists(select 1 from busbar_operations r where r.reverses_id=s.id) reversed from busbar_shipments s join busbar_operations o on o.id=s.id"),("receipts","select s.*,o.created_at_utc,exists(select 1 from busbar_operations r where r.reverses_id=s.id) reversed from busbar_receipts s join busbar_operations o on o.id=s.id"),("audit","select * from busbar_audit order by changed_at_utc desc limit 200")}
) result[key] = await Rows(c, sql, key == "products" ? parameters : []);
        foreach (var product in (List<Dictionary<string, object?>>)result["products"]!) SetQrState(product);
        result["publicationOutstandingCount"] = (await Rows(c, "select count(*) total from busbar_products where status in ('Complete','Cancelled') and manufactured_at_utc is not null and (publication_state<>'Published' or revision<>published_revision)"))[0]["total"];
        result["pagination"] = new
        {
            page,
            pageSize,
            productCount = (await Rows(c, "select count(*) total from busbar_products p" + productFilter, parameters))[0]["total"],
            ledgerCount = (await Rows(c, "select count(*) total from busbar_operations"))[0]["total"]
        }
;
        return result;
    });
    private const string ProjectQuery = "select p.*,coalesce(s.quantity,0)::int shipped_quantity,(p.requested_quantity-coalesce(s.quantity,0))::int remaining_quantity,case when p.requested_quantity=coalesce(s.quantity,0) then 'Complete' else 'InProgress' end status from busbar_projects p left join lateral (select sum(quantity) quantity from busbar_shipments s where s.project_id=p.id and not exists(select 1 from busbar_operations r where r.reverses_id=s.id)) s on true";

    public Task<Guid> Master(string kind, BusbarMasterRequest request, Guid actor) => Transaction(async c =>
    {
        var table = kind switch
        {
            "product-families" => "busbar_product_families",
            "materials" => "busbar_materials",
            "workers" => "busbar_workers",
            _ => throw new BusbarException("invalid_kind", "잘못된 기준정보입니다.")
        }
;
        var id = request.Id ?? Guid.NewGuid();
        var code = Text(request.Code, "코드");
        var name = Text(request.Name, "이름");
        object before = request.Id is null ? new
        {
        }
 : await One(c, table, id);
        if (kind == "materials")
        {
            var unit = Text(request.Unit, "단위");
            Require(request.SupplyType is "사급" or "도급", "공급 구분을 선택하세요.");
            if (request.Id is not null)
            {
                var previous = (Dictionary<string, object?>)before;
                Require((string)previous["unit"]! == unit, "기존 자재의 단위는 변경할 수 없습니다.");
            }
            await Exec(c, $"insert into {table}(id,code,name,is_active,unit,supply_type) values(@id,@code,@name,@active,@unit,@supply) on conflict(id) do update set code=excluded.code,name=excluded.name,is_active=excluded.is_active,supply_type=excluded.supply_type", ("id", id), ("code", code), ("name", name), ("active", request.IsActive), ("unit", unit), ("supply", request.SupplyType));
        }
        else await Exec(c, $"insert into {table}(id,code,name,is_active) values(@id,@code,@name,@active) on conflict(id) do update set code=excluded.code,name=excluded.name,is_active=excluded.is_active", ("id", id), ("code", code), ("name", name), ("active", request.IsActive));
        if (kind == "product-families")
        {
            ValidatePrice(request.StandardUnitPrice);
            Require((request.EcountProductCode?.Trim().Length ?? 0) <= 20, "이카운트 품목 코드는 20자 이내여야 합니다.");
            await Exec(c, "update busbar_product_families set ecount_product_code=@product,standard_unit_price=@price where id=@id",
                ("product", string.IsNullOrWhiteSpace(request.EcountProductCode) ? null : request.EcountProductCode.Trim()),
                ("price", request.StandardUnitPrice), ("id", id));
        }
        await Audit(c, kind, id, actor, "기준정보 저장", before, request);
        return id;
    });

    public Task<Guid> Settings(BusbarSettingsRequest r, Guid actor) => Transaction(async c =>
    {
        var before = await Rows(c, "select * from busbar_settings");
        Require((r.EcountCustomerCode?.Trim().Length ?? 0) <= 30 && (r.EcountWarehouseCode?.Trim().Length ?? 0) <= 5, "거래처 코드는 30자, 창고 코드는 5자 이내여야 합니다.");
        await Exec(c, "update busbar_settings set common_project_code=@code,ecount_customer_code=coalesce(@customer,ecount_customer_code),ecount_warehouse_code=coalesce(@warehouse,ecount_warehouse_code)",
            ("code", Text(r.CommonProjectCode, "공통 프로젝트 코드")), ("customer", r.EcountCustomerCode?.Trim()), ("warehouse", r.EcountWarehouseCode?.Trim()));
        await Audit(c, "Settings", Guid.Empty, actor, "공통 코드 설정", before, r);
        return Guid.Empty;
    });

    public Task<Guid> Bom(BusbarBomRequest r, Guid actor) => Transaction(async c =>
    {
        await Active(c, "busbar_product_families", r.ProductFamilyId);
        Require(r.Lines is
        {
            Count: > 0
        }
        && r.Lines.Select(l => l.MaterialId).Distinct().Count() == r.Lines.Count, "중복 없는 자재 소요량이 필요합니다.");
        foreach (var l in r.Lines)
        {
            Require(l.Quantity > 0 && decimal.Round(l.Quantity, 4) == l.Quantity, "소요량은 소수 네 자리 이내 양수여야 합니다.");
            await Active(c, "busbar_materials", l.MaterialId);
        }
        var id = Guid.NewGuid();
        await Exec(c, "insert into busbar_boms(id,product_family_id,version) select @id,@family,coalesce(max(version),0)+1 from busbar_boms where product_family_id=@family", ("id", id), ("family", r.ProductFamilyId));
        foreach (var l in r.Lines) await Exec(c, "insert into busbar_bom_lines values(@id,@material,@quantity)", ("id", id), ("material", l.MaterialId), ("quantity", l.Quantity));
        await Audit(c, "Bom", id, actor, "표준 소요량 새 버전", new
        {
        }
       , r);
        return id;
    });

    public Task<Guid> Project(BusbarProjectRequest r, Guid actor) => Transaction(c => SaveProject(c, r, actor));

    private static void ValidatePrice(decimal? price) => Require(price is null ||
        (price >= 0 && price < 100000000000000m && decimal.Round(price.Value, 4) == price),
        "단가는 0 이상, 100조 미만이며 소수 네 자리 이내여야 합니다.");

    public Task<object> CommercialPreview(Guid projectId) => ReadSnapshot<object>(async c =>
    {
        var project = await One(c, "busbar_projects", projectId);
        var family = await One(c, "busbar_product_families", Id(project, "productFamilyId"));
        var settings = (await Rows(c, "select * from busbar_settings"))[0];
        var price = family["standardUnitPrice"] as decimal?;
        var quantity = Convert.ToInt32(project["requestedQuantity"]);
        var missing = new List<string>();
        if (price is null) missing.Add("제품군 기준 단가");
        if (string.IsNullOrWhiteSpace(family["ecountProductCode"] as string)) missing.Add("제품군 이카운트 품목 코드");
        if (string.IsNullOrWhiteSpace(settings["ecountCustomerCode"] as string)) missing.Add("고정 거래처 코드");
        if (string.IsNullOrWhiteSpace(settings["ecountWarehouseCode"] as string)) missing.Add("고정 출하창고 코드");
        if (string.IsNullOrWhiteSpace(project["commonProjectCode"] as string) || ((string)project["commonProjectCode"]!).Length > 14) missing.Add("14자 이내 공통 프로젝트 코드");
        var supply = price * quantity;
        var vat = supply is null ? (decimal?)null : supply.Value * 0.1m;
        return new { projectId, workOrderNumber = project["customerJobNumber"], purchaseOrderNumber = "",
            customerCode = settings["ecountCustomerCode"], warehouseCode = settings["ecountWarehouseCode"],
            productCode = family["ecountProductCode"], commonProjectCode = project["commonProjectCode"],
            unitPrice = price, quantity, currency = "KRW", vatRate = 0.1m, supplyAmount = supply, vatAmount = vat,
            totalAmount = supply + vat, missingFields = missing, transmissionEnabled = false };
    });

    private static async Task<Guid> SaveProject(NpgsqlConnection c, BusbarProjectRequest r, Guid actor)
    {
        await Active(c, "busbar_product_families", r.ProductFamilyId);
        Require(r.RequestedQuantity > 0, "요청 수량은 양수여야 합니다.");
        var id = r.Id ?? Guid.NewGuid();
        object before = new
        {
        }
       ;
        if (r.Id is not null)
        {
            before = await One(c, "busbar_projects", id);
            var shipped = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_shipments s where project_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", id));
            Require(r.RequestedQuantity >= Num(shipped[0], "quantity"), "출하 수량보다 작게 변경할 수 없습니다.");
            Require(Id((Dictionary<string, object?>)before, "productFamilyId") == r.ProductFamilyId, "기존 등록 건의 제품군은 변경할 수 없습니다.");
            Text(r.Reason, "정정 사유");
        }
        var code = (string)(await Rows(c, "select common_project_code from busbar_settings"))[0]["commonProjectCode"]!;
        Text(code, "공통 프로젝트 코드");
        await Exec(c, "insert into busbar_projects(id,name,customer_job_number,common_project_code,product_family_id,requested_quantity,destination,due_date) values(@id,@name,@job,@code,@family,@quantity,@destination,@date) on conflict(id) do update set name=excluded.name,customer_job_number=excluded.customer_job_number,requested_quantity=excluded.requested_quantity,destination=excluded.destination,due_date=excluded.due_date", ("id", id), ("name", Text(r.Name, "프로젝트명")), ("job", r.CustomerJobNumber?.Trim() ?? ""), ("code", code), ("family", r.ProductFamilyId), ("quantity", r.RequestedQuantity), ("destination", Text(r.Destination, "도착지")), ("date", r.DueDate));
        await Audit(c, "Project", id, actor, r.Reason ?? "프로젝트 등록", before, r);
        return id;
    }

    public Task<Guid> Plan(BusbarPlanRequest r, Guid actor) => Transaction(async c =>
    {
        await Active(c, "busbar_product_families", r.ProductFamilyId);
        Require(r.Quantity >= 0, "목표 수량은 0 이상이어야 합니다.");
        var id = r.Id ?? Guid.NewGuid();
        Require(id != Guid.Empty, "계획 식별자가 필요합니다.");
        var existing = await Rows(c, "select * from busbar_plans where id=@id", ("id", id));
        object before = existing.Count == 0 ? new { } : existing[0];
        var previousQuantity = 0;
        if (existing.Count > 0)
        {
            var previous = existing[0];
            Require(Id(previous, "productFamilyId") == r.ProductFamilyId &&
                (previous["planDate"] is DateOnly date ? date : DateOnly.FromDateTime((DateTime)previous["planDate"]!)) == r.PlanDate,
                "제품이 연결된 계획의 제품군과 날짜는 변경할 수 없습니다. 별도 계획을 등록하세요.");
            previousQuantity = (bool)previous["productsInitialized"]! ? Convert.ToInt32(previous["quantity"]) : 0;
        }
        var difference = r.Quantity - previousQuantity;
        var untouched = new List<Dictionary<string, object?>>();
        if (difference < 0)
        {
            untouched = await Rows(c, """
                select id,plan_sequence from busbar_products p
                where plan_id=@plan and status='Draft' and worker_id is null
                  and not exists(select 1 from busbar_photos f where f.product_id=p.id)
                order by plan_sequence desc limit @quantity
                """, ("plan", id), ("quantity", -difference));
            Require(untouched.Count == -difference,
                "작업자 지정·사진 등록·완료된 제품은 계획 수량 감소로 철회할 수 없습니다. 미착수 제품 수를 확인하세요.");
        }
        await Exec(c, """
            insert into busbar_plans(id,product_family_id,plan_date,quantity,products_initialized)
            values(@id,@family,@date,@quantity,true)
            on conflict(id) do update set quantity=excluded.quantity,products_initialized=true
            """, ("id", id), ("family", r.ProductFamilyId), ("date", r.PlanDate), ("quantity", r.Quantity));
        if (difference > 0)
        {
            var sequence = Convert.ToInt32((await Rows(c,
                "select coalesce(max(plan_sequence),0) value from busbar_products where plan_id=@id", ("id", id)))[0]["value"]);
            for (var index = 0; index < difference; index++)
            {
                var productId = Guid.NewGuid();
                await Exec(c, """
                    insert into busbar_products(id,request_id,product_family_id,public_token,created_by,plan_id,plan_sequence)
                    values(@id,@request,@family,@token,@actor,@plan,@sequence)
                    """, ("id", productId), ("request", Guid.NewGuid()), ("family", r.ProductFamilyId),
                    ("token", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()),
                    ("actor", actor), ("plan", id), ("sequence", ++sequence));
            }
        }
        foreach (var product in untouched)
        {
            await Exec(c, "update busbar_products set status='Cancelled' where id=@id", ("id", Id(product)));
            await Audit(c, "Product", Id(product), actor, "생산계획 수량 감소로 미착수 제품 철회",
                new { status = "Draft", planId = id, planSequence = product["planSequence"] },
                new { status = "Cancelled", planId = id, planSequence = product["planSequence"] });
        }
        await Audit(c, "Plan", id, actor, "생산계획 저장", before, r);
        return id;
    });

    public Task<Guid> Purchase(BusbarPurchaseRequest r, Guid actor) => Transaction(c => SavePurchase(c, r, actor));

    private static async Task<Guid> SavePurchase(NpgsqlConnection c, BusbarPurchaseRequest r, Guid actor)
    {
        await Active(c, "busbar_materials", r.MaterialId);
        Require(r.Quantity > 0 && decimal.Round(r.Quantity, 4) == r.Quantity, "발주 수량은 소수 네 자리 이내 양수여야 합니다.");
        var id = r.Id ?? Guid.NewGuid();
        object before = new
        {
        }
       ;
        if (r.Id is not null)
        {
            before = await One(c, "busbar_purchases", id);
            Require(Id((Dictionary<string, object?>)before, "materialId") == r.MaterialId, "발주 자재는 변경할 수 없습니다.");
            Text(r.Reason, "정정 사유");
            var receipt = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_receipts s where purchase_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", id));
            Require(r.Quantity >= Num(receipt[0], "quantity"), "입고량보다 작은 발주 수량입니다.");
        }
        var code = (string)(await Rows(c, "select common_project_code from busbar_settings"))[0]["commonProjectCode"]!;
        Text(code, "공통 프로젝트 코드");
        await Exec(c, "insert into busbar_purchases(id,order_number,material_id,quantity,order_date,common_project_code) values(@id,@number,@material,@quantity,@date,@code) on conflict(id) do update set order_number=excluded.order_number,quantity=excluded.quantity,order_date=excluded.order_date", ("id", id), ("number", Text(r.OrderNumber, "발주번호")), ("material", r.MaterialId), ("quantity", r.Quantity), ("date", r.OrderDate), ("code", code));
        await Audit(c, "Purchase", id, actor, r.Reason ?? "발주 등록", before, r);
        return id;
    }

    private static async Task<Guid?> Existing(NpgsqlConnection c, Guid requestId, string kind, Guid? reference, object? payload = null)
    {
        Require(requestId != Guid.Empty, "요청 식별자가 필요합니다.");
        var rows = await Rows(c, "select * from busbar_operations where request_id=@id", ("id", requestId));
        if (rows.Count == 0) return null;
        Require((string)rows[0]["kind"]! == kind && Equals(rows[0]["referenceId"], reference), "이미 사용된 요청 식별자입니다.");
        Require(payload is null || Equals(rows[0]["requestFingerprint"], Fingerprint(payload)), "같은 요청 식별자로 다른 내용을 처리할 수 없습니다.");
        return Id(rows[0]);
    }

    private static string Fingerprint(object payload) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));

    private static async Task<Guid> Operation(NpgsqlConnection c, Guid request, string kind, Guid? reference, Guid actor, string reason, Guid? reverses = null, object? payload = null)
    {
        var id = Guid.NewGuid();
        await Exec(c, "insert into busbar_operations(id,request_id,kind,reference_id,reverses_id,reason,created_by,request_fingerprint) values(@id,@request,@kind,@reference,@reverses,@reason,@actor,@fingerprint)", ("id", id), ("request", request), ("kind", kind), ("reference", reference), ("reverses", reverses), ("reason", reason), ("actor", actor), ("fingerprint", payload is null ? "" : Fingerprint(payload)));
        return id;
    }

    private static async Task Delta(NpgsqlConnection c, Guid operation, string kind, Guid item, decimal quantity)
    {
        Require(kind is "Material" or "Finished", "재고 구분이 올바르지 않습니다.");
        Require(decimal.Round(quantity, 4) == quantity && (kind != "Finished" || decimal.Truncate(quantity) == quantity), "수량 단위를 확인하세요.");
        await One(c, kind == "Material" ? "busbar_materials" : "busbar_product_families", item);
        await Exec(c, "insert into busbar_stock values(@kind,@item,0) on conflict do nothing", ("kind", kind), ("item", item));
        var rows = await Rows(c, "select balance from busbar_stock where stock_kind=@kind and item_id=@item", ("kind", kind), ("item", item));
        Require(kind != "Finished" || Num(rows[0], "balance") + quantity >= 0, "완제품 재고가 부족합니다.");
        await Exec(c, "update busbar_stock set balance=balance+@quantity where stock_kind=@kind and item_id=@item", ("quantity", quantity), ("kind", kind), ("item", item));
        await Exec(c, "insert into busbar_ledger values(@id,@operation,@kind,@item,@quantity)", ("id", Guid.NewGuid()), ("operation", operation), ("kind", kind), ("item", item), ("quantity", quantity));
    }

    public Task<Guid> Adjustment(BusbarAdjustmentRequest r, Guid actor) => Transaction(async c =>
    {
        var kind = r.IsOpening ? "Opening" : "Adjustment";
        var old = await Existing(c, r.RequestId, kind, r.ItemId, r);
        if (old is not null) return old.Value;
        Text(r.Reason, "사유");
        Require(r.Quantity != 0, "0이 아닌 증감 수량이 필요합니다.");
        if (r.IsOpening) Require(r.Quantity > 0, "기초재고는 양수여야 합니다.");
        var id = await Operation(c, r.RequestId, kind, r.ItemId, actor, r.Reason, payload: r);
        await Delta(c, id, r.StockKind, r.ItemId, r.Quantity);
        return id;
    });

    public Task<Guid> Receipt(BusbarReceiptRequest r, Guid actor) => Transaction(async c =>
    {
        var old = await Existing(c, r.RequestId, "Receipt", r.PurchaseId, r);
        if (old is not null) return old.Value;
        Require(r.Quantity > 0, "입고 수량은 양수여야 합니다.");
        var purchase = await One(c, "busbar_purchases", r.PurchaseId);
        var received = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_receipts s where purchase_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", r.PurchaseId));
        Require(Num(received[0], "quantity") + r.Quantity <= Num(purchase, "quantity"), "발주 잔여 수량을 초과했습니다.");
        var id = await Operation(c, r.RequestId, "Receipt", r.PurchaseId, actor, "입고 등록", payload: r);
        await Delta(c, id, "Material", Id(purchase, "materialId"), r.Quantity);
        await Exec(c, "insert into busbar_receipts values(@id,@purchase,@quantity)", ("id", id), ("purchase", r.PurchaseId), ("quantity", r.Quantity));
        return id;
    });

    public Task<Guid> Shipment(BusbarShipmentRequest r, Guid actor) => Transaction(async c =>
    {
        var old = await Existing(c, r.RequestId, "Shipment", r.ProjectId, r);
        if (old is not null) return old.Value;
        Require(r.Quantity > 0, "출하 수량은 양수여야 합니다.");
        var project = await One(c, "busbar_projects", r.ProjectId);
        var shipped = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_shipments s where project_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", r.ProjectId));
        Require(Num(shipped[0], "quantity") + r.Quantity <= Num(project, "requestedQuantity"), "요청 잔여 수량을 초과했습니다.");
        var id = await Operation(c, r.RequestId, "Shipment", r.ProjectId, actor, "분할 출하", payload: r);
        await Delta(c, id, "Finished", Id(project, "productFamilyId"), -r.Quantity);
        await Exec(c, "insert into busbar_shipments values(@id,@project,@quantity)", ("id", id), ("project", r.ProjectId), ("quantity", r.Quantity));
        return id;
    });

    public Task<Guid> Reverse(Guid operation, BusbarReverseRequest r, Guid actor) => Transaction(c => ReverseOperation(c, operation, r, actor, false));

    private static async Task<Guid> ReverseOperation(NpgsqlConnection c, Guid operation, BusbarReverseRequest r, Guid actor, bool production)
    {
        var old = await Existing(c, r.RequestId, "Reversal", operation, r);
        if (old is not null) return old.Value;
        Text(r.Reason, "취소 사유");
        var original = await One(c, "busbar_operations", operation);
        var kind = (string)original["kind"]!;
        Require(kind != "Reversal" && (production || kind != "Production"), "제품 생산은 제품 화면에서 취소하세요.");
        Require((await Rows(c, "select id from busbar_operations where reverses_id=@id", ("id", operation))).Count == 0, "이미 취소된 기록입니다.");
        var id = await Operation(c, r.RequestId, "Reversal", operation, actor, r.Reason, operation, r);
        foreach (var l in await Rows(c, "select * from busbar_ledger where operation_id=@id", ("id", operation))) await Delta(c, id, (string)l["stockKind"]!, Id(l, "itemId"), -Num(l, "quantity"));
        return id;
    }

    public Task<Guid> Product(BusbarProductRequest r, Guid actor) => Transaction(async c =>
    {
        Require(r.RequestId != Guid.Empty, "요청 식별자가 필요합니다.");
        var existing = await Rows(c, "select * from busbar_products where request_id=@id", ("id", r.RequestId));
        if (existing.Count > 0)
        {
            Require(Id(existing[0], "productFamilyId") == r.ProductFamilyId && Id(existing[0], "workerId") == r.WorkerId, "이미 사용된 요청 식별자입니다.");
            return Id(existing[0]);
        }
        await Active(c, "busbar_product_families", r.ProductFamilyId);
        await Active(c, "busbar_workers", r.WorkerId);
        var worker = await One(c, "busbar_workers", r.WorkerId);
        var id = Guid.NewGuid();
        await Exec(c, "insert into busbar_products(id,request_id,product_family_id,worker_id,worker_name,public_token,created_by) values(@id,@request,@family,@worker,@name,@token,@actor)", ("id", id), ("request", r.RequestId), ("family", r.ProductFamilyId), ("worker", r.WorkerId), ("name", worker["name"]), ("token", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()), ("actor", actor));
        return id;
    });

    public Task<Guid> Photo(Guid product, string side, byte[] content, string? reason, Guid actor, Guid? workerId = null) => Transaction(async c =>
    {
        Require(side is "front" or "back", "사진 위치를 확인하세요.");
        var p = await One(c, "busbar_products", product);
        Require((string)p["status"]! != "Cancelled", "취소된 제품입니다.");
        if ((string)p["status"]! == "Draft")
        {
            var selectedWorkerId = workerId ?? (p["workerId"] as Guid?);
            Require(selectedWorkerId is not null && selectedWorkerId != Guid.Empty, "실제 작업자를 선택한 뒤 사진을 등록하세요.");
            await Active(c, "busbar_workers", selectedWorkerId!.Value);
            var worker = await One(c, "busbar_workers", selectedWorkerId.Value);
            await Exec(c, "update busbar_products set worker_id=@worker,worker_name=@name where id=@id",
                ("worker", selectedWorkerId.Value), ("name", worker["name"]), ("id", product));
            if (!Equals(p["workerId"], selectedWorkerId.Value) || !Equals(p["workerName"], worker["name"]))
            {
                await Audit(c, "ProductWorker", product, actor, "사진 등록 시 작업자 지정",
                    new { workerId = p["workerId"], workerName = p["workerName"] },
                    new { workerId = selectedWorkerId.Value, workerName = worker["name"] });
            }
        }
        else
        {
            Text(reason, "사진 정정 사유");
            Require(workerId is null || Equals(p["workerId"], workerId.Value),
                "완료 제품의 작업자는 작업자 정정 기능에서 변경하세요.");
        }
        var now = timeProvider.GetUtcNow();
        await Exec(c, "insert into busbar_photos(product_id,side,content,content_type,registered_at_utc,registered_by) values(@id,@side,@content,'image/jpeg',@now,@actor) on conflict(product_id,side) do update set content=excluded.content,registered_at_utc=excluded.registered_at_utc,registered_by=excluded.registered_by", ("id", product), ("side", side), ("content", content), ("now", now), ("actor", actor));
        await Exec(c, "insert into busbar_photo_history values(@history,@id,@side,@content,@now,@actor,@reason)",
            ("history", Guid.NewGuid()), ("id", product), ("side", side), ("content", content), ("now", now),
            ("actor", actor), ("reason", reason ?? "사진 등록"));
        var photos = await Rows(c, "select side from busbar_photos where product_id=@id", ("id", product));
        if ((string)p["status"]! == "Draft" && photos.Count == 2)
        {
            var boms = await Rows(c, "select id from busbar_boms where product_family_id=@family order by version desc limit 1", ("family", p["productFamilyId"]));
            Require(boms.Count > 0, "표준 소요량을 설정한 뒤 두 번째 사진을 다시 등록하세요.");
            var bom = Id(boms[0]);
            var operation = await Operation(c, product, "Production", product, actor, "사진 두 장 등록 생산 완료");
            await Delta(c, operation, "Finished", Id(p, "productFamilyId"), 1);
            foreach (var l in await Rows(c, "select * from busbar_bom_lines where bom_id=@id", ("id", bom))) await Delta(c, operation, "Material", Id(l, "materialId"), -Num(l, "quantity"));
            await Exec(c, "update busbar_products set status='Complete',number='IB-'||lpad(nextval('busbar_product_number_seq')::text,8,'0'),manufactured_at_utc=@now,photo_registered_by=@actor,bom_id=@bom,revision=revision+1,publication_state='Pending',publication_error=null where id=@id", ("now", now), ("bom", bom), ("id", product), ("actor", actor));
            await EnsureQr(c, product, (string)p["publicToken"]!);
        }
        else if ((string)p["status"]! == "Complete") await Exec(c, "update busbar_products set revision=revision+1,publication_state='Pending',publication_error=null where id=@id", ("id", product));
        else await Exec(c, "update busbar_products set revision=revision+1 where id=@id", ("id", product));
        await Audit(c, "ProductPhoto", product, actor, reason ?? "사진 등록", new
        {
            side
        }
       , new
       {
           side,
           registeredAtUtc = now
       });
        return product;
    });

    public Task<Guid> CorrectProduct(Guid id, BusbarProductCorrectionRequest r, Guid actor) => Transaction(async c =>
    {
        Text(r.Reason, "정정 사유");
        var p = await One(c, "busbar_products", id);
        Require((string)p["status"]! != "Cancelled", "취소된 제품입니다.");
        await Active(c, "busbar_workers", r.WorkerId);
        var worker = await One(c, "busbar_workers", r.WorkerId);
        await Exec(c, "update busbar_products set worker_id=@worker,worker_name=@name,revision=revision+case when status='Complete' then 1 else 0 end,publication_state='Pending',publication_error=null where id=@id", ("worker", r.WorkerId), ("name", worker["name"]), ("id", id));
        await Audit(c, "Product", id, actor, r.Reason, p, r);
        return id;
    });

    public Task<Guid> CancelProduct(Guid id, BusbarReverseRequest r, Guid actor) => Transaction(async c =>
    {
        Text(r.Reason, "취소 사유");
        var p = await One(c, "busbar_products", id);
        var prior = await Rows(c, "select * from busbar_operations where request_id=@request", ("request", r.RequestId));
        if ((string)p["status"]! == "Cancelled")
        {
            var referenceMatches = prior.Count > 0 && (Equals(prior[0]["referenceId"], id) || (await Rows(c, "select id from busbar_operations where id=@operation and kind='Production' and reference_id=@product", ("operation", prior[0]["referenceId"]), ("product", id))).Count > 0);
            Require(referenceMatches && (string)prior[0]["kind"]! == "Reversal" && Equals(prior[0]["requestFingerprint"], Fingerprint(r)), "이미 취소된 제품입니다.");
            return id;
        }
        if ((string)p["status"]! == "Complete")
        {
            var op = await Rows(c, "select id from busbar_operations where kind='Production' and reference_id=@id", ("id", id));
            await ReverseOperation(c, Id(op[0]), r, actor, true);
        }
        else await Operation(c, r.RequestId, "Reversal", id, actor, r.Reason, payload: r);
        await Exec(c, "update busbar_products set status='Cancelled',revision=revision+1,publication_state='Pending',publication_error=null where id=@id", ("id", id));
        await Audit(c, "Product", id, actor, r.Reason, p, new
        {
            status = "Cancelled"
        });
        return id;
    });

    public Task<Dictionary<string, object?>> GetProduct(Guid id) => ReadSnapshot(async c =>
    {
        var rows = await Rows(c, "select p.*,exists(select 1 from busbar_product_qr qr where qr.product_id=p.id) qr_ready,(select display_name from qms_users u where u.id=coalesce(p.photo_registered_by,p.created_by)) registered_by_display_name,exists(select 1 from busbar_photos f where f.product_id=p.id and side='front') has_front,exists(select 1 from busbar_photos f where f.product_id=p.id and side='back') has_back from busbar_products p where p.id=@id", ("id", id));
        if (rows.Count == 0) throw new BusbarException("not_found", "제품을 찾을 수 없습니다.", 404);
        SetQrState(rows[0]);
        return rows[0];
    });

    private void SetQrState(Dictionary<string, object?> product)
    {
        product["qrState"] = (string)product["status"]! == "Cancelled" ? "Cancelled"
            : (bool)product["qrReady"]! ? "Ready"
            : product["manufacturedAtUtc"] is null ? "AwaitingCompletion"
            : publicationOptions?.PublicBaseUrl is null ? "ConfigurationPending" : "PendingGeneration";
    }

    private async Task<byte[]?> EnsureQr(NpgsqlConnection c, Guid product, string token)
    {
        var existing = await Rows(c, "select png from busbar_product_qr where product_id=@id", ("id", product));
        if (existing.Count > 0) return (byte[])existing[0]["png"]!;
        // Manufacturing must remain available before a public website has been configured.
        if (publicationOptions?.PublicBaseUrl is null) return null;
        var url = publicationOptions.GetPublicUrl(token);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var image = new PngByteQRCode(data);
        var png = image.GetGraphic(8);
        await Exec(c, "insert into busbar_product_qr(product_id,url,png,created_at_utc) values(@id,@url,@png,@now)",
            ("id", product), ("url", url), ("png", png), ("now", timeProvider.GetUtcNow()));
        return png;
    }

    public Task<byte[]> GetPrintableQr(Guid id, bool allowPersist = true) => allowPersist
        ? Transaction(c => ReadPrintableQr(c, id, true))
        : ReadSnapshot(c => ReadPrintableQr(c, id, false));

    private async Task<byte[]> ReadPrintableQr(NpgsqlConnection c, Guid id, bool allowPersist)
    {
        var product = await One(c, "busbar_products", id);
        if ((string)product["status"]! != "Complete" || (string)product["publicationState"]! != "Published"
            || Convert.ToInt32(product["revision"]) != Convert.ToInt32(product["publishedRevision"]))
            throw new BusbarException("publication_not_ready", "최신 제품 정보의 게시가 완료된 뒤 출력하세요.", 409);
        var stored = await Rows(c, "select url,png from busbar_product_qr where product_id=@id", ("id", id));
        if (stored.Count > 0 && publicationOptions?.PublicBaseUrl is not null &&
            !string.Equals((string)stored[0]["url"]!, publicationOptions.GetPublicUrl((string)product["publicToken"]!), StringComparison.Ordinal))
            throw new BusbarException("qr_public_url_changed", "저장된 QR 주소와 현재 공개 주소가 다릅니다. 공개 사이트 설정을 확인하세요.", 409);
        if (stored.Count > 0) return (byte[])stored[0]["png"]!;
        if (!allowPersist)
            throw new BusbarException("qr_generation_pending", "QR 생성이 대기 중입니다. 읽기 전용 검수 모드에서는 새 QR을 생성하지 않습니다.", 409);
        // Backfill previously completed products under the same lock as production and corrections.
        var png = await EnsureQr(c, id, (string)product["publicToken"]!);
        return png ?? throw new BusbarException("qr_configuration_pending", "외부 게시 주소가 설정되지 않았습니다.", 409);
    }

    public Task<byte[]> GetPhoto(Guid id, string side) => ReadSnapshot(async c =>
    {
        var rows = await Rows(c, "select content from busbar_photos where product_id=@id and side=@side", ("id", id), ("side", side));
        if (rows.Count == 0) throw new BusbarException("not_found", "사진이 없습니다.", 404);
        return (byte[])rows[0]["content"]!;
    });

    public Task<Guid> RetryPublication(Guid id) => Transaction(async c =>
    {
        var p = await One(c, "busbar_products", id);
        Require((string)p["status"]! != "Draft" && p["manufacturedAtUtc"] is not null, "완료된 제품만 게시할 수 있습니다.");
        if ((string)p["publicationState"]! == "Published" && Convert.ToInt32(p["revision"]) == Convert.ToInt32(p["publishedRevision"])) return id;
        await Exec(c, "update busbar_products set publication_state='Pending',publication_error=null where id=@id", ("id", id));
        return id;
    });

    public Task<bool> ValidateImportRow(object row) => ReadSnapshot(async c =>
    {
        Text((string)(await Rows(c, "select common_project_code from busbar_settings"))[0]["commonProjectCode"]!, "공통 프로젝트 코드");
        if (row is BusbarProjectRequest p)
        {
            Text(p.Name, "프로젝트명"); Text(p.Destination, "도착지");
            Require(p.RequestedQuantity > 0, "요청 수량은 양수여야 합니다.");
            await Active(c, "busbar_product_families", p.ProductFamilyId);
            if (p.Id is not null)
            {
                Text(p.Reason, "정정 사유");
                var existing = await One(c, "busbar_projects", p.Id.Value);
                Require(Id(existing, "productFamilyId") == p.ProductFamilyId, "기존 등록 건의 제품군은 변경할 수 없습니다.");
                var shipped = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_shipments s where project_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", p.Id.Value));
                Require(p.RequestedQuantity >= Num(shipped[0], "quantity"), "출하량보다 작은 요청 수량입니다.");
            }
        }
        else if (row is BusbarPurchaseRequest purchase)
        {
            Text(purchase.OrderNumber, "발주번호");
            Require(purchase.Quantity > 0 && decimal.Round(purchase.Quantity, 4) == purchase.Quantity, "수량은 소수 네 자리 이내 양수여야 합니다.");
            await Active(c, "busbar_materials", purchase.MaterialId);
            if (purchase.Id is not null)
            {
                Text(purchase.Reason, "정정 사유");
                var existing = await One(c, "busbar_purchases", purchase.Id.Value);
                Require(Id(existing, "materialId") == purchase.MaterialId, "발주 자재는 변경할 수 없습니다.");
                var received = await Rows(c, "select coalesce(sum(quantity),0) quantity from busbar_receipts s where purchase_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=s.id)", ("id", purchase.Id.Value));
                Require(purchase.Quantity >= Num(received[0], "quantity"), "입고량보다 작은 발주 수량입니다.");
            }
        }
        return true;
    });

    public Task<Guid> ResolveCode(string kind, string code) => ReadSnapshot(async c =>
    {
        var table = kind == "Material" ? "busbar_materials" : "busbar_product_families";
        var rows = await Rows(c, $"select id from {table} where code=@code and is_active", ("code", code.Trim()));
        if (rows.Count == 0) throw new BusbarException("unknown_code", "등록된 기준정보 코드를 확인하세요.");
        return Id(rows[0]);
    });

    public Task<object> ApplyProjects(IReadOnlyList<BusbarProjectRequest> rows, Guid actor) => Transaction<object>(async c =>
    {
        Require(rows.Count is > 0 and <= 500, "1~500개 행을 적용하세요.");
        Require(rows.Where(x => x.Id is not null).Select(x => x.Id).Distinct().Count() == rows.Count(x => x.Id is not null), "등록 건 ID가 중복되었습니다.");
        foreach (var r in rows) await SaveProject(c, r, actor);
        return new
        {
            count = rows.Count
        }
       ;
    });

    public Task<object> ApplyPurchases(IReadOnlyList<BusbarPurchaseRequest> rows, Guid actor) => Transaction<object>(async c =>
    {
        Require(rows.Count is > 0 and <= 500, "1~500개 행을 적용하세요.");
        Require(rows.Where(x => x.Id is not null).Select(x => x.Id).Distinct().Count() == rows.Count(x => x.Id is not null), "발주 ID가 중복되었습니다.");
        foreach (var r in rows) await SavePurchase(c, r, actor);
        return new
        {
            count = rows.Count
        }
       ;
    });
}

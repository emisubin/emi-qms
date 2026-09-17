namespace Emi.Qms.Api.InteriorBusbar;

public sealed partial class InteriorBusbarStore
{
    public Task<Guid> SetDeleted(string kind, Guid id, string reason, Guid actor, bool deleted) => Transaction(async c =>
    {
        var (table, auditKind) = kind switch
        {
            "projects" => ("busbar_projects", "Project"),
            "plans" => ("busbar_plans", "Plan"),
            "purchases" => ("busbar_purchases", "Purchase"),
            "product-families" => ("busbar_product_families", "product-families"),
            "materials" => ("busbar_materials", "materials"),
            "workers" => ("busbar_workers", "workers"),
            "boms" => ("busbar_boms", "Bom"),
            _ => throw new BusbarException("invalid_kind", "삭제 대상을 확인하세요.")
        };
        var normalizedReason = Text(reason, deleted ? "삭제 사유" : "복원 사유");
        var before = await One(c, table, id);
        if ((bool)before["isDeleted"]! == deleted) return id;

        if (deleted) await CheckDelete(c, kind, id, before, actor);
        else await CheckRestore(c, kind, before);

        if (kind == "projects")
        {
            if (deleted)
                await Exec(c, "update busbar_ecount_jobs set state='Held',message='프로젝트 삭제 · 자동 전송 중지',updated_at_utc=now() where project_id=@id and state in ('Pending','Failed')", ("id", id));
            else
                await Exec(c, "update busbar_ecount_jobs set message='프로젝트 복원 · 관리자 수동 재시도 필요',updated_at_utc=now() where project_id=@id and state='Held' and message='프로젝트 삭제 · 자동 전송 중지'", ("id", id));
        }
        if (kind == "plans" && deleted)
            await Exec(c, "update busbar_plans set products_initialized=false where id=@id", ("id", id));

        if (deleted)
            await Exec(c, $"update {table} set is_deleted=true,deleted_at_utc=@now,deleted_by=@actor,delete_reason=@reason,restored_at_utc=null,restored_by=null,restore_reason=null where id=@id",
                ("now", timeProvider.GetUtcNow()), ("actor", actor), ("reason", normalizedReason), ("id", id));
        else
            await Exec(c, $"update {table} set is_deleted=false,restored_at_utc=@now,restored_by=@actor,restore_reason=@reason where id=@id",
                ("now", timeProvider.GetUtcNow()), ("actor", actor), ("reason", normalizedReason), ("id", id));

        await Audit(c, auditKind, id, actor, normalizedReason, before, await One(c, table, id));
        return id;
    });

    private static async Task CheckDelete(Npgsql.NpgsqlConnection c, string kind, Guid id, Dictionary<string, object?> row, Guid actor)
    {
        if (kind == "projects")
        {
            Require((await Rows(c, "select id from busbar_ecount_jobs where project_id=@id and state in ('InFlight','Unknown') limit 1", ("id", id))).Count == 0,
                "이카운트 전송 중이거나 결과 확인이 필요한 프로젝트입니다. 전표 상태를 먼저 확인하세요.");
            return;
        }
        if (kind == "plans")
        {
            Require((await Rows(c, "select id from busbar_products p where plan_id=@id and status<>'Cancelled' and (status='Complete' or worker_id is not null or exists(select 1 from busbar_photos f where f.product_id=p.id)) limit 1", ("id", id))).Count == 0,
                "작업자 지정·사진 등록·완료된 제품이 있습니다. 제품을 먼저 취소한 뒤 계획을 삭제하세요.");
            foreach (var product in await Rows(c, "select * from busbar_products where plan_id=@id and status='Draft' and worker_id is null and not exists(select 1 from busbar_photos f where f.product_id=busbar_products.id)", ("id", id)))
            {
                await Exec(c, "update busbar_products set status='Cancelled',revision=revision+1,publication_state='Pending',publication_error=null where id=@id", ("id", Id(product)));
                await Audit(c, "Product", Id(product), actor, "생산계획 삭제로 미착수 제품 철회", product, new { status = "Cancelled", planId = id, planSequence = product["planSequence"] });
            }
            return;
        }
        if (kind == "purchases")
        {
            Require((await Rows(c, "select id from busbar_receipts r where purchase_id=@id and not exists(select 1 from busbar_operations o where o.reverses_id=r.id) limit 1", ("id", id))).Count == 0,
                "활성 입고를 먼저 취소한 뒤 발주를 삭제하세요.");
            return;
        }
        if (kind == "product-families")
        {
            Require(await BalanceIsZero(c, "Finished", id), "완제품 재고가 남아 있어 제품군을 삭제할 수 없습니다.");
            Require((await Rows(c, "select id from busbar_projects where product_family_id=@id and not is_deleted limit 1", ("id", id))).Count == 0, "사용 중인 프로젝트가 있어 제품군을 삭제할 수 없습니다.");
            Require((await Rows(c, "select id from busbar_plans where product_family_id=@id and not is_deleted limit 1", ("id", id))).Count == 0, "사용 중인 생산계획이 있어 제품군을 삭제할 수 없습니다.");
            Require((await Rows(c, "select id from busbar_products where product_family_id=@id and status<>'Cancelled' limit 1", ("id", id))).Count == 0, "사용 중인 제품이 있습니다. 제품을 먼저 취소하세요.");
            return;
        }
        if (kind == "materials")
        {
            Require(await BalanceIsZero(c, "Material", id), "자재 재고가 남아 있어 삭제할 수 없습니다.");
            Require((await Rows(c, "select id from busbar_purchases where material_id=@id and not is_deleted limit 1", ("id", id))).Count == 0, "사용 중인 발주가 있어 자재를 삭제할 수 없습니다.");
            Require((await Rows(c, "select b.id from busbar_boms b join busbar_product_families f on f.id=b.product_family_id join busbar_bom_lines l on l.bom_id=b.id where l.material_id=@id and not b.is_deleted and not f.is_deleted limit 1", ("id", id))).Count == 0, "사용 중인 BOM이 있어 자재를 삭제할 수 없습니다.");
            return;
        }
        if (kind == "workers")
        {
            Require((await Rows(c, "select id from busbar_products where worker_id=@id and status<>'Cancelled' limit 1", ("id", id))).Count == 0,
                "진행 중이거나 완료된 제품이 있습니다. 제품을 먼저 취소하세요.");
            return;
        }
        if (kind == "boms")
        {
            Require((await Rows(c, "select id from busbar_products where bom_id=@id limit 1", ("id", id))).Count == 0,
                "생산 이력에서 사용하는 BOM은 삭제할 수 없습니다.");
            // Completion always reads the absolute latest version and rejects its tombstone,
            // so deleting the newest unused version can never fall back to an older BOM.
        }
    }

    private static async Task CheckRestore(Npgsql.NpgsqlConnection c, string kind, Dictionary<string, object?> row)
    {
        if (kind is "projects" or "plans") await Active(c, "busbar_product_families", Id(row, "productFamilyId"));
        else if (kind == "purchases") await Active(c, "busbar_materials", Id(row, "materialId"));
        else if (kind == "product-families")
        {
            var latest = await Rows(c, "select id,is_deleted from busbar_boms where product_family_id=@id order by version desc limit 1", ("id", Id(row)));
            if (latest.Count > 0 && !(bool)latest[0]["isDeleted"]!)
                foreach (var line in await Rows(c, "select material_id from busbar_bom_lines where bom_id=@id", ("id", Id(latest[0]))))
                    await Active(c, "busbar_materials", Id(line, "materialId"));
        }
        else if (kind == "boms")
        {
            await Active(c, "busbar_product_families", Id(row, "productFamilyId"));
            foreach (var line in await Rows(c, "select material_id from busbar_bom_lines where bom_id=@id", ("id", Id(row))))
                await Active(c, "busbar_materials", Id(line, "materialId"));
        }
    }

    private static async Task<bool> BalanceIsZero(Npgsql.NpgsqlConnection c, string kind, Guid id)
    {
        var rows = await Rows(c, "select balance from busbar_stock where stock_kind=@kind and item_id=@id", ("kind", kind), ("id", id));
        return rows.Count == 0 || Num(rows[0], "balance") == 0;
    }
}

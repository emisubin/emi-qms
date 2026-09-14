using System.Text.Json;
using Npgsql;

namespace Emi.Qms.Api.InteriorBusbar;

internal sealed record BusbarEcountAttempt(Guid Id, Guid JobId, string Kind, string Payload);
internal sealed record BusbarEcountResult(string State, string? SlipNumber = null);

public sealed partial class InteriorBusbarStore
{
    private static Task EnqueueEcount(NpgsqlConnection c, Guid project, string kind) => Exec(c,
        "insert into busbar_ecount_jobs(id,project_id,kind) values(@id,@project,@kind) on conflict(project_id,kind) do nothing",
        ("id", Guid.NewGuid()), ("project", project), ("kind", kind));

    private static async Task<bool> EcountProjectComplete(NpgsqlConnection c, Guid project)
    {
        var rows = await Rows(c, """
            select p.requested_quantity=coalesce(sum(s.quantity) filter(where o.id is null),0) complete
            from busbar_projects p left join busbar_shipments s on s.project_id=p.id
            left join busbar_operations o on o.reverses_id=s.id where p.id=@id group by p.id
            """, ("id", project));
        return rows.Count > 0 && (bool)rows[0]["complete"]!;
    }

    private static async Task SyncEcountSale(NpgsqlConnection c, Guid project)
    {
        if (await EcountProjectComplete(c, project))
        {
            await EnqueueEcount(c, project, "Sale");
            await Exec(c, "update busbar_ecount_jobs set state='Pending',message=null,updated_at_utc=now() where project_id=@id and kind='Sale' and state='Held' and message='납품 미완료' and not needs_review", ("id", project));
        }
        else
        {
            await Exec(c, """
                update busbar_ecount_jobs set
                  needs_review=needs_review or state in ('InFlight','Succeeded','Unknown'),
                  state=case when state in ('Pending','Failed','Held') then 'Held' else state end,
                  message='납품 미완료',updated_at_utc=now()
                where project_id=@id and kind='Sale'
                """, ("id", project));
        }
    }

    // Called under the same transaction lock as each business mutation. Sent/uncertain
    // snapshots stay immutable; business changes require reconciliation, never a second send.
    private static async Task InvalidateEcountJobs(NpgsqlConnection c, string predicate, Guid? value)
    {
        var jobs = await Rows(c, $"select * from busbar_ecount_jobs where state in ('InFlight','Succeeded','Unknown') and project_id in(select id from busbar_projects where {predicate})", ("value", value));
        foreach (var job in jobs)
        {
            var project = await One(c, "busbar_projects", Id(job, "projectId"));
            var family = await One(c, "busbar_product_families", Id(project, "productFamilyId"));
            var settings = (await Rows(c, "select * from busbar_settings"))[0];
            await Exec(c, """
                update busbar_ecount_jobs set needs_review=true,message='전송 후 기준정보 또는 프로젝트 변경 확인 필요',updated_at_utc=now()
                where id=@id and exists(select 1 from busbar_ecount_attempts where id=current_attempt_id and payload<>@payload::jsonb)
                """, ("id", Id(job)), ("payload", EcountPayload(project, family, settings)));
        }
    }

    private static string EcountPayload(Dictionary<string, object?> project, Dictionary<string, object?> family, Dictionary<string, object?> settings)
    {
        var price = family["standardUnitPrice"] as decimal?;
        var supply = price * Convert.ToInt32(project["requestedQuantity"]);
        var vat = supply * 0.1m;
        return JsonSerializer.Serialize(new {
            projectId = Id(project), projectName = project["name"], workOrderNumber = project["customerJobNumber"],
            purchaseOrderNumber = "", productCode = family["ecountProductCode"],
            customerCode = settings["ecountCustomerCode"], warehouseCode = settings["ecountWarehouseCode"],
            commonProjectCode = project["commonProjectCode"], quantity = project["requestedQuantity"], unitPrice = price,
            supplyAmount = supply, vatAmount = vat, totalAmount = supply + vat,
            dueDate = project["dueDate"], currency = "KRW", vatRate = 0.1m
        });
    }

    public Task<object> EcountStatus(Guid project) => ReadSnapshot<object>(async c =>
    {
        await One(c, "busbar_projects", project);
        return new { transmissionEnabled = false, jobs = await Rows(c, """
            select id,kind,state,needs_review,message,slip_number,updated_at_utc,
              (select count(*) from busbar_ecount_attempts a where a.job_id=j.id) attempt_count
            from busbar_ecount_jobs j where project_id=@project order by kind
            """, ("project", project)) };
    });

    public Task<Guid> RetryEcount(Guid job, string reason, Guid actor) => Transaction(async c =>
    {
        Text(reason, "재시도 사유");
        var before = await One(c, "busbar_ecount_jobs", job);
        Require((string)before["state"]! is "Held" or "Failed" && !(bool)before["needsReview"]!,
            "이미 전송했거나 결과 확인이 필요한 건은 다시 전송할 수 없습니다.");
        if ((string)before["kind"]! == "Sale") Require(await EcountProjectComplete(c, Id(before, "projectId")), "납품 완료 후 다시 대기시킬 수 있습니다.");
        await Exec(c, "update busbar_ecount_jobs set state='Pending',message=null,updated_at_utc=now() where id=@id", ("id", job));
        await Audit(c, "EcountJob", job, actor, reason, before, new { state = "Pending" });
        return job;
    });

    // The authenticated provider adapter will call these methods. There is deliberately
    // no background dispatcher or HTTP send until provider configuration is verified.
    internal Task<BusbarEcountAttempt?> ClaimEcountJob(Guid job) => Transaction<BusbarEcountAttempt?>(async c =>
    {
        var row = await One(c, "busbar_ecount_jobs", job);
        if ((string)row["state"]! != "Pending" || (bool)row["needsReview"]!) return null;
        var project = await One(c, "busbar_projects", Id(row, "projectId"));
        var family = await One(c, "busbar_product_families", Id(project, "productFamilyId"));
        var settings = (await Rows(c, "select * from busbar_settings"))[0];
        var kind = (string)row["kind"]!;
        var price = family["standardUnitPrice"] as decimal?;
        var commonCode = (string)project["commonProjectCode"]!;
        string? issue = null;
        if (price is null || string.IsNullOrWhiteSpace(family["ecountProductCode"] as string) ||
            string.IsNullOrWhiteSpace(settings["ecountCustomerCode"] as string) || string.IsNullOrWhiteSpace(settings["ecountWarehouseCode"] as string) ||
            string.IsNullOrWhiteSpace(commonCode) || commonCode.Length > 14) issue = "단가 또는 이카운트 기준정보 설정 필요";
        var vat = price * Convert.ToInt32(project["requestedQuantity"]) * 0.1m;
        if (vat is not null && decimal.Round(vat.Value, 4) != vat.Value) issue = "부가세가 API 소수 네 자리를 초과함 · 금액 확인 필요";
        if (kind == "Order" && price >= 1000000000000m) issue = "주문서 API 단가 범위 초과 · 금액 확인 필요";
        if (kind == "Sale")
        {
            if (!await EcountProjectComplete(c, Id(project))) issue = "납품 미완료";
            else if ((await Rows(c, "select id from busbar_ecount_jobs where project_id=@id and kind='Order' and (state<>'Succeeded' or needs_review)", ("id", Id(project)))).Count > 0)
                issue = "주문서 전송 결과 확인 필요";
        }
        if (issue is not null)
        {
            await Exec(c, "update busbar_ecount_jobs set state='Held',message=@message,updated_at_utc=now() where id=@id", ("id", job), ("message", issue));
            return null;
        }
        var now = timeProvider.GetUtcNow();
        var attempt = Guid.NewGuid();
        // Domain snapshot, not a wire request: final ERP amount policy is separate.
        var payload = EcountPayload(project, family, settings);
        await Exec(c, "insert into busbar_ecount_attempts(id,job_id,payload,state,started_at_utc) values(@attempt,@job,@payload::jsonb,'InFlight',@now)",
            ("attempt", attempt), ("job", job), ("payload", payload), ("now", now));
        await Exec(c, "update busbar_ecount_jobs set state='InFlight',current_attempt_id=@attempt,message=null,updated_at_utc=@now where id=@job",
            ("attempt", attempt), ("job", job), ("now", now));
        return new(attempt, job, kind, payload);
    });

    internal Task<bool> FinishEcountAttempt(Guid attempt, BusbarEcountResult result) => Transaction(async c =>
    {
        Require(result.State is "Succeeded" or "Failed" or "Unknown", "전송 결과를 확인하세요.");
        if (result.State == "Succeeded") Text(result.SlipNumber, "이카운트 전표번호");
        var a = await One(c, "busbar_ecount_attempts", attempt);
        var job = await One(c, "busbar_ecount_jobs", Id(a, "jobId"));
        if (!Equals(job["currentAttemptId"], attempt) || (string)a["state"]! is not ("InFlight" or "Unknown")) return false;
        var message = result.State switch { "Unknown" => "전표 생성 여부 확인 필요 · 자동 재전송 중지", "Failed" => "전표 미생성 확인 · 설정 점검 후 재시도 가능", _ => null };
        await Exec(c, "update busbar_ecount_attempts set state=@state,slip_number=@slip,finished_at_utc=@now where id=@id",
            ("state", result.State), ("slip", result.SlipNumber), ("now", timeProvider.GetUtcNow()), ("id", attempt));
        await Exec(c, "update busbar_ecount_jobs set state=@state,slip_number=@slip,needs_review=case when @state='Failed' then false else needs_review end,message=case when needs_review and @state<>'Failed' then message else @message end,updated_at_utc=@now where id=@id",
            ("state", result.State), ("slip", result.SlipNumber), ("message", message), ("now", timeProvider.GetUtcNow()), ("id", Id(job)));
        if (result.State == "Succeeded" && (string)job["kind"]! == "Order")
            await Exec(c, "update busbar_ecount_jobs set state='Pending',message=null where project_id=@project and kind='Sale' and state='Held' and message='주문서 전송 결과 확인 필요' and not needs_review", ("project", Id(job, "projectId")));
        return true;
    });

    internal Task<int> RecoverEcountAttempts() => Transaction(async c =>
    {
        var stale = await Rows(c, "select j.id from busbar_ecount_jobs j join busbar_ecount_attempts a on a.id=j.current_attempt_id where j.state='InFlight' and a.started_at_utc<@cutoff", ("cutoff", timeProvider.GetUtcNow().AddMinutes(-5)));
        foreach (var row in stale)
        {
            await Exec(c, "update busbar_ecount_attempts set state='Unknown' where id=(select current_attempt_id from busbar_ecount_jobs where id=@id)", ("id", Id(row)));
            await Exec(c, "update busbar_ecount_jobs set state='Unknown',message='전송 중단 · 전표 생성 여부 확인 필요',updated_at_utc=@now where id=@id", ("id", Id(row)), ("now", timeProvider.GetUtcNow()));
        }
        return stale.Count;
    });
}

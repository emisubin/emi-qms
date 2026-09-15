using System.Text.Json;
using Npgsql;

namespace Emi.Qms.Api.InteriorBusbar;

internal sealed record BusbarEcountAttempt(Guid Id, Guid JobId, string Kind, string Payload);
internal sealed record BusbarEcountResult(string State, string? SlipNumber = null);

public sealed partial class InteriorBusbarStore
{
    private static async Task<string?> EcountEmployeeCode(NpgsqlConnection c, Dictionary<string, object?> project)
    {
        if (project["registeredBy"] is not Guid user) return null;
        var rows = await Rows(c, "select employee_code from busbar_ecount_employees where user_id=@id", ("id", user));
        return rows.Count == 0 ? null : rows[0]["employeeCode"] as string;
    }

    public Task<Guid> EcountEmployee(BusbarEcountEmployeeRequest request, Guid actor) => Transaction(async c =>
    {
        await One(c, "qms_users", request.UserId);
        var code = Text(request.EmployeeCode, "이카운트 담당자 코드");
        Require(code.Length <= 30 && !code.Any(char.IsControl), "담당자 코드는 30자 이내로 입력하세요.");
        Text(request.Reason, "변경 사유");
        var before = await Rows(c, "select * from busbar_ecount_employees where user_id=@id", ("id", request.UserId));
        await Exec(c, "insert into busbar_ecount_employees(user_id,employee_code) values(@id,@code) on conflict(user_id) do update set employee_code=excluded.employee_code", ("id", request.UserId), ("code", code));
        await InvalidateEcountJobs(c, "registered_by=@value", request.UserId);
        await Audit(c, "EcountEmployee", request.UserId, actor, request.Reason, before, request);
        return request.UserId;
    });

    private static Task EnqueueEcount(NpgsqlConnection c, Guid project, string kind) => Exec(c,
        "insert into busbar_ecount_jobs(id,project_id,kind) values(@id,@project,@kind) on conflict(project_id,kind) where shipment_id is null do nothing",
        ("id", Guid.NewGuid()), ("project", project), ("kind", kind));

    private static async Task<int> EcountShipped(NpgsqlConnection c, Guid project) => Convert.ToInt32((await Rows(c,
        "select coalesce(sum(s.quantity) filter(where o.id is null),0) quantity from busbar_shipments s left join busbar_operations o on o.reverses_id=s.id where s.project_id=@id", ("id", project)))[0]["quantity"]);

    private static Task EnqueueShipmentSale(NpgsqlConnection c, Guid project, Guid shipment) => Exec(c,
        "insert into busbar_ecount_jobs(id,project_id,kind,shipment_id) values(@id,@project,'Sale',@shipment) on conflict(shipment_id) where shipment_id is not null do nothing",
        ("id", Guid.NewGuid()), ("project", project), ("shipment", shipment));

    private static Task CancelShipmentSale(NpgsqlConnection c, Guid shipment) => Exec(c, """
        update busbar_ecount_jobs set
          needs_review=needs_review or state in ('InFlight','Succeeded','Unknown'),
          state=case when state in ('Pending','Failed','Held') then 'Held' else state end,
          message='출하 취소 · 이미 생성된 판매전표는 이카운트에서 확인 필요',updated_at_utc=now()
        where shipment_id=@id
        """, ("id", shipment));

    internal static (string Date, string Number)? ParseOrderSlip(string? slip)
    {
        if (slip is null || slip.Length > 40) return null;
        var match = System.Text.RegularExpressions.Regex.Match(slip.Trim(), @"^(\d{4})/?(\d{2})/?(\d{2})\s*-\s*([1-9]\d{0,8})$");
        if (!match.Success) return null;
        var date = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;
        return DateOnly.TryParseExact(date, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _) ? (date, match.Groups[4].Value) : null;
    }

    private static async Task<string?> SaleIssue(NpgsqlConnection c, Dictionary<string, object?> job)
    {
        if (job["shipmentId"] is not Guid shipment) return "이전 프로젝트 단위 판매 대기 · 출하별 전송 전 확인 필요";
        if ((await Rows(c, "select id from busbar_operations where reverses_id=@id", ("id", shipment))).Count > 0) return "취소된 출하는 전송할 수 없습니다.";
        var orders = await Rows(c, "select * from busbar_ecount_jobs where project_id=@id and kind='Order'", ("id", Id(job, "projectId")));
        if (orders.Count != 1 || (string)orders[0]["state"]! != "Succeeded" || (bool)orders[0]["needsReview"]!) return "주문서 전송 결과 확인 필요";
        if (ParseOrderSlip(orders[0]["slipNumber"] as string) is null) return "원주문 전표번호 형식 확인 필요";
        if ((await Rows(c, """
            select id from busbar_ecount_jobs where project_id=@project and kind='Sale' and id<>@id
            and (state in ('InFlight','Unknown') or needs_review or (shipment_id is null and state='Succeeded'))
            """, ("project", Id(job, "projectId")), ("id", Id(job)))).Count > 0) return "다른 출하의 판매전표 확인 필요";
        return null;
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
                where id=@id and exists(select 1 from busbar_ecount_attempts where id=current_attempt_id and case when payload ? 'itemRemarks' then coalesce(reviewed_payload, payload - 'ioDate' - 'ioType')
                        else coalesce(reviewed_payload, payload - 'ioDate' - 'ioType') - 'itemRemarks' end<>
                    case when payload ? 'itemRemarks' then @payload::jsonb else @payload::jsonb - 'itemRemarks' end)
                """, ("id", Id(job)), ("payload", await EcountPayload(c, project, family, settings, job)));
        }
    }

    private static async Task<string> EcountPayload(NpgsqlConnection c, Dictionary<string, object?> project, Dictionary<string, object?> family, Dictionary<string, object?> settings, Dictionary<string, object?>? job = null)
    {
        var price = family["standardUnitPrice"] as decimal?;
        var quantity = Convert.ToInt32(project["requestedQuantity"]);
        Dictionary<string, object?>? shipment = null;
        (string Date, string Number)? source = null;
        if (job?["kind"] as string == "Sale" && job["shipmentId"] is Guid shipmentId)
        {
            shipment = await One(c, "busbar_shipments", shipmentId);
            quantity = Convert.ToInt32(shipment["quantity"]);
            var orders = await Rows(c, "select slip_number from busbar_ecount_jobs where project_id=@id and kind='Order'", ("id", Id(project)));
            source = orders.Count == 1 ? ParseOrderSlip(orders[0]["slipNumber"] as string) : null;
        }
        var supply = price * quantity;
        var vat = supply * 0.1m;
        var payload = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new {
            projectId = Id(project), projectName = project["name"], workOrderNumber = project["customerJobNumber"],
            purchaseOrderNumber = "", employeeCode = await EcountEmployeeCode(c, project), registeredByName = project["registeredByName"], productCode = family["ecountProductCode"],
            customerCode = settings["ecountCustomerCode"], warehouseCode = settings["ecountWarehouseCode"],
            commonProjectCode = project["commonProjectCode"], quantity, unitPrice = price,
            supplyAmount = supply, vatAmount = vat, totalAmount = supply + vat,
            dueDate = project["dueDate"] is DateOnly date ? date : DateOnly.FromDateTime((DateTime)project["dueDate"]!), currency = "KRW", vatRate = 0.1m
        }))!;
        if (shipment is not null)
        {
            payload["itemRemarks"] = $"납품처: {project["destination"]}";
            payload["shipmentId"] = Id(shipment).ToString();
            payload["sourceOrderDate"] = source?.Date;
            payload["sourceOrderNumber"] = source?.Number;
        }
        return payload.ToJsonString();
    }

    public Task<object> EcountStatus(Guid project) => ReadSnapshot<object>(async c =>
    {
        await One(c, "busbar_projects", project);
        var runtime = (await Rows(c, "select paused,message,environment from busbar_ecount_runtime"))[0];
        return new { transmissionEnabled = ecountOptions?.Enabled == true, environment = runtime["environment"] ?? ecountOptions?.Environment, paused = runtime["paused"], connectionMessage = runtime["message"], jobs = await Rows(c, """
            select j.id,j.kind,state,needs_review,message,slip_number,j.updated_at_utc,shipment_id,
              s.quantity shipment_quantity,o.created_at_utc shipped_at_utc,
              (select count(*) from busbar_ecount_attempts a where a.job_id=j.id) attempt_count
            from busbar_ecount_jobs j left join busbar_shipments s on s.id=j.shipment_id left join busbar_operations o on o.id=s.id where j.project_id=@project order by j.created_at_utc,j.id
            """, ("project", project)) };
    });

    public Task<bool> ResumeEcount(string reason, Guid actor) => Transaction(async c =>
    {
        Text(reason, "재개 사유");
        var available = (await Rows(c, "select pg_try_advisory_xact_lock(9070095) available"))[0];
        Require((bool)available["available"]!, "전송 처리 중입니다. 잠시 후 다시 확인하세요.");
        Require(ecountOptions?.Enabled == true, "이카운트 연결 설정을 먼저 활성화하세요.");
        var before = (await Rows(c, "select * from busbar_ecount_runtime"))[0];
        await Exec(c, "update busbar_ecount_runtime set paused=false,message=null,consecutive_failures=0");
        await Audit(c, "EcountConnection", Guid.Empty, actor, reason, before, new { paused = false });
        return true;
    });

    // Manual ERP verification never changes the frozen request or sends a new request.
    public Task<Guid> ReconcileEcount(Guid id, BusbarEcountReconcileRequest request, Guid actor) => Transaction(async c =>
    {
        Text(request.Reason, "확인 사유");
        var available = (await Rows(c, "select pg_try_advisory_xact_lock(9070095) available"))[0];
        Require((bool)available["available"]!, "전송 처리 중입니다. 잠시 후 다시 확인하세요.");
        var before = await One(c, "busbar_ecount_jobs", id);
        var state = (string)before["state"]!;
        Require(request.Outcome is "Recorded" or "NotRecorded" or "Reviewed", "확인 결과를 선택하세요.");
        Require(request.Outcome == "Reviewed" ? state == "Succeeded" && (bool)before["needsReview"]! : state == "Unknown", "현재 전송 상태에서는 확인 결과를 반영할 수 없습니다.");
        if (request.Outcome == "Recorded") Text(request.SlipNumber, "이카운트 전표번호");
        if (request.Outcome == "Reviewed")
        {
            var project = await One(c, "busbar_projects", Id(before, "projectId"));
            var family = await One(c, "busbar_product_families", Id(project, "productFamilyId"));
            var settings = (await Rows(c, "select * from busbar_settings"))[0];
            await Exec(c, "update busbar_ecount_jobs set needs_review=false,message=null,reviewed_payload=@payload::jsonb,reviewed_shipped=@shipped,updated_at_utc=@now where id=@id",
                ("payload", await EcountPayload(c, project, family, settings, before)), ("shipped", await EcountShipped(c, Id(project))), ("now", timeProvider.GetUtcNow()), ("id", id));
        }
        else
        {
            var result = request.Outcome == "Recorded" ? "Succeeded" : "Failed";
            var slip = request.Outcome == "Recorded" ? request.SlipNumber!.Trim() : null;
            await Exec(c, "update busbar_ecount_attempts set state=@state,slip_number=@slip,finished_at_utc=@now where id=@attempt and state='Unknown'",
                ("state", result), ("slip", slip), ("now", timeProvider.GetUtcNow()), ("attempt", before["currentAttemptId"]));
            await Exec(c, "update busbar_ecount_jobs set state=@state,slip_number=@slip,needs_review=case when @state='Failed' then false else needs_review end,message=case when needs_review and @state='Succeeded' then message else '담당자 전표 확인 반영' end,updated_at_utc=@now where id=@id",
                ("state", result), ("slip", slip), ("now", timeProvider.GetUtcNow()), ("id", id));
        }
        await Audit(c, "EcountJob", id, actor, request.Reason, before, new { request.Outcome, job = await One(c, "busbar_ecount_jobs", id) });
        return id;
    });

    public Task<Guid> RetryEcount(Guid job, string reason, Guid actor) => Transaction(async c =>
    {
        Text(reason, "재시도 사유");
        var before = await One(c, "busbar_ecount_jobs", job);
        Require((string)before["state"]! is "Held" or "Failed" && !(bool)before["needsReview"]!,
            "이미 전송했거나 결과 확인이 필요한 건은 다시 전송할 수 없습니다.");
        if ((string)before["kind"]! == "Sale")
        {
            var issue = await SaleIssue(c, before);
            Require(issue is null, issue ?? "판매전표 확인 필요");
        }
        await Exec(c, "update busbar_ecount_jobs set state='Pending',message=null,updated_at_utc=now() where id=@id", ("id", job));
        await Audit(c, "EcountJob", job, actor, reason, before, new { state = "Pending" });
        return job;
    });

    // Claims commit before the authenticated worker sends; attempt payloads remain immutable.
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
        var payloadText = await EcountPayload(c, project, family, settings, row);
        var vatNode = System.Text.Json.Nodes.JsonNode.Parse(payloadText)!["vatAmount"];
        var vat = vatNode?.GetValue<decimal>();
        if (vat is not null && decimal.Round(vat.Value, 4) != vat.Value) issue = "부가세가 API 소수 네 자리를 초과함 · 금액 확인 필요";
        if (kind == "Order" && price >= 1000000000000m) issue = "주문서 API 단가 범위 초과 · 금액 확인 필요";
        if (kind == "Sale")
        {
            issue = await SaleIssue(c, row) ?? issue;
            if (System.Text.Json.Nodes.JsonNode.Parse(payloadText)?["itemRemarks"]?.GetValue<string>().Length > 200)
                issue = "판매 품목 적요 200자 초과 · 프로젝트 도착지를 195자 이내로 수정 필요";
        }
        if (string.IsNullOrWhiteSpace(await EcountEmployeeCode(c, project))) issue = "프로젝트 최초 등록자의 이카운트 담당자 연결 필요";
        if (issue is not null)
        {
            await Exec(c, "update busbar_ecount_jobs set state='Held',message=@message,updated_at_utc=now() where id=@id", ("id", job), ("message", issue));
            return null;
        }
        var now = timeProvider.GetUtcNow();
        var attempt = Guid.NewGuid();
        // Domain snapshot, not a wire request: final ERP amount policy is separate.
        var snapshot = System.Text.Json.Nodes.JsonNode.Parse(payloadText)!;
        snapshot["ioDate"] = now.ToOffset(TimeSpan.FromHours(9)).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        snapshot["ioType"] = ecountOptions?.IoType;
        var payload = snapshot.ToJsonString();
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
        await Exec(c, """
            update busbar_ecount_runtime set
              consecutive_failures=case when @state='Succeeded' then 0 else consecutive_failures+1 end,
              paused=paused or @state='Unknown' or (@state='Failed' and consecutive_failures>=2),
              message=case when @state='Unknown' then '전표 생성 여부 확인 필요 · 자동 전송 중지'
                when @state='Failed' and consecutive_failures>=2 then '전송 오류 3회 · 설정 확인 후 재개 필요' else message end
            """, ("state", result.State));
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
        if (stale.Count > 0) await Exec(c, "update busbar_ecount_runtime set paused=true,message='중단된 전송의 전표 생성 여부 확인 필요'");
        return stale.Count;
    });
}

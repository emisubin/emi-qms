using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using ImageMagick;
using ClosedXML.Excel;
namespace Emi.Qms.Api.InteriorBusbar;

public static class InteriorBusbarEndpointExtensions
{
    public const string ManagerRole = "interior-busbar-manager";

    public static IEndpointRouteBuilder MapInteriorBusbarEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/interior-busbar").RequireAuthorization();
        ((IEndpointConventionBuilder)api).Add(endpointBuilder =>
        {
            var route = (Microsoft.AspNetCore.Routing.RouteEndpointBuilder)endpointBuilder;
            var method = route.Metadata.OfType<HttpMethodMetadata>().Single().HttpMethods.Single();
            var name = "InteriorBusbar" + method + System.Text.RegularExpressions.Regex.Replace(route.RoutePattern.RawText ?? "", "[^A-Za-z0-9]", "");
            route.Metadata.Add(new EndpointNameMetadata(name));
        });
        api.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var provider = http.RequestServices.GetRequiredService<DatabaseConnectionStringProvider>();
            if (provider.GetCurrentBusinessUnit()?.Code != BusinessUnitCodes.Cheongju) return Results.Json(new
            {
                code = "business_unit_forbidden",
                message = "청주에서만 사용할 수 있습니다."
            }
, statusCode: 403);
            if (string.Equals(http.User.FindFirstValue(QmsClaimTypes.ApprovalPending), bool.TrueString, StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(http.User.FindFirstValue(QmsClaimTypes.UserId), out var actor)) return Results.Forbid();
            var profile = await http.RequestServices.GetRequiredService<IIdentityStore>().GetProfileByUserIdAsync(actor, http.RequestAborted);
            if (profile?.User.IsActive != true || ApprovalReadinessPolicy.IsApprovalPending(profile)) return Results.Forbid();
            var access = BusbarAccess.For(profile, string.Equals(http.User.FindFirstValue(QmsClaimTypes.IsOverallAdministrator), bool.TrueString, StringComparison.OrdinalIgnoreCase));
            var routePath = (http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText;
            if (routePath is "/api/interior-busbar/access" or "/api/interior-busbar/workspace" or "/api/interior-busbar/masters" or
                "/api/interior-busbar/product-families" or "/api/interior-busbar/materials" or "/api/interior-busbar/workers" or "/api/interior-busbar/boms" or "/api/interior-busbar/settings")
                access = await http.RequestServices.GetRequiredService<InteriorBusbarStore>().MasterAccess(actor, access);
            if (routePath == "/api/interior-busbar/masters" && !access.MastersRead || routePath == "/api/interior-busbar/master-access" && !access.ManageMasterPermissions) return Results.Forbid();
            http.Items["busbarAccess"] = access;
            if (http.Request.Method != "GET" && !access.Allows((http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText)) return Results.Forbid();
            try
            {
                return await next(context);
            }
            catch (BusbarException ex)
            {
                return Results.Json(new
                {
                    code = ex.Code,
                    message = ex.Message
                }
               , statusCode: ex.Status);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState is "23505" or "23503" or "23514" or "22003")
            {
                return Results.Json(new
                {
                    code = "conflict",
                    message = "중복 코드, 참조 대상 또는 수량을 확인하세요."
                }
               , statusCode: 409);
            }
        });
        api.MapGet("/access", (HttpContext h) => Results.Ok(h.Items["busbarAccess"]));
        api.MapGet("/masters", (HttpContext h, InteriorBusbarStore s) => s.Workspace(((BusbarAccess)h.Items["busbarAccess"]!).Any, permissions: (BusbarAccess)h.Items["busbarAccess"]!));
        api.MapGet("/master-access", async (IIdentityStore identities, InteriorBusbarStore s, CancellationToken ct) =>
        {
            var eligible = (await identities.GetUsersAsync(ct)).Where(u => u.IsActive && !u.ApprovalPending && u.UserId != null).DistinctBy(u => u.UserId).ToDictionary(u => u.UserId!.Value);
            var users = await s.MasterAccessUsers();
            return Results.Ok(users.Where(u => eligible.ContainsKey((Guid)u["userId"]!)).Select(u => new { userId = u["userId"], displayName = u["displayName"], departmentName = eligible[(Guid)u["userId"]!].DepartmentName,
                access = u["canEdit"] is bool edit ? edit ? "Edit" : "Read" : "None",
                automatic = eligible[(Guid)u["userId"]!].Roles.Contains(QmsRoles.SystemAdministrator) }));
        });
        api.MapPut("/master-access", async (BusbarMasterAccessRequest r, ClaimsPrincipal u, IIdentityStore identities, InteriorBusbarStore s, CancellationToken ct) =>
        {
            var target = await identities.GetProfileByUserIdAsync(r.UserId, ct);
            if (target?.User.IsActive != true || ApprovalReadinessPolicy.IsApprovalPending(target)) return Results.BadRequest(new { message = "활성 청주 사용자를 선택하세요." });
            return Results.Ok(new { id = await s.SetMasterAccess(r, Actor(u)) });
        });
        api.MapGet("/workspace", (HttpContext h, InteriorBusbarStore s, int? page, int? pageSize, Guid? planId, Guid? productFamilyId, DateOnly? planDateFrom, DateOnly? planDateTo, string? status) => s.Workspace(((BusbarAccess)h.Items["busbarAccess"]!).Any, page ?? 1, pageSize ?? 100, planId, productFamilyId, planDateFrom, planDateTo, status, (BusbarAccess)h.Items["busbarAccess"]!));
        foreach (var kind in new[] { "product-families", "materials", "workers" })
        {
            var captured = kind;
            api.MapPost("/" + kind, async (BusbarMasterRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
            {
                id = await s.Master(captured, r, Actor(u))
            }));
        }
        api.MapPut("/settings", async (BusbarSettingsRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Settings(r, Actor(u))
        }));
        api.MapPost("/boms", async (BusbarBomRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Bom(r, Actor(u))
        }));
        api.MapGet("/projects/{id:guid}/ecount-status", (Guid id, InteriorBusbarStore s) => s.EcountStatus(id));
        api.MapPost("/ecount-jobs/{id:guid}/reconcile", async (Guid id, BusbarEcountReconcileRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new { id = await s.ReconcileEcount(id, r, Actor(u)) }));
        api.MapPost("/ecount/resume", async (BusbarEcountRetryRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new { resumed = await s.ResumeEcount(r.Reason, Actor(u)) }));
        api.MapPost("/ecount-jobs/{id:guid}/retry", async (Guid id, BusbarEcountRetryRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new { id = await s.RetryEcount(id, r.Reason, Actor(u)) }));
        api.MapGet("/projects/{id:guid}/commercial-preview", (Guid id, InteriorBusbarStore s) => s.CommercialPreview(id));
        api.MapPost("/projects", async (BusbarProjectRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Project(r, Actor(u))
        }));
        api.MapPost("/plans", async (BusbarPlanRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Plan(r, Actor(u))
        }));
        api.MapPost("/purchases", async (BusbarPurchaseRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Purchase(r, Actor(u))
        }));
        api.MapPost("/receipts", async (BusbarReceiptRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Receipt(r, Actor(u))
        }));
        api.MapPost("/adjustments", async (BusbarAdjustmentRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Adjustment(r, Actor(u))
        }));
        api.MapGet("/projects/{id:guid}", (Guid id, InteriorBusbarStore s) => s.ProjectDetail(id));
        api.MapGet("/projects/{id:guid}/scan", (Guid id, string code, InteriorBusbarStore s) => s.ResolveShipmentPanel(id, code));
        api.MapPost("/shipments", async (BusbarShipmentRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Shipment(r, Actor(u))
        }));
        api.MapPost("/ledger/{id:guid}/reverse", async (Guid id, BusbarReverseRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Reverse(id, r, Actor(u))
        }));
        api.MapPost("/products", async (BusbarProductRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.Product(r, Actor(u))
        }));
        api.MapGet("/products/{id:guid}", (Guid id, InteriorBusbarStore s) => s.GetProduct(id));
        api.MapPatch("/products/{id:guid}", async (Guid id, BusbarProductCorrectionRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.CorrectProduct(id, r, Actor(u))
        }));
        api.MapPost("/products/{id:guid}/cancel", async (Guid id, BusbarReverseRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.CancelProduct(id, r, Actor(u))
        }));
        api.MapPost("/products/{id:guid}/publication/retry", async (Guid id, InteriorBusbarStore s) => Results.Ok(new
        {
            id = await s.RetryPublication(id)
        }));
        api.MapPut("/products/{id:guid}/photos/{side}", async (Guid id, string side, HttpRequest request, ClaimsPrincipal user, InteriorBusbarStore s) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new
            {
                message = "사진 파일이 필요합니다."
            });
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length is <= 0 or > 10_000_000) return Results.BadRequest(new
            {
                message = "10MB 이하 사진을 선택하세요."
            });
            await using var stream = file.OpenReadStream();
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            var uploaded = bytes.ToArray();
            var supportedSignature = uploaded.Length >= 12 &&
                ((uploaded[0] == 0xff && uploaded[1] == 0xd8 && uploaded[2] == 0xff) ||
                 uploaded.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                 (uploaded.AsSpan(0, 4).SequenceEqual("RIFF"u8) && uploaded.AsSpan(8, 4).SequenceEqual("WEBP"u8)));
            if (!supportedSignature) return Results.BadRequest(new { message = "JPEG, PNG, WebP 사진을 선택하세요." });
            byte[] normalized;
            try
            {
                using var info = new MagickImage();
                info.Ping(uploaded);
                if (info.Width > 12000 || info.Height > 12000 || ((long)info.Width * info.Height) > 40_000_000) return Results.BadRequest(new
                {
                    message = "사진 해상도가 너무 큽니다."
                });
                using var image = new MagickImage(uploaded);
                if (image.Format is not (MagickFormat.Jpeg or MagickFormat.Png or MagickFormat.WebP)) return Results.BadRequest(new
                {
                    message = "JPEG, PNG, WebP 사진을 선택하세요."
                });
                image.AutoOrient();
                image.Strip();
                image.Resize(new MagickGeometry(2400, 2400)
                {
                    Greater = true
                });
                image.Quality = 85;
                normalized = image.ToByteArray(MagickFormat.Jpeg);
            }
            catch (MagickException)
            {
                return Results.BadRequest(new
                {
                    message = "올바른 사진 파일이 아닙니다."
                });
            }
            var workerValue = form["workerId"].FirstOrDefault();
            Guid? selectedWorker = null;
            if (!string.IsNullOrWhiteSpace(workerValue))
            {
                if (!Guid.TryParse(workerValue, out var parsedWorker)) return Results.BadRequest(new { message = "작업자 선택을 확인하세요." });
                selectedWorker = parsedWorker;
            }
            return Results.Ok(new
            {
                id = await s.Photo(id, side, normalized, form["reason"].FirstOrDefault(), Actor(user), selectedWorker)
            });
        }
).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(11_000_000));
        api.MapGet("/products/{id:guid}/photos/{side}", async (Guid id, string side, InteriorBusbarStore s) => Results.File(await s.GetPhoto(id, side), "image/jpeg"));
        api.MapGet("/products/{id:guid}/qr", async (Guid id, InteriorBusbarStore s, IConfiguration configuration) =>
            Results.File(await s.GetPrintableQr(id, allowPersist: !ReviewSafeMode.IsEnabled(configuration)), "image/png"));
        api.MapPost("/projects/import/preview", (HttpRequest r, InteriorBusbarStore s) => Preview(r, false, s));
        api.MapPost("/purchases/import/preview", (HttpRequest r, InteriorBusbarStore s) => Preview(r, true, s));
        api.MapPost("/projects/import/apply", (BusbarImportApplyRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => s.ApplyProjects(r.Rows, Actor(u)));
        api.MapPost("/purchases/import/apply", (BusbarPurchaseImportApplyRequest r, ClaimsPrincipal u, InteriorBusbarStore s) => s.ApplyPurchases(r.Rows, Actor(u)));
        api.MapGet("/projects/import/template", () => Template(false));
        api.MapGet("/purchases/import/template", () => Template(true));
        return app;
    }

    private static IResult Template(bool purchase)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("업로드");
        var headers = purchase ? new[] { "발주ID", "발주번호", "자재코드", "수량", "발주일", "정정사유" } : new[] { "등록건ID", "프로젝트명", "LSE Task No", "제품군코드", "요청수량", "도착지", "납품예정일", "정정사유" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
            sheet.Column(i + 1).Width = 22;
        }
        sheet.SheetView.FreezeRows(1);
        using var output = new MemoryStream();
        book.SaveAs(output);
        return Results.File(output.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", purchase ? "busbar-purchases.xlsx" : "busbar-projects.xlsx");
    }

    private static Guid Actor(ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(QmsClaimTypes.UserId)!);

    private static async Task<IResult> Preview(HttpRequest request, bool purchase, InteriorBusbarStore store)
    {
        if (!request.HasFormContentType) return Results.BadRequest(new
        {
            message = "엑셀 파일이 필요합니다."
        });
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        if (file is null || file.Length is <= 0 or > 5_000_000) return Results.BadRequest(new
        {
            message = "5MB 이하 엑셀 파일을 선택하세요."
        });
        var rows = new List<object>();
        var errors = new List<string>();
        try
        {
            using var book = new XLWorkbook(file.OpenReadStream());
            var sheet = book.Worksheet(1);
            var end = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (end > 501) return Results.BadRequest(new
            {
                message = "최대 500개 행을 업로드하세요."
            });
            for (var n = 2; n <= end; n++)
            {
                try
                {
                    var cells = sheet.Row(n);
                    var raw = cells.Cell(1).GetString().Trim();
                    Guid? id = raw.Length == 0 ? null : Guid.Parse(raw);
                    if (purchase) rows.Add(new BusbarPurchaseRequest(id, cells.Cell(2).GetString(), await store.ResolveCode("Material", cells.Cell(3).GetString()), cells.Cell(4).GetValue<decimal>(), DateOnly.FromDateTime(cells.Cell(5).GetDateTime()), id is null ? null : cells.Cell(6).GetString()));
                    else rows.Add(new BusbarProjectRequest(id, cells.Cell(2).GetString(), cells.Cell(3).GetString(), await store.ResolveCode("Finished", cells.Cell(4).GetString()), cells.Cell(5).GetValue<int>(), cells.Cell(6).GetString(), DateOnly.FromDateTime(cells.Cell(7).GetDateTime()), id is null ? null : cells.Cell(8).GetString()));
                    await store.ValidateImportRow(rows[^1]);
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or BusbarException)
                {
                    errors.Add($"{n}행: {(ex is BusbarException ? ex.Message : "ID·수량·날짜 형식을 확인하세요.")}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.BadRequest(new
            {
                message = "읽을 수 없는 엑셀 파일입니다."
            });
        }
        var ids = rows.Select(row => row is BusbarProjectRequest p ? p.Id : ((BusbarPurchaseRequest)row).Id).Where(id => id is not null).ToList();
        if (ids.Distinct().Count() != ids.Count) errors.Add("등록 건 ID가 중복되었습니다.");
        return Results.Ok(new
        {
            rows,
            errors
        });
    }
}

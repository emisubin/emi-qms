using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Security;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    private static async Task AssertOsanManagementHttpAsync(
        IsolationDatabaseSet databases,
        HttpClient client,
        Guid projectId,
        Guid customerId,
        Guid completedTargetId,
        CapturingCleanUploadMalwareScanner uploadScanner)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string editToken;
        using (var management = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{projectId:D}/management",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(management, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            Assert.True(body.RootElement.GetProperty("canManage").GetBoolean());
            editToken = body.RootElement.GetProperty("editToken").GetString()!;
        }

        var updatePayload = new
        {
            expectedToken = editToken,
            fields = new
            {
                title = "Osan managed project",
                projectCode = "OSAN-ROUTED-001",
                customerName = "Routed customer",
                customerId,
                poNumber = "001-PO",
                workOrderNumber = "WO/001",
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Routed product",
                quantity = 1,
                operationId = Guid.NewGuid()
            }
        };
        foreach (var denied in new[]
                 {
                     Request(HttpMethod.Put, $"/api/osan/projects/{projectId:D}", "dev-sales", BusinessUnitCodes.Osan),
                     Request(HttpMethod.Put, $"/api/osan/projects/{projectId:D}", "dev-admin", BusinessUnitCodes.Cheongju)
                 })
        {
            using (denied)
            {
                denied.Content = JsonContent.Create(updatePayload);
                using var response = await client.SendAsync(denied, cancellationToken);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        using (var update = Request(
                   HttpMethod.Put,
                   $"/api/osan/projects/{projectId:D}",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            update.Content = JsonContent.Create(updatePayload);
            using var response = await client.SendAsync(update, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Guid stepId;
        using (var getProgress=Request(HttpMethod.Get,$"/api/osan/projects/{projectId:D}/progress","dev-manufacturing",BusinessUnitCodes.Osan))
        using (var response=await client.SendAsync(getProgress,cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            stepId=body.RootElement.GetProperty("targets").EnumerateArray().Single(t=>t.GetProperty("targetId").GetGuid()==completedTargetId)
                .GetProperty("steps")[0].GetProperty("stepId").GetGuid();
        }
        foreach (var route in new[] { "issues", "issues/records", "issues/resolve" })
        {
            using (var osanRequest = Request(HttpMethod.Post,
                       $"/api/osan/projects/{projectId:D}/progress/{route}",
                       "dev-manufacturing", BusinessUnitCodes.Osan))
            {
                osanRequest.Content = JsonContent.Create(new { });
                using var response = await client.SendAsync(osanRequest, cancellationToken);
                Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                    $"OSAN {route} should reach handler validation, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            }
            using (var cheongjuRequest = Request(HttpMethod.Post,
                       $"/api/osan/projects/{projectId:D}/progress/{route}",
                       "dev-admin", BusinessUnitCodes.Cheongju))
            {
                cheongjuRequest.Content = JsonContent.Create(new { });
                using var response = await client.SendAsync(cheongjuRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        foreach(var action in new[]{"reject","reset"})
        foreach(var pair in new[]{("dev-manufacturing",BusinessUnitCodes.Osan),("dev-admin",BusinessUnitCodes.Cheongju)})
        {
            using var request=Request(HttpMethod.Post,$"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/{action}",pair.Item1,pair.Item2);
            request.Content=JsonContent.Create(new {operationId=Guid.NewGuid(),reason="denied",expectedVersion=2});
            using var response=await client.SendAsync(request,cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
        foreach(var unit in new[]{BusinessUnitCodes.Osan,BusinessUnitCodes.Cheongju})
        {
            using var request=Request(HttpMethod.Get,$"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/history","dev-admin",unit);
            using var response=await client.SendAsync(request,cancellationToken);
            Assert.Equal(unit==BusinessUnitCodes.Osan?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
        }

        var replacementBytes = CreateValidPng();
        var operationId = Guid.NewGuid();
        MultipartFormDataContent SaveContent(string reason = "배선 상태가 잘 보이는 사진으로 교체합니다.")
        {
            var body = CreateProgressCompletionContent(operationId, "individual", 1,
                JsonSerializer.Serialize(new[] { new { targetId = completedTargetId, expectedVersion = 2 } }), replacementBytes);
            body.Add(new StringContent(reason), "reason");
            return body;
        }
        foreach (var pair in new[] { ("dev-sales", BusinessUnitCodes.Osan), ("dev-admin", BusinessUnitCodes.Cheongju) })
        {
            using var denied = Request(HttpMethod.Post, $"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/edit", pair.Item1, pair.Item2);
            denied.Content = SaveContent();
            using var response = await client.SendAsync(denied, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var blank = Request(HttpMethod.Post, $"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/edit", "dev-manufacturing", BusinessUnitCodes.Osan))
        {
            blank.Content = SaveContent("   ");
            using var response = await client.SendAsync(blank, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        uploadScanner.Status = UploadMalwareScanStatus.Unavailable;
        using (var unavailable = Request(HttpMethod.Post, $"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/edit", "dev-manufacturing", BusinessUnitCodes.Osan))
        {
            unavailable.Content = SaveContent();
            using var response = await client.SendAsync(unavailable, cancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        uploadScanner.Status = UploadMalwareScanStatus.Clean;
        for (var replay = 0; replay < 2; replay++)
        {
            using var save = Request(HttpMethod.Post, $"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/edit", "dev-manufacturing", BusinessUnitCodes.Osan);
            save.Content = SaveContent();
            using var response = await client.SendAsync(save, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Guid revisionPhotoId;
        using (var progress = Request(HttpMethod.Get, $"/api/osan/projects/{projectId:D}/progress", "dev-manufacturing", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(progress, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var target = body.RootElement.GetProperty("targets").EnumerateArray().Single(item => item.GetProperty("targetId").GetGuid() == completedTargetId);
            var step = target.GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("stepId").GetGuid() == stepId);
            Assert.True(step.GetProperty("canEdit").GetBoolean());
            revisionPhotoId = Assert.Single(step.GetProperty("photos").EnumerateArray()).GetProperty("photoId").GetGuid();
        }
        using (var download = Request(HttpMethod.Get, $"/api/osan/projects/{projectId:D}/progress/photos/{revisionPhotoId:D}", "dev-manufacturing", BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(download, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(replacementBytes, await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }

        await AssertRejectedStageOverallRecipientsAsync(databases,client,projectId,completedTargetId,stepId);

        Guid deleteProjectId;
        using (var create = Request(HttpMethod.Post, "/api/osan/projects", "dev-admin", BusinessUnitCodes.Osan))
        {
            create.Content = JsonContent.Create(new
            {
                title = "Delete through HTTP",
                projectCode = $"DELETE-{Guid.NewGuid():N}",
                customerName = "Routed customer",
                customerId,
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Synthetic product",
                quantity = 1,
                operationId = Guid.NewGuid()
            });
            using var response = await client.SendAsync(create, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            deleteProjectId = body.RootElement.GetProperty("project").GetProperty("projectId").GetGuid();
            Assert.Equal($"/osan/api/osan/projects/{deleteProjectId:D}", response.Headers.Location?.ToString());
            using var followLocation = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
            followLocation.Headers.Add(DevelopmentAuthenticationDefaults.UserHeader, "dev-admin");
            using var followed = await client.SendAsync(followLocation, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        }
        string deleteToken;
        using (var management = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{deleteProjectId:D}/management",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(management, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            deleteToken = body.RootElement.GetProperty("editToken").GetString()!;
        }
        using (var deniedDelete = Request(
                   HttpMethod.Delete,
                   $"/api/osan/projects/{deleteProjectId:D}",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            deniedDelete.Content = JsonContent.Create(new { expectedToken = deleteToken, reason = "denied" });
            using var response = await client.SendAsync(deniedDelete, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var delete = Request(
                   HttpMethod.Delete,
                   $"/api/osan/projects/{deleteProjectId:D}",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            delete.Content = JsonContent.Create(new { expectedToken = deleteToken, reason = "synthetic cleanup" });
            using var response = await client.SendAsync(delete, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task AssertRejectedStageOverallRecipientsAsync(IsolationDatabaseSet databases,HttpClient client,
        Guid projectId,Guid targetId,Guid stepId)
    {
        var ct=TestContext.Current.CancellationToken;
        var activeOverall=Guid.NewGuid();var inactiveOverall=Guid.NewGuid();var inactiveIdentity=Guid.NewGuid();var campusAdmin=Guid.NewGuid();
        await databases.ExecuteAsync("DIRECTORY",BusinessUnitConnectionPurpose.Migration,$"""
            insert into directory_identities(user_id,auth_provider,external_subject,is_active) values
              ('{activeOverall:D}','Dev','rejection-active-overall',true),
              ('{inactiveOverall:D}','Dev','rejection-inactive-overall',true),
              ('{inactiveIdentity:D}','Dev','rejection-inactive-identity',false),
              ('{campusAdmin:D}','Dev','rejection-campus-admin',true);
            insert into directory_overall_administrators(user_id,is_active) values
              ('{activeOverall:D}',true),('{inactiveOverall:D}',false),('{inactiveIdentity:D}',true);
            insert into directory_business_unit_memberships(user_id,business_unit_code,is_active)
              values('{campusAdmin:D}','OSAN',true);
            """,ct);
        await databases.ExecuteAsync(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,$"""
            insert into qms_users(id,development_user_key,display_name,email,auth_provider,is_active) values
              ('{activeOverall:D}','rejection-active-overall','Explicit overall Admin','active-overall@example.invalid','Dev',true),
              ('{inactiveOverall:D}','rejection-inactive-overall','Inactive overall','inactive-overall@example.invalid','Dev',true),
              ('{inactiveIdentity:D}','rejection-inactive-identity','Inactive directory identity','inactive-identity@example.invalid','Dev',true),
              ('{campusAdmin:D}','rejection-campus-admin','Campus Admin','campus-admin@example.invalid','Dev',true);
            insert into user_roles(user_id,role_id,assignment_source)
              select u.id,r.id,'explicit' from qms_users u cross join roles r
              where u.id in ('{activeOverall:D}','{inactiveOverall:D}','{inactiveIdentity:D}','{campusAdmin:D}') and r.code='system-administrator';
            """,ct);
        Assert.Equal(0L,await databases.ReadScalarAsync<long>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,$"""
            select count(*) from osan_stage_records where step_id='{stepId:D}' and event_type in ('Complete','Edit')
              and actor_user_id in ('{activeOverall:D}','{inactiveOverall:D}','{inactiveIdentity:D}','{campusAdmin:D}');
            """,ct));
        var contributors=await databases.ReadColumnAsync(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,
            $"select distinct actor_user_id::text from osan_stage_records where step_id='{stepId:D}' and event_type in ('Complete','Edit')",ct);
        Assert.NotEmpty(contributors);
        var version=await databases.ReadScalarAsync<int>(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,
            $"select version from osan_project_targets where id='{targetId:D}'",ct);
        var operation=Guid.NewGuid();
        using(var request=Request(HttpMethod.Post,$"/api/osan/projects/{projectId:D}/progress/steps/{stepId:D}/reject","dev-admin",BusinessUnitCodes.Osan))
        {
            request.Content=JsonContent.Create(new{operationId=operation,reason="Synthetic rejection recipient boundary",expectedVersion=version});
            using var response=await client.SendAsync(request,ct);
            Assert.True(response.StatusCode==HttpStatusCode.OK,await response.Content.ReadAsStringAsync(ct));
        }
        var key=$"osan:project:{projectId:D}:StepRejected:{operation:D}";
        var recipients=await databases.ReadColumnAsync(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,$"""
            select r.user_id::text from notification_recipients r join notifications n on n.id=r.notification_id
            where n.idempotency_key='{key}' order by r.user_id;
            """,ct);
        Assert.Contains(activeOverall.ToString("D"),recipients);
        Assert.All(contributors,contributor=>Assert.Contains(contributor,recipients));
        Assert.DoesNotContain(inactiveOverall.ToString("D"),recipients);
        Assert.DoesNotContain(inactiveIdentity.ToString("D"),recipients);
        Assert.DoesNotContain(campusAdmin.ToString("D"),recipients);
        Assert.Equal(recipients.Count,recipients.Distinct().Count());
        var mailRecipients=await databases.ReadColumnAsync(BusinessUnitCodes.Osan,BusinessUnitConnectionPurpose.Migration,$"""
            select d.recipient_user_id::text from notification_deliveries d join notifications n on n.id=d.notification_id
            where n.idempotency_key='{key}' and d.channel='Mail' order by d.recipient_user_id;
            """,ct);
        Assert.Equal(recipients,mailRecipients);
    }

}

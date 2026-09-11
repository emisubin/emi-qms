using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Security;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    private static async Task AssertOsanManagementHttpAsync(
        IsolationDatabaseSet databases,
        HttpClient client,
        Guid projectId,
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
                poNumber = "001-PO",
                workOrderNumber = "WO/001",
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Routed product",
                quantity = 2,
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

        var requestId = Guid.NewGuid();
        for (var replay = 0; replay < 2; replay++)
        {
            using var requestEdit = Request(
                HttpMethod.Post,
                $"/api/osan/projects/{projectId:D}/progress/photo-edits",
                "dev-manufacturing",
                BusinessUnitCodes.Osan);
            requestEdit.Content = JsonContent.Create(new
            {
                requestId,
                targetId = completedTargetId,
                stageSequence = 1
            });
            using var response = await client.SendAsync(requestEdit, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(1L, await CountPhotoEditAuditEventsAsync(
            databases,
            "RequestOsanProgressPhotoEdit",
            cancellationToken));

        var replacementBytes = CreateValidPng();
        var operationId = Guid.NewGuid();
        MultipartFormDataContent SaveContent() => CreateProgressCompletionContent(
            operationId,
            "individual",
            1,
            JsonSerializer.Serialize(new[]
            {
                new { targetId = completedTargetId, expectedVersion = 2 }
            }),
            replacementBytes);

        using (var beforeApproval = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/save",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            beforeApproval.Content = SaveContent();
            using var response = await client.SendAsync(beforeApproval, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var nonAdminApproval = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/approve",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            using var response = await client.SendAsync(nonAdminApproval, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var wrongUnitApproval = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/approve",
                   "dev-admin",
                   BusinessUnitCodes.Cheongju))
        {
            using var response = await client.SendAsync(wrongUnitApproval, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        for (var replay = 0; replay < 2; replay++)
        {
            using var approval = Request(
                HttpMethod.Post,
                $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/approve",
                "dev-admin",
                BusinessUnitCodes.Osan);
            using var response = await client.SendAsync(approval, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(1L, await CountPhotoEditAuditEventsAsync(
            databases,
            "ApproveOsanProgressPhotoEdit",
            cancellationToken));
        using (var differentUserSave = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/save",
                   "dev-sales",
                   BusinessUnitCodes.Osan))
        {
            differentUserSave.Content = SaveContent();
            using var response = await client.SendAsync(differentUserSave, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        uploadScanner.Status = UploadMalwareScanStatus.Unavailable;
        using (var unavailableSave = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/save",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            unavailableSave.Content = SaveContent();
            using var response = await client.SendAsync(unavailableSave, cancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        uploadScanner.Status = UploadMalwareScanStatus.Clean;

        for (var replay = 0; replay < 2; replay++)
        {
            using var save = Request(
                HttpMethod.Post,
                $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/save",
                "dev-manufacturing",
                BusinessUnitCodes.Osan);
            save.Content = SaveContent();
            using var response = await client.SendAsync(save, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(1L, await CountPhotoEditAuditEventsAsync(
            databases,
            "SaveOsanProgressPhotoEdit",
            cancellationToken));
        Assert.Equal(3L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            """
            select count(*)
            from audit_events
            where target_type='osan_photo_edit_requests'
              and outcome='Succeeded';
            """,
            cancellationToken));

        Guid revisionPhotoId;
        using (var listEdits = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(listEdits, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("approvedAt").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, item.GetProperty("usedAt").ValueKind);
            revisionPhotoId = Assert.Single(item.GetProperty("photoIds").EnumerateArray()).GetGuid();
        }
        using (var downloadRevision = Request(
                   HttpMethod.Get,
                   $"/api/osan/projects/{projectId:D}/progress/photos/{revisionPhotoId:D}",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        using (var response = await client.SendAsync(downloadRevision, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(replacementBytes, await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        Assert.Equal(1L, await databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"select count(*) from osan_photo_revision_files where request_id='{requestId:D}';",
            cancellationToken));

        await AssertRejectedStageOverallRecipientsAsync(databases,client,projectId,completedTargetId,stepId);

        Guid deleteProjectId;
        using (var create = Request(HttpMethod.Post, "/api/osan/projects", "dev-admin", BusinessUnitCodes.Osan))
        {
            create.Content = JsonContent.Create(new
            {
                title = "Delete through HTTP",
                projectCode = $"DELETE-{Guid.NewGuid():N}",
                customerName = "Synthetic customer",
                deliveryDate = new DateOnly(2026, 12, 31),
                productName = "Synthetic product",
                quantity = 1,
                operationId = Guid.NewGuid()
            });
            using var response = await client.SendAsync(create, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            deleteProjectId = body.RootElement.GetProperty("project").GetProperty("projectId").GetGuid();
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

    private static Task<long> CountPhotoEditAuditEventsAsync(
        IsolationDatabaseSet databases,
        string action,
        CancellationToken cancellationToken) => databases.ReadScalarAsync<long>(
            BusinessUnitCodes.Osan,
            BusinessUnitConnectionPurpose.Migration,
            $"""
            select count(*)
            from audit_events
            where target_type='osan_photo_edit_requests'
              and action='{action}'
              and outcome='Succeeded';
            """,
            cancellationToken);
}

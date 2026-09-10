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

        var requestId = Guid.NewGuid();
        using (var requestEdit = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits",
                   "dev-manufacturing",
                   BusinessUnitCodes.Osan))
        {
            requestEdit.Content = JsonContent.Create(new
            {
                requestId,
                targetId = completedTargetId,
                stageSequence = 1
            });
            using var response = await client.SendAsync(requestEdit, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

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
        using (var approval = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/approve",
                   "dev-admin",
                   BusinessUnitCodes.Osan))
        {
            using var response = await client.SendAsync(approval, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using (var differentUserSave = Request(
                   HttpMethod.Post,
                   $"/api/osan/projects/{projectId:D}/progress/photo-edits/{requestId:D}/save",
                   "dev-admin",
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
}

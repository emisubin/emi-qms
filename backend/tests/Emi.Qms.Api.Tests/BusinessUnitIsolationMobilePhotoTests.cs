using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.OsanProjects;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed partial class BusinessUnitIsolationTests
{
    private static async Task AssertMobilePhotoHttpAsync(HttpClient client)
    {
        var ct = TestContext.Current.CancellationToken;
        using var create = Request(HttpMethod.Post, "/api/osan/projects", "dev-admin", BusinessUnitCodes.Osan);
        create.Content = JsonContent.Create(new { title = "Mobile Photo", projectCode = "MOBILE-PHOTO", customerName = "Synthetic", productName = "Panel", quantity = 1, deliveryDate = new DateOnly(2026, 12, 31), operationId = Guid.NewGuid() });
        using var created = await client.SendAsync(create, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var project = body.RootElement.GetProperty("project");
        var projectId = project.GetProperty("projectId").GetGuid();
        var targetId = project.GetProperty("targets")[0].GetProperty("targetId").GetGuid();
        var original = OsanHeicPhotoTests.CreateHeic();
        foreach (var bu in new[] { BusinessUnitCodes.Osan, BusinessUnitCodes.Cheongju })
        {
            using var request = Request(HttpMethod.Post, $"/api/osan/projects/{projectId}/progress/photo-preview", "dev-admin", bu);
            var multipart = new MultipartFormDataContent(); var file = new ByteArrayContent(original);
            file.Headers.ContentType = new("image/heic"); multipart.Add(file, "photos", "phone.heic"); request.Content = multipart;
            using var response = await client.SendAsync(request, ct);
            Assert.True(response.StatusCode == (bu == BusinessUnitCodes.Osan ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
                $"Preview {bu}: {response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
            if (bu == BusinessUnitCodes.Osan)
            {
                using var preview = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                Assert.Equal("image/jpeg", preview.RootElement.GetProperty("contentType").GetString());
                var jpeg = Convert.FromBase64String(preview.RootElement.GetProperty("base64").GetString()!);
                Assert.NotNull((await OsanProgressPhotoValidator.ValidateAsync("preview.jpg", "image/jpeg", jpeg, ct)).Photo);
            }
        }
        using var complete = Request(HttpMethod.Post, $"/api/osan/projects/{projectId}/progress/completions", "dev-admin", BusinessUnitCodes.Osan);
        complete.Content = CreateProgressCompletionContent(Guid.NewGuid(), "individual", 1,
            JsonSerializer.Serialize(new[] { new { targetId, expectedVersion = 1 } }), original, "image/heic", "phone.heic");
        using var completed = await client.SendAsync(complete, ct);
        var completedText = await completed.Content.ReadAsStringAsync(ct);
        Assert.True(completed.StatusCode == HttpStatusCode.OK, completedText);
        using var completedBody = JsonDocument.Parse(completedText);
        var photoId = completedBody.RootElement.GetProperty("project").GetProperty("targets")[0].GetProperty("steps")[0].GetProperty("photos")[0].GetProperty("photoId").GetGuid();
        foreach (var preview in new[] { false, true })
        {
            using var download = Request(HttpMethod.Get, $"/api/osan/projects/{projectId}/progress/photos/{photoId}?preview={preview.ToString().ToLowerInvariant()}", "dev-admin", BusinessUnitCodes.Osan);
            using var response = await client.SendAsync(download, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(preview ? "image/jpeg" : "image/heic", response.Content.Headers.ContentType?.MediaType);
            if (!preview) Assert.Equal(OsanHeicMetadataSanitizer.Sanitize(original), await response.Content.ReadAsByteArrayAsync(ct));
        }
        using var deniedDownload = Request(HttpMethod.Get, $"/api/osan/projects/{projectId}/progress/photos/{photoId}?preview=true", "dev-admin", BusinessUnitCodes.Cheongju);
        using var denied = await client.SendAsync(deniedDownload, ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var wrongProject = Request(HttpMethod.Get, $"/api/osan/projects/{Guid.NewGuid()}/progress/photos/{photoId}?preview=true", "dev-admin", BusinessUnitCodes.Osan);
        using var missing = await client.SendAsync(wrongProject, ct); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}

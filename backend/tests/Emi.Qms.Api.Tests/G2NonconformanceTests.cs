using System.Data;
using System.Buffers.Binary;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using ImageMagick;
using Emi.Qms.Api.Authorization;
using System.Net;
using System.Net.Http.Json;
using Emi.Qms.Api.Identity;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.G2;
using Emi.Qms.Api.ReviewSafe;
using Emi.Qms.Api.Security;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class G2NonconformanceTests
{
    private static readonly G2QualityPhoto[] Photos=CreatePhotos();
    private static readonly Lazy<byte[]> LargePng=new(CreateLargePng);
    private static G2QualityPhoto[] CreatePhotos() { using var image=new MagickImage(MagickColors.White,2,2);return [new("test.png","image/png",Convert.ToBase64String(image.ToByteArray(MagickFormat.Png)))]; }
    private sealed class Clock:TimeProvider { public DateTimeOffset Now=new(2026,10,12,3,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class CapturingScanner(UploadMalwareScanStatus status):IUploadMalwareScanner
    {
        public UploadMalwareScanStatus Status {get;set;}=status;
        public List<byte[]> Files {get;}=[];
        public async Task<UploadMalwareScanResult> ScanAsync(Stream content,CancellationToken ct) { using var copy=new MemoryStream();await content.CopyToAsync(copy,ct);Files.Add(copy.ToArray());return new(Status,Status.ToString()); }
    }
    private static byte[] CreateLargePng()
    {
        const int width=1800,height=1100,rowBytes=width*3;var raw=new byte[(rowBytes+1)*height];var random=new Random(20261013);
        for(var row=0;row<height;row++)random.NextBytes(raw.AsSpan(row*(rowBytes+1)+1,rowBytes));
        using var compressed=new MemoryStream();using(var zlib=new ZLibStream(compressed,CompressionLevel.SmallestSize,true))zlib.Write(raw);
        using var png=new MemoryStream();png.Write(new byte[]{137,80,78,71,13,10,26,10});var header=new byte[13];BinaryPrimitives.WriteUInt32BigEndian(header,(uint)width);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4),(uint)height);header[8]=8;header[9]=2;
        WritePngChunk(png,"IHDR"u8,header);WritePngChunk(png,"IDAT"u8,compressed.ToArray());WritePngChunk(png,"IEND"u8,[]);return png.ToArray();
    }
    private static void WritePngChunk(Stream output,ReadOnlySpan<byte> type,ReadOnlySpan<byte> data)
    {
        Span<byte> number=stackalloc byte[4];BinaryPrimitives.WriteUInt32BigEndian(number,(uint)data.Length);output.Write(number);output.Write(type);output.Write(data);uint crc=uint.MaxValue;
        foreach(var value in type){crc^=value;for(var bit=0;bit<8;bit++)crc=(crc&1)!=0?0xedb88320U^(crc>>1):crc>>1;}
        foreach(var value in data){crc^=value;for(var bit=0;bit<8;bit++)crc=(crc&1)!=0?0xedb88320U^(crc>>1):crc>>1;}
        BinaryPrimitives.WriteUInt32BigEndian(number,crc^uint.MaxValue);output.Write(number);
    }
    [Fact] public void PreviewEndpointUsesSharedUploadSecurityAndUpgLimits()
    {
        using var factory=new QmsWebApplicationFactory();
        var endpoint=factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(e=>e.RoutePattern.RawText=="/api/g2/nonconformances/photos/preview");
        Assert.NotNull(endpoint.Metadata.GetMetadata<SanitizeImageMetadataAfterScanAttribute>());
        Assert.Equal(G2QualityRules.MaximumStageBytes,endpoint.Metadata.GetRequiredMetadata<UploadTotalSizeLimitAttribute>().MaximumBytes);
        Assert.Equal(G2QualityRules.MaximumPreviewMultipartBytes,endpoint.Metadata.GetRequiredMetadata<IRequestSizeLimitMetadata>().MaxRequestBodySize);
        Assert.True(G2QualityRules.MaximumBase64Characters>G2QualityRules.MaximumPreviewMultipartBytes);
    }
    [Fact] public async Task RejectInvalidPhotosAndVersion()
    {
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("text",[]));
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("text",[new("x.svg","image/svg+xml","PHN2Zz4=")]));
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("",Photos));
        Assert.Throws<DBConcurrencyException>(()=>G2QualityRules.Version(1,2));
        await G2QualityRules.ValidateImagesAsync("text",Photos,TestContext.Current.CancellationToken);
        using(var image=new MagickImage(MagickColors.CornflowerBlue,3,2))
        {
            var jpeg=image.ToByteArray(MagickFormat.Jpeg);var alias=new G2QualityPhoto("alias.jpg","image/jpg",Convert.ToBase64String(jpeg));
            Assert.Equal("image/jpeg",(await Emi.Qms.Api.OsanProjects.OsanProgressPhotoValidator.ValidateAsync("alias.jpg","image/jpg",jpeg,TestContext.Current.CancellationToken)).Photo!.NormalizedMime);
            await G2QualityRules.ValidateImagesAsync("text",[alias],TestContext.Current.CancellationToken);
        }
        var large=LargePng.Value;Assert.True(large.Length>5_000_000);
        var largeBase64=Convert.ToBase64String(large);await G2QualityRules.ValidateImagesAsync("text",[new("large.png","image/png",largeBase64)],TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(()=>G2QualityRules.Validate("text",Enumerable.Range(0,4).Select(i=>new G2QualityPhoto($"overflow-{i}.png","image/png",largeBase64)).ToArray()));
        await Assert.ThrowsAsync<ArgumentException>(()=>G2QualityRules.ValidateImagesAsync("text",[new("bad.png","image/png",Convert.ToBase64String(new byte[]{137,80,78,71,13,10,26,10,0}))],TestContext.Current.CancellationToken));
    }
    [Fact] public async Task LifecyclePermissionsProjectionConcurrencyAndPurge()
    {
        var ct=TestContext.Current.CancellationToken;
        await using var db=await BusinessUnitIsolationTests.IsolationDatabaseSet.CreateAsync(ct);
        var provider=new DatabaseConnectionStringProvider(db.Configuration);
        await BusinessUnitIsolationTests.BootstrapTargetsAsync(new DatabaseRoleBootstrapper(db.Configuration,new DatabaseRuntimePrivilegeManager(),NullLogger<DatabaseRoleBootstrapper>.Instance),ct);
        await new DatabaseMigrationRunner(provider,DatabaseMigrationCatalog.FromPaths(Path.Combine(db.RepositoryRoot,"database","migrations"),Path.Combine(db.RepositoryRoot,"database","business-migrations")),new DatabaseRuntimePrivilegeManager(),db.Configuration,NullLogger<DatabaseMigrationRunner>.Instance).ApplyAndVerifyAsync(BusinessUnitCodes.Cheongju,ct);
        var quality=Guid.Parse("50000000-0000-0000-0000-000000000005");var staff=Guid.Parse("50000000-0000-0000-0000-000000000004");var admin=Guid.Parse("50000000-0000-0000-0000-000000000001");
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"""
        insert into qms_users(id,development_user_key,display_name,department_id,is_active) values
        ('50000000-0000-0000-0000-000000000005','quality-test','Synthetic quality',(select id from departments where code='quality'),true),
        ('50000000-0000-0000-0000-000000000004','staff-test','Synthetic staff',(select id from departments where code='manufacturing'),true),
        ('50000000-0000-0000-0000-000000000001','admin-test','Synthetic admin',(select id from departments where code='sales'),true);
        insert into user_roles(user_id,role_id) select '50000000-0000-0000-0000-000000000001',id from roles where code='system-administrator';
        insert into g2_daily_metrics(work_date,metric_code,quantity,created_by_user_id,updated_by_user_id) values ('2026-10-11','Defect',2,'50000000-0000-0000-0000-000000000005','50000000-0000-0000-0000-000000000005');
        """,ct);
        var clock=new Clock();var scanner=new CapturingScanner(UploadMalwareScanStatus.Clean);var security=Options.Create(new UploadSecurityOptions{Enabled=true,FailClosed=true});var store=new G2NonconformanceStore(new CheongjuDatabase(provider),clock,scanner,security);var day=new DateOnly(2026,10,11);
        var request=new SaveG2Quality("SERIAL",day,"production","Issue",false,Photos,null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request,staff,ct));
        var first=(await store.SaveFirstAsync(null,request,quality,ct))!;
        Assert.Null(first.Photos[0].Base64); Assert.NotNull(first.Photos[0].Id);Assert.True(first.Photos[0].ByteSize>0);
        Assert.NotNull((await store.PhotoAsync(first.Id,first.Photos[0].Id!.Value,ct))!.Base64);
        var largePhoto=LargePng.Value;var largeRequest=request with {Serial="LARGE-PHOTO",Photos=[new("large.png","image/png",Convert.ToBase64String(largePhoto))]};var largeItem=(await store.SaveFirstAsync(null,largeRequest,quality,ct))!;
        var storedLarge=(await store.PhotoAsync(largeItem.Id,largeItem.Photos[0].Id!.Value,ct))!;Assert.Equal(largePhoto,scanner.Files[^1]);Assert.True(largeItem.Photos[0].ByteSize>5_000_000);Assert.True(Convert.FromBase64String(storedLarge.Base64!).Length>5_000_000);
        var scansBeforeLargeRetention=scanner.Files.Count;var retainedLarge=(await store.SaveFirstAsync(largeItem.Id,largeRequest with {Content="Retained large photo",Photos=largeItem.Photos,ExpectedVersion=largeItem.Version},quality,ct))!;Assert.Equal(scansBeforeLargeRetention,scanner.Files.Count);Assert.Equal(storedLarge.Base64,(await store.PhotoAsync(largeItem.Id,largeItem.Photos[0].Id!.Value,ct))!.Base64);await store.DeleteAsync(largeItem.Id,retainedLarge.Version,false,quality,ct);
        var largeOverflow=largeRequest with {Serial="LARGE-OVERFLOW",Photos=Enumerable.Range(0,4).Select(i=>new G2QualityPhoto($"large-{i}.png","image/png",largeRequest.Photos[0].Base64)).ToArray()};var scansBeforeOverflow=scanner.Files.Count;await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveFirstAsync(null,largeOverflow,quality,ct));Assert.Equal(scansBeforeOverflow,scanner.Files.Count);
        using(var webpImage=new MagickImage(MagickColors.MediumSeaGreen,19,13))
        {
            var rawWebp=webpImage.ToByteArray(MagickFormat.WebP);var webpRequest=request with {Serial="WEBP",Photos=[new("photo.webp","application/octet-stream",Convert.ToBase64String(rawWebp))]};
            var webpItem=(await store.SaveFirstAsync(null,webpRequest,quality,ct))!;var storedWebp=(await store.PhotoAsync(webpItem.Id,webpItem.Photos[0].Id!.Value,ct))!;var storedBytes=Convert.FromBase64String(storedWebp.Base64!);
            Assert.Equal("image/png",storedWebp.ContentType);Assert.True(storedBytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}));Assert.Equal(rawWebp,scanner.Files[^1]);Assert.NotEqual(rawWebp,storedBytes);
            await store.DeleteAsync(webpItem.Id,webpItem.Version,false,quality,ct);
        }
        await Assert.ThrowsAsync<G2UnsafePhotoException>(()=>new G2NonconformanceStore(new CheongjuDatabase(provider),clock,new CapturingScanner(UploadMalwareScanStatus.Infected),security).SaveFirstAsync(null,request with {Serial="INFECTED"},quality,ct));
        await Assert.ThrowsAsync<G2PhotoScanUnavailableException>(()=>new G2NonconformanceStore(new CheongjuDatabase(provider),clock,new CapturingScanner(UploadMalwareScanStatus.Unavailable),security).SaveFirstAsync(null,request with {Serial="UNAVAILABLE"},quality,ct));
        var initial=(await store.SaveFirstAsync(null,request with {Serial="BACKLOG",InitialStock=true},admin,ct))!;
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"update qms_users set is_active=false where id='50000000-0000-0000-0000-000000000001'",ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request with {Serial="Inactive"},admin,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"update qms_users set is_active=true where id='50000000-0000-0000-0000-000000000001';delete from user_roles where user_id='50000000-0000-0000-0000-000000000001'",ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.SaveFirstAsync(null,request with {Serial="Revoked"},admin,ct));
        await db.ExecuteAsync(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"insert into user_roles(user_id,role_id) select '50000000-0000-0000-0000-000000000001',id from roles where code='system-administrator';update g2_quality_settings set activated_on='2026-10-12';insert into g2_defect_inventory_counts(count_date,quantity,created_by_user_id,updated_by_user_id) values('2026-10-10',7,'50000000-0000-0000-0000-000000000005','50000000-0000-0000-0000-000000000005')",ct);
        var operations=new G2OperationsStore(new CheongjuDatabase(provider),clock);
        var range=await operations.GetRangeAsync(day,day.AddDays(1),ct);
        Assert.Equal(3,range.Days[0].Defect!.Quantity); Assert.Equal(9,range.Days[0].DefectInventory);
        var historical=await operations.GetRangeAsync(day.AddDays(-1),day.AddDays(-1),ct);Assert.Equal(7,historical.Days[0].DefectInventoryCount!.Quantity);Assert.Equal(7,historical.Days[0].DefectInventory);
        await Assert.ThrowsAsync<ArgumentException>(()=>operations.SaveDefectInventoryCountAsync(day,new(10,null),admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>operations.SaveMetricsAsync(day,[new(G2MetricCodes.Defect,3,null)],admin,ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveActionAsync(first.Id,new(day.AddDays(-1),"Invalid date",Photos,first.Version),quality,ct));
        var action=(await store.SaveActionAsync(first.Id,new(day.AddDays(1),"Fixed",Photos,first.Version),quality,ct))!;
        await Assert.ThrowsAsync<ArgumentException>(()=>store.DeleteAsync(first.Id,action.Version,false,quality,ct));
        clock.Now=clock.Now.AddDays(1);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveFirstAsync(first.Id,request with {OccurrenceDate=day.AddDays(2),ExpectedVersion=action.Version},quality,ct));
        clock.Now=clock.Now.AddDays(-1);
        await Assert.ThrowsAsync<DBConcurrencyException>(()=>store.SaveActionAsync(first.Id,new(day,"Changed",Photos,first.Version),quality,ct));
        range=await operations.GetRangeAsync(day,day.AddDays(1),ct);Assert.Equal(1,range.Days[1].RepairTotal);Assert.Equal(1,range.Days[1].DefectInventory);
        var reopened=(await store.DeleteAsync(first.Id,action.Version,true,quality,ct))!; Assert.Null(reopened.Action);
        range=await operations.GetRangeAsync(day,day.AddDays(1),ct);Assert.Null(range.Days[1].RepairTotal);Assert.Equal(2,range.Days[1].DefectInventory);
        var scansBeforeRetainedEdit=scanner.Files.Count;var edits=await Task.WhenAll(Enumerable.Range(0,2).Select(async i=>{try{await store.SaveFirstAsync(first.Id,request with {ExpectedVersion=reopened.Version,Content=$"Edit{i}",Photos=reopened.Photos},quality,ct);return true;}catch(DBConcurrencyException){return false;}}));Assert.Single(edits,x=>x);Assert.Equal(scansBeforeRetainedEdit,scanner.Files.Count);
        var list=JsonSerializer.SerializeToElement(await store.ReadAsync(staff,ct));Assert.False(list.GetProperty("canEdit").GetBoolean());
        await store.DeleteAsync(first.Id,reopened.Version+1,false,admin,ct);
        Assert.Null(await store.PhotoAsync(first.Id,first.Photos[0].Id!.Value,ct));
        Assert.True(await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_nonconformance_history",ct)>0);
        clock.Now=clock.Now.AddMonths(3).AddSeconds(1);await store.PruneAsync(ct);
        Assert.Equal(0,await db.ReadScalarAsync<long>(BusinessUnitCodes.Cheongju,BusinessUnitConnectionPurpose.Administrator,"select count(*) from g2_nonconformance_history",ct));
        Assert.NotNull(await store.PhotoAsync(initial.Id,initial.Photos[0].Id!.Value,ct));
        using var factory=QmsWebApplicationFactory.Create("Testing",new Dictionary<string,string?>{
            ["DevAuthentication:Enabled"]="true",["Database:ApplyMigrationsOnStartup"]="false",["DevelopmentData:SeedEnabled"]="false",
            ["UploadSecurity:Enabled"]="true",["UploadSecurity:FailClosed"]="true",["UploadSecurity:RejectImageMetadata"]="true",
            ["ConnectionStrings:QmsDatabase"]=provider.GetConnectionString(db.BusinessUnits.GetBusiness(BusinessUnitCodes.Cheongju))},configureTestServices:services=>
            {
                var registered=services.Single(service=>service.ServiceType==typeof(IUploadMalwareScanner));services.Remove(registered);services.AddSingleton<IUploadMalwareScanner>(scanner);
            });
        using var client=factory.CreateClient();client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"dev-manufacturing");
        using var denied=await client.PostAsJsonAsync("/api/g2/nonconformances",request,ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        var heic=OsanHeicPhotoTests.CreateHeic();using(var deniedPreviewBody=new MultipartFormDataContent()){var file=new ByteArrayContent(heic);file.Headers.ContentType=new("image/heic");deniedPreviewBody.Add(file,"photo","phone.heic");using var deniedPreview=await client.PostAsync("/api/g2/nonconformances/photos/preview",deniedPreviewBody,ct);Assert.Equal(HttpStatusCode.Forbidden,deniedPreview.StatusCode);}
        client.DefaultRequestHeaders.Remove(DevelopmentAuthenticationDefaults.UserHeader);client.DefaultRequestHeaders.Add(DevelopmentAuthenticationDefaults.UserHeader,"dev-quality");
        scanner.Status=UploadMalwareScanStatus.Infected;using(var infected=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial="HTTP-INFECTED"},ct))Assert.Equal(HttpStatusCode.UnprocessableEntity,infected.StatusCode);
        scanner.Status=UploadMalwareScanStatus.Unavailable;using(var unavailable=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial="HTTP-UNAVAILABLE"},ct))Assert.Equal(HttpStatusCode.ServiceUnavailable,unavailable.StatusCode);
        scanner.Status=UploadMalwareScanStatus.Clean;
        using var created=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial="HTTP"},ct);Assert.True(created.IsSuccessStatusCode,await created.Content.ReadAsStringAsync(ct));
        var httpItem=(await created.Content.ReadFromJsonAsync<G2QualityItem>(ct))!;
        using var aliasImage=new MagickImage(MagickColors.CornflowerBlue,8,6);var aliasJpeg=aliasImage.ToByteArray(MagickFormat.Jpeg);var aliasRequest=request with {Serial="HTTP-JPG-ALIAS",Photos=[new("alias.jpg","image/jpg",Convert.ToBase64String(aliasJpeg))]};using var createdAlias=await client.PostAsJsonAsync("/api/g2/nonconformances",aliasRequest,ct);Assert.Equal(HttpStatusCode.OK,createdAlias.StatusCode);var aliasItem=(await createdAlias.Content.ReadFromJsonAsync<G2QualityItem>(ct))!;Assert.Equal("image/jpeg",aliasItem.Photos[0].ContentType);
        using(var aliasDownload=await client.GetAsync($"/api/g2/nonconformances/{aliasItem.Id}/photos/{aliasItem.Photos[0].Id}",ct))Assert.Equal("image/jpeg",aliasDownload.Content.Headers.ContentType?.MediaType);
        using(var aliasPreviewBody=new MultipartFormDataContent()){var file=new ByteArrayContent(aliasJpeg);file.Headers.ContentType=new("image/jpg");aliasPreviewBody.Add(file,"photo","alias.jpg");using var preview=await client.PostAsync("/api/g2/nonconformances/photos/preview",aliasPreviewBody,ct);Assert.Equal(HttpStatusCode.OK,preview.StatusCode);using var json=JsonDocument.Parse(await preview.Content.ReadAsStringAsync(ct));Assert.Equal("image/jpeg",json.RootElement.GetProperty("contentType").GetString());}
        var heicRequest=request with {Serial="HTTP-HEIC",Photos=[new("phone.heic","image/heic",Convert.ToBase64String(heic))]};using var createdHeic=await client.PostAsJsonAsync("/api/g2/nonconformances",heicRequest,ct);Assert.Equal(HttpStatusCode.OK,createdHeic.StatusCode);var heicItem=(await createdHeic.Content.ReadFromJsonAsync<G2QualityItem>(ct))!;
        using(var originalHeic=await client.GetAsync($"/api/g2/nonconformances/{heicItem.Id}/photos/{heicItem.Photos[0].Id}",ct)){Assert.Equal("image/heic",originalHeic.Content.Headers.ContentType?.MediaType);Assert.True((await originalHeic.Content.ReadAsByteArrayAsync(ct)).Length>0);}
        using(var renderedHeic=await client.GetAsync($"/api/g2/nonconformances/{heicItem.Id}/photos/{heicItem.Photos[0].Id}?preview=true",ct)){Assert.Equal(HttpStatusCode.OK,renderedHeic.StatusCode);Assert.Equal("image/jpeg",renderedHeic.Content.Headers.ContentType?.MediaType);Assert.True((await renderedHeic.Content.ReadAsByteArrayAsync(ct)).AsSpan().StartsWith(new byte[]{255,216}));}
        using(var previewBody=new MultipartFormDataContent()){var file=new ByteArrayContent(heic);file.Headers.ContentType=new("image/heic");previewBody.Add(file,"photo","phone.heic");using var preview=await client.PostAsync("/api/g2/nonconformances/photos/preview",previewBody,ct);Assert.Equal(HttpStatusCode.OK,preview.StatusCode);using var previewJson=JsonDocument.Parse(await preview.Content.ReadAsStringAsync(ct));Assert.Equal("image/jpeg",previewJson.RootElement.GetProperty("contentType").GetString());Assert.True(Convert.FromBase64String(previewJson.RootElement.GetProperty("base64").GetString()!).AsSpan().StartsWith(new byte[]{255,216}));}
        using(var largePreviewBody=new MultipartFormDataContent()){var file=new ByteArrayContent(largePhoto);file.Headers.ContentType=new("image/png");largePreviewBody.Add(file,"photo","large.png");using var preview=await client.PostAsync("/api/g2/nonconformances/photos/preview",largePreviewBody,ct);Assert.Equal(HttpStatusCode.OK,preview.StatusCode);}
        using var duplicate=await client.PostAsJsonAsync("/api/g2/nonconformances",request with {Serial=" http "},ct);Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var duplicateSaves=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try {await store.SaveFirstAsync(null,request with {Serial="CONCURRENT"},quality,ct);return true;}catch(G2QualityDuplicateSerialException){return false;}}));Assert.Single(duplicateSaves,x=>x);
        var context=new DefaultHttpContext();
        BusinessUnitRequestContextFeature.Set(context,new(BusinessUnitAccessStatuses.Selected,Guid.NewGuid(),db.BusinessUnits.GetBusiness(BusinessUnitCodes.Osan),[BusinessUnitCodes.Osan],false,"test"));
        using var wrongProvider=new DatabaseConnectionStringProvider(db.Configuration,new HttpContextAccessor {HttpContext=context});
        var wrongStore=new G2NonconformanceStore(new CheongjuDatabase(wrongProvider),clock,scanner,security);
        var wrongCampus=await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.PhotoAsync(httpItem.Id,httpItem.Photos[0].Id!.Value,ct));Assert.Equal("business_unit_module_mismatch",wrongCampus.Reason);
        await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.ReadAsync(quality,ct));
        await Assert.ThrowsAsync<BusinessUnitContextUnavailableException>(()=>wrongStore.SaveFirstAsync(null,request with {Serial="WRONG-CAMPUS"},quality,ct));
        using(var anonymous=factory.CreateClient()) {using var unauthorized=await anonymous.GetAsync($"/api/g2/nonconformances/{httpItem.Id}/photos/{httpItem.Photos[0].Id}",ct);Assert.Equal(HttpStatusCode.Unauthorized,unauthorized.StatusCode);}
        using var photo=await client.GetAsync($"/api/g2/nonconformances/{httpItem.Id}/photos/{httpItem.Photos[0].Id}",ct);Assert.Equal(HttpStatusCode.OK,photo.StatusCode);
        using var repaired=await client.PutAsJsonAsync($"/api/g2/nonconformances/{httpItem.Id}/action",new SaveG2QualityAction(day,"Fixed",Photos,httpItem.Version),ct);Assert.Equal(HttpStatusCode.OK,repaired.StatusCode);
        using var blocked=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}?expectedVersion=2",ct);Assert.Equal(HttpStatusCode.BadRequest,blocked.StatusCode);
        using var undo=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}/action?expectedVersion=2",ct);Assert.Equal(HttpStatusCode.OK,undo.StatusCode);
        using var deleted=await client.DeleteAsync($"/api/g2/nonconformances/{httpItem.Id}?expectedVersion=3",ct);Assert.Equal(HttpStatusCode.OK,deleted.StatusCode);
    }
}

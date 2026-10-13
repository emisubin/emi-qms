using System.Data;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.InteriorBusbar;
using Emi.Qms.Api.OsanProjects;
using Emi.Qms.Api.Security;
namespace Emi.Qms.Api.G2;
public static class G2NonconformanceEndpoints
{
    public static IEndpointRouteBuilder MapG2NonconformanceEndpoints(this IEndpointRouteBuilder app)
    {
        var g=app.MapGroup("/api/g2/nonconformances").RequireAuthorization(QmsPolicies.G2Read);
        g.MapGet("",(G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>Results.Ok(await s.ReadAsync(Actor(u),ct))));
        g.MapPost("",(SaveG2Quality r,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>Results.Ok(await s.SaveFirstAsync(null,r,Actor(u),ct))));
        g.MapPut("/{id:guid}",(Guid id,SaveG2Quality r,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>Results.Ok(await s.SaveFirstAsync(id,r,Actor(u),ct))));
        g.MapPut("/{id:guid}/action",(Guid id,SaveG2QualityAction r,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>Results.Ok(await s.SaveActionAsync(id,r,Actor(u),ct))));
        g.MapDelete("/{id:guid}",(Guid id,int expectedVersion,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>{await s.DeleteAsync(id,expectedVersion,false,Actor(u),ct);return Results.Ok(new{saved=true});}));
        g.MapDelete("/{id:guid}/action",(Guid id,int expectedVersion,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>Results.Ok(await s.DeleteAsync(id,expectedVersion,true,Actor(u),ct))));
        g.MapPost("/photos/preview",(HttpRequest request,G2NonconformanceStore s,ClaimsPrincipal u,CancellationToken ct)=>Safe(async()=>
        {
            await s.EnsureCanEditAsync(Actor(u),ct);
            if(!request.HasFormContentType)return Results.BadRequest(new{code="invalid_request",message="사진을 선택해 주세요."});
            IFormCollection form;
            try { form=await request.ReadFormAsync(ct); }
            catch(Exception e) when(e is InvalidDataException or IOException or BadHttpRequestException) { return Results.BadRequest(new{code="invalid_request",message="사진 업로드 요청을 읽지 못했습니다. 다시 선택해 주세요."}); }
            var file=form.Files.GetFile("photo");
            if(form.Files.Count!=1 || file is null || file.Length is < 1 or > G2QualityRules.MaximumPhotoBytes)return Results.BadRequest(new{code="invalid_request",message="5MB 이하 사진 한 장을 선택해 주세요."});
            using var buffer=new MemoryStream((int)file.Length); await file.CopyToAsync(buffer,ct);
            var (photo,error)=await InteriorBusbarPhotoValidator.ValidateAsync(file.FileName,file.ContentType,buffer.ToArray(),ct);
            if(photo is null || photo.Content.Length>G2QualityRules.MaximumPhotoBytes)return Results.BadRequest(new{code="invalid_request",message=error??"5MB 이하 사진 한 장을 선택해 주세요."});
            try
            {
                var bytes=photo.ContentType=="image/heic"?await OsanHeicImageCodec.PreviewAsync(photo.Content,ct):photo.Content;
                return Results.Ok(new{contentType=photo.ContentType=="image/heic"?"image/jpeg":photo.ContentType,base64=Convert.ToBase64String(bytes)});
            }
            catch(InvalidDataException) { return Results.BadRequest(new{code="invalid_request",message="사진 미리보기를 만들 수 없습니다. 사진을 다시 선택해 주세요."}); }
        })).WithMetadata(new SanitizeImageMetadataAfterScanAttribute())
           .WithMetadata(new UploadTotalSizeLimitAttribute(G2QualityRules.MaximumPhotoBytes))
           .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(G2QualityRules.MaximumPreviewMultipartBytes));
        g.MapGet("/{id:guid}/photos/{photoId:guid}",(Guid id,Guid photoId,bool? preview,HttpContext http,G2NonconformanceStore s,CancellationToken ct)=>Safe(async()=>
        {
            var p=await s.PhotoAsync(id,photoId,ct); if(p is null)return Results.NotFound();
            http.Response.Headers.XContentTypeOptions="nosniff";
            var bytes=Convert.FromBase64String(p.Base64!);
            if(preview==true && p.ContentType=="image/heic")
            {
                try { return Results.File(await OsanHeicImageCodec.PreviewAsync(bytes,ct),"image/jpeg"); }
                catch(InvalidDataException) { return Results.BadRequest(new{code="invalid_request",message="사진 미리보기를 만들 수 없습니다."}); }
            }
            return Results.File(bytes,p.ContentType);
        }));
        return app;
    }
    private static Guid Actor(ClaimsPrincipal u)=>Guid.TryParse(u.FindFirst(QmsClaimTypes.UserId)?.Value,out var id)?id:throw new UnauthorizedAccessException();
    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try{return await action();}
        catch(UnauthorizedAccessException){return Results.Forbid();}
        catch(G2UnsafePhotoException){return Results.UnprocessableEntity(new{code="unsafe_photo",message="안전하지 않은 파일이 차단되었습니다. 파일을 확인한 후 다시 시도해 주세요."});}
        catch(G2PhotoScanUnavailableException){return Results.Json(new{code="photo_scan_unavailable",message="파일 안전 검사를 완료할 수 없습니다. 잠시 후 다시 시도해 주세요."},statusCode:StatusCodes.Status503ServiceUnavailable);}
        catch(KeyNotFoundException){return Results.NotFound(new{code="not_found",message="정보가 없거나 삭제되었습니다."});}
        catch(G2QualityDuplicateSerialException){return Results.Conflict(new{code="duplicate_serial",message="이미 등록된 시리얼입니다. 기존 부적합 정보를 확인해 주세요."});}
        catch(DBConcurrencyException e){return Results.Conflict(new{code="version_conflict",message=e.Message});}
        catch(ArgumentException e){return Results.BadRequest(new{code="invalid_request",message=e.Message});}
    }
}

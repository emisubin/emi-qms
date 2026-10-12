using System.Data;
using System.Security.Claims;
using Emi.Qms.Api.Authorization;
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
        g.MapGet("/{id:guid}/photos/{photoId:guid}",(Guid id,Guid photoId,G2NonconformanceStore s,CancellationToken ct)=>Safe(async()=>{var p=await s.PhotoAsync(id,photoId,ct);return p is null?Results.NotFound():Results.File(Convert.FromBase64String(p.Base64!),p.ContentType);}));
        return app;
    }
    private static Guid Actor(ClaimsPrincipal u)=>Guid.TryParse(u.FindFirst(QmsClaimTypes.UserId)?.Value,out var id)?id:throw new UnauthorizedAccessException();
    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try{return await action();}
        catch(UnauthorizedAccessException){return Results.Forbid();}
        catch(KeyNotFoundException){return Results.NotFound(new{code="not_found",message="정보가 없거나 삭제되었습니다."});}
        catch(G2QualityDuplicateSerialException){return Results.Conflict(new{code="duplicate_serial",message="이미 등록된 시리얼입니다. 기존 부적합 정보를 확인해 주세요."});}
        catch(DBConcurrencyException e){return Results.Conflict(new{code="version_conflict",message=e.Message});}
        catch(ArgumentException e){return Results.BadRequest(new{code="invalid_request",message=e.Message});}
    }
}

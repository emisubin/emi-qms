using System.Security.Claims;
using Emi.Qms.Api.Authorization;
namespace Emi.Qms.Api.G2;
public static class G2MaterialEndpoints
{
    public static IEndpointRouteBuilder MapG2MaterialEndpoints(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/g2/materials").RequireAuthorization(QmsPolicies.G2Read);
        group.MapGet("",async(DateOnly date,G2MaterialStore store,ClaimsPrincipal user,CancellationToken ct)=>await Safe(async()=>Results.Ok(await store.ReadAsync(date,Actor(user),ct))));
        group.MapPut("/{date}",async(DateOnly date,SaveG2MaterialsRequest request,G2MaterialStore store,ClaimsPrincipal user,CancellationToken ct)=>await Safe(async()=>{await store.SaveAsync(date,request,Actor(user),ct);return Results.Ok(new{saved=true});}));
        group.MapPost("/import",async(HttpRequest request,G2MaterialStore store,ClaimsPrincipal user,TimeProvider time,CancellationToken ct)=>await Safe(async()=>
        {
            if(!await store.CanEditAsync(Actor(user),ct))return Results.Forbid();
            if(!request.HasFormContentType || request.ContentLength>G2MaterialExcel.MaxBytes)return Results.BadRequest(new{message="25MB 이하의 .xlsx 파일을 선택해 주세요."});
            var form=await request.ReadFormAsync(ct);var file=form.Files.GetFile("file");
            if(file is null || file.Length>G2MaterialExcel.MaxBytes || !Path.GetExtension(file.FileName).Equals(".xlsx",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("25MB 이하의 .xlsx 파일을 선택해 주세요.");
            if(!DateOnly.TryParse(form["date"],out var date))throw new ArgumentException("실사일을 선택해 주세요.");G2MaterialRules.ValidateDate(date,G2MaterialRules.Today(time));
            using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);stream.Position=0;
            return Results.Ok(G2MaterialExcel.Parse(stream,string.IsNullOrWhiteSpace(form["sheet"])?null:form["sheet"].ToString(),date,await store.CatalogAsync(ct)));
        }));
        group.MapGet("/{id:int}/images/{index:int}",async(int id,int index,G2MaterialStore store,IConfiguration config,CancellationToken ct)=>await Safe(async()=>
        {
            var item=(await store.CatalogAsync(ct)).FirstOrDefault(x=>x.Id==id);var keys=item?.ImageKeys;
            var root=config["G2Materials:ImageRoot"];
            if(keys is null || index<0 || index>=keys.Length || string.IsNullOrWhiteSpace(root))return Results.NotFound();
            var key=keys[index];if(Path.GetFileName(key)!=key || key.Contains('\\'))return Results.NotFound();
            var path=Path.Combine(root,key);if(!File.Exists(path))return Results.NotFound();
            var type=Path.GetExtension(path).ToLowerInvariant() switch{".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".webp"=>"image/webp",_=>null};
            return type is null?Results.NotFound():Results.File(path,type);
        }));
        return app;
    }
    private static Guid Actor(ClaimsPrincipal user)=>Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value,out var id)?id:throw new UnauthorizedAccessException();
    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try{return await action();}catch(UnauthorizedAccessException){return Results.Forbid();}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]>{{"request",[e.Message]}});}catch(InvalidDataException){return Results.BadRequest(new{message="업로드 형식을 확인해 주세요."});}
    }
}

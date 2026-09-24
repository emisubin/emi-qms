using System.Security.Claims;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.Notices;

public sealed record NoticeSettingsRequest(int ExpectedVersion, bool Pinned, bool PopupEnabled, bool Reannounce = false);
public sealed record NoticeSettings(int Version, bool Pinned, bool PopupEnabled, int PopupVersion);
public sealed record NoticePopup(Guid NoticeId, string Title, string Body, int PopupVersion);

public sealed partial class NoticeStore
{
    public async Task<NoticeSettings?> SettingsAsync(Guid id, NoticeSettingsRequest? request, Guid actor, CancellationToken ct)
    {
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "select version,pinned,popup_enabled,popup_version from notice_posts where id=@id and deleted_at_utc is null for update";
        command.Parameters.AddWithValue("id", id);
        NoticeSettings current;
        await using(var reader = await command.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct)) return null;
            current = new(reader.GetInt32(0),reader.GetBoolean(1),reader.GetBoolean(2),reader.GetInt32(3));
        }
        if(request is not null)
        {
            if(current.Version != request.ExpectedVersion) return current with { Version = -1 };
            var popupVersion = current.PopupVersion + (request.PopupEnabled && (!current.PopupEnabled || request.Reannounce) ? 1 : 0);
            command.CommandText = """
                insert into notice_post_revisions(notice_post_id,version,title,body,body_format,changed_by_user_id)
                select id,version,title,body,body_format,@actor from notice_posts where id=@id;
                update notice_posts set pinned=@pinned,popup_enabled=@enabled,popup_version=@popup,version=version+1,
                  updated_at_utc=now(),updated_by_user_id=@actor where id=@id;
                insert into notice_setting_events(notice_id,actor_user_id,pinned,popup_enabled,popup_version)
                values(@id,@actor,@pinned,@enabled,@popup);
                """;
            command.Parameters.AddWithValue("actor",actor);
            command.Parameters.AddWithValue("pinned",request.Pinned);
            command.Parameters.AddWithValue("enabled",request.PopupEnabled);
            command.Parameters.AddWithValue("popup",popupVersion);
            await command.ExecuteNonQueryAsync(ct);
            current = new(current.Version+1,request.Pinned,request.PopupEnabled,popupVersion);
        }
        await tx.CommitAsync(ct);
        return current;
    }

    public async Task<bool> MarkNoticeReadAsync(Guid id, Guid actor, CancellationToken ct)
    {
        await using var source = CreateDataSource();
        await using var command = source.CreateCommand("""
            insert into notice_reads(notice_id,user_id)
            select id,@actor from notice_posts where id=@id and deleted_at_utc is null
            on conflict(notice_id,user_id) do update set read_at_utc=now();
            """);
        command.Parameters.AddWithValue("id",id); command.Parameters.AddWithValue("actor",actor);
        return await command.ExecuteNonQueryAsync(ct)>0;
    }

    public async Task<IReadOnlyList<NoticePopup>> PendingPopupsAsync(Guid actor, CancellationToken ct)
    {
        await using var source=CreateDataSource();
        await using var command=source.CreateCommand("""
            select id,title,body,popup_version from notice_posts p where deleted_at_utc is null and popup_enabled
            and not exists(select 1 from notice_popup_receipts r where r.notice_id=p.id and r.user_id=@actor and r.popup_version=p.popup_version)
            order by pinned desc,created_at_utc desc,id limit 10;
            """);
        command.Parameters.AddWithValue("actor",actor);
        var items=new List<NoticePopup>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3)));
        return items;
    }

    public async Task<bool> ClaimPopupAsync(Guid id, int version, Guid actor, CancellationToken ct)
    {
        await using var source=CreateDataSource();
        await using var command=source.CreateCommand("""
            insert into notice_popup_receipts(notice_id,user_id,popup_version)
            select id,@actor,popup_version from notice_posts where id=@id and deleted_at_utc is null and popup_enabled and popup_version=@version
            on conflict do nothing;
            """);
        command.Parameters.AddWithValue("id",id);command.Parameters.AddWithValue("actor",actor);command.Parameters.AddWithValue("version",version);
        return await command.ExecuteNonQueryAsync(ct)>0;
    }
}

public static class OsanNoticeEndpoints
{
    public static IEndpointRouteBuilder MapOsanNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        var api=app.MapGroup("/api/osan/notices").RequireAuthorization();
        api.AddEndpointFilter(async (context,next)=>
        {
            if(BusinessUnitRequestContextFeature.Get(context.HttpContext)?.Target?.Code != BusinessUnitCodes.Osan)
                return Results.Forbid();
            return await next(context);
        });
        api.MapGet("/popups",async(NoticeStore store,ClaimsPrincipal user,CancellationToken ct)=>Results.Ok(new {items=await store.PendingPopupsAsync(Id(user),ct)}));
        api.MapPost("/{id:guid}/read",async(Guid id,NoticeStore store,ClaimsPrincipal user,CancellationToken ct)=>
            await store.MarkNoticeReadAsync(id,Id(user),ct) ? Results.Ok() : Results.NotFound());
        api.MapPost("/{id:guid}/popups/{version:int}/claim",async(Guid id,int version,NoticeStore store,ClaimsPrincipal user,CancellationToken ct)=>
            Results.Ok(new {claimed=await store.ClaimPopupAsync(id,version,Id(user),ct)}));
        api.MapGet("/{id:guid}/settings",async(Guid id,NoticeStore store,ClaimsPrincipal user,CancellationToken ct)=>
        {
            if(!user.IsInRole(QmsRoles.SystemAdministrator))return Results.Forbid();
            var result=await store.SettingsAsync(id,null,Id(user),ct);
            return result is null? Results.NotFound():Results.Ok(result);
        });
        api.MapPut("/{id:guid}/settings",async(Guid id,NoticeSettingsRequest request,NoticeStore store,ClaimsPrincipal user,CancellationToken ct)=>
        {
            if(!user.IsInRole(QmsRoles.SystemAdministrator))return Results.Forbid();
            var result=await store.SettingsAsync(id,request,Id(user),ct);
            return result is null?Results.NotFound():result.Version<0?Results.Conflict(new {message="공지 상태가 변경되었습니다. 다시 불러와 주세요."}):Results.Ok(result);
        }).WithName("UpdateOsanNoticeSettings");
        return app;
    }
    private static Guid Id(ClaimsPrincipal user)=>Guid.Parse(user.FindFirst(QmsClaimTypes.UserId)!.Value);
}

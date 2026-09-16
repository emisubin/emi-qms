using Npgsql;
namespace Emi.Qms.Api.InteriorBusbar;

public sealed record BusbarMasterAccessRequest(Guid UserId, string Access, string Reason);
public sealed partial class InteriorBusbarStore
{
    public Task<BusbarAccess> MasterAccess(Guid actor, BusbarAccess access) => ReadSnapshot(async c =>
    {
        if (access.ManageMasterPermissions) return access;
        var rows = await Rows(c, "select can_edit from busbar_master_access where user_id=@id", ("id", actor));
        return access with { MastersRead = rows.Count > 0, MastersWrite = rows.Count > 0 && (bool)rows[0]["canEdit"]! };
    });

    public Task<List<Dictionary<string, object?>>> MasterAccessUsers() => ReadSnapshot(c => Rows(c,
        "select u.id user_id,u.display_name,a.can_edit from qms_users u left join busbar_master_access a on a.user_id=u.id order by u.display_name,u.id"));

    public Task<Guid> SetMasterAccess(BusbarMasterAccessRequest request, Guid actor) => Transaction(async c =>
    {
        Require(request.Access is "None" or "Read" or "Edit", "권한을 선택하세요.");
        Text(request.Reason, "변경 사유");
        await One(c, "qms_users", request.UserId);
        var before = await Rows(c, "select * from busbar_master_access where user_id=@id", ("id", request.UserId));
        if (request.Access == "None") await Exec(c, "delete from busbar_master_access where user_id=@id", ("id", request.UserId));
        else await Exec(c, "insert into busbar_master_access(user_id,can_edit,updated_by) values(@id,@edit,@actor) on conflict(user_id) do update set can_edit=excluded.can_edit,updated_by=excluded.updated_by,updated_at_utc=now()",
            ("id", request.UserId), ("edit", request.Access == "Edit"), ("actor", actor));
        await Audit(c, "MasterAccess", request.UserId, actor, request.Reason, before, request);
        return request.UserId;
    });
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed record OsanPhotoEditRequest(Guid RequestId, Guid TargetId, int StageSequence);
public sealed record OsanPhotoEditItem(Guid RequestId, Guid TargetId, Guid StepId, Guid RequestedBy,
    string RequestedByName, DateTimeOffset RequestedAt, DateTimeOffset? ApprovedAt,
    string? ApprovedByName, DateTimeOffset? UsedAt, IReadOnlyList<Guid> PhotoIds, IReadOnlyList<Guid> OriginalPhotoIds, DateTimeOffset? InvalidatedAt = null);

public sealed class OsanPhotoEditStore(DatabaseConnectionStringProvider db)
{
    private NpgsqlDataSource Source() => NpgsqlDataSource.Create(db.GetConnectionString()
        ?? throw new InvalidOperationException("QMS database connection string is not configured."));

    public async Task<IReadOnlyList<OsanPhotoEditItem>> ListAsync(Guid project, CancellationToken ct)
    {
        await using var source = Source();
        await using var cmd = source.CreateCommand("""
            select r.id,r.target_id,r.step_id,r.requested_by,u.display_name,r.requested_at,
              r.approved_at,a.display_name,r.used_at,
              array(select f.id from osan_photo_revision_files f where f.request_id=r.id order by f.display_order),
              array(select l.photo_id from osan_progress_step_photos l join osan_progress_photos p on p.id=l.photo_id
                where l.step_id=r.step_id order by p.display_order),r.invalidated_at
            from osan_photo_edit_requests r join qms_users u on u.id=r.requested_by
            left join qms_users a on a.id=r.approved_by where r.project_id=@id order by r.requested_at desc;
            """);
        cmd.Parameters.AddWithValue("id", project);
        var items = new List<OsanPhotoEditItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2),
            reader.GetGuid(3),reader.GetString(4),reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6)?null:reader.GetFieldValue<DateTimeOffset>(6),reader.IsDBNull(7)?null:reader.GetString(7),
            reader.IsDBNull(8)?null:reader.GetFieldValue<DateTimeOffset>(8),reader.GetFieldValue<Guid[]>(9),reader.GetFieldValue<Guid[]>(10),reader.IsDBNull(11)?null:reader.GetFieldValue<DateTimeOffset>(11)));
        return items;
    }

    private static async Task<bool> LockProject(NpgsqlConnection c, NpgsqlTransaction tx, Guid project, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand(); cmd.Transaction=tx;
        cmd.CommandText="select id from projects where id=@id and project_profile='Osan' and deleted_at_utc is null for update";
        cmd.Parameters.AddWithValue("id",project);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<OsanManagementResult> RequestAsync(Guid project, OsanPhotoEditRequest request, Guid actor, CancellationToken ct)
    {
        if(request.RequestId==Guid.Empty || request.StageSequence is <1 or >7) return new(400);
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        await using var tx=await c.BeginTransactionAsync(ct);
        if(!await LockProject(c,tx,project,ct)) return new(404);
        await using var cmd=c.CreateCommand(); cmd.Transaction=tx;
        cmd.Parameters.AddWithValue("project",project); cmd.Parameters.AddWithValue("target",request.TargetId);
        cmd.Parameters.AddWithValue("stage",request.StageSequence);cmd.Parameters.AddWithValue("actor",actor);
        cmd.Parameters.AddWithValue("id",request.RequestId);
        cmd.CommandText="""
            insert into osan_photo_edit_requests(id,project_id,target_id,step_id,requested_by)
            select @id,@project,@target,id,@actor from osan_active_project_target_steps
            where project_id=@project and target_id=@target and sequence_number=@stage and status='Completed'
            on conflict do nothing;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText="""
            select r.id from osan_photo_edit_requests r join osan_active_project_target_steps s on s.id=r.step_id
            where r.project_id=@project and r.target_id=@target and s.sequence_number=@stage
              and r.requested_by=@actor and r.used_at is null and r.invalidated_at is null;
            """;
        var id=await cmd.ExecuteScalarAsync(ct);
        if(id is null) return new(409,Message:"완료된 단계만 요청할 수 있습니다. 다른 사용자의 요청이 있다면 관리자에게 확인해 주세요.");
        cmd.CommandText="select step_id from osan_photo_edit_requests where id=@id";
        cmd.Parameters["id"].Value=(Guid)id;
        var step=(Guid)(await cmd.ExecuteScalarAsync(ct))!;
        cmd.CommandText="select 1 from osan_stage_records where operation_id=@id and event_type='Request'";
        if(await cmd.ExecuteScalarAsync(ct) is null)
            await OsanStageRecords.AddAsync(c,tx,project,step,(Guid)id,"Request",actor,"",null,[],ct);
        await tx.CommitAsync(ct); return new(200,new { requestId=(Guid)id });
    }

    public async Task<OsanManagementResult> ApproveAsync(Guid project, Guid requestId, Guid actor, CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        await using var tx=await c.BeginTransactionAsync(ct);
        if(!await LockProject(c,tx,project,ct))return new(404);
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="""
            update osan_photo_edit_requests set approved_by=coalesce(approved_by,@actor),approved_at=coalesce(approved_at,now())
            where project_id=@project and id=@id and used_at is null and invalidated_at is null returning step_id;
            """;
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("id",requestId);cmd.Parameters.AddWithValue("actor",actor);
        var step=await cmd.ExecuteScalarAsync(ct);
        if(step is null)return new(409,Message:"이미 사용했거나 찾을 수 없는 요청입니다.");
        cmd.CommandText="select 1 from osan_stage_records where operation_id=@id and event_type='Approve'";
        if(await cmd.ExecuteScalarAsync(ct) is null)
            await OsanStageRecords.AddAsync(c,tx,project,(Guid)step,requestId,"Approve",actor,"",null,[],ct);
        await tx.CommitAsync(ct);return new(200,new{approved=true});
    }

    public async Task<OsanManagementResult> SaveAsync(Guid project, Guid requestId, CompleteOsanProgressInput input, Guid actor, CancellationToken ct, bool isAdministrator=false)
    {
        var retained=input.RetainedPhotoIds?.ToArray()??[];
        if(input.Targets.Count!=1 || input.Targets[0] is null || retained.Distinct().Count()!=retained.Length
            || OsanStageRecords.ValidateContent(input,isAdministrator,retained.Length).Count>0
            || input.Photos.Sum(p=>(long)p.Content.Length)>OsanProgressPhotoValidator.MaximumTotalBytes) return new(400,Message:"사진 및 코멘트 입력 조건을 확인해 주세요.");
        var fingerprint=OsanStageRecords.Fingerprint(new { project,requestId,actor,target=input.Targets[0]!.TargetId,input.StageSequence,input.Comment,retained,
                photos=input.Photos.Select(p=>new{p.FileName,p.Sha256}) });
        await using var source=Source();await using var c=await source.OpenConnectionAsync(ct);
        await using var tx=await c.BeginTransactionAsync(ct);
        if(!await LockProject(c,tx,project,ct))return new(404);
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("id",requestId);
        cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("target",input.Targets[0]!.TargetId);
        cmd.Parameters.AddWithValue("stage",input.StageSequence);
        cmd.CommandText="""
            select r.approved_at,r.used_at,r.fingerprint,s.id,r.invalidated_at from osan_photo_edit_requests r
            join osan_active_project_target_steps s on s.id=r.step_id
            where r.project_id=@project and r.id=@id and r.target_id=@target and s.sequence_number=@stage
              and (s.status='Completed' or s.rejected) for update of r;
            """;
        Guid step;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct) || reader.IsDBNull(0) || !reader.IsDBNull(4))return new(403,Message:"해당 대상·단계에 대한 관리자 승인이 필요합니다.");
            if(!reader.IsDBNull(1))return reader.GetString(2)==fingerprint
                ? new(200,new{saved=true,replayed=true}):new(409,Message:"이미 사용한 승인입니다. 다시 승인받아 주세요.");
            step=reader.GetGuid(3);
        }
        cmd.Parameters.AddWithValue("step",step);cmd.Parameters.AddWithValue("retained",retained);
        cmd.CommandText="select id,byte_size,sha256 from osan_current_progress_photos where project_id=@project and step_id=@step and id=any(@retained)";
        var hashes=input.Photos.Select(p=>p.Sha256).ToList();long bytes=input.Photos.Sum(p=>(long)p.Content.Length);var found=0;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)){found++;bytes+=reader.GetInt32(1);hashes.Add(reader.GetString(2));}
        if(found!=retained.Length || bytes>OsanProgressPhotoValidator.MaximumTotalBytes || hashes.Distinct().Count()!=hashes.Count)
            return new(400,Message:"유지할 사진은 현재 단계 사진이어야 하며 중복 없이 전체 15MiB 이하여야 합니다.");
        var photoIds=new List<Guid>(retained);var order=retained.Length;
        foreach(var p in input.Photos)
        {
            var photoId=Guid.NewGuid();photoIds.Add(photoId);
            await using var insert=c.CreateCommand();insert.Transaction=tx;
            insert.CommandText="""
                insert into osan_photo_revision_files(id,request_id,display_order,original_file_name,normalized_mime,sha256,content)
                values(@id,@request,@ordering,@name,@mime,@hash,@content);
                """;
            insert.Parameters.AddWithValue("id",photoId);insert.Parameters.AddWithValue("request",requestId);
            insert.Parameters.AddWithValue("ordering",++order);insert.Parameters.AddWithValue("name",p.FileName);
            insert.Parameters.AddWithValue("mime",p.NormalizedMime);insert.Parameters.AddWithValue("hash",p.Sha256);
            insert.Parameters.AddWithValue("content",p.Content);await insert.ExecuteNonQueryAsync(ct);
        }
        cmd.Parameters.AddWithValue("fingerprint",fingerprint);
        cmd.CommandText="update osan_photo_edit_requests set used_at=now(),fingerprint=@fingerprint,saved_by=@actor where id=@id";
        await cmd.ExecuteNonQueryAsync(ct);
        await OsanStageRecords.AddAsync(c,tx,project,step,requestId,"Edit",actor,input.Comment,null,photoIds.ToArray(),ct);
        await OsanStageRecords.RecalculateAsync(c,tx,project,input.Targets[0]!.TargetId,ct);
        await OsanStageRecords.NotifyAsync(c,tx,project,step,requestId,actor,Emi.Qms.Api.Notifications.OsanNotificationKind.StepEdited,input.Comment,photoIds.Count,ct);
        await tx.CommitAsync(ct);
        return new(200,new{saved=true,replayed=false});
    }
}

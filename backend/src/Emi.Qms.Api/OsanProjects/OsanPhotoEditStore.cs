using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed class OsanPhotoEditStore(OsanDatabase db)
{
    private RuntimeDataSourceLease Source() => db.RentDataSource(db.GetConnectionString()
        ?? throw new InvalidOperationException("QMS database connection string is not configured."));

    public async Task<OsanManagementResult> SaveAsync(Guid project, Guid step, CompleteOsanProgressInput input,
        Guid actor, CancellationToken ct, bool isAdministrator=false)
    {
        var reason=input.Reason?.Trim();
        var retained=input.RetainedPhotoIds?.ToArray()??[];
        if(input.OperationId==Guid.Empty || input.StageSequence is <1 or >7 || input.Targets.Count!=1
            || input.Targets[0] is null || string.IsNullOrWhiteSpace(reason) || reason.Length>1000
            || retained.Distinct().Count()!=retained.Length
            || OsanStageRecords.ValidateContent(input,isAdministrator,retained.Length).Count>0
            || input.Photos.Sum(p=>(long)p.Content.Length)>OsanProgressPhotoValidator.MaximumTotalBytes)
            return new(400,Message:"사진·코멘트와 수정 사유(1~1000자)를 확인해 주세요.");
        var fingerprint=OsanStageRecords.Fingerprint(new { project,step,actor,input.StageSequence,input.Comment,reason,
            target=input.Targets[0],retained,photos=input.Photos.Select(p=>new{p.FileName,p.Sha256}) });
        await using var source=Source();await using var c=await source.OpenConnectionAsync(ct);
        await using var tx=await c.BeginTransactionAsync(ct);
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.Parameters.AddWithValue("project",project);
        cmd.CommandText="select id from projects where id=@project and deleted_at_utc is null for update";
        if(await cmd.ExecuteScalarAsync(ct) is null)return new(404);
        if(!await OsanPolicyStore.CanCompleteAsync(c,tx,actor,input.StageSequence,isAdministrator,ct))
            return new(403,Message:"해당 Gate 진행 권한이 없습니다.");
        cmd.CommandText="select status='Completed' and delivery_date < (now() at time zone 'Asia/Seoul')::date from projects where id=@project";
        if(!isAdministrator && await cmd.ExecuteScalarAsync(ct) is true)
            return new(403,Message:"납품 완료 프로젝트는 관리자만 변경할 수 있습니다.");
        cmd.Parameters.AddWithValue("operation",input.OperationId);
        cmd.CommandText="select fingerprint from osan_stage_records where operation_id=@operation";
        var old=await cmd.ExecuteScalarAsync(ct);
        if(old is not null)return old.ToString()==fingerprint
            ?new(200,new{saved=true,replayed=true}):new(409,Message:"같은 요청 식별자가 다른 처리에 사용되었습니다.");
        cmd.Parameters.AddWithValue("target",input.Targets[0]!.TargetId);
        cmd.Parameters.AddWithValue("stage",input.StageSequence);
        cmd.Parameters.AddWithValue("stepId",step);
        cmd.CommandText="""
            select s.status,s.rejected,t.version,
                exists(select 1 from osan_stage_issues i where i.step_id=s.id and i.status='Open')
            from osan_active_project_target_steps s join osan_active_project_targets t on t.id=s.target_id
            where s.project_id=@project and s.id=@stepId and s.target_id=@target and s.sequence_number=@stage;
            """;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))return new(404);
            if(reader.GetInt32(2)!=input.Targets[0]!.ExpectedVersion)
                return new(409,Message:"진행 상태가 변경되었습니다. 다시 조회해 주세요.");
            if(reader.GetBoolean(3))return new(409,Message:"미해결 공정 이상은 조치 완료로 처리해 주세요.");
            if(reader.GetString(0)!="Completed" && !reader.GetBoolean(1))
                return new(409,Message:"완료되었거나 반려된 단계만 수정할 수 있습니다.");
        }
        cmd.Parameters.AddWithValue("step",step);cmd.Parameters.AddWithValue("retained",retained);
        cmd.CommandText="select id,byte_size,sha256 from osan_current_progress_photos where project_id=@project and step_id=@step and id=any(@retained)";
        var hashes=input.Photos.Select(p=>p.Sha256).ToList();long bytes=input.Photos.Sum(p=>(long)p.Content.Length);var found=0;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)){found++;bytes+=reader.GetInt32(1);hashes.Add(reader.GetString(2));}
        if(found!=retained.Length || bytes>OsanProgressPhotoValidator.MaximumTotalBytes || hashes.Distinct().Count()!=hashes.Count)
            return new(400,Message:"유지할 사진은 현재 단계 사진이어야 하며 중복 없이 전체 40MiB 이하여야 합니다.");
        var photoIds=new List<Guid>(retained);var order=retained.Length;
        foreach(var p in input.Photos)
        {
            var photoId=Guid.NewGuid();photoIds.Add(photoId);
            await using var insert=c.CreateCommand();insert.Transaction=tx;
            insert.CommandText="""
                insert into osan_direct_edit_files(id,project_id,operation_id,uploaded_by_user_id,display_order,original_file_name,normalized_mime,sha256,content)
                values(@id,@project,@operation,@actor,@ordering,@name,@mime,@hash,@content);
                """;
            insert.Parameters.AddWithValue("id",photoId);insert.Parameters.AddWithValue("project",project); insert.Parameters.AddWithValue("operation",input.OperationId); insert.Parameters.AddWithValue("actor",actor);
            insert.Parameters.AddWithValue("ordering",++order);insert.Parameters.AddWithValue("name",p.FileName);
            insert.Parameters.AddWithValue("mime",p.NormalizedMime);insert.Parameters.AddWithValue("hash",p.Sha256);
            insert.Parameters.AddWithValue("content",p.Content);await insert.ExecuteNonQueryAsync(ct);
        }
        await OsanStageRecords.AddAsync(c,tx,project,step,input.OperationId,"Edit",actor,input.Comment,reason,photoIds.ToArray(),ct,fingerprint);
        await OsanStageRecords.RecalculateAsync(c,tx,project,input.Targets[0]!.TargetId,ct);
        await OsanStageRecords.NotifyAsync(c,tx,project,step,input.OperationId,actor,Emi.Qms.Api.Notifications.OsanNotificationKind.StepEdited,input.Comment,photoIds.Count,ct);
        await tx.CommitAsync(ct);
        return new(200,new{saved=true,replayed=false});
    }
}

using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed partial class OsanProgressStore
{
    public async Task<IReadOnlyList<OsanStageHistoryItem>?> HistoryAsync(Guid project,Guid step,CancellationToken ct)
    {
        await using var source=CreateDataSource();await using var c=await source.OpenConnectionAsync(ct);
        await using var cmd=c.CreateCommand();
        cmd.CommandText="select 1 from osan_active_project_target_steps where project_id=@project and id=@step";
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("step",step);
        if(await cmd.ExecuteScalarAsync(ct) is null)return null;
        cmd.CommandText="""
            select r.id,r.event_type,u.display_name,r.occurred_at_utc,r.comment,r.reason,r.photo_ids
            from osan_stage_records r join qms_users u on u.id=r.actor_user_id
            where r.project_id=@project and r.step_id=@step order by r.occurred_at_utc desc,r.id desc;
            """;
        var records=new List<(Guid Id,string Type,string Actor,DateTimeOffset Time,string Comment,string? Reason,Guid[] Photos)>();
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) records.Add((reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetFieldValue<DateTimeOffset>(3),reader.GetString(4),reader.IsDBNull(5)?null:reader.GetString(5),reader.GetFieldValue<Guid[]>(6)));
        var result=new List<OsanStageHistoryItem>();
        foreach(var r in records)
        {
            var photos=new List<OsanProgressPhotoResponse>();
            cmd.CommandText="""
                select p.id,ids.ordinality::integer,p.original_file_name,p.normalized_mime,p.byte_size,p.sha256,p.uploaded_at_utc,p.uploaded_by_user_id,u.display_name
                from unnest(@photos) with ordinality ids(id,ordinality)
                join osan_all_progress_photos p on p.id=ids.id and p.project_id=@project join qms_users u on u.id=p.uploaded_by_user_id order by ids.ordinality;
                """;
            if(cmd.Parameters.Contains("photos"))cmd.Parameters.Remove("photos");cmd.Parameters.AddWithValue("photos",r.Photos);
            await using(var reader=await cmd.ExecuteReaderAsync(ct))
                while(await reader.ReadAsync(ct))photos.Add(new(reader.GetGuid(0),reader.GetInt32(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetString(5),reader.GetFieldValue<DateTimeOffset>(6),reader.GetGuid(7),reader.GetString(8)));
            result.Add(new(r.Id,r.Type,r.Actor,r.Time,r.Comment,r.Reason,photos));
        }
        return result;
    }

    public async Task<OsanManagementResult> StageActionAsync(Guid project,Guid step,OsanStageActionRequest input,string action,Guid actor,CancellationToken ct)
    {
        if(action is not ("Reject" or "Reset") || input.OperationId==Guid.Empty || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length>1000)
            return new(400,Message:"처리 사유를 1~1000자로 입력해 주세요.");
        await using var source=CreateDataSource();await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        if(await LockProjectAsync(c,tx,project,ct) is null)return new(404);
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("step",step);cmd.Parameters.AddWithValue("operation",input.OperationId);
        var fingerprint=OsanStageRecords.Fingerprint(new{project,step,input.Reason,input.ExpectedVersion,action,actor});
        cmd.CommandText="select fingerprint from osan_stage_records where operation_id=@operation";
        var old=await cmd.ExecuteScalarAsync(ct);
        if(old is not null)return old.ToString()==fingerprint?new(200,new{replayed=true}):new(409,Message:"같은 요청 식별자가 다른 처리에 사용되었습니다.");
        cmd.CommandText="""
            select s.target_id,s.status,t.version,
              exists(select 1 from osan_photo_edit_requests r where r.step_id=s.id and r.used_at is null and r.invalidated_at is null and r.approved_at is not null)
            from osan_active_project_target_steps s join osan_active_project_targets t on t.id=s.target_id
            where s.project_id=@project and s.id=@step;
            """;
        Guid target;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            if(!await reader.ReadAsync(ct))return new(404);
            target=reader.GetGuid(0);
            if(reader.GetInt32(2)!=input.ExpectedVersion)return new(409,Message:"진행 상태가 변경되었습니다. 다시 조회해 주세요.");
            if(action=="Reject" && (reader.GetString(1)!="Completed" || reader.GetBoolean(3)))return new(409,Message:"수정 승인 중이거나 미완료 단계는 반려할 수 없습니다.");
        }
        cmd.CommandText="update osan_photo_edit_requests set invalidated_at=now() where step_id=@step and used_at is null and invalidated_at is null";
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("target",target);cmd.Parameters.AddWithValue("reject",action=="Reject");
        cmd.CommandText="""
            update osan_project_target_steps set status='NotStarted',completed_at_utc=null,completed_by_user_id=null,
              current_record_id=case when @reject then current_record_id else null end,
              comment=case when @reject then comment else '' end,rejected=@reject,updated_at_utc=now() where id=@step;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        if(action=="Reject")
        {
            cmd.CommandText="""
                insert into osan_photo_edit_requests(id,project_id,target_id,step_id,requested_by,approved_by,approved_at)
                values(@operation,@project,@target,@step,@actor,@actor,now());
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await OsanStageRecords.AddAsync(c,tx,project,step,input.OperationId,action,actor,"",input.Reason,[],ct,fingerprint);
        await OsanStageRecords.RecalculateAsync(c,tx,project,target,ct);
        if(action=="Reject")await OsanStageRecords.NotifyAsync(c,tx,project,step,input.OperationId,actor,
            Emi.Qms.Api.Notifications.OsanNotificationKind.StepRejected,input.Reason,0,ct,
            await OsanStageRecords.ReadOverallAdministratorsAsync(connectionStringProvider,ct));
        await tx.CommitAsync(ct);return new(200,new{replayed=false});
    }
}

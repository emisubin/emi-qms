using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Emi.Qms.Api.Notifications;

namespace Emi.Qms.Api.OsanProjects;

internal static class OsanStageRecords
{
    internal static Dictionary<string,string[]> ValidateContent(CompleteOsanProgressInput input, bool admin, int retainedCount = 0)
    {
        var errors = new Dictionary<string,string[]>();
        if (input.Comment is null || input.Comment.Length > 1000) errors["comment"] = ["코멘트는 1000자 이하여야 합니다."];
        if (input.Photos.Count + retainedCount > 5) errors["photos"] = ["사진은 최대 5장까지 등록할 수 있습니다."];
        if (input.Photos.Count + retainedCount == 0 && (!admin || string.IsNullOrWhiteSpace(input.Comment)))
            errors["photos"] = [admin ? "사진이 없으면 코멘트를 입력해 주세요." : "사진을 최소 1장 등록해 주세요."];
        return errors;
    }

    internal static async Task<Guid> AddAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid project, Guid step,
        Guid operation, string kind, Guid actor, string comment, string? reason, Guid[] photos, CancellationToken ct, string fingerprint = "")
    {
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="""
            insert into osan_stage_records(id,operation_id,project_id,target_id,step_id,event_type,actor_user_id,comment,reason,photo_ids,fingerprint)
            select @id,@operation,project_id,target_id,id,@kind,@actor,@comment,@reason,@photos,@fingerprint
            from osan_project_target_steps where id=@step and project_id=@project returning id;
            """;
        var id=Guid.NewGuid();cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("operation",operation);
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("step",step);cmd.Parameters.AddWithValue("kind",kind);
        cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("comment",comment);
        cmd.Parameters.AddWithValue("reason",NpgsqlTypes.NpgsqlDbType.Text,(object?)reason??DBNull.Value);
        cmd.Parameters.AddWithValue("photos",photos);cmd.Parameters.AddWithValue("fingerprint",fingerprint);
        await cmd.ExecuteScalarAsync(ct);
        if(kind is "Complete" or "Edit")
        {
            cmd.CommandText="""
                update osan_project_target_steps set current_record_id=@id,comment=@comment,rejected=false,
                    status='Completed',completed_at_utc=now(),completed_by_user_id=@actor,updated_at_utc=now()
                where id=@step and project_id=@project;
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return id;
    }

    internal static async Task RecalculateAsync(NpgsqlConnection c,NpgsqlTransaction tx,Guid project,Guid target,CancellationToken ct)
    {
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="""
            update osan_project_targets t set version=version+1,updated_at_utc=now(),
              status=case when x.n=7 then 'Completed' when x.n=0 then 'NotStarted' else 'InProgress' end
            from (select count(*) filter(where status='Completed') n from osan_project_target_steps where target_id=@target) x
            where t.id=@target and t.project_id=@project;
            update projects set status=case when not exists(select 1 from osan_active_project_target_steps where project_id=@project and status<>'Completed')
              then 'Completed' else 'Active' end,updated_at_utc=now() where id=@project;
            """;
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("target",target);await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task NotifyAsync(NpgsqlConnection c,NpgsqlTransaction tx,Guid project,Guid step,Guid operation,Guid actor,
        OsanNotificationKind kind,string comment,int photoCount,CancellationToken ct,IReadOnlyList<Guid>? overallAdministrators=null)
    {
        await using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="select target_id,step_name from osan_project_target_steps where project_id=@project and id=@step";
        cmd.Parameters.AddWithValue("project",project);cmd.Parameters.AddWithValue("step",step);
        Guid target;string name;
        await using(var reader=await cmd.ExecuteReaderAsync(ct)){await reader.ReadAsync(ct);target=reader.GetGuid(0);name=reader.GetString(1);}
        List<Guid>? recipients=null;
        if(kind==OsanNotificationKind.StepRejected)
        {
            recipients=overallAdministrators?.ToList()??[];
            cmd.CommandText="select distinct actor_user_id from osan_stage_records where project_id=@project and step_id=@step and event_type in ('Complete','Edit')";
            await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))recipients.Add(reader.GetGuid(0));
        }
        await OsanNotificationWriter.WriteAsync(c,tx,project,operation,kind,actor,DateTimeOffset.UtcNow,ct,name,[target],comment,photoCount,recipients);
        cmd.CommandText="select status from projects where id=@project";
        if(kind==OsanNotificationKind.StepEdited && (string?)await cmd.ExecuteScalarAsync(ct)=="Completed")
            await OsanNotificationWriter.WriteAsync(c,tx,project,operation,OsanNotificationKind.ProjectCompleted,actor,DateTimeOffset.UtcNow,ct);
    }

    internal static async Task<IReadOnlyList<Guid>> ReadOverallAdministratorsAsync(DatabaseConnectionStringProvider provider,CancellationToken ct)
    {
        if(!provider.BusinessUnits.Enabled || provider.BusinessUnits.Directory is not { } directory)return [];
        await using var source=NpgsqlDataSource.Create(provider.GetConnectionString(directory));
        await using var cmd=source.CreateCommand("select distinct a.user_id from directory_overall_administrators a join directory_identities i on i.user_id=a.user_id where a.is_active and i.is_active");
        var result=new List<Guid>();await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))result.Add(reader.GetGuid(0));return result;
    }

    internal static string Fingerprint(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

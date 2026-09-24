using Emi.Qms.Api.BusinessUnits;
using Npgsql;

namespace Emi.Qms.Api.DeploymentMaintenance;

public sealed record DeploymentMaintenanceStatus(Guid? ReleaseId,int Version,int PopupVersion,string? Title,string? Body,
    DateTimeOffset? StartsAtUtc,DateTimeOffset? ExpectedEndsAtUtc,string State,bool WriteBlocked,
    Guid? NoticeId,bool PopupPending);
public sealed record DeploymentMaintenanceCommandResult(int Status,string? ErrorCode = null,
    string? Message = null,DeploymentMaintenanceStatus? Value = null);
public sealed record PrepareDeploymentMaintenance(Guid ReleaseId,Guid ActorUserId,string Title,string Body,
    DateTimeOffset StartsAtUtc,DateTimeOffset ExpectedEndsAtUtc);

public sealed class DeploymentMaintenanceStore
{
    private readonly DatabaseConnectionStringProvider provider;
    private readonly BusinessUnitDatabaseTarget? target;
    public DeploymentMaintenanceStore(DatabaseConnectionStringProvider provider) : this(provider,null) { }
    private DeploymentMaintenanceStore(DatabaseConnectionStringProvider provider,BusinessUnitDatabaseTarget? target)
    { this.provider=provider;this.target=target; }
    public static DeploymentMaintenanceStore ForTarget(DatabaseConnectionStringProvider provider,
        BusinessUnitDatabaseTarget target)=>new(provider,target);
    private string ConnectionString => target is null
        ? provider.GetConnectionString() ?? throw new InvalidOperationException("QMS database is not configured.")
        : provider.GetConnectionString(target);

    public async Task<DeploymentMaintenanceStatus> ReadAsync(Guid? actor,CancellationToken ct)
    {
        await using var connection=new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        return await ReadAsync(connection,actor,ct);
    }

    private static async Task<DeploymentMaintenanceStatus> ReadAsync(NpgsqlConnection connection,
        Guid? actor,CancellationToken ct)
    {
        await using var command=connection.CreateCommand();
        command.CommandText="""
            select m.release_id,m.version,m.popup_version,m.title,m.body,m.starts_at_utc,m.expected_ends_at_utc,
                m.state,m.notice_id,
                m.release_id is not null and m.state <> 'Completed' and @actor is not null and not exists(
                    select 1 from deployment_maintenance_popup_receipts r
                    where r.release_id=m.release_id and r.version=m.popup_version and r.user_id=@actor)
            from deployment_maintenance m where m.id=1
            """;
        command.Parameters.AddWithValue("actor",(object?)actor??DBNull.Value);
        await using var reader=await command.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Maintenance state row is missing.");
        var state=reader.GetString(7);
        return new(reader.IsDBNull(0)?null:reader.GetGuid(0),reader.GetInt32(1),reader.GetInt32(2),
            reader.IsDBNull(3)?null:reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),
            reader.IsDBNull(5)?null:reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6)?null:reader.GetFieldValue<DateTimeOffset>(6),state,
            state is "Active" or "Delayed" or "Failed",
            reader.IsDBNull(8)?null:reader.GetGuid(8),reader.GetBoolean(9));
    }

    public async Task<bool> ClaimPopupAsync(Guid releaseId,int version,Guid actor,CancellationToken ct)
    {
        if(releaseId==Guid.Empty || version<1)return false;
        await using var connection=new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var command=connection.CreateCommand();
        command.CommandText="""
            insert into deployment_maintenance_popup_receipts(release_id,version,user_id)
            select m.release_id,m.popup_version,@actor from deployment_maintenance m
            join qms_users u on u.id=@actor and u.is_active=true
            where m.id=1 and m.release_id=@release and m.popup_version=@version and m.state <> 'Completed'
            on conflict do nothing
            """;
        command.Parameters.AddWithValue("release",releaseId);
        command.Parameters.AddWithValue("version",version);
        command.Parameters.AddWithValue("actor",actor);
        return await command.ExecuteNonQueryAsync(ct)>0;
    }

    public async Task<DeploymentMaintenanceCommandResult> PrepareAsync(PrepareDeploymentMaintenance input,CancellationToken ct)
    {
        input=input with { StartsAtUtc=DatabasePrecision(input.StartsAtUtc),
            ExpectedEndsAtUtc=DatabasePrecision(input.ExpectedEndsAtUtc) };
        var title=input.Title?.Trim();var body=input.Body?.Trim();
        if(input.ReleaseId==Guid.Empty || input.ActorUserId==Guid.Empty || string.IsNullOrEmpty(title)
            || title.Length>100 || string.IsNullOrEmpty(body) || body.Length>1800
            || input.ExpectedEndsAtUtc<=input.StartsAtUtc || input.ExpectedEndsAtUtc<=DateTimeOffset.UtcNow)
            return new(400,"release_maintenance_input_invalid","배포 식별자, 안내 내용과 예정 시간을 확인해 주세요.");
        await using var connection=new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var tx=await connection.BeginTransactionAsync(ct);
        await using var command=connection.CreateCommand();command.Transaction=tx;
        command.CommandText="select state,release_id from deployment_maintenance where id=1 for update";
        string state;Guid? currentRelease;
        await using(var reader=await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);state=reader.GetString(0);
            currentRelease=reader.IsDBNull(1)?null:reader.GetGuid(1);
        }
        if(currentRelease==input.ReleaseId)return new(409,"release_maintenance_duplicate","이미 등록된 배포 식별자입니다.");
        if(state is not ("Idle" or "Completed"))
            return new(409,"release_maintenance_in_progress","이전 배포 상태를 먼저 완료해 주세요.");
        command.Parameters.AddWithValue("release",input.ReleaseId);
        command.Parameters.AddWithValue("actor",input.ActorUserId);
        command.Parameters.AddWithValue("title",title);
        command.Parameters.AddWithValue("body",body);
        command.Parameters.AddWithValue("notice_body",body);
        command.Parameters.AddWithValue("start",input.StartsAtUtc);
        command.Parameters.AddWithValue("end",input.ExpectedEndsAtUtc);
        command.CommandText="""
            insert into notice_posts(title,body,body_format,author_user_id,author_display_name_snapshot,
                author_department_name_snapshot,request_id,popup_enabled,popup_version)
            select @title,@notice_body,'PlainTextV1',u.id,u.display_name,d.name,@release,false,0
            from qms_users u left join departments d on d.id=u.department_id
            where u.id=@actor and u.is_active=true
            on conflict(author_user_id,request_id) do nothing returning id
            """;
        var notice=await command.ExecuteScalarAsync(ct);
        if(notice is not Guid noticeId)
            return new(409,"release_maintenance_notice_conflict","공지 작성자 또는 배포 식별자를 확인해 주세요.");
        command.Parameters.AddWithValue("notice",noticeId);
        command.CommandText="""
            update deployment_maintenance set release_id=@release,version=1,popup_version=1,state='Announced',
                title=@title,body=@body,starts_at_utc=@start,expected_ends_at_utc=@end,
                notice_id=@notice,updated_at_utc=now() where id=1
            """;
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new(200,Value:await ReadAsync(input.ActorUserId,ct));
    }

    public async Task<DeploymentMaintenanceCommandResult> TransitionAsync(Guid releaseId,int expectedVersion,
        string action,DateTimeOffset? revisedEnd,bool verified,Guid actor,CancellationToken ct)
    {
        if(revisedEnd is { } end) revisedEnd=DatabasePrecision(end);
        if(releaseId==Guid.Empty || expectedVersion<1 || actor==Guid.Empty)
            return new(400,"release_maintenance_input_invalid","배포 식별자와 버전을 확인해 주세요.");
        await using var connection=new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        // Exclusive lock waits for all API and provider worker leases to finish.
        await DeploymentMaintenanceLease.AcquireExclusiveAsync(connection,ct);
        try
        {
            await using var tx=await connection.BeginTransactionAsync(ct);
            await using var command=connection.CreateCommand();command.Transaction=tx;
            command.CommandText="select release_id,version,state,body,notice_id,expected_ends_at_utc,starts_at_utc from deployment_maintenance where id=1 for update";
            Guid? currentId;int currentVersion;string currentState;string? body;Guid? noticeId;DateTimeOffset? currentEnd;DateTimeOffset? currentStart;
            await using(var reader=await command.ExecuteReaderAsync(ct))
            {
                await reader.ReadAsync(ct);
                currentId=reader.IsDBNull(0)?null:reader.GetGuid(0);currentVersion=reader.GetInt32(1);
                currentState=reader.GetString(2);body=reader.IsDBNull(3)?null:reader.GetString(3);
                noticeId=reader.IsDBNull(4)?null:reader.GetGuid(4);
                currentEnd=reader.IsDBNull(5)?null:reader.GetFieldValue<DateTimeOffset>(5);
                currentStart=reader.IsDBNull(6)?null:reader.GetFieldValue<DateTimeOffset>(6);
            }
            if(currentId!=releaseId || currentVersion!=expectedVersion)
                return new(409,"release_maintenance_stale","배포 상태가 변경되었습니다. 다시 조회해 주세요.");
            if(action=="delay" && (revisedEnd is null || revisedEnd<=currentStart || revisedEnd<=DateTimeOffset.UtcNow))
                return new(400,"release_maintenance_expected_end_invalid","변경된 종료 예정 시간은 시작과 현재 시간보다 뒤여야 합니다.");
            string nextState;
            switch(action)
            {
                case "activate" when currentState=="Announced": nextState="Active";break;
                case "delay" when currentState is "Active" or "Delayed" && revisedEnd is not null:
                    nextState="Delayed";break;
                case "fail" when currentState is "Active" or "Delayed" or "Completed": nextState="Failed";break;
                case "complete" when currentState is "Active" or "Delayed" or "Failed" && verified:
                    nextState="Completed";break;
                default:return new(409,"release_maintenance_transition_invalid","배포 상태와 완료 확인을 다시 확인해 주세요.");
            }
            command.Parameters.AddWithValue("state",nextState);
            command.Parameters.AddWithValue("version",expectedVersion);
            command.Parameters.AddWithValue("end",(object?)revisedEnd??DBNull.Value);
            command.Parameters.AddWithValue("popup_bump",action=="delay" && revisedEnd!=currentEnd ? 1 : 0);
            command.CommandText="""
                update deployment_maintenance set state=@state,version=version+1,
                    popup_version=popup_version+@popup_bump,
                    expected_ends_at_utc=coalesce(@end,expected_ends_at_utc),updated_at_utc=now()
                where id=1 and version=@version
                """;
            await command.ExecuteNonQueryAsync(ct);
            if(noticeId is Guid id)
            {
                command.Parameters.AddWithValue("notice",id);
                command.Parameters.AddWithValue("actor",actor);
                command.Parameters.AddWithValue("notice_body",body!);
                command.CommandText="""
                    insert into notice_post_revisions(notice_post_id,version,title,body,body_format,changed_by_user_id)
                    select id,version,title,body,body_format,@actor from notice_posts where id=@notice;
                    update notice_posts set body=@notice_body,updated_at_utc=now(),
                        updated_by_user_id=@actor,version=version+1 where id=@notice;
                    """;
                await command.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return new(200,Value:await ReadAsync(actor,ct));
        }
        finally { await DeploymentMaintenanceLease.ReleaseExclusiveAsync(connection); }
    }

    // PostgreSQL timestamps and Npgsql store microseconds, not .NET's 100ns ticks.
    private static DateTimeOffset DatabasePrecision(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}

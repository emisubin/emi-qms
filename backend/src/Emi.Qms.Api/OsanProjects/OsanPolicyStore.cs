using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed record OsanCustomer(Guid CustomerId, string Name, long Version);
public sealed record OsanCustomerUser(Guid UserId, string DisplayName, string? DepartmentName,
    long Version, IReadOnlyList<Guid> CustomerIds);
public sealed record OsanGateDepartment(Guid DepartmentId, string Name);
public sealed record OsanGate(int StageSequence, string Name, IReadOnlyList<Guid> DepartmentIds);
public sealed record OsanGateConfiguration(long Version, IReadOnlyList<OsanGateDepartment> Departments,
    IReadOnlyList<OsanGate> Gates);
public sealed record OsanGateUpdate(int StageSequence, IReadOnlyList<Guid> DepartmentIds);
public sealed record OsanPendingGateApproval(Guid ProjectId, string ProjectCode, string ProjectTitle,
    Guid RequestId, Guid TargetId, int StageSequence, string RequestedByName, DateTimeOffset RequestedAt);
public sealed record OsanPolicyWriteResult(int Status, string? Code = null, string? Message = null, object? Value = null);

public sealed class OsanPolicyStore(DatabaseConnectionStringProvider db)
{
    private NpgsqlDataSource Source() => NpgsqlDataSource.Create(db.GetConnectionString()
        ?? throw new InvalidOperationException("QMS database connection string is not configured."));

    public async Task<IReadOnlyList<OsanCustomer>> CustomersAsync(string? query, CancellationToken ct)
    {
        await using var source = Source();
        await using var command = source.CreateCommand("select id,name,version from osan_customers where archived_at_utc is null order by name,id");
        var rows = new List<OsanCustomer>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new OsanCustomer(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2));
            if (string.IsNullOrWhiteSpace(query) || CustomerMatches(query, row.Name)) rows.Add(row);
        }
        return rows;
    }

    public static bool CustomerMatches(string input, string officialName)
    {
        static string Key(string value) => new(value.Where(ch => !char.IsWhiteSpace(ch))
            .Select(char.ToUpperInvariant).ToArray());
        var typed = Key(input.Trim());
        return typed.Length > 0 && Key(officialName).Contains(typed, StringComparison.Ordinal);
    }

    public static async Task<OsanCustomer?> BoundCustomerAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid? customerId, string enteredName, CancellationToken ct)
    {
        if (customerId is null || customerId == Guid.Empty) return null;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id,name,version from osan_customers where id=@id and archived_at_utc is null for share";
        command.Parameters.AddWithValue("id", customerId.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var customer = new OsanCustomer(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2));
        return CustomerMatches(enteredName, customer.Name) ? customer : null;
    }

    public async Task<OsanPolicyWriteResult> CreateCustomerAsync(string? name, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
            return new(400, "osan_customer_name_invalid", "고객사 이름은 1~200자로 입력해 주세요.");
        await using var source = Source();
        await using var command = source.CreateCommand("""
            insert into osan_customers(name) values(@name)
            on conflict(name) do nothing returning id,name,version
            """);
        command.Parameters.AddWithValue("name", name);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(201, Value: new OsanCustomer(reader.GetGuid(0),reader.GetString(1),reader.GetInt64(2)))
            : new(409,"osan_customer_duplicate","이미 등록된 고객사입니다.");
    }

    public async Task<OsanPolicyWriteResult> RenameCustomerAsync(Guid id, string? name, long expectedVersion, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || expectedVersion < 1)
            return new(400,"osan_customer_input_invalid","고객사 이름과 버전을 확인해 주세요.");
        await using var source = Source();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id from projects where project_profile='Osan' and osan_customer_id=@id order by id for update";
        command.Parameters.AddWithValue("id",id);
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) { }
        command.CommandText = """
            update osan_customers set name=@name,version=version+1,updated_at_utc=now()
            where id=@id and version=@version and archived_at_utc is null returning id,name,version
            """;
        command.Parameters.AddWithValue("name",name);
        command.Parameters.AddWithValue("version",expectedVersion);
        try
        {
            OsanCustomer? customer;
            await using (var reader = await command.ExecuteReaderAsync(ct))
                customer = await reader.ReadAsync(ct)
                    ? new(reader.GetGuid(0),reader.GetString(1),reader.GetInt64(2)) : null;
            if (customer is null) return new(409,"osan_customer_stale","고객사 정보가 변경되었습니다. 다시 조회해 주세요.");
            command.CommandText = "update projects set customer_name=@name where project_profile='Osan' and osan_customer_id=@id";
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return new(200,Value:customer);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new(409,"osan_customer_duplicate","이미 등록된 고객사입니다.");
        }
    }

    public async Task<OsanPolicyWriteResult> ArchiveCustomerAsync(Guid id, long expectedVersion, CancellationToken ct)
    {
        if (expectedVersion < 1)
            return new(400,"osan_customer_input_invalid","고객사 버전을 확인해 주세요.");
        await using var source = Source();
        // A single conditional update serializes against bindings holding FOR SHARE and
        // competing rename/archive requests without changing existing project/assignment rows.
        await using var command = source.CreateCommand("""
            update osan_customers
            set archived_at_utc=now(),updated_at_utc=now(),version=version+1
            where id=@id and version=@version and archived_at_utc is null
            returning id,name,version
            """);
        command.Parameters.AddWithValue("id",id);
        command.Parameters.AddWithValue("version",expectedVersion);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(200,Value:new OsanCustomer(reader.GetGuid(0),reader.GetString(1),reader.GetInt64(2)))
            : new(409,"osan_customer_stale","고객사 정보가 변경되었습니다. 다시 조회해 주세요.");
    }

    public async Task<IReadOnlyList<OsanCustomerUser>> AssignmentUsersAsync(CancellationToken ct)
    {
        await using var source = Source();
        await using var command = source.CreateCommand("""
            select u.id,u.display_name,d.name,v.version,
              coalesce(array_agg(a.customer_id order by a.customer_id)
                filter(where c.id is not null),array[]::uuid[])
            from qms_users u left join departments d on d.id=u.department_id
            join osan_customer_assignment_versions v on v.user_id=u.id
            left join osan_customer_assignments a on a.user_id=u.id
            left join osan_customers c on c.id=a.customer_id and c.archived_at_utc is null
            where u.is_active=true
            group by u.id,u.display_name,d.name,v.version
            order by u.display_name,u.id
            """);
        var rows = new List<OsanCustomerUser>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0),reader.GetString(1),
            reader.IsDBNull(2)?null:reader.GetString(2),reader.GetInt64(3),reader.GetFieldValue<Guid[]>(4)));
        return rows;
    }

    public async Task<OsanPolicyWriteResult> AssignAsync(Guid userId, IReadOnlyList<Guid>? ids,
        long expectedVersion, CancellationToken ct)
    {
        if (ids is null || ids.Contains(Guid.Empty) || expectedVersion < 1)
            return new(400,"osan_assignment_invalid","고객사 선택과 버전을 확인해 주세요.");
        var distinct = ids.Distinct().ToArray();
        await using var source = Source();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("user",userId);
        command.Parameters.AddWithValue("version",expectedVersion);
        command.Parameters.AddWithValue("ids",distinct);
        command.CommandText = """
            select v.version from osan_customer_assignment_versions v
            join qms_users u on u.id=v.user_id and u.is_active=true
            where v.user_id=@user for update of v
            """;
        var version = await command.ExecuteScalarAsync(ct);
        if (version is null) return new(404,"osan_user_not_found","사용자를 찾을 수 없습니다.");
        if ((long)version != expectedVersion) return new(409,"osan_assignment_stale","담당 고객사 설정이 변경되었습니다. 다시 조회해 주세요.");
        // Lock every currently active customer, in stable order, through assignment replacement.
        // This also keeps an archive from changing which old assignments are preserved midway.
        command.CommandText = "select id from osan_customers where archived_at_utc is null order by id for share";
        var activeIds = new HashSet<Guid>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) activeIds.Add(reader.GetGuid(0));
        if (distinct.Any(id => !activeIds.Contains(id)))
            return new(400,"osan_customer_not_found","등록된 고객사만 선택할 수 있습니다.");
        command.CommandText = """
            delete from osan_customer_assignments a using osan_customers c
            where a.user_id=@user and c.id=a.customer_id and c.archived_at_utc is null
            """;
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "insert into osan_customer_assignments(user_id,customer_id) select @user,unnest(@ids)";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "update osan_customer_assignment_versions set version=version+1 where user_id=@user returning version";
        var nextVersion = (long)(await command.ExecuteScalarAsync(ct))!;
        await transaction.CommitAsync(ct);
        return new(200,Value:new {userId,version=nextVersion,customerIds=distinct});
    }

    private static readonly string[] StageNames = ["입고검사","배치검사","배선검사","8계통","동작검사","출하검사","포장"];

    public async Task<OsanGateConfiguration> GatesAsync(CancellationToken ct)
    {
        await using var source = Source();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText="select version from osan_gate_configuration where id=1";
        var version=(long)(await versionCommand.ExecuteScalarAsync(ct))!;
        await using var command=connection.CreateCommand();
        command.CommandText="select id,name from departments order by name,id";
        var departments=new List<OsanGateDepartment>();
        await using(var reader=await command.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) departments.Add(new(reader.GetGuid(0),reader.GetString(1)));
        command.CommandText="select stage_sequence,department_id from osan_gate_departments order by stage_sequence,department_id";
        var mapped=Enumerable.Range(1,7).ToDictionary(n=>n,_=>new List<Guid>());
        await using(var reader=await command.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) mapped[reader.GetInt16(0)].Add(reader.GetGuid(1));
        return new(version,departments,Enumerable.Range(1,7)
            .Select(n=>new OsanGate(n,StageNames[n-1],mapped[n])).ToArray());
    }

    public async Task<OsanPolicyWriteResult> SetGatesAsync(IReadOnlyList<OsanGateUpdate>? gates,
        long expectedVersion, CancellationToken ct)
    {
        if(gates is null || gates.Count!=7 || gates.Select(g=>g.StageSequence).Order().Where((n,i)=>n!=i+1).Any()
            || gates.Any(g=>g.DepartmentIds is null || g.DepartmentIds.Contains(Guid.Empty)) || expectedVersion<1)
            return new(400,"osan_gate_invalid","7개 Gate와 부서를 확인해 주세요.");
        var ids=gates.SelectMany(g=>g.DepartmentIds).Distinct().ToArray();
        await using var source=Source();
        await using var connection=await source.OpenConnectionAsync(ct);
        await using var transaction=await connection.BeginTransactionAsync(ct);
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.Parameters.AddWithValue("version",expectedVersion);
        command.Parameters.AddWithValue("ids",ids);
        command.CommandText="select version from osan_gate_configuration where id=1 for update";
        if((long)(await command.ExecuteScalarAsync(ct))! != expectedVersion)
            return new(409,"osan_gate_stale","Gate 설정이 변경되었습니다. 다시 조회해 주세요.");
        command.CommandText="select count(*) from departments where id=any(@ids)";
        if((long)(await command.ExecuteScalarAsync(ct))! != ids.Length)
            return new(400,"osan_department_not_found","등록된 부서만 선택할 수 있습니다.");
        command.CommandText="delete from osan_gate_departments";
        await command.ExecuteNonQueryAsync(ct);
        foreach(var gate in gates)
        {
            command.Parameters.AddWithValue("stage",gate.StageSequence);
            command.Parameters["ids"].Value=gate.DepartmentIds.Distinct().ToArray();
            command.CommandText="insert into osan_gate_departments(stage_sequence,department_id) select @stage,unnest(@ids)";
            await command.ExecuteNonQueryAsync(ct);
            command.Parameters.Remove("stage");
        }
        command.CommandText="update osan_gate_configuration set version=version+1,updated_at_utc=now() where id=1 returning version";
        var next=(long)(await command.ExecuteScalarAsync(ct))!;
        await transaction.CommitAsync(ct);
        return new(200,Value:new {version=next});
    }

    public static async Task<bool> CanCompleteAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,
        Guid actor,int stage,bool admin,CancellationToken ct)
    {
        if(admin) return true;
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select version from osan_gate_configuration where id=1 for share";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText="""
            select 1 from qms_users u join osan_gate_departments g on g.department_id=u.department_id
            where u.id=@actor and u.is_active=true and g.stage_sequence=@stage
            """;
        command.Parameters.AddWithValue("actor",actor);
        command.Parameters.AddWithValue("stage",stage);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<IReadOnlySet<int>> AllowedGateStagesAsync(Guid userId,bool admin,CancellationToken ct)
    {
        if(admin) return Enumerable.Range(1,7).ToHashSet();
        await using var source=Source();
        await using var command=source.CreateCommand("""
            select g.stage_sequence from qms_users u
            join osan_gate_departments g on g.department_id=u.department_id
            where u.id=@user and u.is_active=true
            """);
        command.Parameters.AddWithValue("user",userId);
        var stages=new HashSet<int>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))stages.Add(reader.GetInt16(0));
        return stages;
    }

    public async Task<IReadOnlyList<OsanPendingGateApproval>> PendingApprovalsAsync(CancellationToken ct)
    {
        await using var source=Source();
        await using var command=source.CreateCommand("""
            select p.id,p.project_code,p.project_title,r.id,r.target_id,s.sequence_number,
              u.display_name,r.requested_at
            from osan_photo_edit_requests r join projects p on p.id=r.project_id
            join osan_project_target_steps s on s.id=r.step_id
            join qms_users u on u.id=r.requested_by
            where p.project_profile='Osan' and p.deleted_at_utc is null
              and r.approved_at is null and r.used_at is null and r.invalidated_at is null
              and not exists(select 1 from osan_stage_issues i where i.step_id=r.step_id and i.status='Open')
            order by r.requested_at,r.id
            """);
        var rows=new List<OsanPendingGateApproval>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),
            reader.GetGuid(3),reader.GetGuid(4),reader.GetInt32(5),reader.GetString(6),reader.GetFieldValue<DateTimeOffset>(7)));
        return rows;
    }
}

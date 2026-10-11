using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Npgsql;
using NpgsqlTypes;
namespace Emi.Qms.Api.G2;

public sealed class G2MaterialStore(CheongjuDatabase database, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private RuntimeDataSourceLease Source() => database.RentDataSource(database.GetConnectionString() ?? throw new InvalidOperationException("Database is not configured."));
    public async Task<bool> CanEditAsync(Guid actor, CancellationToken ct)
    {
        await using var source = Source(); await using var connection = await source.OpenConnectionAsync(ct);
        return await CanEdit(connection, null, actor, ct);
    }
    private static async Task<bool> CanEdit(NpgsqlConnection connection, NpgsqlTransaction? tx, Guid actor, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from qms_users u join departments d on d.id=u.department_id where u.id=@actor and u.is_active and d.is_active and d.code='materials')",connection,tx);
        command.Parameters.AddWithValue("actor",actor); return (bool)(await command.ExecuteScalarAsync(ct))!;
    }
    public async Task<IReadOnlyList<G2MaterialCatalog>> CatalogAsync(CancellationToken ct)
    {
        await using var source = Source(); await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select id,source_row,metadata::text from g2_material_items order by id", connection);
        await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<G2MaterialCatalog>();
        while (await reader.ReadAsync(ct)) { var item = JsonSerializer.Deserialize<G2MaterialCatalog>(reader.GetString(2),Json) ?? throw new ArgumentException("품목 설정을 확인해 주세요."); result.Add(item with {Id=reader.GetInt32(0),SourceRow=reader.GetInt32(1)}); }
        if (result.Count != 92 || result.Any(x=>x.SetQuantity<=0)) throw new ArgumentException("고정 92개 품목 설정이 필요합니다.");
        return result;
    }
    public async Task<G2MaterialResponse> ReadAsync(DateOnly date, Guid actor, CancellationToken ct)
    {
        G2MaterialRules.ValidateDate(date,G2MaterialRules.Today(time)); var catalog=await CatalogAsync(ct); var editable=await CanEditAsync(actor,ct);
        var values = new Dictionary<int,(G2MaterialValues Values,DateTime Updated)>();
        await using var source=Source(); await using var connection=await source.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand("select item_id,working,kitting,rack,materials,erp,transit,updated_at_utc,calculation_overrides::text from g2_material_counts where count_date=@date",connection); command.Parameters.AddWithValue("date",date);
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) { var id=reader.GetInt32(0); decimal? V(int n)=>reader.IsDBNull(n)?null:reader.GetDecimal(n); values[id]=(new(id,V(1),V(2),V(3),V(4),V(5),V(6),reader.IsDBNull(8)?null:JsonSerializer.Deserialize<Dictionary<string,decimal>>(reader.GetString(8),Json)),reader.GetDateTime(7)); }
        return new(date,editable,catalog.Select(x=>new G2MaterialItem(x.Id,x.SourceRow,x.Category,x.Vendor,x.Name,x.Code,x.Spec,x.Unit,x.SetQuantity,x.PackBox,x.PackPalletEa,x.PackPalletBox,x.PackNote,x.ImageKeys,x.CellFills,values.TryGetValue(x.Id,out var v)?v.Values:new(x.Id,null,null,null,null,null,null),values.TryGetValue(x.Id,out v)?v.Updated:null)).ToArray());
    }
    public async Task SaveAsync(DateOnly date, SaveG2MaterialsRequest request, Guid actor, CancellationToken ct)
    {
        G2MaterialRules.ValidateDate(date,G2MaterialRules.Today(time)); G2MaterialRules.Validate(request.Items);
        var catalog=await CatalogAsync(ct);
        await using var source=Source(); await using var connection=await source.OpenConnectionAsync(ct); await using var tx=await connection.BeginTransactionAsync(ct);
        if(!await CanEdit(connection,tx,actor,ct)) throw new UnauthorizedAccessException();
        foreach(var item in request.Items) G2MaterialRules.ValidateCalculations(item,catalog.Single(x=>x.Id==item.Id));
        // Entire snapshots serialize so concurrent saves cannot interleave different rows.
        await using(var gate=new NpgsqlCommand("select pg_advisory_xact_lock(4733,@day)",connection,tx)) {gate.Parameters.AddWithValue("day",date.DayNumber); await gate.ExecuteNonQueryAsync(ct);}
        foreach(var item in request.Items.OrderBy(x=>x.Id))
        {
            await using var command=new NpgsqlCommand("insert into g2_material_counts(count_date,item_id,working,kitting,rack,materials,erp,transit,calculation_overrides,updated_by_user_id) values(@date,@id,@working,@kitting,@rack,@materials,@erp,@transit,@overrides,@actor) on conflict(count_date,item_id) do update set working=excluded.working,kitting=excluded.kitting,rack=excluded.rack,materials=excluded.materials,erp=excluded.erp,transit=excluded.transit,calculation_overrides=excluded.calculation_overrides,updated_by_user_id=excluded.updated_by_user_id,updated_at_utc=now()",connection,tx);
            command.Parameters.AddWithValue("date",date); command.Parameters.AddWithValue("id",item.Id);command.Parameters.AddWithValue("actor",actor);
            foreach(var (key,value) in new (string,decimal?)[]{("working",item.Working),("kitting",item.Kitting),("rack",item.Rack),("materials",item.Materials),("erp",item.Erp),("transit",item.Transit)}) command.Parameters.AddWithValue(key,NpgsqlDbType.Numeric,(object?)value??DBNull.Value);
            command.Parameters.AddWithValue("overrides",NpgsqlDbType.Jsonb,DBNull.Value);
            await command.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task<int> PruneAsync(CancellationToken ct)
    {
        await using var source=Source();await using var connection=await source.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand("delete from g2_material_counts where count_date < @cutoff",connection);command.Parameters.AddWithValue("cutoff",G2MaterialRules.Cutoff(G2MaterialRules.Today(time)));return await command.ExecuteNonQueryAsync(ct);
    }
}

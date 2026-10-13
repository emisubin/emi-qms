using System.Data;
using System.Text.Json;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.InteriorBusbar;
using Emi.Qms.Api.Security;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
namespace Emi.Qms.Api.G2;

public sealed record G2QualityPhoto(string FileName, string ContentType, string? Base64 = null, Guid? Id = null, int? ByteSize = null);
public sealed record G2QualityAction(DateOnly CompletionDate, string Content, G2QualityPhoto[] Photos);
public sealed record G2QualityItem(Guid Id, string Serial, DateOnly OccurrenceDate, string OccurrenceType, string Content, bool InitialStock, G2QualityPhoto[] Photos, G2QualityAction? Action, int Version);
public sealed record SaveG2Quality(string Serial, DateOnly OccurrenceDate, string OccurrenceType, string Content, bool InitialStock, G2QualityPhoto[] Photos, int? ExpectedVersion);
public sealed record SaveG2QualityAction(DateOnly CompletionDate, string Content, G2QualityPhoto[] Photos, int ExpectedVersion);

public sealed class G2QualityDuplicateSerialException : Exception;
public sealed class G2UnsafePhotoException : Exception;
public sealed class G2PhotoScanUnavailableException : Exception;

public static class G2QualityRules
{
    public const int MaximumPhotoCount = 10;
    public const int MaximumPhotoBytes = 5_000_000;
    public const int MaximumStageBytes = 20_000_000;
    public const long MaximumPreviewMultipartBytes = 7_000_000;

    public static void Validate(string content, G2QualityPhoto[]? photos)
    {
        ValidateContentAndCount(content, photos);
        long total = 0;
        foreach(var photo in photos!)
        {
            total += DecodeNewPhoto(photo).Length;
        }
        if(total > MaximumStageBytes) throw new ArgumentException("사진 합계는 20MB 이하여야 합니다.");
    }
    public static async Task ValidateImagesAsync(string content,G2QualityPhoto[] photos,CancellationToken ct)
    {
        Validate(content,photos);
        long total = 0;
        foreach(var photo in photos)
        {
            var (validated,error)=await InteriorBusbarPhotoValidator.ValidateAsync(photo.FileName,photo.ContentType,Convert.FromBase64String(photo.Base64!),ct);
            if(validated is null) throw new ArgumentException(error ?? "올바른 사진을 첨부해 주세요.");
            if(validated.Content.Length > MaximumPhotoBytes) throw new ArgumentException("사진은 장당 5MB 이하 JPEG, PNG, HEIC, WebP만 가능합니다.");
            total += validated.Content.Length;
        }
        if(total > MaximumStageBytes) throw new ArgumentException("사진 합계는 20MB 이하여야 합니다.");
    }
    internal static void ValidateContentAndCount(string content,G2QualityPhoto[]? photos)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 10000) throw new ArgumentException("내용을 1~10,000자로 입력해 주세요.");
        if (photos is null || photos.Length is < 1 or > MaximumPhotoCount) throw new ArgumentException("사진을 1~10장 첨부해 주세요.");
    }
    internal static byte[] DecodeNewPhoto(G2QualityPhoto? photo)
    {
        if (photo is null || string.IsNullOrWhiteSpace(photo.FileName) || photo.FileName.Length > 255 || string.IsNullOrWhiteSpace(photo.ContentType) || photo.Base64 is null || photo.Base64.Length > MaximumPreviewMultipartBytes) throw new ArgumentException("사진 파일을 확인해 주세요.");
        if(photo.ContentType.Trim().ToLowerInvariant() is not ("image/png" or "image/jpeg" or "image/webp" or "image/heic" or "image/heif" or "application/octet-stream")) throw new ArgumentException("사진은 장당 5MB 이하 JPEG, PNG, HEIC, WebP만 가능합니다.");
        byte[] bytes;
        try { bytes=Convert.FromBase64String(photo.Base64); } catch(FormatException) { throw new ArgumentException("사진 파일을 확인해 주세요."); }
        if(bytes.Length is < 1 or > MaximumPhotoBytes) throw new ArgumentException("사진은 장당 5MB 이하 JPEG, PNG, HEIC, WebP만 가능합니다.");
        return bytes;
    }
    public static void ValidateDate(DateOnly date, DateOnly today) { if(date.Year < 2000 || date > today) throw new ArgumentException("오늘 또는 과거 날짜를 선택해 주세요."); }
    public static void Version(int? expected, int actual) { if(expected != actual) throw new DBConcurrencyException("다른 사용자가 변경했습니다. 새로고침 후 다시 시도해 주세요."); }
}

public sealed class G2NonconformanceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly CheongjuDatabase database;
    private readonly TimeProvider time;
    private readonly IUploadMalwareScanner? malwareScanner;
    private readonly UploadSecurityOptions uploadSecurity;
    public G2NonconformanceStore(CheongjuDatabase database,TimeProvider time)
    {
        this.database=database; this.time=time; uploadSecurity=new UploadSecurityOptions();
    }
    public G2NonconformanceStore(CheongjuDatabase database,TimeProvider time,IUploadMalwareScanner malwareScanner,IOptions<UploadSecurityOptions> uploadSecurity)
    {
        this.database=database; this.time=time; this.malwareScanner=malwareScanner; this.uploadSecurity=uploadSecurity.Value;
    }
    private RuntimeDataSourceLease Source() => database.RentDataSource(database.GetConnectionString() ?? throw new InvalidOperationException("Database is not configured."));
    private static async Task<bool> CanEdit(NpgsqlConnection c, NpgsqlTransaction? tx, Guid actor, CancellationToken ct)
    {
        await using var q=new NpgsqlCommand("select exists(select 1 from qms_users u where u.id=@actor and u.is_active and (exists(select 1 from user_roles ur join roles r on r.id=ur.role_id where ur.user_id=u.id and r.code='system-administrator') or exists(select 1 from departments d where d.id=u.department_id and d.is_active and d.code='quality')))",c,tx);
        q.Parameters.AddWithValue("actor",actor); return (bool)(await q.ExecuteScalarAsync(ct))!;
    }
    public async Task EnsureCanEditAsync(Guid actor,CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        if(!await CanEdit(c,null,actor,ct)) throw new UnauthorizedAccessException();
    }
    public async Task<object> ReadAsync(Guid actor,CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        var canEdit=await CanEdit(c,null,actor,ct); var items=new List<G2QualityItem>();
        await using var q=new NpgsqlCommand("select data::text from g2_nonconformances order by data->>'occurrenceDate' desc, id",c);
        await using var reader=await q.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) items.Add(Public(JsonSerializer.Deserialize<G2QualityItem>(reader.GetString(0),Json)!));
        return new {canEdit,items};
    }
    public Task<G2QualityItem?> SaveFirstAsync(Guid? id,SaveG2Quality request,Guid actor,CancellationToken ct)
    {
        G2QualityRules.ValidateDate(request.OccurrenceDate,G2MaterialRules.Today(time));
        if(string.IsNullOrWhiteSpace(request.Serial) || request.Serial.Length>100 || request.OccurrenceType is not ("production" or "supplier")) throw new ArgumentException("시리얼과 발생 유형을 확인해 주세요.");
        return Mutate(id,request.ExpectedVersion,actor,current=>new(id??Guid.NewGuid(),request.Serial.Trim(),request.OccurrenceDate,request.OccurrenceType,request.Content,request.InitialStock,request.Photos,current?.Action,(current?.Version??0)+1),ct);
    }
    public Task<G2QualityItem?> SaveActionAsync(Guid id,SaveG2QualityAction request,Guid actor,CancellationToken ct)
    {
        G2QualityRules.ValidateDate(request.CompletionDate,G2MaterialRules.Today(time));
        return Mutate(id,request.ExpectedVersion,actor,current=>current! with {Action=new(request.CompletionDate,request.Content,request.Photos),Version=current!.Version+1},ct);
    }
    public Task<G2QualityItem?> DeleteAsync(Guid id,int expected, bool action,Guid actor,CancellationToken ct)=>Mutate(id,expected,actor,current=>
    {
        if(!action && current!.Action is not null) throw new ArgumentException("조치 정보를 먼저 삭제해 주세요.");
        return action?current! with {Action=null,Version=current!.Version+1}:null;
    },ct);
    private async Task<G2QualityItem?> Mutate(Guid? id,int? expected,Guid actor,Func<G2QualityItem?,G2QualityItem?> change,CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        if(!await CanEdit(c,tx,actor,ct)) throw new UnauthorizedAccessException();
        G2QualityItem? current=null;
        if(id is not null)
        {
            await using var read=new NpgsqlCommand("select data::text from g2_nonconformances where id=@id for update",c,tx); read.Parameters.AddWithValue("id",id.Value);
            var raw=await read.ExecuteScalarAsync(ct) as string;
            if(raw is null) throw new KeyNotFoundException();
            current=JsonSerializer.Deserialize<G2QualityItem>(raw,Json)!; G2QualityRules.Version(expected,current.Version);
        }
        var next=change(current);
        if(next is not null)
        {
            if(next.Action is not null && next.Action.CompletionDate < next.OccurrenceDate) throw new ArgumentException("수리 완료일은 발생 일자보다 빠를 수 없습니다.");
            var existing=current?.Photos.Concat(current.Action?.Photos??[]).Where(p=>p.Id is not null).GroupBy(p=>p.Id!.Value).ToDictionary(group=>group.Key,group=>group.First())??[];
            next=next with {Photos=await ResolvePhotosAsync(next.Content,next.Photos,existing,ct),Action=next.Action is null?null:next.Action with {Photos=await ResolvePhotosAsync(next.Action.Content,next.Action.Photos,existing,ct)}};
        }
        if(current is not null)
        {
            await using var history=new NpgsqlCommand("insert into g2_nonconformance_history(case_id,snapshot,changed_by,expires_at) values(@id,@data,@actor,@expires)",c,tx);
            history.Parameters.AddWithValue("id",current.Id); history.Parameters.AddWithValue("data",NpgsqlDbType.Jsonb,JsonSerializer.Serialize(current,Json)); history.Parameters.AddWithValue("actor",actor); history.Parameters.AddWithValue("expires",time.GetUtcNow().AddMonths(3)); await history.ExecuteNonQueryAsync(ct);
        }
        await using var write=new NpgsqlCommand(next is null?"delete from g2_nonconformances where id=@id":"insert into g2_nonconformances(id,data,updated_by) values(@id,@data,@actor) on conflict(id) do update set data=excluded.data,updated_by=excluded.updated_by,updated_at=now()",c,tx);
        write.Parameters.AddWithValue("id",next?.Id??id!.Value); write.Parameters.AddWithValue("data",NpgsqlDbType.Jsonb,JsonSerializer.Serialize(next,Json)); write.Parameters.AddWithValue("actor",actor);
        try { await write.ExecuteNonQueryAsync(ct); } catch(PostgresException e) when(e.SqlState == PostgresErrorCodes.UniqueViolation) { throw new G2QualityDuplicateSerialException(); }
        await tx.CommitAsync(ct); return next is null ? null : Public(next);
    }
    private async Task<G2QualityPhoto[]> ResolvePhotosAsync(string content,G2QualityPhoto[]? requested,IReadOnlyDictionary<Guid,G2QualityPhoto> existing,CancellationToken ct)
    {
        G2QualityRules.ValidateContentAndCount(content,requested);
        long inputTotal=0;
        foreach(var photo in requested!)
        {
            if(photo is null)throw new ArgumentException("사진을 첨부해 주세요.");
            if(photo.Id is Guid retainedId && existing.TryGetValue(retainedId,out var retained) && (photo.Base64 is null || photo.Base64==retained.Base64))
            {
                try { inputTotal+=Convert.FromBase64String(retained.Base64??throw new FormatException()).Length; }
                catch(FormatException) { throw new ArgumentException("사진을 다시 첨부해 주세요."); }
            }
            else inputTotal+=G2QualityRules.DecodeNewPhoto(photo).Length;
        }
        if(inputTotal>G2QualityRules.MaximumStageBytes)throw new ArgumentException("사진 합계는 20MB 이하여야 합니다.");
        var resolved=new List<G2QualityPhoto>(requested!.Length);
        foreach(var photo in requested)
        {
            if(photo is null) throw new ArgumentException("사진을 첨부해 주세요.");
            if(photo.Id is Guid retainedId && existing.TryGetValue(retainedId,out var retained) && (photo.Base64 is null || photo.Base64==retained.Base64))
            {
                resolved.Add(retained); continue;
            }
            var original=G2QualityRules.DecodeNewPhoto(photo);
            await ScanAsync(original,ct);
            var (validated,error)=await InteriorBusbarPhotoValidator.ValidateAsync(photo.FileName,photo.ContentType,original,ct);
            if(validated is null) throw new ArgumentException(error??"올바른 사진을 첨부해 주세요.");
            if(validated.Content.Length>G2QualityRules.MaximumPhotoBytes) throw new ArgumentException("사진은 장당 5MB 이하 JPEG, PNG, HEIC, WebP만 가능합니다.");
            resolved.Add(new(SafeFileName(photo.FileName),validated.ContentType,Convert.ToBase64String(validated.Content),Guid.NewGuid(),validated.Content.Length));
        }
        long total=0;
        foreach(var photo in resolved)
        {
            if(photo.Base64 is null) throw new ArgumentException("사진을 다시 첨부해 주세요.");
            byte[] bytes;
            try { bytes=Convert.FromBase64String(photo.Base64); } catch(FormatException) { throw new ArgumentException("사진을 다시 첨부해 주세요."); }
            if(bytes.Length is < 1 or > G2QualityRules.MaximumPhotoBytes) throw new ArgumentException("사진은 장당 5MB 이하 JPEG, PNG, HEIC, WebP만 가능합니다.");
            total+=bytes.Length;
        }
        if(total>G2QualityRules.MaximumStageBytes) throw new ArgumentException("사진 합계는 20MB 이하여야 합니다.");
        return resolved.ToArray();
    }
    private async Task ScanAsync(byte[] original,CancellationToken ct)
    {
        if(!uploadSecurity.Enabled || malwareScanner is null)return;
        await using var stream=new MemoryStream(original,writable:false);
        var result=await malwareScanner.ScanAsync(stream,ct);
        if(result.Status==UploadMalwareScanStatus.Infected)throw new G2UnsafePhotoException();
        if(result.Status==UploadMalwareScanStatus.Unavailable && uploadSecurity.FailClosed)throw new G2PhotoScanUnavailableException();
    }
    private static string SafeFileName(string fileName)
    {
        var safe=Path.GetFileName(fileName.Replace('\\','/')).Trim();
        safe=new string(safe.Where(c=>!char.IsControl(c)).ToArray());
        return safe.Length>255?safe[..255]:safe;
    }
    private static G2QualityItem Public(G2QualityItem item)=>item with {Photos=item.Photos.Select(PublicPhoto).ToArray(),Action=item.Action is null?null:item.Action with {Photos=item.Action.Photos.Select(PublicPhoto).ToArray()}};
    private static G2QualityPhoto PublicPhoto(G2QualityPhoto photo)=>photo with {ByteSize=photo.ByteSize??(photo.Base64 is null?null:Convert.FromBase64String(photo.Base64).Length),Base64=null};
    public async Task<G2QualityPhoto?> PhotoAsync(Guid id,Guid photoId,CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        await using var q=new NpgsqlCommand("select data::text from g2_nonconformances where id=@id",c); q.Parameters.AddWithValue("id",id);
        var raw=await q.ExecuteScalarAsync(ct) as string; if(raw is null)return null;
        var item=JsonSerializer.Deserialize<G2QualityItem>(raw,Json)!;
        return item.Photos.Concat(item.Action?.Photos??[]).FirstOrDefault(p=>p.Id==photoId);
    }
    public async Task PruneAsync(CancellationToken ct)
    {
        await using var source=Source(); await using var c=await source.OpenConnectionAsync(ct);
        await using var q=new NpgsqlCommand("delete from g2_nonconformance_history where expires_at <= @now",c); q.Parameters.AddWithValue("now",time.GetUtcNow()); await q.ExecuteNonQueryAsync(ct);
    }
}

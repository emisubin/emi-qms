using System.Security.Cryptography;
using Npgsql;

namespace Emi.Qms.Api.Identity;

public sealed class UserProfilePhotoStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<string?> GetVersionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            "select content_hash || '-' || version::text from user_profile_photos where user_id=@user_id");
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<UserProfilePhoto?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select normalized_mime, content_hash, version, content
            from user_profile_photos
            where user_id=@user_id;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UserProfilePhoto(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), (byte[])reader[3])
            : null;
    }

    public async Task<UserProfilePhotoMetadata> SaveAsync(Guid userId, byte[] content, CancellationToken cancellationToken)
    {
        var validation = ProfileImageValidator.Validate(content);
        if (!validation.Succeeded)
        {
            throw new ProfileImageValidationException(validation.ErrorMessage!);
        }

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existed = false;
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = "select true from user_profile_photos where user_id=@user_id for update";
            check.Parameters.AddWithValue("user_id", userId);
            existed = await check.ExecuteScalarAsync(cancellationToken) is true;
        }

        long version;
        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                insert into user_profile_photos (
                    user_id, normalized_mime, byte_size, content_hash, version, content,
                    updated_by_profile_user_id, updated_at_utc
                )
                values (@user_id, @mime, @byte_size, @hash, 1, @content, @user_id, now())
                on conflict (user_id) do update
                set normalized_mime=excluded.normalized_mime,
                    byte_size=excluded.byte_size,
                    content_hash=excluded.content_hash,
                    version=user_profile_photos.version + 1,
                    content=excluded.content,
                    updated_by_profile_user_id=excluded.updated_by_profile_user_id,
                    updated_at_utc=now()
                returning version;
                """;
            AddPhotoParameters(upsert, userId, validation.NormalizedMime!, content, hash);
            version = Convert.ToInt64(await upsert.ExecuteScalarAsync(cancellationToken));
        }

        await InsertAuditAsync(connection, transaction, userId, existed ? "Replace" : "Upload",
            hash, content.Length, validation.NormalizedMime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UserProfilePhotoMetadata($"{hash}-{version}", validation.NormalizedMime!, content.Length);
    }

    public async Task<bool> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? hash = null;
        string? mime = null;
        int? byteSize = null;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                delete from user_profile_photos
                where user_id=@user_id
                returning content_hash, byte_size, normalized_mime;
                """;
            command.Parameters.AddWithValue("user_id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                hash = reader.GetString(0);
                byteSize = reader.GetInt32(1);
                mime = reader.GetString(2);
            }
        }

        if (hash is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await InsertAuditAsync(connection, transaction, userId, "Remove", hash, byteSize, mime, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static void AddPhotoParameters(NpgsqlCommand command, Guid userId, string mime, byte[] content, string hash)
    {
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("mime", mime);
        command.Parameters.AddWithValue("byte_size", content.Length);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("content", content);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        string action,
        string? hash,
        int? byteSize,
        string? mime,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into user_profile_photo_audit_events (
                profile_user_id, actor_profile_user_id, action, content_hash, byte_size, normalized_mime
            ) values (@user_id, @user_id, @action, @hash, @byte_size, @mime);
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("hash", hash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("byte_size", byteSize ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("mime", mime ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }
        return NpgsqlDataSource.Create(connectionString);
    }
}

public sealed record UserProfilePhoto(string NormalizedMime, string ContentHash, long Version, byte[] Content);
public sealed record UserProfilePhotoMetadata(string ProfilePhotoVersion, string NormalizedMime, int ByteSize);
public sealed class ProfileImageValidationException(string message) : Exception(message);

using Microsoft.Data.Sqlite;

namespace OfflineDriveGallery.Core;

public record CatalogVolume(string Id, string Name, string LastPath, string? LastScanUtc, int FileCount, int Warnings);
public record CatalogItem(string VolumeId, string VolumeName, string LastRootPath, string RelativePath,
    string Name, string Extension, long Size, long ModifiedTicks, byte[]? Thumbnail);
public record ScanProgress(int FilesSeen, int ThumbnailsMade, string CurrentPath);
public record ScanResult(int FilesSeen, int ThumbnailsMade, int Warnings, bool Complete);

public interface IThumbnailProvider
{
    byte[]? MakeThumbnail(string path);
}

public sealed class Catalog(string databasePath)
{
    public string DatabasePath { get; } = Path.GetFullPath(databasePath);

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS volumes (
              id TEXT PRIMARY KEY, name TEXT NOT NULL, last_path TEXT NOT NULL,
              last_scan_utc TEXT, file_count INTEGER NOT NULL DEFAULT 0,
              warnings INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS files (
              volume_id TEXT NOT NULL, relative_path TEXT NOT NULL, name TEXT NOT NULL,
              extension TEXT NOT NULL, size INTEGER NOT NULL, modified_ticks INTEGER NOT NULL,
              thumbnail BLOB, seen_token TEXT NOT NULL,
              PRIMARY KEY (volume_id, relative_path),
              FOREIGN KEY (volume_id) REFERENCES volumes(id)
            );
            CREATE INDEX IF NOT EXISTS ix_files_name ON files(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_files_extension ON files(extension);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<CatalogVolume> Volumes()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, last_path, last_scan_utc, file_count, warnings FROM volumes ORDER BY name COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var result = new List<CatalogVolume>();
        while (reader.Read()) result.Add(new CatalogVolume(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5)));
        return result;
    }

    public IReadOnlyList<CatalogItem> Search(string query, string? volumeId = null, bool mediaOnly = false,
        int limit = 180, int offset = 0)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        var pattern = "%" + query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        command.CommandText = """
            SELECT f.volume_id, v.name, v.last_path, f.relative_path, f.name, f.extension,
                   f.size, f.modified_ticks, f.thumbnail
            FROM files f JOIN volumes v ON v.id = f.volume_id
            WHERE (@volume IS NULL OR f.volume_id = @volume)
              AND (@media = 0 OR f.extension IN
                   ('.jpg','.jpeg','.png','.gif','.bmp','.tif','.tiff','.webp','.heic',
                    '.mp4','.mov','.mkv','.avi','.wmv','.webm','.m4v'))
              AND (@query = '' OR f.name LIKE @pattern ESCAPE '\' COLLATE NOCASE
                   OR f.relative_path LIKE @pattern ESCAPE '\' COLLATE NOCASE)
            ORDER BY f.name COLLATE NOCASE, v.name COLLATE NOCASE, f.relative_path COLLATE NOCASE
            LIMIT @limit OFFSET @offset;
            """;
        command.Parameters.AddWithValue("@volume", (object?)volumeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@media", mediaOnly ? 1 : 0);
        command.Parameters.AddWithValue("@query", query.Trim());
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 500));
        command.Parameters.AddWithValue("@offset", Math.Max(0, offset));
        using var reader = command.ExecuteReader();
        var result = new List<CatalogItem>();
        while (reader.Read()) result.Add(new CatalogItem(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6), reader.GetInt64(7),
            reader.IsDBNull(8) ? null : (byte[])reader[8]));
        return result;
    }

    public ScanResult Scan(string root, string volumeId, string displayName, IThumbnailProvider thumbnails,
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (string.IsNullOrWhiteSpace(volumeId) || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Drive identity and name are required.");

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var token = Guid.NewGuid().ToString("N");
        using (var volume = connection.CreateCommand())
        {
            volume.Transaction = transaction;
            volume.CommandText = """
                INSERT INTO volumes(id, name, last_path) VALUES(@id, @name, @path)
                ON CONFLICT(id) DO UPDATE SET name=@name, last_path=@path;
                """;
            volume.Parameters.AddWithValue("@id", volumeId);
            volume.Parameters.AddWithValue("@name", displayName.Trim());
            volume.Parameters.AddWithValue("@path", root);
            volume.ExecuteNonQuery();
        }

        using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT size, modified_ticks, thumbnail FROM files WHERE volume_id=@id AND relative_path=@path";
        existing.Parameters.AddWithValue("@id", volumeId);
        var existingPath = existing.Parameters.Add("@path", SqliteType.Text);

        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = """
            INSERT INTO files(volume_id, relative_path, name, extension, size, modified_ticks, thumbnail, seen_token)
            VALUES(@id, @path, @name, @ext, @size, @modified, @thumb, @seen)
            ON CONFLICT(volume_id, relative_path) DO UPDATE SET
              name=@name, extension=@ext, size=@size, modified_ticks=@modified,
              thumbnail=@thumb, seen_token=@seen;
            """;
        upsert.Parameters.AddWithValue("@id", volumeId);
        var pathParam = upsert.Parameters.Add("@path", SqliteType.Text);
        var nameParam = upsert.Parameters.Add("@name", SqliteType.Text);
        var extParam = upsert.Parameters.Add("@ext", SqliteType.Text);
        var sizeParam = upsert.Parameters.Add("@size", SqliteType.Integer);
        var modifiedParam = upsert.Parameters.Add("@modified", SqliteType.Integer);
        var thumbParam = upsert.Parameters.Add("@thumb", SqliteType.Blob);
        upsert.Parameters.AddWithValue("@seen", token);

        int filesSeen = 0, thumbnailsMade = 0, warnings = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(folder).ToArray(); }
            catch (IOException) { warnings++; continue; }
            catch (UnauthorizedAccessException) { warnings++; continue; }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
                    var info = new FileInfo(entry);
                    var relative = Path.GetRelativePath(root, entry);
                    var modified = info.LastWriteTimeUtc.Ticks;
                    byte[]? thumbnail = null;
                    bool changed = true;
                    existingPath.Value = relative;
                    using (var reader = existing.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            changed = reader.GetInt64(0) != info.Length || reader.GetInt64(1) != modified;
                            if (!changed && !reader.IsDBNull(2)) thumbnail = (byte[])reader[2];
                        }
                    }
                    if (changed || thumbnail is null)
                    {
                        thumbnail = thumbnails.MakeThumbnail(entry);
                        if (thumbnail is not null) thumbnailsMade++;
                    }
                    pathParam.Value = relative;
                    nameParam.Value = info.Name;
                    extParam.Value = info.Extension.ToLowerInvariant();
                    sizeParam.Value = info.Length;
                    modifiedParam.Value = modified;
                    thumbParam.Value = (object?)thumbnail ?? DBNull.Value;
                    upsert.ExecuteNonQuery();
                    filesSeen++;
                    if (filesSeen % 50 == 0) progress?.Report(new ScanProgress(filesSeen, thumbnailsMade, relative));
                }
                catch (IOException) { warnings++; }
                catch (UnauthorizedAccessException) { warnings++; }
            }
        }

        if (warnings == 0)
        {
            using var remove = connection.CreateCommand();
            remove.Transaction = transaction;
            remove.CommandText = "DELETE FROM files WHERE volume_id=@id AND seen_token<>@seen";
            remove.Parameters.AddWithValue("@id", volumeId);
            remove.Parameters.AddWithValue("@seen", token);
            remove.ExecuteNonQuery();
        }
        using (var finish = connection.CreateCommand())
        {
            finish.Transaction = transaction;
            finish.CommandText = """
                UPDATE volumes SET last_scan_utc=@now, warnings=@warnings,
                  file_count=(SELECT COUNT(*) FROM files WHERE volume_id=@id)
                WHERE id=@id;
                """;
            finish.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            finish.Parameters.AddWithValue("@warnings", warnings);
            finish.Parameters.AddWithValue("@id", volumeId);
            finish.ExecuteNonQuery();
        }
        transaction.Commit();
        progress?.Report(new ScanProgress(filesSeen, thumbnailsMade, root));
        return new ScanResult(filesSeen, thumbnailsMade, warnings, warnings == 0);
    }
}

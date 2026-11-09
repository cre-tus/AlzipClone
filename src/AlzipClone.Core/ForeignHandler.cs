using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace AlzipClone.Core;

/// <summary>Read-only RAR/7Z via SharpCompress, mirroring how the
/// reference product plugs in external format DLLs. Writing these
/// formats is out of scope (encoders are proprietary).</summary>
public static class ForeignHandler
{
    public static ArchiveModel Open(string path, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        EncodingHelper.EnsureRegistered();
        var format = FormatDetector.Detect(path);
        var model = new ArchiveModel { FilePath = path, Format = format };
        using var archive = OpenArchive(path, options.Password);
        foreach (var entry in archive.Entries)
        {
            DateTime mtime = entry.LastModifiedTime ?? DateTime.Now;
            if (entry.IsDirectory)
            {
                model.Entries.Add(new ArchiveEntry(
                    Normalize(entry.Key), 0, entry.CompressedSize, mtime,
                    format.ToString(), true, entry.IsEncrypted, "", 0));
            }
            else
            {
                model.Entries.Add(new ArchiveEntry(
                    Normalize(entry.Key), entry.Size, entry.CompressedSize, mtime,
                    format.ToString(), false, entry.IsEncrypted, "", (uint)(entry.Crc & 0xFFFFFFFF)));
            }
        }
        return model;
    }

    public static void Extract(
        string archivePath,
        string destDir,
        ExtractOptions? options = null,
        IEnumerable<string>? onlyEntries = null,
        Action<ProgressInfo>? progress = null)
    {
        options ??= new ExtractOptions();
        EncodingHelper.EnsureRegistered();
        Directory.CreateDirectory(destDir);
        using var archive = OpenArchive(archivePath, options.Password);
        var names = onlyEntries != null ? new HashSet<string>(onlyEntries) : null;
        var entries = archive.Entries.Where(e => names == null || names.Contains(Normalize(e.Key))).ToList();
        int total = entries.Count;
        int done = 0;
        foreach (var entry in entries)
        {
            string name = Normalize(entry.Key);
            progress?.Invoke(new ProgressInfo(name, done, total));
            string target = SafeJoin(destDir, name);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(target);
            }
            else
            {
                if (!options.Overwrite && File.Exists(target))
                {
                    done++;
                    continue;
                }
                string? parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);
                entry.WriteToFile(target, new ExtractionOptions
                {
                    ExtractFullPath = false,
                    Overwrite = true,
                    PreserveFileTime = true,
                });
            }
            done++;
        }
        progress?.Invoke(new ProgressInfo("", done, total));
    }

    /// <summary>Decode test without writing files. Returns (total, failed, firstErrors).</summary>
    public static (int Total, int Failed, List<string> Errors) Test(string archivePath, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        EncodingHelper.EnsureRegistered();
        var errors = new List<string>();
        using var archive = OpenArchive(archivePath, options.Password);
        int failed = 0;
        int total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
                continue;
            total++;
            try
            {
                using var sink = new MemoryStream();
                entry.WriteTo(sink);
            }
            catch (Exception ex)
            {
                failed++;
                if (errors.Count < 10)
                    errors.Add($"{Normalize(entry.Key)}: {ex.Message}");
            }
        }
        return (total, failed, errors);
    }

    private static IArchive OpenArchive(string path, string password)
    {
        try
        {
            return ArchiveFactory.OpenArchive(path, new ReaderOptions
            {
                Password = string.IsNullOrEmpty(password) ? null : password,
            });
        }
        catch (Exception ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("암호화된 파일입니다. 암호를 입력하세요.", ex);
        }
    }

    private static string Normalize(string? key)
    {
        string n = (key ?? "").Replace('\\', '/');
        int colon = n.IndexOf(':');
        if (colon >= 0)
            n = n[(colon + 1)..];
        return n.TrimStart('/');
    }

    private static string SafeJoin(string destDir, string name)
    {
        string full = Path.GetFullPath(Path.Combine(destDir, name));
        string root = Path.GetFullPath(destDir);
        if (full != root && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"안전하지 않은 경로입니다: {name}");
        return full;
    }
}

using AlzipClone.Core.Egg;

namespace AlzipClone.Core;

/// <summary>EGG read/extract/verify built on the in-house spec parser.
/// Writing EGG is forbidden by the ESTsoft license (clause 3), so only
/// reading is offered.</summary>
public static class EggHandler
{
    public static ArchiveModel Open(string path, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        EncodingHelper.EnsureRegistered();
        using var arc = EggArchive.Open(path);
        var model = new ArchiveModel { FilePath = path, Format = ArchiveFormat.Egg };
        model.Comment = arc.ArchiveComment;
        foreach (var e in arc.ToEntries())
            model.Entries.Add(e);
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
        using var arc = EggArchive.Open(path: archivePath);
        var names = onlyEntries != null ? new HashSet<string>(onlyEntries) : null;
        var files = arc.Files.Where(f => names == null || names.Contains(f.ResolvedName)).ToList();
        int total = files.Count;
        int done = 0;
        foreach (var f in arc.Files)
        {
            if (names != null && !names.Contains(f.ResolvedName))
                continue;
            progress?.Invoke(new ProgressInfo(f.ResolvedName, done, total));
            string target = SafeJoin(destDir, f.ResolvedName);
            if (f.IsDirectory)
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
                byte[] bytes = arc.GetFileBytes(f,
                    options.Password.Length > 0 ? options.Password : null, verifyCrc: true);
                File.WriteAllBytes(target, bytes);
                try { File.SetLastWriteTime(target, f.Modified); } catch { }
            }
            done++;
        }
        progress?.Invoke(new ProgressInfo("", done, total));
    }

    /// <summary>CRC test without writing files. Returns (total, failed, firstErrors).</summary>
    public static (int Total, int Failed, List<string> Errors) Test(string archivePath, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        EncodingHelper.EnsureRegistered();
        var errors = new List<string>();
        using var arc = EggArchive.Open(archivePath);
        int failed = 0;
        string? pw = options.Password.Length > 0 ? options.Password : null;
        foreach (var f in arc.Files)
        {
            if (f.IsDirectory || f.Length == 0)
                continue;
            try
            {
                arc.GetFileBytes(f, pw, verifyCrc: true);
            }
            catch (Exception ex)
            {
                failed++;
                if (errors.Count < 10)
                    errors.Add($"{f.ResolvedName}: {ex.Message}");
            }
        }
        return (arc.Files.Count(f => !f.IsDirectory), failed, errors);
    }

    private static string SafeJoin(string destDir, string name)
    {
        string n = name.Replace('\\', '/');
        int colon = n.IndexOf(':');
        if (colon >= 0)
            n = n[(colon + 1)..];
        n = n.TrimStart('/');
        string full = Path.GetFullPath(Path.Combine(destDir, n));
        string root = Path.GetFullPath(destDir);
        if (full != root && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new EggFormatException($"안전하지 않은 경로입니다: {name}");
        return full;
    }
}

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace AlzipClone.Core;

/// <summary>TAR / TGZ / GZ read/write built on SharpZipLib.</summary>
public static class TarHandler
{
    public static ArchiveModel Open(string path, NameEncoding encoding = NameEncoding.Utf8)
    {
        var format = FormatDetector.Detect(path);
        var model = new ArchiveModel { FilePath = path, Format = format };
        if (format == ArchiveFormat.Gz)
        {
            var info = new FileInfo(path);
            string inner = Path.GetFileNameWithoutExtension(path);
            using var fs = File.OpenRead(path);
            using var gz = new GZipInputStream(fs);
            long size = 0;
            var buf = new byte[81920];
            int n;
            while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                size += n;
            model.Entries.Add(new ArchiveEntry(inner, size, info.Length, info.LastWriteTime, "GZip", false, false));
            return model;
        }

        foreach (var e in ListTar(path, format))
            model.Entries.Add(e);
        return model;
    }

    public static void Create(
        string destPath,
        IEnumerable<string> files,
        ArchiveFormat format,
        CompressLevel level = CompressLevel.Normal)
    {
        if (format != ArchiveFormat.Tar && format != ArchiveFormat.Tgz && format != ArchiveFormat.Gz)
            throw new ArgumentOutOfRangeException(nameof(format));
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var list = files.ToList();
        if (format == ArchiveFormat.Gz)
        {
            if (list.Count != 1 || !File.Exists(list[0]))
                throw new InvalidOperationException("GZ는 파일 하나만 담을 수 있습니다.");
            using var input = File.OpenRead(list[0]);
            using var output = File.Create(destPath);
            using var gz = new GZipOutputStream(output) { IsStreamOwner = false };
            gz.SetLevel(MapLevel(level));
            input.CopyTo(gz);
            gz.Finish();
            return;
        }

        using var fs = File.Create(destPath);
        Stream outStream = fs;
        GZipOutputStream? gzOut = null;
        if (format == ArchiveFormat.Tgz)
        {
            gzOut = new GZipOutputStream(fs) { IsStreamOwner = false };
            gzOut.SetLevel(MapLevel(level));
            outStream = gzOut;
        }
        using var tar = TarArchive.CreateOutputTarArchive(outStream, System.Text.Encoding.UTF8);
        foreach (var f in list)
        {
            if (Directory.Exists(f))
            {
                var entry = TarEntry.CreateEntryFromFile(f);
                tar.WriteEntry(entry, false);
                foreach (var sub in Directory.GetFiles(f, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(Path.GetDirectoryName(f)!, sub);
                    var subEntry = TarEntry.CreateEntryFromFile(sub);
                    subEntry.Name = rel.Replace('\\', '/');
                    tar.WriteEntry(subEntry, true);
                }
            }
            else if (File.Exists(f))
            {
                var fileEntry = TarEntry.CreateEntryFromFile(f);
                fileEntry.Name = Path.GetFileName(f);
                tar.WriteEntry(fileEntry, true);
            }
            else
            {
                throw new FileNotFoundException($"추가할 파일을 찾을 수 없습니다: {f}", f);
            }
        }
        tar.Close();
        gzOut?.Finish();
        gzOut?.Dispose();
    }

    public static void Extract(
        string archivePath,
        string destDir,
        IEnumerable<string>? onlyEntries = null,
        Action<ProgressInfo>? progress = null,
        bool overwrite = true)
    {
        var format = FormatDetector.Detect(archivePath);
        Directory.CreateDirectory(destDir);
        if (format == ArchiveFormat.Gz)
        {
            string inner = Path.GetFileNameWithoutExtension(archivePath);
            string dest = Path.Combine(destDir, inner);
            if (File.Exists(dest) && !overwrite)
                throw new IOException($"이미 존재합니다: {dest}");
            using var fs = File.OpenRead(archivePath);
            using var gz = new GZipInputStream(fs);
            using var output = File.Create(dest);
            progress?.Invoke(new ProgressInfo(inner, 0, 1));
            gz.CopyTo(output);
            progress?.Invoke(new ProgressInfo("", 1, 1));
            return;
        }

        var names = onlyEntries != null ? new HashSet<string>(onlyEntries) : null;
        var entries = ListTar(archivePath, format);
        int total = names == null ? entries.Count : names.Count;
        int done = 0;
        using var inFs = File.OpenRead(archivePath);
        Stream inStream = inFs;
        GZipInputStream? gzIn = null;
        if (format == ArchiveFormat.Tgz)
        {
            gzIn = new GZipInputStream(inFs) { IsStreamOwner = false };
            inStream = gzIn;
        }
        using var tar = TarArchive.CreateInputTarArchive(inStream, System.Text.Encoding.UTF8);
        tar.SetKeepOldFiles(!overwrite);
        if (names == null)
        {
            tar.ExtractContents(destDir);
            foreach (var e in entries)
            {
                progress?.Invoke(new ProgressInfo(e.FullName, done, total));
                done++;
            }
        }
        else
        {
            // selective extract: stream manually
            using var fs2 = File.OpenRead(archivePath);
            Stream s2 = fs2;
            GZipInputStream? g2 = null;
            if (format == ArchiveFormat.Tgz)
            {
                g2 = new GZipInputStream(fs2) { IsStreamOwner = false };
                s2 = g2;
            }
            using var tin = new TarInputStream(s2, System.Text.Encoding.UTF8);
            TarEntry? te;
            while ((te = tin.GetNextEntry()) != null)
            {
                string name = (te.Name ?? "").TrimStart('/');
                if (!names.Contains(name))
                    continue;
                progress?.Invoke(new ProgressInfo(name, done, total));
                string dest = Path.Combine(destDir, name.Replace('/', Path.DirectorySeparatorChar));
                if (te.IsDirectory)
                {
                    Directory.CreateDirectory(dest);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    if (File.Exists(dest) && !overwrite)
                        throw new IOException($"이미 존재합니다: {dest}");
                    using var output = File.Create(dest);
                    tin.CopyEntryContents(output);
                    File.SetLastWriteTime(dest, te.ModTime);
                }
                done++;
            }
            g2?.Dispose();
        }
        gzIn?.Dispose();
        progress?.Invoke(new ProgressInfo("", done, total));
    }

    /// <summary>Read-through integrity test. Returns (total, failed, firstErrors).</summary>
    public static (int Total, int Failed, List<string> Errors) Test(string archivePath)
    {
        var format = FormatDetector.Detect(archivePath);
        var errors = new List<string>();
        int total = 0, failed = 0;
        try
        {
            if (format == ArchiveFormat.Gz)
            {
                using var fs = File.OpenRead(archivePath);
                using var gz = new GZipInputStream(fs);
                gz.CopyTo(Stream.Null);
                return (1, 0, errors);
            }
            using var inFs = File.OpenRead(archivePath);
            Stream s = inFs;
            GZipInputStream? gzIn = null;
            if (format == ArchiveFormat.Tgz)
            {
                gzIn = new GZipInputStream(inFs) { IsStreamOwner = false };
                s = gzIn;
            }
            using var tin = new TarInputStream(s, System.Text.Encoding.UTF8);
            TarEntry? te;
            var buf = new byte[81920];
            while ((te = tin.GetNextEntry()) != null)
            {
                total++;
                try
                {
                    while (tin.Read(buf, 0, buf.Length) > 0) { }
                }
                catch (Exception ex)
                {
                    failed++;
                    if (errors.Count < 10)
                        errors.Add($"{te.Name}: {ex.Message}");
                }
            }
            gzIn?.Dispose();
        }
        catch (Exception ex)
        {
            failed++;
            errors.Add($"아카이브: {ex.Message}");
        }
        return (total, failed, errors);
    }

    private static List<ArchiveEntry> ListTar(string path, ArchiveFormat format)
    {
        var list = new List<ArchiveEntry>();
        using var fs = File.OpenRead(path);
        Stream s = fs;
        GZipInputStream? gz = null;
        if (format == ArchiveFormat.Tgz)
        {
            gz = new GZipInputStream(fs) { IsStreamOwner = false };
            s = gz;
        }
        using var tin = new TarInputStream(s, System.Text.Encoding.UTF8);
        TarEntry? te;
        while ((te = tin.GetNextEntry()) != null)
        {
            string name = (te.Name ?? "").TrimStart('/');
            list.Add(new ArchiveEntry(
                FullName: name,
                Size: te.Size,
                PackedSize: 0,
                Modified: te.ModTime,
                Method: format == ArchiveFormat.Tgz ? "GZip+Tar" : "Tar",
                IsDirectory: te.IsDirectory,
                Encrypted: false));
        }
        gz?.Dispose();
        return list;
    }

    private static int MapLevel(CompressLevel level) => level switch
    {
        CompressLevel.StoreOnly => 0,
        CompressLevel.Fast => 1,
        CompressLevel.Best => 9,
        CompressLevel.Optimal => 6,
        _ => 6,
    };
}

using Ionic.Zip;

namespace AlzipClone.Core;

public sealed record ProgressInfo(string CurrentFile, int Done, int Total);

public sealed record CreateOptions(
    CompressLevel Level = CompressLevel.Normal,
    ZipCrypto Crypto = ZipCrypto.None,
    string Password = "",
    NameEncoding Encoding = NameEncoding.Utf8,
    string Comment = "",
    long SplitBytes = 0);

public sealed record ExtractOptions(
    string Password = "",
    NameEncoding Encoding = NameEncoding.Utf8,
    bool Overwrite = true);

/// <summary>ZIP read/write/verify built on DotNetZip.</summary>
public static class ZipHandler
{
    public static ArchiveModel Open(string path, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        var model = new ArchiveModel { FilePath = path, Format = ArchiveFormat.Zip };
        using var zip = ReadZip(path, options);
        model.Comment = zip.Comment ?? "";
        foreach (var e in zip)
        {
            model.Entries.Add(new ArchiveEntry(
                FullName: e.FileName.Replace('\\', '/'),
                Size: e.UncompressedSize,
                PackedSize: e.CompressedSize,
                Modified: e.LastModified,
                Method: e.CompressionMethod.ToString(),
                IsDirectory: e.IsDirectory,
                Encrypted: e.UsesEncryption,
                Comment: e.Comment ?? "",
                Crc: unchecked((uint)e.Crc)));
        }
        return model;
    }

    public static void Create(string destPath, IEnumerable<string> files, CreateOptions? options = null)
    {
        options ??= new CreateOptions();
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var zip = new ZipFile(destPath)
        {
            CompressionLevel = MapLevel(options.Level),
            Comment = options.Comment ?? "",
        };
        ApplyEncoding(zip, options.Encoding);
        ApplyCrypto(zip, options);
        if (options.SplitBytes > 0)
            zip.MaxOutputSegmentSize = (int)Math.Min(options.SplitBytes, int.MaxValue);
        foreach (var f in files)
        {
            if (Directory.Exists(f))
                zip.AddDirectory(f, Path.GetFileName(f));
            else if (File.Exists(f))
                zip.AddFile(f, "");
            else
                throw new FileNotFoundException($"추가할 파일을 찾을 수 없습니다: {f}", f);
        }
        zip.Save();
    }

    public static void Extract(
        string archivePath,
        string destDir,
        ExtractOptions? options = null,
        IEnumerable<string>? onlyEntries = null,
        Action<ProgressInfo>? progress = null)
    {
        options ??= new ExtractOptions();
        Directory.CreateDirectory(destDir);
        using var zip = ReadZip(archivePath, options);
        var names = onlyEntries != null
            ? new HashSet<string>(onlyEntries)
            : null;
        int total = names == null ? zip.Count : names.Count;
        int done = 0;
        foreach (var e in zip)
        {
            if (names != null && !names.Contains(e.FileName.Replace('\\', '/')))
                continue;
            progress?.Invoke(new ProgressInfo(e.FileName, done, total));
            e.Extract(destDir, options.Overwrite ? ExtractExistingFileAction.OverwriteSilently : ExtractExistingFileAction.DoNotOverwrite);
            done++;
        }
        progress?.Invoke(new ProgressInfo("", done, total));
    }

    /// <summary>CRC test without writing files. Returns (total, failed, firstErrors).</summary>
    public static (int Total, int Failed, List<string> Errors) Test(string archivePath, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        var errors = new List<string>();
        using var zip = ReadZip(archivePath, options);
        int failed = 0;
        foreach (var e in zip)
        {
            if (e.IsDirectory)
                continue;
            try
            {
                using var sink = Stream.Null;
                using var s = e.OpenReader(options.Password.Length > 0 ? options.Password : null);
                s.CopyTo(sink);
            }
            catch (Exception ex)
            {
                failed++;
                if (errors.Count < 10)
                    errors.Add($"{e.FileName}: {ex.Message}");
            }
        }
        return (zip.Count, failed, errors);
    }

    public static void SetComment(string archivePath, string comment, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        using var zip = ReadZip(archivePath, options);
        zip.Comment = comment ?? "";
        zip.Save();
    }

    public static void AddFiles(string archivePath, IEnumerable<string> files, CreateOptions? options = null)
    {
        options ??= new CreateOptions();
        using var zip = ReadZip(archivePath, new ExtractOptions());
        ApplyEncoding(zip, options.Encoding);
        ApplyCrypto(zip, options);
        foreach (var f in files)
        {
            if (Directory.Exists(f))
                zip.AddDirectory(f, Path.GetFileName(f));
            else if (File.Exists(f))
                zip.UpdateFile(f, "");
            else
                throw new FileNotFoundException($"추가할 파일을 찾을 수 없습니다: {f}", f);
        }
        zip.Save();
    }

    public static void DeleteEntries(string archivePath, IEnumerable<string> fullNames, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        using var zip = ReadZip(archivePath, options);
        foreach (var n in fullNames)
            zip.RemoveEntry(n);
        zip.Save();
    }

    public static void RenameEntry(string archivePath, string oldFullName, string newName, ExtractOptions? options = null)
    {
        options ??= new ExtractOptions();
        if (newName.Contains('/') || newName.Contains('\\'))
            throw new InvalidDataException("파일명에 경로를 포함할 수 없습니다.");
        using var zip = ReadZip(archivePath, options);
        var entry = zip.Entries.FirstOrDefault(e => e.FileName == oldFullName)
            ?? throw new FileNotFoundException($"항목을 찾을 수 없습니다: {oldFullName}");
        int slash = oldFullName.LastIndexOf('/');
        entry.FileName = slash < 0 ? newName : oldFullName[..(slash + 1)] + newName;
        zip.Save();
    }

    private static ZipFile ReadZip(string path, ExtractOptions options)
    {
        // NOTE: the alternate encoding must be supplied at Read time;
        // setting it afterwards does not re-decode the central directory.
        ReadOptions? readOptions = options.Encoding == NameEncoding.Utf8
            ? null
            : new ReadOptions { Encoding = EncodingHelper.Resolve(options.Encoding) };
        var zip = readOptions == null ? ZipFile.Read(path) : ZipFile.Read(path, readOptions);
        ApplyEncoding(zip, options.Encoding);
        if (options.Password.Length > 0)
            zip.Password = options.Password;
        return zip;
    }

    private static void ApplyEncoding(ZipFile zip, NameEncoding enc)
    {
        if (enc == NameEncoding.Utf8)
        {
            zip.AlternateEncodingUsage = ZipOption.Never;
            return;
        }
        zip.AlternateEncoding = EncodingHelper.Resolve(enc);
        zip.AlternateEncodingUsage = ZipOption.AsNecessary;
    }

    private static void ApplyCrypto(ZipFile zip, CreateOptions options)
    {
        switch (options.Crypto)
        {
            case ZipCrypto.None:
                zip.Encryption = EncryptionAlgorithm.None;
                zip.Password = null;
                break;
            case ZipCrypto.Zip20:
                RequirePassword(options);
                zip.Encryption = EncryptionAlgorithm.PkzipWeak;
                zip.Password = options.Password;
                break;
            case ZipCrypto.Aes128:
                RequirePassword(options);
                zip.Encryption = EncryptionAlgorithm.WinZipAes128;
                zip.Password = options.Password;
                break;
            case ZipCrypto.Aes256:
                RequirePassword(options);
                zip.Encryption = EncryptionAlgorithm.WinZipAes256;
                zip.Password = options.Password;
                break;
        }
    }

    private static void RequirePassword(CreateOptions options)
    {
        if (string.IsNullOrEmpty(options.Password))
            throw new InvalidOperationException("암호를 입력하세요.");
    }

    private static Ionic.Zlib.CompressionLevel MapLevel(CompressLevel level) => level switch
    {
        CompressLevel.StoreOnly => Ionic.Zlib.CompressionLevel.None,
        CompressLevel.Fast => Ionic.Zlib.CompressionLevel.BestSpeed,
        CompressLevel.Best => Ionic.Zlib.CompressionLevel.BestCompression,
        CompressLevel.Optimal => Ionic.Zlib.CompressionLevel.Default,
        _ => Ionic.Zlib.CompressionLevel.Level6,
    };
}

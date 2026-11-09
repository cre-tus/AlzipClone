namespace AlzipClone.Core;

/// <summary>Format-dispatching facade used by the UI.</summary>
public static class ArchiveService
{
    /// <summary>Opens any supported archive. Split sets are joined to a temp file first.</summary>
    public static (ArchiveModel Model, string? TempJoinedPath) Open(
        string path, ExtractOptions? options = null, NameEncoding encoding = NameEncoding.Utf8)
    {
        options ??= new ExtractOptions();
        var format = FormatDetector.Detect(path);
        switch (format)
        {
            case ArchiveFormat.Zip:
                return (ZipHandler.Open(path, options), null);
            case ArchiveFormat.Tar:
            case ArchiveFormat.Tgz:
            case ArchiveFormat.Gz:
                return (TarHandler.Open(path, encoding), null);
            case ArchiveFormat.Split:
            {
                string temp = Path.Combine(Path.GetTempPath(), "AlzipClone", Guid.NewGuid().ToString("N"));
                string joined = SplitManager.Join(path, temp);
                var inner = FormatDetector.Detect(joined);
                ArchiveModel model = inner switch
                {
                    ArchiveFormat.Tar or ArchiveFormat.Tgz or ArchiveFormat.Gz
                        => TarHandler.Open(joined, encoding),
                    _ => ZipHandler.Open(joined, options),
                };
                model = new ArchiveModel { FilePath = path, Format = ArchiveFormat.Split, Comment = model.Comment };
                // re-list from the joined file so entries stay valid
                var relist = inner switch
                {
                    ArchiveFormat.Tar or ArchiveFormat.Tgz or ArchiveFormat.Gz
                        => TarHandler.Open(joined, encoding),
                    _ => ZipHandler.Open(joined, options),
                };
                foreach (var e in relist.Entries)
                    model.Entries.Add(e);
                model.Comment = relist.Comment;
                return (model, joined);
            }
            case ArchiveFormat.Egg:
                return (EggHandler.Open(path, options), null);
            case ArchiveFormat.Alz:
                return (AlzHandler.Open(path, options), null);
            case ArchiveFormat.Rar:
            case ArchiveFormat.SevenZ:
                return (ForeignHandler.Open(path, options), null);
            case ArchiveFormat.Cab:
                return (CabHandler.Open(path, options), null);
            default:
                throw new NotSupportedException("지원하지 않는 압축 형식입니다.");
        }
    }

    public static void Extract(
        ArchiveModel model,
        string destDir,
        ExtractOptions? options = null,
        IEnumerable<string>? onlyEntries = null,
        Action<ProgressInfo>? progress = null,
        string? joinedTempPath = null)
    {
        options ??= new ExtractOptions();
        string realPath = joinedTempPath ?? model.FilePath;
        var inner = model.Format == ArchiveFormat.Split ? FormatDetector.Detect(realPath) : model.Format;
        switch (inner)
        {
            case ArchiveFormat.Zip:
                ZipHandler.Extract(realPath, destDir, options, onlyEntries, progress);
                break;
            case ArchiveFormat.Tar:
            case ArchiveFormat.Tgz:
            case ArchiveFormat.Gz:
                TarHandler.Extract(realPath, destDir, onlyEntries, progress, options.Overwrite);
                break;
            case ArchiveFormat.Egg:
                EggHandler.Extract(realPath, destDir, options, onlyEntries, progress);
                break;
            case ArchiveFormat.Alz:
                AlzHandler.Extract(realPath, destDir, options, onlyEntries, progress);
                break;
            case ArchiveFormat.Rar:
            case ArchiveFormat.SevenZ:
                ForeignHandler.Extract(realPath, destDir, options, onlyEntries, progress);
                break;
            case ArchiveFormat.Cab:
                CabHandler.Extract(realPath, destDir, options, onlyEntries, progress);
                break;
            default:
                throw new NotSupportedException("지원하지 않는 압축 형식입니다.");
        }
        var info = new FileInfo(model.FilePath);
        AppDataStore.AddHistory(new HistoryRow(info.Name, DateTime.Now, info.Exists ? info.Length : 0, destDir));
        AppDataStore.PushMru(model.FilePath);
    }

    public static (int Total, int Failed, List<string> Errors) Test(
        ArchiveModel model, ExtractOptions? options = null, string? joinedTempPath = null)
    {
        options ??= new ExtractOptions();
        string realPath = joinedTempPath ?? model.FilePath;
        var inner = model.Format == ArchiveFormat.Split ? FormatDetector.Detect(realPath) : model.Format;
        return inner switch
        {
            ArchiveFormat.Zip => ZipHandler.Test(realPath, options),
            ArchiveFormat.Tar or ArchiveFormat.Tgz or ArchiveFormat.Gz => TarHandler.Test(realPath),
            ArchiveFormat.Egg => EggHandler.Test(realPath, options),
            ArchiveFormat.Alz => AlzHandler.Test(realPath, options),
            ArchiveFormat.Rar or ArchiveFormat.SevenZ
                => ForeignHandler.Test(realPath, options),
            ArchiveFormat.Cab => CabHandler.Test(realPath, options),
            _ => throw new NotSupportedException("지원하지 않는 압축 형식입니다."),
        };
    }

    public static void Create(
        string destPath,
        IEnumerable<string> files,
        ArchiveFormat format,
        CreateOptions? options = null,
        Action<ProgressInfo>? progress = null)
    {
        options ??= new CreateOptions();
        switch (format)
        {
            case ArchiveFormat.Zip:
                ZipHandler.Create(destPath, files, options);
                break;
            case ArchiveFormat.Tar:
            case ArchiveFormat.Tgz:
            case ArchiveFormat.Gz:
                TarHandler.Create(destPath, files, format, options.Level);
                break;
            default:
                throw new NotSupportedException("지원하지 않는 압축 형식입니다.");
        }
        progress?.Invoke(new ProgressInfo("", 1, 1));
        AppDataStore.PushMru(destPath);
    }

    public static void RemoveTemp(string? joinedTempPath)
    {
        if (string.IsNullOrEmpty(joinedTempPath))
            return;
        try
        {
            string? dir = Path.GetDirectoryName(joinedTempPath);
            if (dir != null && dir.Contains("AlzipClone"))
                Directory.Delete(dir, true);
        }
        catch (Exception)
        {
            // best effort
        }
    }
}

using System.Text.Json;

namespace AlzipClone.Core;

/// <summary>App settings (observed ALZip Config subset), stored as JSON.</summary>
public sealed class AppSettings
{
    public ArchiveFormat DefaultFormat { get; set; } = ArchiveFormat.Zip;
    public CompressLevel DefaultLevel { get; set; } = CompressLevel.Normal;
    public NameEncoding DefaultEncoding { get; set; } = NameEncoding.Cp949;
    public string DefaultFolder { get; set; } = "";
    public bool CreateFolderOnExtract { get; set; } = true;
    public bool DeleteArchiveAfterExtract { get; set; } = false;
    public bool OpenFolderAfterExtract { get; set; } = true;
    public bool ShowLeftPane { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowCommentPane { get; set; } = true;
    public bool ShowColumnPackedSize { get; set; } = true;
    public bool ShowColumnRatio { get; set; } = true;
    public bool ShowColumnModifiedDate { get; set; } = true;
    public bool ShowColumnMethod { get; set; } = false;
    public bool ShowColumnCrc { get; set; } = false;
    public bool AutoCloseAfterCompress { get; set; } = false;
    public bool AutoCloseAfterExtract { get; set; } = false;
    public bool TestAfterCompress { get; set; } = false;

    public string ViewMode { get; set; } = "LargeIcon";
}

/// <summary>One extraction-history row (observed 이력보기 columns).</summary>
public sealed record HistoryRow(string Name, DateTime Date, long ArchiveBytes, string Location);

public static class AppDataStore
{
    public static string AppDir
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AlzipClone");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings LoadSettings()
    {
        string path = Path.Combine(AppDir, "settings.json");
        if (!File.Exists(path))
            return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        File.WriteAllText(Path.Combine(AppDir, "settings.json"), JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static List<string> LoadMru()
    {
        string path = Path.Combine(AppDir, "mru.json");
        if (!File.Exists(path))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new List<string>();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    public static void PushMru(string archivePath, int max = 9)
    {
        var list = LoadMru();
        list.Remove(archivePath);
        list.Insert(0, archivePath);
        while (list.Count > max)
            list.RemoveAt(list.Count - 1);
        File.WriteAllText(Path.Combine(AppDir, "mru.json"), JsonSerializer.Serialize(list, JsonOptions));
    }

    public static void ClearMru() => File.WriteAllText(
        Path.Combine(AppDir, "mru.json"), JsonSerializer.Serialize(new List<string>(), JsonOptions));

    public static List<HistoryRow> LoadHistory()
    {
        string path = Path.Combine(AppDir, "history.json");
        if (!File.Exists(path))
            return new List<HistoryRow>();
        try
        {
            return JsonSerializer.Deserialize<List<HistoryRow>>(File.ReadAllText(path)) ?? new List<HistoryRow>();
        }
        catch (Exception)
        {
            return new List<HistoryRow>();
        }
    }

    public static void AddHistory(HistoryRow row, int max = 100)
    {
        var list = LoadHistory();
        list.Insert(0, row);
        while (list.Count > max)
            list.RemoveAt(list.Count - 1);
        File.WriteAllText(Path.Combine(AppDir, "history.json"), JsonSerializer.Serialize(list, JsonOptions));
    }
}

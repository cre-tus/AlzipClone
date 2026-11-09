namespace AlzipClone.Core;

/// <summary>Which formats support in-place editing (add/delete/rename/comment/password).</summary>
public static class ArchiveCapabilities
{
    public static bool IsWritable(ArchiveFormat format) => format == ArchiveFormat.Zip;
}

/// <summary>Byte counts formatted like ALZip (1024-based, 1 decimal).</summary>
public static class ByteFormat
{
    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}바이트",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1048576.0:F1}MB",
        _ => $"{bytes / 1073741824.0:F2}GB",
    };
}

/// <summary>Status-bar texts, matching ALZip's two-part layout.</summary>
public static class StatusText
{
    public const string EmptyHint = "새 압축파일을 만들려면 \"새로압축\"을 선택하세요";

    public static string Selection(int files, long bytes) =>
        $"{files} 파일 선택, {ByteFormat.Format(bytes)}";

    public static string Total(int files, long bytes) =>
        $"총 {files}파일, {ByteFormat.Format(bytes)}";
}

/// <summary>Entry display helpers shared by the list view and the sorter.</summary>
public static class EntryText
{
    public static string Kind(ArchiveEntry e)
    {
        if (e.IsDirectory)
            return "폴더";
        string ext = Path.GetExtension(e.Name);
        return string.IsNullOrEmpty(ext) ? "파일" : ext.TrimStart('.').ToUpperInvariant() + " 파일";
    }

    public static string Dir(ArchiveEntry e)
    {
        string t = e.FullName.TrimEnd('/');
        int i = t.LastIndexOf('/');
        return i < 0 ? "" : t[..i];
    }

    public static string ParentDir(string dir)
    {
        if (string.IsNullOrEmpty(dir))
            return "";
        int i = dir.LastIndexOf('/');
        return i < 0 ? "" : dir[..i];
    }
}

/// <summary>
/// Entry ordering: null (the ".." row) always first, folders before files,
/// then the requested key in the requested direction.
/// </summary>
public static class EntrySort
{
    public static int Compare(ArchiveEntry? x, ArchiveEntry? y, string key, bool ascending,
        IReadOnlyList<ArchiveEntry>? modelOrder = null)
    {
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;
        if (x.IsDirectory != y.IsDirectory)
            return x.IsDirectory ? -1 : 1;
        int c = key switch
        {
            "압축크기" => x.PackedSize.CompareTo(y.PackedSize),
            "원본크기" => x.Size.CompareTo(y.Size),
            "압축률" => x.Ratio.CompareTo(y.Ratio),
            "종류" => string.Compare(EntryText.Kind(x), EntryText.Kind(y), StringComparison.Ordinal),
            "수정한 날짜" => x.Modified.CompareTo(y.Modified),
            "경로" => string.Compare(EntryText.Dir(x), EntryText.Dir(y), StringComparison.Ordinal),
            "압축방식" => string.Compare(x.Method, y.Method, StringComparison.Ordinal),
            "CRC" => x.Crc.CompareTo(y.Crc),
            "설명" => string.Compare(x.Comment, y.Comment, StringComparison.Ordinal),
            "원본 순서" => IndexOf(modelOrder, x).CompareTo(IndexOf(modelOrder, y)),
            _ => string.Compare(x.Name, y.Name, StringComparison.Ordinal),
        };
        if (c == 0)
            c = string.Compare(x.FullName, y.FullName, StringComparison.Ordinal);
        return ascending ? c : -c;
    }

    private static int IndexOf(IReadOnlyList<ArchiveEntry>? list, ArchiveEntry e)
    {
        if (list == null)
            return 0;
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], e))
                return i;
        return int.MaxValue;
    }
}

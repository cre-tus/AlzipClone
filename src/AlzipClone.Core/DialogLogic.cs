namespace AlzipClone.Core;

/// <summary>Pure request builders behind the dialogs (UI-thin, fully unit-testable).</summary>
public static class DialogLogic
{
    public sealed record NewArchiveRequest(
        string DestPath,
        List<string> Files,
        ArchiveFormat Format,
        CompressLevel Level,
        ZipCrypto Crypto,
        string Password,
        NameEncoding Encoding,
        long SplitBytes);

    public static (NewArchiveRequest? Request, string? Error) BuildNewArchiveRequest(
        string destPath,
        IEnumerable<string> files,
        int formatIndex,
        int levelIndex,
        int splitIndex,
        string splitCustomMb,
        ZipCrypto crypto,
        string password)
    {
        var list = files.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
        if (list.Count == 0)
            return (null, "압축할 파일을 추가하세요.");
        if (string.IsNullOrWhiteSpace(destPath))
            return (null, "파일명을 입력하세요.");
        if (crypto != ZipCrypto.None && string.IsNullOrEmpty(password))
            return (null, "암호를 입력하세요.");
        if (formatIndex is < 0 or > 3)
            return (null, "압축포맷을 선택하세요.");
        if (!TryParseSplitPreset(splitIndex, splitCustomMb, out long splitBytes))
            return (null, "분할크기를 확인하세요.");
        return (new NewArchiveRequest(
            destPath.Trim(),
            list,
            (ArchiveFormat)(formatIndex + 1),
            (CompressLevel)Math.Clamp(levelIndex, 0, 4),
            crypto,
            password,
            NameEncoding.Cp949,
            splitBytes), null);
    }

    public static bool TryParseSplitPreset(int splitIndex, string customMb, out long bytes)
    {
        bytes = splitIndex switch
        {
            0 => 0,
            1 => 10L * 1024 * 1024,
            2 => 25L * 1024 * 1024,
            3 => 50L * 1024 * 1024,
            4 => 700L * 1024 * 1024,
            5 => (long)(4.7 * 1024 * 1024 * 1024),
            6 when long.TryParse(customMb, out long mb) && mb > 0 => mb * 1024 * 1024,
            _ => -1,
        };
        return bytes >= 0;
    }

    public static (string DestDir, string? Error) BuildExtractDest(string destInput, bool createNamedFolder, string archivePath)
    {
        if (string.IsNullOrWhiteSpace(destInput))
            return ("", "압축 풀 위치를 입력하세요.");
        string dest = destInput.Trim();
        if (createNamedFolder)
            dest = Path.Combine(dest, Path.GetFileNameWithoutExtension(archivePath));
        return (dest, null);
    }
}

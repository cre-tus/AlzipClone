using AlzipClone.Core;

namespace AlzipClone.Tests;

/// <summary>Read-only foreign formats (CAB via makecab, 7Z via 7zr,
/// RAR4/5 corpus files) under %TEMP%\alzip-test\foreign.</summary>
public sealed class ForeignInteropTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acfgn-" + Guid.NewGuid().ToString("N"));
    public ForeignInteropTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Fact]
    public void Reads_Makecab_Cab()
    {
        string? sample = FindForeign("sample.cab");
        if (sample == null)
            return;
        Assert.Equal(ArchiveFormat.Cab, FormatDetector.Detect(sample));
        var model = CabHandler.Open(sample);
        Assert.Equal(2, model.Entries.Count);
        string dest = Path.Combine(_dir, "cab");
        CabHandler.Extract(sample, dest);
        var files = Directory.GetFiles(dest, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
        Assert.Contains(files, f => File.ReadAllText(f) == "cab root file");
        Assert.Contains(files, f => File.ReadAllText(f) == "cab nested file");
        var (total, failed, _) = CabHandler.Test(sample);
        Assert.Equal(2, total);
        Assert.Equal(0, failed);
    }

    [Fact]
    public void Reads_7zr_SevenZ()
    {
        string? sample = FindForeign("sample.7z");
        if (sample == null)
            return;
        Assert.Equal(ArchiveFormat.SevenZ, FormatDetector.Detect(sample));
        var model = ForeignHandler.Open(sample);
        Assert.Contains(model.Entries, e => e.FullName == "root.txt" && e.Size == 13);
        Assert.Contains(model.Entries, e => e.FullName == "sub/nested.txt" && e.Size == 15);
        string dest = Path.Combine(_dir, "sz");
        ForeignHandler.Extract(sample, dest);
        Assert.Equal("cab root file", File.ReadAllText(Path.Combine(dest, "root.txt")));
        Assert.Equal("cab nested file", File.ReadAllText(Path.Combine(dest, "sub", "nested.txt")));
    }

    [Theory]
    [InlineData("sample-rar4.rar")]
    [InlineData("sample-rar5.rar")]
    public void Reads_Rar_Corpus(string name)
    {
        string? sample = FindForeign(name);
        if (sample == null)
            return;
        Assert.Equal(ArchiveFormat.Rar, FormatDetector.Detect(sample));
        var model = ForeignHandler.Open(sample);
        Assert.True(model.Entries.Count > 0);
        Assert.All(model.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.FullName)));
        string dest = Path.Combine(_dir, "r-" + name);
        ForeignHandler.Extract(sample, dest);
        Assert.True(Directory.GetFiles(dest, "*", SearchOption.AllDirectories).Length > 0);
        var (_, failed, _) = ForeignHandler.Test(sample);
        Assert.Equal(0, failed);
    }

    private static string? FindForeign(string name)
    {
        string p = Path.Combine(Path.GetTempPath(), "alzip-test", "foreign", name);
        return File.Exists(p) ? p : null;
    }
}

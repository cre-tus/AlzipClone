using AlzipClone.Core;

namespace AlzipClone.Tests;

public sealed class TarAndSplitTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acz-test-" + Guid.NewGuid().ToString("N"));

    public TarAndSplitTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Theory]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.Tgz)]
    public void Tar_Roundtrip(ArchiveFormat format)
    {
        string a = Path.Combine(_dir, "f.txt");
        File.WriteAllText(a, "tar content");
        string ext = format == ArchiveFormat.Tar ? ".tar" : ".tgz";
        string archive = Path.Combine(_dir, "a" + ext);

        TarHandler.Create(archive, new[] { a }, format);
        var model = TarHandler.Open(archive);
        Assert.Equal(format, model.Format);
        Assert.Contains(model.Entries, e => e.FullName == "f.txt");

        string dest = Path.Combine(_dir, "out");
        TarHandler.Extract(archive, dest);
        Assert.Equal("tar content", File.ReadAllText(Path.Combine(dest, "f.txt")));

        var (total, failed, _) = TarHandler.Test(archive);
        Assert.Equal(1, total);
        Assert.Equal(0, failed);
    }

    [Fact]
    public void Gz_Single_File_Roundtrip()
    {
        string a = Path.Combine(_dir, "g.txt");
        File.WriteAllText(a, "gzip me");
        string archive = Path.Combine(_dir, "g.txt.gz");
        TarHandler.Create(archive, new[] { a }, ArchiveFormat.Gz);
        var model = TarHandler.Open(archive);
        Assert.Equal(ArchiveFormat.Gz, model.Format);
        string dest = Path.Combine(_dir, "gout");
        TarHandler.Extract(archive, dest);
        Assert.Equal("gzip me", File.ReadAllText(Path.Combine(dest, "g.txt")));
    }

    [Fact]
    public void Split_Join_Verifies_Hash()
    {
        var rnd = new Random(7);
        var data = new byte[3 * 1024 * 1024 + 123];
        rnd.NextBytes(data);
        string src = Path.Combine(_dir, "big.bin");
        File.WriteAllBytes(src, data);
        string expected = SplitManager.ComputeSha256(src);

        var volumes = SplitManager.Split(src, Path.Combine(_dir, "vols"), 1024 * 1024);
        Assert.Equal(4, volumes.Count);

        string joined = SplitManager.Join(volumes[0], Path.Combine(_dir, "joined"));
        Assert.Equal(expected, SplitManager.ComputeSha256(joined));
    }

    [Fact]
    public void Join_Detects_Tampering()
    {
        string src = Path.Combine(_dir, "s.bin");
        File.WriteAllBytes(src, new byte[] { 1, 2, 3, 4, 5 });
        var volumes = SplitManager.Split(src, Path.Combine(_dir, "v"), 2);
        File.WriteAllBytes(volumes[0], new byte[] { 9, 9 });
        Assert.Throws<InvalidDataException>(() => SplitManager.Join(volumes[0], Path.Combine(_dir, "j")));
    }

    [Fact]
    public void FormatDetector_Magics()
    {
        string zip = Path.Combine(_dir, "x.zip");
        ZipHandler.Create(zip, new[] { WriteA() });
        Assert.Equal(ArchiveFormat.Zip, FormatDetector.Detect(zip));

        string gz = Path.Combine(_dir, "x.gz");
        TarHandler.Create(gz, new[] { WriteA() }, ArchiveFormat.Gz);
        Assert.Equal(ArchiveFormat.Gz, FormatDetector.Detect(gz));

        string tar = Path.Combine(_dir, "x.tar");
        TarHandler.Create(tar, new[] { WriteA() }, ArchiveFormat.Tar);
        Assert.Equal(ArchiveFormat.Tar, FormatDetector.Detect(tar));

        string egg = Path.Combine(_dir, "x.egg");
        File.WriteAllBytes(egg, new byte[] { (byte)'E', (byte)'G', (byte)'G', (byte)'A', 0, 1 });
        Assert.Equal(ArchiveFormat.Egg, FormatDetector.Detect(egg));

        string big = Path.Combine(_dir, "big.bin");
        File.WriteAllBytes(big, new byte[10]);
        var volumes = SplitManager.Split(big, Path.Combine(_dir, "sv"), 2);
        Assert.True(volumes.Count >= 2);
        Assert.Equal(ArchiveFormat.Split, FormatDetector.Detect(volumes[1]));
    }

    private string WriteA()
    {
        string a = Path.Combine(_dir, "a.txt");
        File.WriteAllText(a, "a");
        return a;
    }
}

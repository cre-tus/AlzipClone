using System.Security.Cryptography;
using AlzipClone.Core;
using AlzipClone.Core.Egg;

namespace AlzipClone.Tests;

/// <summary>Interop against the ALZip-produced sample matrix
/// (%TEMP%\alzip-test\egg-matrix). Each test silently passes when the
/// reference ALZip run that creates the sample has not been executed.</summary>
public sealed class EggMatrixTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acegm-" + Guid.NewGuid().ToString("N"));
    public EggMatrixTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Theory]
    [InlineData("egg-store.egg", "Store")]
    [InlineData("egg-fast.egg", "Deflate")]
    [InlineData("egg-normal.egg", "Deflate")]
    [InlineData("egg-optimal.egg", "Deflate")]
    public void Reads_Matrix_Methods(string name, string method)
    {
        string? sample = FindMatrixSample(name);
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        var entry = Assert.Single(model.Entries);
        Assert.Equal("egg-probe-big.dat", entry.FullName);
        Assert.Equal(3145728, entry.Size);
        Assert.Contains(method, entry.Method);
        string dest = Path.Combine(_dir, "m-" + name);
        EggHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(3145728, new FileInfo(extracted).Length);
    }

    [Fact]
    public void Reads_Matrix_Best_Is_Method4()
    {
        string? sample = FindMatrixSample("egg-best.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.Contains("LZMA", Assert.Single(model.Entries).Method);
    }

    [Fact]
    public void Extracts_Matrix_Korean_Name()
    {
        string? sample = FindMatrixSample("egg-kor.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.Equal("알집한글테스트.txt", Assert.Single(model.Entries).FullName);
        string dest = Path.Combine(_dir, "kor");
        EggHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal("korean filename probe", File.ReadAllText(extracted));
    }

    [Theory]
    [InlineData("egg-aes256.egg")]
    [InlineData("egg-aes128.egg")]
    [InlineData("egg-zip2.egg")]
    public void Extracts_Matrix_Crypto(string name)
    {
        string? sample = FindMatrixSample(name);
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.True(Assert.Single(model.Entries).Encrypted);
        string dest = Path.Combine(_dir, "c-" + name);
        EggHandler.Extract(sample, dest, new ExtractOptions(Password: "test1234"));
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal("hello alzip", File.ReadAllText(extracted));
    }

    [Theory]
    [InlineData("egg-aes256.egg")]
    [InlineData("egg-aes128.egg")]
    [InlineData("egg-zip2.egg")]
    public void Matrix_Crypto_Wrong_Password_Fails(string name)
    {
        string? sample = FindMatrixSample(name);
        if (sample == null)
            return;
        Assert.Throws<EggPasswordException>(() =>
            EggHandler.Extract(sample, Path.Combine(_dir, "w-" + name),
                new ExtractOptions(Password: "wrong-password")));
    }

    [Fact]
    public void Matrix_Lea_Detected_Unsupported()
    {
        string? sample = FindMatrixSample("egg-lea128.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.True(Assert.Single(model.Entries).Encrypted);
        var ex = Assert.Throws<EggUnsupportedException>(() =>
            EggHandler.Extract(sample, Path.Combine(_dir, "lea"),
                new ExtractOptions(Password: "test1234")));
        Assert.Contains("LEA", ex.Message);
    }

    [Fact]
    public void Extracts_Matrix_AesBig()
    {
        string? sample = FindMatrixSample("egg-aesbig.egg");
        string? original = FindAlzipSample("big.bin");
        if (sample == null || original == null)
            return;
        string dest = Path.Combine(_dir, "aesbig");
        EggHandler.Extract(sample, dest, new ExtractOptions(Password: "test1234"));
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    [Fact]
    public void Extracts_Matrix_Solid_Fast()
    {
        string? sample = FindMatrixSample("egg-solid-fast.egg");
        if (sample == null)
            return;
        string dest = Path.Combine(_dir, "solidfast");
        EggHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal("hello alzip", File.ReadAllText(extracted));
    }

    [Fact]
    public void Extracts_Matrix_Bzip2()
    {
        string? sample = FindMatrixSample("egg-text-optimal.egg");
        string? original = FindAlzipSample("text-oracle.txt");
        if (sample == null || original == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.Contains("BZip2", Assert.Single(model.Entries).Method);
        string dest = Path.Combine(_dir, "bz2");
        EggHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    [Fact]
    public void Reads_Matrix_Text_Best_Is_Method4()
    {
        string? sample = FindMatrixSample("egg-text-best.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.Contains("LZMA", Assert.Single(model.Entries).Method);
    }

    [Fact]
    public void Extracts_Matrix_Tree()
    {
        string? sample = FindMatrixSample("egg-tree.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        // Reference writer stores flat '/'-joined names, no dir records.
        Assert.Equal(2, model.Entries.Count);
        Assert.Contains(model.Entries, e => e.FullName == "egg-tree/root.txt");
        Assert.Contains(model.Entries, e => e.FullName == "egg-tree/sub/nested.txt");
        string dest = Path.Combine(_dir, "tree");
        EggHandler.Extract(sample, dest);
        var files = Directory.GetFiles(dest, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
        Assert.Contains(files, f => File.ReadAllText(f) == "root file");
        Assert.Contains(files, f => File.ReadAllText(f) == "nested file");
    }

    private static string? FindAlzipSample(string name)
    {
        string p = Path.Combine(Path.GetTempPath(), "alzip-test", name);
        return File.Exists(p) ? p : null;
    }

    private static string? FindMatrixSample(string name)
    {
        string p = Path.Combine(Path.GetTempPath(), "alzip-test", "egg-matrix", name);
        return File.Exists(p) ? p : null;
    }
}

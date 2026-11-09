using System.Security.Cryptography;
using System.Text;
using AlzipClone.Core;
using AlzipClone.Core.Alz;

namespace AlzipClone.Tests;

/// <summary>Interop against ALZip-produced ALZ samples
/// (%TEMP%\alzip-test\egg-matrix).</summary>
public sealed class AlzInteropTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acalz-" + Guid.NewGuid().ToString("N"));
    public AlzInteropTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Theory]
    [InlineData("alz-store.alz", "Deflate")] // ALZ 보통 (index fallback)
    [InlineData("alz-normal.alz", "Store")] // ALZ 묶기
    [InlineData("alz-best.alz", "Deflate")] // ALZ 낮음
    [InlineData("alz-optimal.alz", "Deflate")] // ALZ 보통
    public void Reads_Matrix_Methods(string name, string method)
    {
        string? sample = FindMatrixSample(name);
        string? original = FindAlzipSample("big.bin");
        if (sample == null || original == null)
            return;
        var model = AlzHandler.Open(sample);
        var entry = Assert.Single(model.Entries);
        Assert.Equal("egg-probe-big.dat", entry.FullName);
        Assert.Equal(3145728, entry.Size);
        Assert.Contains(method, entry.Method);
        string dest = Path.Combine(_dir, "m-" + name);
        AlzHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    [Fact]
    public void Reads_Matrix_Text_Deflate()
    {
        string? sample = FindMatrixSample("alz-text-optimal.alz");
        string? original = FindAlzipSample("text-oracle.txt");
        if (sample == null || original == null)
            return;
        var model = AlzHandler.Open(sample);
        Assert.Contains("Deflate", Assert.Single(model.Entries).Method);
        string dest = Path.Combine(_dir, "txt");
        AlzHandler.Extract(sample, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    [Fact]
    public void Reads_Matrix_Korean_Cp949()
    {
        string? sample = FindMatrixSample("alz-kor.alz");
        if (sample == null)
            return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var model = AlzHandler.Open(sample, new ExtractOptions(Encoding: NameEncoding.Cp949));
        Assert.Equal("알집한글테스트.txt", Assert.Single(model.Entries).FullName);
        string dest = Path.Combine(_dir, "kkor");
        AlzHandler.Extract(sample, dest, new ExtractOptions(Encoding: NameEncoding.Cp949));
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal("korean filename probe", File.ReadAllText(extracted));
    }

    [Theory]
    [InlineData("alz-pw.alz", "hello alzip")]
    public void Extracts_Matrix_Encrypted_Store(string name, string content)
    {
        string? sample = FindMatrixSample(name);
        if (sample == null)
            return;
        var model = AlzHandler.Open(sample);
        Assert.True(Assert.Single(model.Entries).Encrypted);
        string dest = Path.Combine(_dir, "p-" + name);
        AlzHandler.Extract(sample, dest, new ExtractOptions(Password: "test1234"));
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(content, File.ReadAllText(extracted));
    }

    [Fact]
    public void Extracts_Matrix_Encrypted_Deflate()
    {
        string? sample = FindMatrixSample("alz-pw2.alz");
        string? original = FindAlzipSample("text-oracle.txt");
        if (sample == null || original == null)
            return;
        string dest = Path.Combine(_dir, "pw2");
        AlzHandler.Extract(sample, dest, new ExtractOptions(Password: "test1234"));
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    [Theory]
    [InlineData("alz-pw.alz")]
    [InlineData("alz-pw2.alz")]
    public void Matrix_Wrong_Password_Fails(string name)
    {
        string? sample = FindMatrixSample(name);
        if (sample == null)
            return;
        Assert.Throws<AlzPasswordException>(() =>
            AlzHandler.Extract(sample, Path.Combine(_dir, "w-" + name),
                new ExtractOptions(Password: "wrong-password")));
    }

    [Fact]
    public void Extracts_Matrix_Tree()
    {
        string? sample = FindMatrixSample("alz-tree.alz");
        if (sample == null)
            return;
        var model = AlzHandler.Open(sample);
        Assert.Equal(2, model.Entries.Count);
        string dest = Path.Combine(_dir, "atree");
        AlzHandler.Extract(sample, dest);
        var files = Directory.GetFiles(dest, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void Opens_Original_Sample_Alz()
    {
        string? sample = FindMatrixSample("sample-alz.alz");
        if (sample == null)
            return;
        Assert.Equal(ArchiveFormat.Alz, FormatDetector.Detect(sample));
        var model = ArchiveService.Open(sample).Model;
        Assert.Equal("egg-probe-hello.txt", Assert.Single(model.Entries).FullName);
        string dest = Path.Combine(_dir, "orig");
        ArchiveService.Extract(model, dest);
        var extracted = Assert.Single(Directory.GetFiles(dest, "*", SearchOption.AllDirectories));
        Assert.Equal("hello alzip", File.ReadAllText(extracted));
    }

    [Fact]
    public void Method3_Throws_Unsupported()
    {
        // Synthetic BLZ record with method 3 (obfuscated deflate):
        // dispatch must fail honestly without a real sample.
        byte[] alz = Convert.FromHexString(
            "414C5A01" + "0A000000" +
            "424C5A01" + "0500" + "20" + "00000000" + "20" + "00" +
            "03" + "00" + "00000000" + "0100" + "0100" +
            "612E747874" + "00" +
            "434C5A01");
        string p = Path.Combine(_dir, "m3.alz");
        File.WriteAllBytes(p, alz);
        var model = AlzHandler.Open(p);
        Assert.Contains("Obfuscated", Assert.Single(model.Entries).Method);
        Assert.Throws<AlzUnsupportedException>(() =>
            AlzHandler.Extract(p, Path.Combine(_dir, "m3o")));
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

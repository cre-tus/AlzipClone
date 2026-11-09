using System.Security.Cryptography;
using AlzipClone.Core;
using AlzipClone.Core.Egg;

namespace AlzipClone.Tests;

public sealed class EggInteropTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acegg-" + Guid.NewGuid().ToString("N"));
    public EggInteropTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Fact]
    public void Parses_Spec_Binary_Sample()
    {
        // Section 4 binary sample: hello.txt holding 'hello' (store).
        // NOTE: the spec's printed CRC ends in 0x35 but CRC32('hello') is
        // 0x3610A686, so this vector is verified with CRC checking off.
        byte[] egg = Convert.FromHexString(
            "45474741" + "0001" + "01000000" + "00000000" + "2282E208" +
            "E390850A" + "00000000" + "0500000000000000" +
            "AC91850A" + "00" + "0900" + "68656C6C6F2E747874" +
            "0B95862C" + "00" + "0900" + "80957F1543DDC901" + "00" +
            "2282E208" +
            "130CB502" + "00" + "00" + "05000000" + "05000000" + "86A61035" +
            "2282E208" + "68656C6C6F" + "2282E208");
        string p = Path.Combine(_dir, "spec-sample.egg");
        File.WriteAllBytes(p, egg);
        using var arc = EggArchive.Open(p);
        Assert.False(arc.IsSolid);
        var file = Assert.Single(arc.Files);
        Assert.Equal("hello.txt", file.ResolvedName);
        Assert.Equal(5ul, file.Length);
        byte[] bytes = arc.GetFileBytes(file, null, verifyCrc: false);
        Assert.Equal("hello"u8.ToArray(), bytes);
    }

    [Fact]
    public void Resolves_Relative_Parent_Paths()
    {
        // Synthetic per spec: dir id=7 'mydir' + file id=9 relative to 7.
        byte[] egg = Convert.FromHexString(
            "45474741" + "0001" + "01000000" + "00000000" + "2282E208" +
            "E390850A" + "07000000" + "0000000000000000" +
            "AC91850A" + "00" + "0500" + "6D79646972" +
            "0B95862C" + "00" + "0900" + "0000000000000000" + "10" +
            "2282E208" +
            "E390850A" + "09000000" + "0300000000000000" +
            "AC91850A" + "20" + "0900" + "07000000" + "662E747874" +
            "2282E208" +
            "130CB502" + "00" + "00" + "03000000" + "03000000" + "C2412435" +
            "2282E208" + "616263" + "2282E208");
        string p = Path.Combine(_dir, "relative.egg");
        File.WriteAllBytes(p, egg);
        var model = EggHandler.Open(p);
        Assert.Equal(2, model.Entries.Count);
        Assert.Contains(model.Entries, e => e.FullName == "mydir" && e.IsDirectory);
        Assert.Contains(model.Entries, e => e.FullName == "mydir/f.txt" && e.Size == 3);
        string dest = Path.Combine(_dir, "rel");
        EggHandler.Extract(p, dest);
        Assert.Equal("abc", File.ReadAllText(Path.Combine(dest, "mydir", "f.txt")));
    }

    [Fact]
    public void Crc32_Matches_Known_Vector()
    {
        Assert.Equal(0x3610A686u, EggCrc32.Compute("hello"u8));
    }

    [Fact]
    public void Reads_Real_Converted_Egg()
    {
        string? sample = FindAlzipSample("test-converted.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.True(model.Entries.Count >= 1);
        string dest = Path.Combine(_dir, "conv");
        EggHandler.Extract(sample, dest);
        var files = Directory.GetFiles(dest, "*", SearchOption.AllDirectories);
        Assert.Equal(3, files.Length);
        string note = Assert.Single(files, f => Path.GetFileName(f) == "note.txt");
        byte[] raw = File.ReadAllBytes(note);
        if (raw.Length > 3 && raw[0] == 239 && raw[1] == 187 && raw[2] == 191) raw = raw[3..];
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("second file" + "\r" + "\n"), raw);
    }

    [Fact]
    public void Reads_Real_Solid_Egg()
    {
        string? sample = FindAlzipSample("made-solid.egg");
        if (sample == null)
            return;
        var model = EggHandler.Open(sample);
        Assert.True(model.Entries.Count >= 1);
        // made-solid.egg uses method 4 (LZMA-framed): listing works,
        // extraction fails with the honest unsupported error.
        var ex = Assert.Throws<EggUnsupportedException>(() =>
            EggHandler.Extract(sample, Path.Combine(_dir, "solid")));
        Assert.Contains("LZMA", ex.Message);
    }

    [Fact]
    public void Reads_Real_Split_Egg_Set()
    {
        string? vol1 = FindAlzipSample("big.vol1.egg");
        string? original = FindAlzipSample("big.bin");
        if (vol1 == null || original == null)
            return;
        var model = EggHandler.Open(vol1);
        Assert.True(model.Entries.Count >= 1);
        string dest = Path.Combine(_dir, "split");
        EggHandler.Extract(vol1, dest);
        var files = Directory.GetFiles(dest, "*", SearchOption.AllDirectories);
        var extracted = Assert.Single(files);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))));
    }

    private static string? FindAlzipSample(string name)
    {
        string p = Path.Combine(Path.GetTempPath(), "alzip-test", name);
        return File.Exists(p) ? p : null;
    }
}

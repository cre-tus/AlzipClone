using AlzipClone.Core;

namespace AlzipClone.Tests;

public sealed class ZipHandlerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acz-test-" + Guid.NewGuid().ToString("N"));

    public ZipHandlerTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    private string WriteFile(string name, string content)
    {
        string p = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Create_Open_Extract_Roundtrip()
    {
        string a = WriteFile("hello.txt", "hello clone");
        string b = WriteFile("sub/note.txt", "second");
        string zip = Path.Combine(_dir, "out.zip");

        ZipHandler.Create(zip, new[] { a, b }, new CreateOptions(Comment: "cmt"));
        var model = ZipHandler.Open(zip);

        Assert.Equal(ArchiveFormat.Zip, model.Format);
        Assert.Equal("cmt", model.Comment);
        Assert.Equal(2, model.FileCount);
        Assert.Contains(model.Entries, e => e.FullName == "hello.txt");

        string dest = Path.Combine(_dir, "out");
        ZipHandler.Extract(zip, dest);
        Assert.Equal("hello clone", File.ReadAllText(Path.Combine(dest, "hello.txt")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(dest, "note.txt")));
    }

    [Theory]
    [InlineData(ZipCrypto.Zip20)]
    [InlineData(ZipCrypto.Aes128)]
    [InlineData(ZipCrypto.Aes256)]
    public void Password_Roundtrip(ZipCrypto crypto)
    {
        string a = WriteFile("secret.txt", "classified");
        string zip = Path.Combine(_dir, $"pw-{(int)crypto}.zip");
        ZipHandler.Create(zip, new[] { a }, new CreateOptions(Crypto: crypto, Password: "test1234"));

        var model = ZipHandler.Open(zip);
        Assert.All(model.Entries.Where(e => !e.IsDirectory), e => Assert.True(e.Encrypted));

        string dest = Path.Combine(_dir, "pwout");
        ZipHandler.Extract(zip, dest, new ExtractOptions(Password: "test1234"));
        Assert.Equal("classified", File.ReadAllText(Path.Combine(dest, "secret.txt")));
    }

    [Fact]
    public void Wrong_Password_Fails()
    {
        string a = WriteFile("s.txt", "x");
        string zip = Path.Combine(_dir, "wp.zip");
        ZipHandler.Create(zip, new[] { a }, new CreateOptions(Crypto: ZipCrypto.Aes256, Password: "right"));
        Assert.ThrowsAny<Exception>(() =>
            ZipHandler.Extract(zip, Path.Combine(_dir, "bad"), new ExtractOptions(Password: "wrong")));
    }

    [Fact]
    public void Test_Detects_Ok()
    {
        string a = WriteFile("t.txt", "data");
        string zip = Path.Combine(_dir, "t.zip");
        ZipHandler.Create(zip, new[] { a });
        var (total, failed, _) = ZipHandler.Test(zip);
        Assert.Equal(1, total);
        Assert.Equal(0, failed);
    }

    [Fact]
    public void Add_And_Delete_Entries()
    {
        string a = WriteFile("a.txt", "a");
        string zip = Path.Combine(_dir, "m.zip");
        ZipHandler.Create(zip, new[] { a });
        string b = WriteFile("b.txt", "b");
        ZipHandler.AddFiles(zip, new[] { b });
        Assert.Equal(2, ZipHandler.Open(zip).FileCount);
        ZipHandler.DeleteEntries(zip, new[] { "a.txt" });
        var model = ZipHandler.Open(zip);
        Assert.Single(model.Entries);
        Assert.Equal("b.txt", model.Entries[0].FullName);
    }

    [Fact]
    public void RenameEntry_Updates_Name_And_Content()
    {
        string a = WriteFile("old.txt", "keep me");
        string zip = Path.Combine(_dir, "r.zip");
        ZipHandler.Create(zip, new[] { a });
        ZipHandler.RenameEntry(zip, "old.txt", "new.txt");
        var model = ZipHandler.Open(zip);
        Assert.Contains(model.Entries, e => e.FullName == "new.txt");
        Assert.DoesNotContain(model.Entries, e => e.FullName == "old.txt");
        string dest = Path.Combine(_dir, "rout");
        ZipHandler.Extract(zip, dest);
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(dest, "new.txt")));
        Assert.Throws<InvalidDataException>(() => ZipHandler.RenameEntry(zip, "new.txt", "sub/evil.txt"));
    }

    [Fact]
    public void SetComment_Persists()
    {
        string a = WriteFile("c.txt", "c");
        string zip = Path.Combine(_dir, "c.zip");
        ZipHandler.Create(zip, new[] { a });
        ZipHandler.SetComment(zip, "REA test comment");
        Assert.Equal("REA test comment", ZipHandler.Open(zip).Comment);
    }

    [Fact]
    public void Cp949_Filename_Roundtrip()
    {
        string a = WriteFile("한글파일.txt", "korean");
        string zip = Path.Combine(_dir, "k.zip");
        ZipHandler.Create(zip, new[] { a }, new CreateOptions(Encoding: NameEncoding.Cp949));
        var model = ZipHandler.Open(zip, new ExtractOptions(Encoding: NameEncoding.Cp949));
        Assert.Contains(model.Entries, e => e.FullName == "한글파일.txt");
        ZipHandler.Extract(zip, Path.Combine(_dir, "kout"), new ExtractOptions(Encoding: NameEncoding.Cp949));
        Assert.True(File.Exists(Path.Combine(_dir, "kout", "한글파일.txt")));
    }

    [Fact]
    public void Reads_Real_Alzip_Plain_Zip()
    {
        string? sample = FindAlzipSample("test.zip");
        if (sample == null)
            return; // interop sample absent; core roundtrips still cover behavior
        var model = ZipHandler.Open(sample);
        Assert.True(model.FileCount >= 2);
    }

    [Fact]
    public void Reads_Real_Alzip_Aes_Zip()
    {
        string? sample = FindAlzipSample("made-aes.zip");
        if (sample == null)
            return;
        var model = ZipHandler.Open(sample);
        Assert.True(model.Entries.Exists(e => e.Encrypted));
        string dest = Path.Combine(_dir, "interop");
        ZipHandler.Extract(sample, dest, new ExtractOptions(Password: "test1234"));
        Assert.True(Directory.GetFiles(dest, "*", SearchOption.AllDirectories).Length >= 1);
    }

    private static string? FindAlzipSample(string name)
    {
        string p = Path.Combine(Path.GetTempPath(), "alzip-test", name);
        return File.Exists(p) ? p : null;
    }
}

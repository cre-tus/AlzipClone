using System.Diagnostics;
using AlzipClone.Core;

namespace AlzipClone.Tests;

public sealed class SfxTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acsfx-" + Guid.NewGuid().ToString("N"));
    public SfxTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    private static string FindStub()
    {
        string? dir = Path.GetDirectoryName(typeof(SfxTests).Assembly.Location);
        while (dir != null)
        {
            string cand = Path.Combine(dir, "SfxStub.exe");
            if (File.Exists(cand))
                return cand;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("SfxStub.exe not found (build the solution first).");
    }

    [Fact]
    public void Build_And_Run_Sfx_Silently()
    {
        string stub = FindStub();
        string a = Path.Combine(_dir, "a.txt");
        string b = Path.Combine(_dir, "b.txt");
        File.WriteAllText(a, "sfx-alpha");
        File.WriteAllText(b, "sfx-beta");
        string exe = Path.Combine(_dir, "setup.exe");
        SfxBuilder.Create(exe, stub, new[] { a, b });
        Assert.True(SfxBuilder.IsSfx(exe));
        var (offset, length) = SfxBuilder.ReadTrailer(exe);
        Assert.True(offset > 100000); // stub precedes payload
        Assert.True(length > 0);

        string dest = Path.Combine(_dir, "out");
        RunSfx(exe, dest);
        Assert.Equal("sfx-alpha", File.ReadAllText(Path.Combine(dest, "a.txt")));
        Assert.Equal("sfx-beta", File.ReadAllText(Path.Combine(dest, "b.txt")));
    }

    [Fact]
    public void Build_And_Run_Sfx_With_Directory()
    {
        string stub = FindStub();
        string sub = Path.Combine(_dir, "tree", "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(_dir, "tree", "root.txt"), "sfx-root");
        File.WriteAllText(Path.Combine(sub, "nested.txt"), "sfx-nested");
        string exe = Path.Combine(_dir, "setup2.exe");
        SfxBuilder.Create(exe, stub, new[] { Path.Combine(_dir, "tree") });
        string dest = Path.Combine(_dir, "out2");
        RunSfx(exe, dest);
        Assert.Equal("sfx-root", File.ReadAllText(Path.Combine(dest, "tree", "root.txt")));
        Assert.Equal("sfx-nested", File.ReadAllText(Path.Combine(dest, "tree", "sub", "nested.txt")));
    }

    private static void RunSfx(string exe, string dest)
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"-o \"{dest}\" -y",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(proc.WaitForExit(60000));
        Assert.Equal(0, proc.ExitCode);
    }

    [Fact]
    public void Encrypted_Payload_Refused()
    {
        string a = Path.Combine(_dir, "a.txt");
        File.WriteAllText(a, "x");
        Assert.Throws<NotSupportedException>(() =>
            SfxBuilder.Create(Path.Combine(_dir, "x.exe"), "stub.exe", new[] { a },
                new CreateOptions(Crypto: ZipCrypto.Aes256, Password: "pw")));
    }

    [Fact]
    public void Non_Sfx_Rejected()
    {
        string a = Path.Combine(_dir, "a.txt");
        File.WriteAllText(a, "x");
        Assert.False(SfxBuilder.IsSfx(a));
        Assert.Throws<InvalidDataException>(() => SfxBuilder.ReadTrailer(a));
    }
}

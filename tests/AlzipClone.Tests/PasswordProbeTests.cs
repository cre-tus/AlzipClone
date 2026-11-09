using AlzipClone.Core;
using Ionic.Zip;

namespace AlzipClone.Tests;

public sealed class PasswordProbeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "acz-probe-" + Guid.NewGuid().ToString("N"));
    public PasswordProbeTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (Exception) { }
    }

    [Fact]
    public void Probe_Empty_Password_Exception_Type()
    {
        string a = Path.Combine(_dir, "s.txt");
        File.WriteAllText(a, "x");
        string zip = Path.Combine(_dir, "p.zip");
        ZipHandler.Create(zip, new[] { a }, new CreateOptions(Crypto: ZipCrypto.Aes256, Password: "right"));
        var ex = Record.Exception(() => ZipHandler.Extract(zip, Path.Combine(_dir, "o"), new ExtractOptions(Password: "")));
        Assert.NotNull(ex);
        File.WriteAllText(Path.Combine(_dir, "extype.txt"), ex.GetType().FullName + " :: " + ex.Message);
        Assert.IsType<BadPasswordException>(ex);
    }
}

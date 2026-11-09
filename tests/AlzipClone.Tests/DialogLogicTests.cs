using AlzipClone.Core;

namespace AlzipClone.Tests;

public sealed class DialogLogicTests
{
    [Fact]
    public void NewArchive_Request_Ok()
    {
        var (req, err) = DialogLogic.BuildNewArchiveRequest(
            "C:\\tmp\\a.zip", new[] { "C:\\tmp\\f.txt" }, 0, 2, 0, "", ZipCrypto.None, "");
        Assert.Null(err);
        Assert.NotNull(req);
        Assert.Equal(ArchiveFormat.Zip, req!.Format);
        Assert.Equal(CompressLevel.Normal, req.Level);
        Assert.Equal(0, req.SplitBytes);
    }

    [Fact]
    public void NewArchive_Requires_Files_And_Name()
    {
        var (_, e1) = DialogLogic.BuildNewArchiveRequest("a.zip", Array.Empty<string>(), 0, 2, 0, "", ZipCrypto.None, "");
        Assert.NotNull(e1);
        var (_, e2) = DialogLogic.BuildNewArchiveRequest("  ", new[] { "f" }, 0, 2, 0, "", ZipCrypto.None, "");
        Assert.NotNull(e2);
    }

    [Fact]
    public void NewArchive_Crypto_Requires_Password()
    {
        var (_, err) = DialogLogic.BuildNewArchiveRequest(
            "a.zip", new[] { "f" }, 0, 2, 0, "", ZipCrypto.Aes256, "");
        Assert.Equal("암호를 입력하세요.", err);
    }

    [Theory]
    [InlineData(0, "", 0)]
    [InlineData(1, "", 10485760)]
    [InlineData(2, "", 26214400)]
    [InlineData(6, "3", 3145728)]
    public void Split_Presets_Parse(int index, string custom, long expected)
    {
        Assert.True(DialogLogic.TryParseSplitPreset(index, custom, out long bytes));
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData(6, "")]
    [InlineData(6, "abc")]
    [InlineData(6, "0")]
    [InlineData(99, "")]
    public void Split_Invalid_Rejected(int index, string custom)
    {
        Assert.False(DialogLogic.TryParseSplitPreset(index, custom, out _));
    }

    [Fact]
    public void Extract_Dest_Builds_Named_Folder()
    {
        var (dest, err) = DialogLogic.BuildExtractDest("C:\\out", true, "C:\\arc\\made-aes.zip");
        Assert.Null(err);
        Assert.Equal(Path.Combine("C:\\out", "made-aes"), dest);
    }

    [Fact]
    public void Extract_Dest_Requires_Path()
    {
        var (_, err) = DialogLogic.BuildExtractDest(" ", true, "a.zip");
        Assert.NotNull(err);
    }
}

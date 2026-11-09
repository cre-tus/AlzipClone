using AlzipClone.Core;

namespace AlzipClone.Tests;

public sealed class ViewLogicTests
{
    [Fact]
    public void IsWritable_Only_Zip()
    {
        Assert.True(ArchiveCapabilities.IsWritable(ArchiveFormat.Zip));
        foreach (var f in new[] { ArchiveFormat.Rar, ArchiveFormat.SevenZ, ArchiveFormat.Egg,
                     ArchiveFormat.Alz, ArchiveFormat.Cab, ArchiveFormat.Tar,
                     ArchiveFormat.Tgz, ArchiveFormat.Gz, ArchiveFormat.Split,
                     ArchiveFormat.Unknown })
            Assert.False(ArchiveCapabilities.IsWritable(f));
    }

    [Theory]
    [InlineData(0, "0바이트")]
    [InlineData(42, "42바이트")]
    [InlineData(1023, "1023바이트")]
    [InlineData(1024, "1.0KB")]
    [InlineData(1536, "1.5KB")]
    [InlineData(1048576, "1.0MB")]
    [InlineData(1073741824, "1.00GB")]
    public void ByteFormat_Known_Values(long bytes, string expected)
    {
        Assert.Equal(expected, ByteFormat.Format(bytes));
    }

    [Fact]
    public void StatusText_Selection_And_Total()
    {
        Assert.Equal("0 파일 선택, 0바이트", StatusText.Selection(0, 0));
        Assert.Equal("1 파일 선택, 10바이트", StatusText.Selection(1, 10));
        Assert.Equal("총 4파일, 42바이트", StatusText.Total(4, 42));
    }

    private static ArchiveEntry File(string name, long size = 10) =>
        new(name, size, size, DateTime.Now, "Deflate", false, false);

    private static ArchiveEntry Dir(string name) =>
        new(name, 0, 0, DateTime.Now, "", true, false);

    [Fact]
    public void Sort_Null_Always_First()
    {
        var f = File("a.txt");
        Assert.True(EntrySort.Compare(null, f, "파일명", true) < 0);
        Assert.True(EntrySort.Compare(f, null, "파일명", true) > 0);
        Assert.True(EntrySort.Compare(null, f, "파일명", false) < 0);
        Assert.True(EntrySort.Compare(f, null, "파일명", false) > 0);
        Assert.Equal(0, EntrySort.Compare(null, null, "파일명", true));
    }

    [Fact]
    public void Sort_Folders_Before_Files_Both_Directions()
    {
        var dir = Dir("exe");
        var file = File("addme.txt"); // sorts before "exe" by name
        Assert.True(EntrySort.Compare(dir, file, "파일명", true) < 0);
        Assert.True(EntrySort.Compare(dir, file, "파일명", false) < 0);
    }

    [Fact]
    public void Sort_Name_Asc_Desc()
    {
        var a = File("a.txt");
        var b = File("b.txt");
        Assert.True(EntrySort.Compare(a, b, "파일명", true) < 0);
        Assert.True(EntrySort.Compare(a, b, "파일명", false) > 0);
    }

    [Fact]
    public void Sort_Size_Asc_Desc()
    {
        var small = File("a.txt", 10);
        var big = File("b.txt", 20);
        Assert.True(EntrySort.Compare(small, big, "원본크기", true) < 0);
        Assert.True(EntrySort.Compare(small, big, "원본크기", false) > 0);
    }

    [Fact]
    public void Sort_Original_Order_Uses_Model_Sequence()
    {
        var first = File("z.txt");
        var second = File("a.txt");
        var model = new List<ArchiveEntry> { first, second };
        Assert.True(EntrySort.Compare(first, second, "원본 순서", true, model) < 0);
        Assert.True(EntrySort.Compare(first, second, "원본 순서", false, model) > 0);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", "")]
    [InlineData("a/b", "a")]
    [InlineData("a/b/c", "a/b")]
    public void ParentDir_Known_Values(string dir, string expected)
    {
        Assert.Equal(expected, EntryText.ParentDir(dir));
    }

    [Fact]
    public void EntryText_Kind_And_Dir()
    {
        Assert.Equal("폴더", EntryText.Kind(Dir("exe")));
        Assert.Equal("TXT 파일", EntryText.Kind(File("sub/a.txt")));
        Assert.Equal("파일", EntryText.Kind(File("README")));
        Assert.Equal("sub", EntryText.Dir(File("sub/a.txt")));
        Assert.Equal("", EntryText.Dir(File("a.txt")));
    }
}

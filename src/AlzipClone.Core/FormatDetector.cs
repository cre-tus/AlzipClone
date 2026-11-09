using System.Text;

namespace AlzipClone.Core;

/// <summary>Detects archive format from magic bytes (never from the extension alone).</summary>
public static class FormatDetector
{
    public static ArchiveFormat Detect(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        // Own split sets (.volN + manifest, no trailing extension) never
        // collide with EGG volumes (.volN.egg), so this stays first: tiny
        // volumes may be shorter than the 4-byte magic read below.
        if (SplitManager.IsVolumeSet(path, out _))
            return ArchiveFormat.Split;

        Span<byte> head = stackalloc byte[8];
        using (var fs = File.OpenRead(path))
        {
            int read = fs.Read(head);
            if (read < 4)
                return ArchiveFormat.Unknown;
        }

        // EGGA magic (every volume of a .volN.egg set starts with one;
        // EggArchive joins volumes itself with header-skipping reads)
        if (head[0] == (byte)'E' && head[1] == (byte)'G' && head[2] == (byte)'G')
            return ArchiveFormat.Egg;

        // RAR4/RAR5, 7z, CAB magics (read-only foreign formats)
        if (head[0] == (byte)'R' && head[1] == (byte)'a' && head[2] == (byte)'r' && head[3] == (byte)'!')
            return ArchiveFormat.Rar;
        if (head[0] == (byte)'7' && head[1] == (byte)'z' && head[2] == 0xBC && head[3] == 0xAF)
            return ArchiveFormat.SevenZ;
        if (head[0] == (byte)'M' && head[1] == (byte)'S' && head[2] == (byte)'C' && head[3] == (byte)'F')
            return ArchiveFormat.Cab;
        // ALZ magic "ALZ\1"
        if (head[0] == (byte)'A' && head[1] == (byte)'L' && head[2] == (byte)'Z')
            return ArchiveFormat.Alz;
        // PK.. (zip, incl. empty archive PK\x05\x06 and spanned PK\x07\x08)
        if (head[0] == (byte)'P' && head[1] == (byte)'K')
            return ArchiveFormat.Zip;
        // gzip magic
        if (head[0] == 0x1F && head[1] == 0x8B)
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tbz", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".tbz2", StringComparison.OrdinalIgnoreCase))
                return ArchiveFormat.Tgz;
            return ArchiveFormat.Gz;
        }

        if (IsTar(path))
            return ArchiveFormat.Tar;
        return ArchiveFormat.Unknown;
    }

    private static bool IsTar(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 512 + 8)
                return false;
            fs.Seek(257, SeekOrigin.Begin);
            Span<byte> magic = stackalloc byte[8];
            return fs.Read(magic) == 8
                && magic[0] == (byte)'u' && magic[1] == (byte)'s'
                && magic[2] == (byte)'t' && magic[3] == (byte)'a'
                && magic[4] == (byte)'r';
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static string DefaultExtension(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => ".zip",
        ArchiveFormat.Tar => ".tar",
        ArchiveFormat.Tgz => ".tgz",
        ArchiveFormat.Gz => ".gz",
        ArchiveFormat.Egg => ".egg",
        ArchiveFormat.Alz => ".alz",
        _ => ".zip",
    };
}

/// <summary>Maps UI encoding choices to .NET encodings (registers the code-page provider).</summary>
public static class EncodingHelper
{
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
            return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _registered = true;
    }

    public static Encoding Resolve(NameEncoding enc)
    {
        EnsureRegistered();
        return enc switch
        {
            NameEncoding.Cp949 => Encoding.GetEncoding(949),
            NameEncoding.EucKr => Encoding.GetEncoding(51949),
            NameEncoding.ShiftJis => Encoding.GetEncoding(932),
            NameEncoding.Gb2312 => Encoding.GetEncoding(936),
            _ => Encoding.UTF8,
        };
    }

    public static string DisplayName(NameEncoding enc) => enc switch
    {
        NameEncoding.Utf8 => "유니코드(UTF-8)",
        NameEncoding.Cp949 => "한국어",
        NameEncoding.EucKr => "한국어(euc-kr)",
        NameEncoding.ShiftJis => "일본어(Shift-JIS)",
        NameEncoding.Gb2312 => "중국어 간체(GB2312)",
        _ => enc.ToString(),
    };
}

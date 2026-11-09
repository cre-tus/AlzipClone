// Self-extracting stub. Depends only on the .NET shared framework
// (System.IO.Compression) so the generated SFX stays a single file.
// Payload layout: [this stub][zip bytes][u64 offset][u64 length]["ACZSFX01"]
// CLI: SfxStub.exe -o <destDir> -y   (silent extract, for tests)
using System.IO.Compression;
using System.Text;

namespace SfxStub;

internal static class Program
{
    public static readonly byte[] Magic = "ACZSFX01"u8.ToArray();

    [STAThread]
    private static int Main(string[] args)
    {
        string? outDir = null;
        bool silent = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-o" && i + 1 < args.Length)
                outDir = args[++i];
            else if (args[i] == "-y")
                silent = true;
        }
        if (silent && outDir != null)
        {
            try
            {
                ExtractTo(Application.ExecutablePath, outDir);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new SfxForm(Application.ExecutablePath));
        return 0;
    }

    public static (long Offset, long Length) ReadTrailer(string sfxPath)
    {
        using var fs = File.OpenRead(sfxPath);
        if (fs.Length < 24)
            throw new InvalidDataException("SFX 형식이 아닙니다.");
        fs.Seek(-24, SeekOrigin.End);
        Span<byte> tail = stackalloc byte[24];
        fs.ReadExactly(tail);
        if (!tail[16..].SequenceEqual(Magic))
            throw new InvalidDataException("SFX 형식이 아닙니다.");
        long offset = BitConverter.ToInt64(tail[..8]);
        long length = BitConverter.ToInt64(tail.Slice(8, 8));
        if (offset < 0 || length <= 0 || offset + length + 24 != fs.Length)
            throw new InvalidDataException("SFX 페이로드가 손상되었습니다.");
        return (offset, length);
    }

    public static void ExtractTo(string sfxPath, string destDir, Action<string>? log = null)
    {
        var (offset, length) = ReadTrailer(sfxPath);
        Directory.CreateDirectory(destDir);
        using var fs = File.OpenRead(sfxPath);
        fs.Seek(offset, SeekOrigin.Begin);
        string temp = Path.Combine(Path.GetTempPath(), "AlzipCloneSfx", Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            using (var tmp = File.Create(temp))
                CopyBounded(fs, tmp, length);
            ZipFile.ExtractToDirectory(temp, destDir, Encoding.UTF8, overwriteFiles: true);
            log?.Invoke("압축 풀기가 완료되었습니다.");
        }
        finally
        {
            try { File.Delete(temp); } catch (Exception) { }
        }
    }

    private static void CopyBounded(Stream input, Stream output, long length)
    {
        byte[] buf = new byte[1024 * 256];
        while (length > 0)
        {
            int n = input.Read(buf, 0, (int)Math.Min(buf.Length, length));
            if (n <= 0)
                throw new EndOfStreamException();
            output.Write(buf, 0, n);
            length -= n;
        }
    }
}

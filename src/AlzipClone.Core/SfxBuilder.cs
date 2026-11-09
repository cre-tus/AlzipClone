namespace AlzipClone.Core;

/// <summary>Builds self-extracting archives: SfxStub.exe + plain ZIP
/// payload + 24-byte trailer. Encrypted payloads are refused because
/// the single-file stub cannot decrypt (System.IO.Compression only).</summary>
public static class SfxBuilder
{
    public static readonly byte[] Magic = "ACZSFX01"u8.ToArray();

    /// <summary>Locates the single-file stub shipped next to the app.</summary>
    public static string DefaultStubPath()
    {
        string cand = Path.Combine(AppContext.BaseDirectory, "SfxStub.exe");
        if (File.Exists(cand))
            return cand;
        throw new FileNotFoundException("SFX 스텁을 찾을 수 없습니다.", cand);
    }

    public static void Create(
        string destExe,
        string? stubExePath,
        IEnumerable<string> files,
        CreateOptions? options = null)
    {
        options ??= new CreateOptions();
        if (options.Crypto != ZipCrypto.None || options.Password.Length > 0)
            throw new NotSupportedException("SFX는 암호 없는 ZIP만 지원합니다 (원본도 암호화 SFX에서 충돌 확인).");
        stubExePath ??= DefaultStubPath();
        if (!File.Exists(stubExePath))
            throw new FileNotFoundException("SFX 스텁을 찾을 수 없습니다.", stubExePath);

        string tempZip = Path.Combine(Path.GetTempPath(), "AlzipCloneSfx",
            Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(tempZip)!);
        try
        {
            ZipHandler.Create(tempZip, files, options with { SplitBytes = 0 });
            byte[] stub = File.ReadAllBytes(stubExePath);
            byte[] payload = File.ReadAllBytes(tempZip);
            string? dir = Path.GetDirectoryName(destExe);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using var output = File.Create(destExe);
            output.Write(stub, 0, stub.Length);
            output.Write(payload, 0, payload.Length);
            output.Write(BitConverter.GetBytes((long)stub.Length));
            output.Write(BitConverter.GetBytes((long)payload.Length));
            output.Write(Magic, 0, Magic.Length);
        }
        finally
        {
            try { File.Delete(tempZip); } catch (Exception) { }
        }
        AppDataStore.PushMru(destExe);
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

    public static bool IsSfx(string path)
    {
        try
        {
            ReadTrailer(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

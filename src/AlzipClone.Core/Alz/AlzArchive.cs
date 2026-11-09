// ALZ reader. Record layout follows the publicly documented structure also
// implemented by the open-source XADMaster ALZip parser (LGPL, used here
// only as a format reference); all code below is written fresh for this
// project and verified byte-for-byte against archives produced by the
// reference ALZip 12.37. Multi-volume (.a01) sets are out of scope.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;

namespace AlzipClone.Core.Alz;

public sealed class AlzFormatException : Exception
{
    public AlzFormatException(string message) : base(message) { }
}

public sealed class AlzUnsupportedException : Exception
{
    public AlzUnsupportedException(string message) : base(message) { }
}

public sealed class AlzPasswordException : Exception
{
    public AlzPasswordException(string message) : base(message) { }
}

public sealed class AlzFile
{
    public string Name = "";
    public ulong UncompressedSize;
    public ulong CompressedSize;
    public uint Crc;
    public byte Method;
    public bool Encrypted;
    public bool IsDirectory;
    public DateTime Modified = DateTime.Now;
    public long DataOffset;
}

/// <summary>Parsed single-volume ALZ archive with random access extraction.</summary>
public sealed class AlzArchive : IDisposable
{
    private readonly Stream _stream;
    private readonly List<AlzFile> _files = new();

    public IReadOnlyList<AlzFile> Files => _files;

    private AlzArchive(Stream stream)
    {
        _stream = stream;
    }

    public static AlzArchive Open(string path, NameEncoding encoding = NameEncoding.Cp949)
    {
        var stream = File.OpenRead(path);
        var arc = new AlzArchive(stream);
        try
        {
            arc.Parse(EncodingHelper.Resolve(encoding));
            return arc;
        }
        catch
        {
            arc.Dispose();
            throw;
        }
    }

    public void Dispose() => _stream.Dispose();

    private uint ReadU32()
    {
        Span<byte> buf = stackalloc byte[4];
        if (_stream.Read(buf) != 4)
            throw new AlzFormatException("ALZ 파일이 잘렸습니다.");
        return BinaryPrimitives.ReadUInt32LittleEndian(buf);
    }

    private ulong ReadNumber(int size)
    {
        Span<byte> buf = stackalloc byte[8];
        if (_stream.Read(buf[..size]) != size)
            throw new AlzFormatException("ALZ 파일이 잘렸습니다.");
        return size switch
        {
            1 => buf[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(buf),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(buf),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(buf),
            _ => throw new AlzFormatException($"잘못된 ALZ 크기 필드({size})."),
        };
    }

    private byte[] ReadBytes(long count)
    {
        if (count < 0 || count > int.MaxValue)
            throw new AlzFormatException($"잘못된 ALZ 크기({count}).");
        var buf = new byte[(int)count];
        int off = 0;
        while (off < buf.Length)
        {
            int got = _stream.Read(buf, off, buf.Length - off);
            if (got <= 0)
                throw new AlzFormatException("ALZ 파일이 잘렸습니다.");
            off += got;
        }
        return buf;
    }

    private void Parse(Encoding nameEncoding)
    {
        byte[] magic = ReadBytes(8);
        if (magic[0] != (byte)'A' || magic[1] != (byte)'L' || magic[2] != (byte)'Z' || magic[3] != 1)
            throw new AlzFormatException("ALZ 형식이 아닙니다.");
        while (true)
        {
            byte[] sig = ReadBytes(4);
            if (sig[0] == (byte)'B' && sig[1] == (byte)'L' && sig[2] == (byte)'Z' && sig[3] == 1)
            {
                ReadFileRecord(nameEncoding);
            }
            else if (sig[0] == (byte)'C' && sig[1] == (byte)'L' && sig[2] == (byte)'Z' && sig[3] == 1)
            {
                break; // end of central records
            }
            else if (sig[0] == (byte)'E' && sig[1] == (byte)'L' && sig[2] == (byte)'Z' && sig[3] == 1)
            {
                break; // comment block at end; not consumed
            }
            else
            {
                throw new AlzFormatException($"알 수 없는 ALZ 레코드({sig[0]:X2}{sig[1]:X2}{sig[2]:X2}{sig[3]:X2}).");
            }
        }
    }

    private void ReadFileRecord(Encoding nameEncoding)
    {
        var f = new AlzFile();
        byte[] head = ReadBytes(9);
        ushort nameLen = BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(0, 2));
        byte attrs = head[2];
        uint dostime = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(3, 4));
        byte flags = head[7];
        // head[8]: reserved
        f.IsDirectory = (attrs & 0x10) != 0;
        f.Encrypted = (flags & 0x01) != 0;
        f.Modified = FromDosTime(dostime);
        int sizeBytes = flags >> 4;
        if (sizeBytes is not (0 or 1 or 2 or 4 or 8))
            throw new AlzFormatException($"잘못된 ALZ 크기 필드({sizeBytes}).");
        if (sizeBytes != 0)
        {
            int method = _stream.ReadByte();
            int reserved = _stream.ReadByte();
            if (method < 0 || reserved < 0)
                throw new AlzFormatException("ALZ 파일이 잘렸습니다.");
            f.Method = (byte)method;
            f.Crc = ReadU32();
            f.CompressedSize = ReadNumber(sizeBytes);
            f.UncompressedSize = ReadNumber(sizeBytes);
        }
        byte[] nameBytes = ReadBytes(nameLen);
        try { f.Name = nameEncoding.GetString(nameBytes).Replace('\\', '/'); }
        catch { f.Name = Encoding.UTF8.GetString(nameBytes).Replace('\\', '/'); }
        int colon = f.Name.IndexOf(':');
        if (colon >= 0)
            f.Name = f.Name[(colon + 1)..];
        f.Name = f.Name.TrimStart('/');
        f.DataOffset = _stream.Position;
        long span = (long)f.CompressedSize + (f.Encrypted ? 12 : 0);
        _stream.Seek(span, SeekOrigin.Current);
        _files.Add(f);
    }

    private static DateTime FromDosTime(uint dostime)
    {
        try
        {
            int date = (int)(dostime >> 16);
            int time = (int)(dostime & 0xFFFF);
            int year = ((date >> 9) & 0x7F) + 1980;
            int month = (date >> 5) & 0x0F;
            int day = date & 0x1F;
            int hour = (time >> 11) & 0x1F;
            int min = (time >> 5) & 0x3F;
            int sec = (time & 0x1F) * 2;
            return new DateTime(year, month, day, hour, min, sec);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    public static string MethodName(byte method) => method switch
    {
        0 => "Store",
        1 => "BZip2",
        2 => "Deflate",
        3 => "Obfuscated",
        _ => $"Unknown({method})",
    };

    /// <summary>Extracts one file's bytes (decrypt + decompress + CRC check).</summary>
    public byte[] GetFileBytes(AlzFile file, string? password, bool verifyCrc)
    {
        if (file.IsDirectory || file.UncompressedSize == 0)
            return Array.Empty<byte>();
        _stream.Seek(file.DataOffset, SeekOrigin.Begin);
        byte[] stored = ReadBytes((long)file.CompressedSize + (file.Encrypted ? 12 : 0));
        byte[] cipher = stored;
        if (file.Encrypted)
        {
            if (string.IsNullOrEmpty(password))
                throw new AlzPasswordException("암호화된 파일입니다. 암호를 입력하세요.");
            cipher = AlzCrypto.Decrypt(stored, password, file.Crc);
        }
        byte[] plain = file.Method switch
        {
            0 => cipher,
            1 => InflateBZip2(cipher, file.UncompressedSize),
            2 => InflateDeflate(cipher, file.UncompressedSize),
            3 => throw new AlzUnsupportedException("난독 Deflate(method 3)는 전용 복호 순서가 없어 풀 수 없습니다."),
            _ => throw new AlzFormatException($"알 수 없는 ALZ 압축 방식({file.Method})."),
        };
        if ((ulong)plain.Length != file.UncompressedSize)
            throw new AlzFormatException($"길이 불일치: 헤더 {file.UncompressedSize}, 실제 {plain.Length}.");
        if (verifyCrc)
        {
            uint crc = Egg.EggCrc32.Compute(plain);
            if (crc != file.Crc)
                throw new AlzFormatException(
                    $"CRC 불일치 (기대값 {file.Crc:X8}, 실제 {crc:X8}). 파일이 손상되었거나 암호가 틀렸습니다.");
        }
        return plain;
    }

    private static byte[] InflateDeflate(byte[] raw, ulong expect)
    {
        using var input = new MemoryStream(raw, false);
        using var ds = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(expect <= 64 * 1024 * 1024 ? (int)expect : 0);
        ds.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] InflateBZip2(byte[] raw, ulong expect)
    {
        using var input = new MemoryStream(raw, false);
        using var bz = new BZip2InputStream(input);
        using var output = new MemoryStream(expect <= 64 * 1024 * 1024 ? (int)expect : 0);
        bz.CopyTo(output);
        return output.ToArray();
    }

    public List<ArchiveEntry> ToEntries()
    {
        var list = new List<ArchiveEntry>();
        foreach (var f in _files)
        {
            list.Add(new ArchiveEntry(
                f.Name, (long)f.UncompressedSize, (long)f.CompressedSize, f.Modified,
                f.IsDirectory ? "Dir" : MethodName(f.Method),
                f.IsDirectory, f.Encrypted, "", f.Crc));
        }
        return list;
    }
}

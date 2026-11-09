// EGG reader implemented from the public EGG Format Specification v1.0
// (ESTsoft Corp., 2009-2011, non-commercial use).
// License clause 3 forbids developing EGG *compression*, so this module
// only parses and extracts. No ALZip binary code or resources are reused.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ICSharpCode.SharpZipLib.BZip2;

namespace AlzipClone.Core.Egg;

public sealed class EggFormatException : Exception
{
    public EggFormatException(string message) : base(message) { }
}

public sealed class EggUnsupportedException : Exception
{
    public EggUnsupportedException(string message) : base(message) { }
}

public sealed class EggPasswordException : Exception
{
    public EggPasswordException(string message) : base(message) { }
}

internal static class EggCrc32
{
    private static readonly uint[] Table = Build();

    private static uint[] Build()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}

/// <summary>Read/seek stream concatenated over an EGG split volume set.</summary>
internal sealed class EggVolumeStream : Stream
{
    private readonly List<string> _paths;
    private readonly List<long> _lengths = new();
    private readonly List<long> _baseOffsets = new();
    private readonly long[] _headerEnds;
    private FileStream? _current;
    private int _currentIndex = -1;
    private long _position;

    public EggVolumeStream(List<string> paths, long[] headerEnds)
    {
        _paths = paths;
        _headerEnds = headerEnds;
        long total = 0;
        foreach (var p in paths)
        {
            long len = new FileInfo(p).Length;
            _baseOffsets.Add(total);
            _lengths.Add(len);
            total += len;
        }
        Length = total;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    private void EnsureVolume(long pos)
    {
        int idx = _baseOffsets.Count - 1;
        while (idx > 0 && _baseOffsets[idx] > pos)
            idx--;
        if (idx != _currentIndex)
        {
            _current?.Dispose();
            _current = new FileStream(_paths[idx], FileMode.Open, FileAccess.Read, FileShare.Read);
            _currentIndex = idx;
        }
        _current!.Position = pos - _baseOffsets[idx];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (count > 0 && _position < Length)
        {
            EnsureVolume(_position);
            long inVolume = _lengths[_currentIndex] - _current!.Position;
            int want = (int)Math.Min(count, inVolume);
            int got = _current.Read(buffer, offset, want);
            if (got <= 0)
                break;
            offset += got;
            count -= got;
            total += got;
            _position += got;
        }
        return total;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (target < 0 || target > Length)
            throw new IOException("Seek out of range.");
        _position = target;
        return _position;
    }

    /// <summary>
    /// Skips pure data bytes, jumping over embedded volume headers.
    /// A split volume starts with its own EGG header; block data cut at a
    /// volume end continues after the next volume's header, and CompressedSize
    /// counts only data bytes. Record reads (headers are never cut) use the
    /// plain concatenated view instead.
    /// </summary>
    public void SkipData(long pureBytes)
    {
        while (pureBytes > 0)
        {
            EnsureVolume(_position);
            if (_position == _baseOffsets[_currentIndex] && _currentIndex > 0)
            {
                // crossed into a new volume exactly at its start: skip header
                _position += _headerEnds[_currentIndex];
                _currentIndex = -1;
                _current?.Dispose();
                _current = null;
                continue;
            }
            long volEnd = _baseOffsets[_currentIndex] + _lengths[_currentIndex];
            long avail = volEnd - _position;
            if (pureBytes <= avail)
            {
                _position += pureBytes;
                return;
            }
            if (_currentIndex + 1 >= _paths.Count)
                throw new EggFormatException("블록 데이터가 볼륨 끝을 초과합니다.");
            pureBytes -= avail;
            _position = _baseOffsets[_currentIndex + 1] + _headerEnds[_currentIndex + 1];
            _currentIndex = -1;
            _current?.Dispose();
            _current = null;
        }
    }

    /// <summary>Reads pure data bytes recorded at a concatenated offset.</summary>
    public byte[] ReadDataBytes(long offset, long length)
    {
        if (length < 0 || length > int.MaxValue)
            throw new EggFormatException($"Bad data size {length}.");
        var buf = new byte[(int)length];
        _position = offset;
        int done = 0;
        while (done < buf.Length)
        {
            EnsureVolume(_position);
            if (_position == _baseOffsets[_currentIndex] && _currentIndex > 0 && done > 0)
            {
                _position += _headerEnds[_currentIndex];
                _currentIndex = -1;
                _current?.Dispose();
                _current = null;
                continue;
            }
            long volEnd = _baseOffsets[_currentIndex] + _lengths[_currentIndex];
            long avail = volEnd - _position;
            if (avail == 0)
            {
                if (_currentIndex + 1 >= _paths.Count)
                    throw new EggFormatException("Truncated block data.");
                _position = _baseOffsets[_currentIndex + 1] + _headerEnds[_currentIndex + 1];
                _currentIndex = -1;
                _current?.Dispose();
                _current = null;
                continue;
            }
            int want = (int)Math.Min(avail, buf.Length - done);
            int got = _current!.Read(buf, done, want);
            if (got <= 0)
                throw new EggFormatException("Truncated block data.");
            done += got;
            _position += got;
        }
        return buf;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _current?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class EggBlock
{
    public byte Method;
    public byte Hint;
    public uint UncompressedSize;
    public uint CompressedSize;
    public uint Crc;
    public long DataOffset;
}

public sealed class EggFile
{
    public uint Id;
    public ulong Length;
    public byte[] NameBytes = Array.Empty<byte>();
    public bool NameUtf8 = true;
    public ushort Locale;
    public bool NameEncrypted;
    public bool IsRelative;
    public uint ParentId;
    public DateTime Modified = DateTime.Now;
    public bool IsDirectory;
    public byte EncryptMethod = 0xFF; // 0xFF = none
    public byte[] EncryptHeader = Array.Empty<byte>();
    public string FileComment = "";
    public List<EggBlock> Blocks = new();
    public string ResolvedName = "";
}

/// <summary>Parsed EGG archive: entries plus random access extraction.</summary>
public sealed class EggArchive : IDisposable
{
    private const uint MagicEgg = 0x41474745; // EGGA
    private const uint MagicFile = 0x0A8590E3;
    private const uint MagicBlock = 0x02B50C13;
    private const uint MagicEnd = 0x08E28222; // EOFARC
    private const uint MagicFileName = 0x0A8591AC;
    private const uint MagicWinInfo = 0x2C86950B;
    private const uint MagicPosixInfo = 0x1EE922E5;
    private const uint MagicComment = 0x04C63672;
    private const uint MagicEncrypt = 0x08D1470F;
    private const uint MagicSplit = 0x24F5A262;
    private const uint MagicSolid = 0x24E5A060;
    private const uint MagicDummy = 0x07463307;
    private const uint MagicSkip = 0xFFFF0000;

    private readonly EggVolumeStream _stream;
    private readonly List<EggFile> _files = new();
    private readonly List<EggBlock> _solidBlocks = new();

    public string ArchiveComment { get; private set; } = "";
    public bool IsSolid { get; private set; }
    public bool IsSplit { get; private set; }
    public IReadOnlyList<string> VolumePaths { get; }

    private EggArchive(EggVolumeStream stream, List<string> volumes)
    {
        _stream = stream;
        VolumePaths = volumes;
    }

    /// <summary>Opens a .egg file (or any volume of a .volN.egg set).</summary>
    public static EggArchive Open(string path)
    {
        var volumes = ResolveVolumes(path);
        var stream = new EggVolumeStream(volumes, ScanVolumeHeaders(volumes));
        var arc = new EggArchive(stream, volumes);
        try
        {
            arc.Parse();
            return arc;
        }
        catch
        {
            arc.Dispose();
            throw;
        }
    }

    /// <summary>Finds all volumes of a split set, ordered by volume number.</summary>
    public static List<string> ResolveVolumes(string path)
    {
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        string name = Path.GetFileName(full);
        var m = System.Text.RegularExpressions.Regex.Match(
            name, @"^(.*)\.vol(\d+)\.egg$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && dir != null)
        {
            string prefix = m.Groups[1].Value;
            var vols = Directory.GetFiles(dir, prefix + ".vol*.egg")
                .Select(f => new
                {
                    Path = f,
                    N = int.TryParse(System.Text.RegularExpressions.Regex.Match(
                        Path.GetFileName(f), @"\.vol(\d+)\.egg$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value, out int n) ? n : -1,
                })
                .Where(v => v.N > 0)
                .OrderBy(v => v.N)
                .Select(v => v.Path)
                .ToList();
            if (vols.Count > 0)
                return vols;
        }
        return new List<string> { full };
    }

    public IReadOnlyList<EggFile> Files => _files;

    public void Dispose() => _stream.Dispose();

    /// <summary>Pre-scans each volume's leading EGG header+extras so data
    /// reads can jump over embedded volume headers.</summary>
    private static long[] ScanVolumeHeaders(List<string> volumes)
    {
        var ends = new long[volumes.Count];
        for (int i = 0; i < volumes.Count; i++)
        {
            using var fs = File.OpenRead(volumes[i]);
            Span<byte> head = stackalloc byte[14];
            if (fs.Read(head) != 14
                || head[0] != (byte)'E' || head[1] != (byte)'G'
                || head[2] != (byte)'G' || head[3] != (byte)'A')
                throw new EggFormatException($"볼륨 {i + 1}에 EGG 헤더가 없습니다: {volumes[i]}");
            long pos = 14;
            Span<byte> m = stackalloc byte[4];
            Span<byte> s = stackalloc byte[4];
            for (int iter = 0; iter < 10000; iter++)
            {
                if (fs.Read(m) != 4)
                    throw new EggFormatException($"볼륨 {i + 1}의 EGG 헤더가 잘렸습니다.");
                pos += 4;
                if (BinaryPrimitives.ReadUInt32LittleEndian(m) == MagicEnd)
                    break;
                int flag = fs.ReadByte();
                if (flag < 0)
                    throw new EggFormatException($"볼륨 {i + 1}의 EGG 헤더가 잘렸습니다.");
                pos += 1;
                long size;
                if ((flag & 1) != 0)
                {
                    if (fs.Read(s) != 4)
                        throw new EggFormatException($"볼륨 {i + 1}의 EGG 헤더가 잘렸습니다.");
                    size = BinaryPrimitives.ReadUInt32LittleEndian(s);
                    pos += 4;
                }
                else
                {
                    if (fs.Read(s[..2]) != 2)
                        throw new EggFormatException($"볼륨 {i + 1}의 EGG 헤더가 잘렸습니다.");
                    size = BinaryPrimitives.ReadUInt16LittleEndian(s);
                    pos += 2;
                }
                fs.Seek(size, SeekOrigin.Current);
                pos += size;
            }
            ends[i] = pos;
        }
        return ends;
    }

    // ---------- parsing ----------

    private uint ReadU32()
    {
        Span<byte> buf = stackalloc byte[4];
        if (_stream.Read(buf) != 4)
            throw new EggFormatException($"Unexpected end of file at offset {_stream.Position}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(buf);
    }

    private ulong ReadU64()
    {
        Span<byte> buf = stackalloc byte[8];
        if (_stream.Read(buf) != 8)
            throw new EggFormatException($"Unexpected end of file at offset {_stream.Position}.");
        return BinaryPrimitives.ReadUInt64LittleEndian(buf);
    }

    private byte[] ReadBytes(long count)
    {
        if (count < 0 || count > int.MaxValue)
            throw new EggFormatException($"Bad size {count} at offset {_stream.Position}.");
        var buf = new byte[(int)count];
        int off = 0;
        while (off < buf.Length)
        {
            int got = _stream.Read(buf, off, buf.Length - off);
            if (got <= 0)
                throw new EggFormatException($"Unexpected end of file at offset {_stream.Position}.");
            off += got;
        }
        return buf;
    }

    private void Parse()
    {
        uint magic = ReadU32();
        if (magic != MagicEgg)
            throw new EggFormatException("Not an EGG archive (missing EGGA signature).");
        ReadEggHeaderRest();
        ParseEggExtras();

        EggFile? current = null;
        while (true)
        {
            long recPos;
            try
            {
                recPos = _stream.Position;
                magic = ReadU32();
            }
            catch (EggFormatException)
            {
                throw new EggFormatException("Truncated EGG archive (missing end marker).");
            }

            if (magic == MagicEnd)
            {
                break; // end of archive
            }
            else if (magic == MagicEgg)
            {
                ReadEggHeaderRest();
                ParseEggExtras();
            }
            else if (magic == MagicFile)
            {
                current = ReadFileHeader();
                _files.Add(current);
            }
            else if (magic == MagicBlock)
            {
                var block = ReadBlockHeader();
                if (IsSolid)
                    _solidBlocks.Add(block);
                else if (current != null)
                    current.Blocks.Add(block);
                else
                    throw new EggFormatException($"Block without file at offset {recPos}.");
            }
            else if (magic == MagicDummy || magic == MagicSkip)
            {
                SkipExtraBody();
            }
            else
            {
                throw new EggFormatException($"Unknown record 0x{magic:X8} at offset {recPos}.");
            }
        }

        ResolveNames();
    }

    private void ReadEggHeaderRest()
    {
        byte[] rest = ReadBytes(10); // version(2) + header id(4) + reserved(4)
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(rest.AsSpan(0, 2));
        if (version != 0x0100)
            throw new EggUnsupportedException($"Unsupported EGG version 0x{version:X4}.");
    }

    private void ParseEggExtras()
    {
        while (true)
        {
            uint magic = ReadU32();
            if (magic == MagicEnd)
                return;
            (byte flags, byte[] data) = ReadExtraBody(magic);
            if (magic == MagicSolid)
                IsSolid = true;
            else if (magic == MagicSplit)
                IsSplit = true;
            else if (magic == MagicComment)
                ArchiveComment = DecodeComment(flags, data);
        }
    }

    private (byte Flags, byte[] Data) ReadExtraBody(uint magic)
    {
        int flagByte = _stream.ReadByte();
        if (flagByte < 0)
            throw new EggFormatException("Truncated extra field.");
        byte flags = (byte)flagByte;
        long size = ((flags & 1) != 0) ? ReadU32() : ReadU32Low16();
        return (flags, ReadBytes(size));
    }

    private void SkipExtraBody()
    {
        int flagByte = _stream.ReadByte();
        if (flagByte < 0)
            throw new EggFormatException("Truncated extra field.");
        long size = ((flagByte & 1) != 0) ? ReadU32() : ReadU32Low16();
        _stream.Seek(size, SeekOrigin.Current);
    }

    private ushort ReadU32Low16()
    {
        Span<byte> buf = stackalloc byte[2];
        if (_stream.Read(buf) != 2)
            throw new EggFormatException("Truncated extra field.");
        return BinaryPrimitives.ReadUInt16LittleEndian(buf);
    }

    private EggFile ReadFileHeader()
    {
        var f = new EggFile { Id = ReadU32(), Length = ReadU64() };
        while (true)
        {
            uint magic = ReadU32();
            if (magic == MagicEnd)
                return f;
            (byte flags, byte[] data) = ReadExtraBody(magic);
            switch (magic)
            {
                case MagicFileName:
                    f.NameUtf8 = (flags & 0x10) == 0;
                    f.NameEncrypted = (flags & 0x08) != 0;
                    f.IsRelative = (flags & 0x20) != 0;
                    ParseFileName(data, f);
                    break;
                case MagicWinInfo:
                    if (data.Length >= 9)
                    {
                        long ft = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(0, 8));
                        try { f.Modified = DateTime.FromFileTimeUtc(ft).ToLocalTime(); } catch { }
                        f.IsDirectory = (data[8] & 0x10) != 0;
                    }
                    break;
                case MagicPosixInfo:
                    if (data.Length >= 20)
                    {
                        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
                        f.IsDirectory = (mode & 0xF000) == 0x4000;
                        long sec = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(12, 8));
                        try { f.Modified = DateTimeOffset.FromUnixTimeSeconds(sec).LocalDateTime; } catch { }
                    }
                    break;
                case MagicEncrypt:
                    if (data.Length >= 1)
                    {
                        f.EncryptMethod = data[0];
                        f.EncryptHeader = data[1..];
                    }
                    break;
                case MagicComment:
                    f.FileComment = DecodeComment(flags, data);
                    break;
                default:
                    break; // forward compatibility: skip unknown extras
            }
        }
    }

    private static void ParseFileName(byte[] data, EggFile f)
    {
        int off = 0;
        if (!f.NameUtf8)
        {
            if (data.Length < 2)
                return;
            f.Locale = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
            off = 2;
        }
        if (f.IsRelative)
        {
            if (data.Length < off + 4)
                return;
            f.ParentId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off, 4));
            off += 4;
        }
        f.NameBytes = data[off..];
    }

    private EggBlock ReadBlockHeader()
    {
        int m = _stream.ReadByte();
        int h = _stream.ReadByte();
        if (m < 0 || h < 0)
            throw new EggFormatException("Truncated block header.");
        var b = new EggBlock
        {
            Method = (byte)m,
            Hint = (byte)h,
            UncompressedSize = ReadU32(),
            CompressedSize = ReadU32(),
            Crc = ReadU32(),
        };
        while (true)
        {
            uint magic = ReadU32();
            if (magic == MagicEnd)
                break;
            SkipExtraBody();
        }
        b.DataOffset = _stream.Position;
        _stream.SkipData(b.CompressedSize);
        return b;
    }

    // ---------- names ----------

    private void ResolveNames()
    {
        var byId = _files.ToDictionary(f => f.Id, f => f);
        foreach (var f in _files)
        {
            string leaf;
            if (f.NameEncrypted)
            {
                leaf = $"(encrypted-name-{f.Id})";
            }
            else if (f.NameBytes.Length == 0 && !f.IsDirectory)
            {
                leaf = $"(no-name-{f.Id})";
            }
            else
            {
                Encoding enc = Encoding.UTF8;
                if (!f.NameUtf8)
                {
                    try
                    {
                        enc = f.Locale == 0
                            ? Encoding.Default
                            : Encoding.GetEncoding(f.Locale);
                    }
                    catch { enc = Encoding.UTF8; }
                }
                try { leaf = enc.GetString(f.NameBytes); }
                catch { leaf = Encoding.UTF8.GetString(f.NameBytes); }
            }
            leaf = leaf.Replace('\\', '/');
            if (f.IsRelative && byId.TryGetValue(f.ParentId, out var parent) && parent != f)
            {
                string basePath = parent.ResolvedName.TrimEnd('/');
                f.ResolvedName = basePath.Length > 0 ? basePath + "/" + leaf : leaf;
            }
            else
            {
                // absolute path stored: strip drive/root for safe extraction
                string s = leaf;
                int colon = s.IndexOf(':');
                if (colon >= 0)
                    s = s[(colon + 1)..];
                f.ResolvedName = s.TrimStart('/');
            }
        }
        // second pass: parents that resolved later (out-of-order ids)
        foreach (var f in _files)
        {
            if (f.IsRelative && byId.TryGetValue(f.ParentId, out var parent) && parent != f)
            {
                string leaf = f.ResolvedName.Contains('/')
                    ? f.ResolvedName.Substring(f.ResolvedName.LastIndexOf('/') + 1)
                    : f.ResolvedName;
                if (parent.ResolvedName.Length > 0 && !f.ResolvedName.StartsWith(parent.ResolvedName + "/"))
                    f.ResolvedName = parent.ResolvedName + "/" + leaf;
            }
        }
    }

    private static string DecodeComment(byte flags, byte[] data)
    {
        // Comment header has no locale field: bit4 set means system ANSI.
        if ((flags & 0x10) != 0)
        {
            try { return Encoding.Default.GetString(data); } catch { }
        }
        return Encoding.UTF8.GetString(data);
    }

    // ---------- data access ----------

    public static string MethodName(byte method) => method switch
    {
        0 => "Store",
        1 => "Deflate",
        2 => "BZip2",
        3 => "AZO",
        4 => "LZMA",
        _ => $"Unknown({method})",
    };

    private byte[] ReadBlockData(EggBlock b)
        => _stream.ReadDataBytes(b.DataOffset, b.CompressedSize);

    private byte[] DecodeBlock(EggBlock b, IEggDecryptor? decryptor, bool isFirst, bool isLast, bool verifyCrc)
    {
        byte[] raw = ReadBlockData(b);
        raw = EggCrypto.Decrypt(raw, decryptor, isFirst, isLast);
        byte[] plain = b.Method switch
        {
            0 => raw,
            1 => InflateDeflate(raw, b.UncompressedSize),
            2 => InflateBZip2(raw, b.UncompressedSize),
            3 => throw new EggUnsupportedException("AZO 압축은 전용 모듈이 없어 풀 수 없습니다."),
            4 => throw new EggUnsupportedException("LZMA 블록은 아직 지원하지 않습니다."),
            _ => throw new EggFormatException($"Unknown compress method {b.Method}."),
        };
        if (verifyCrc)
        {
            uint crc = EggCrc32.Compute(plain);
            if (crc != b.Crc)
                throw new EggFormatException(
                    $"CRC 불일치 (기대값 {b.Crc:X8}, 실제 {crc:X8}). 파일이 손상되었거나 암호가 틀렸습니다.");
        }
        return plain;
    }

    private static byte[] InflateDeflate(byte[] raw, uint expect)
    {
        // Reference writer stores raw RFC1951 deflate streams; accept
        // zlib-wrapped (RFC1950) data too for foreign writers.
        try
        {
            return InflateWith(raw, expect, rawDeflate: true);
        }
        catch (InvalidDataException)
        {
            return InflateWith(raw, expect, rawDeflate: false);
        }
    }

    private static byte[] InflateWith(byte[] raw, uint expect, bool rawDeflate)
    {
        using var input = new MemoryStream(raw, false);
        Stream ds = rawDeflate
            ? new DeflateStream(input, CompressionMode.Decompress)
            : new ZLibStream(input, CompressionMode.Decompress);
        using (ds)
        using (var output = new MemoryStream(expect <= 64 * 1024 * 1024 ? (int)expect : 0))
        {
            ds.CopyTo(output);
            return output.ToArray();
        }
    }

    private static byte[] InflateBZip2(byte[] raw, uint expect)
    {
        using var input = new MemoryStream(raw, false);
        using var bz = new BZip2InputStream(input);
        using var output = new MemoryStream(expect <= 64 * 1024 * 1024 ? (int)expect : 0);
        bz.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Decompresses one file's byte range (handles solid slicing).</summary>
    public byte[] GetFileBytes(EggFile file, string? password, bool verifyCrc)
    {
        if (file.EncryptMethod != 0xFF && string.IsNullOrEmpty(password) && file.Blocks.Count + _solidBlocks.Count > 0)
        {
            // Encrypted names may still list; data needs a password.
            if (file.Length > 0)
                throw new EggPasswordException("암호화된 파일입니다. 암호를 입력하세요.");
        }
        if (IsSolid)
        {
            if (_files.Any(f => f.EncryptMethod != 0xFF))
                throw new EggUnsupportedException("솔리드+암호 조합은 아직 지원하지 않습니다.");
            using var joint = new MemoryStream();
            for (int i = 0; i < _solidBlocks.Count; i++)
            {
                byte[] plain = DecodeBlock(_solidBlocks[i], null, i == 0, i == _solidBlocks.Count - 1, verifyCrc);
                joint.Write(plain, 0, plain.Length);
            }
            long start = 0;
            foreach (var f in _files)
            {
                if (f == file)
                    break;
                start += (long)f.Length;
            }
            byte[] all = joint.ToArray();
            if (start + (long)file.Length > all.Length)
                throw new EggFormatException("Solid 범위가 블록 데이터를 초과합니다.");
            var slice = new byte[(int)file.Length];
            Buffer.BlockCopy(all, (int)start, slice, 0, (int)file.Length);
            return slice;
        }
        else
        {
            IEggDecryptor? decryptor = file.EncryptMethod != 0xFF
                ? EggCrypto.CreateDecryptor(file, file.Blocks, password)
                : null;
            using var output = new MemoryStream(file.Length <= 64 * 1024 * 1024 ? (int)file.Length : 0);
            for (int i = 0; i < file.Blocks.Count; i++)
            {
                byte[] plain = DecodeBlock(file.Blocks[i], decryptor, i == 0, i == file.Blocks.Count - 1, verifyCrc);
                output.Write(plain, 0, plain.Length);
            }
            byte[] all = output.ToArray();
            if ((ulong)all.Length != file.Length)
                throw new EggFormatException($"길이 불일치: 헤더 {file.Length}, 실제 {all.Length}.");
            return all;
        }
    }

    /// <summary>Maps parsed files to UI entries (solid-aware packed sizes).</summary>
    public List<ArchiveEntry> ToEntries()
    {
        var list = new List<ArchiveEntry>();
        // solid: distribute packed bytes across files by length
        ulong totalLen = 0;
        ulong totalPacked = 0;
        if (IsSolid)
        {
            foreach (var f in _files)
                totalLen += f.Length;
            foreach (var b in _solidBlocks)
                totalPacked += b.CompressedSize;
        }
        foreach (var f in _files)
        {
            long packed;
            string method;
            if (IsSolid)
            {
                packed = totalLen == 0 ? 0 : (long)(totalPacked * f.Length / totalLen);
                method = "Solid";
            }
            else
            {
                packed = f.Blocks.Sum(b => (long)b.CompressedSize);
                method = f.Blocks.Count == 0 ? (f.IsDirectory ? "Dir" : "Store")
                    : string.Join("+", f.Blocks.Select(b => MethodName(b.Method)).Distinct());
            }
            bool encrypted = f.EncryptMethod != 0xFF;
            uint crc = (!IsSolid && f.Blocks.Count == 1) ? f.Blocks[0].Crc : 0;
            list.Add(new ArchiveEntry(
                f.ResolvedName, (long)f.Length, packed, f.Modified, method,
                f.IsDirectory, encrypted, f.FileComment, crc));
        }
        return list;
    }
}

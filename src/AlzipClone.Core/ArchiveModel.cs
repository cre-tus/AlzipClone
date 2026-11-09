namespace AlzipClone.Core;

/// <summary>Archive formats this clone can handle.</summary>
public enum ArchiveFormat
{
    Unknown,
    Zip,
    Tar,
    Tgz,
    Gz,
    Egg,   // P2: read via own parser from the public EGG spec
    Alz,   // P3: read via own parser
    Split, // own .volN chunk set (any payload)
    Rar,   // P4: read-only via SharpCompress (like ALZip's plugin DLLs)
    SevenZ, // P4: read-only via SharpCompress
    Cab,   // P4: read-only via SetupAPI
}

/// <summary>Compression strength, mirroring ALZip's 5 method levels.</summary>
public enum CompressLevel
{
    StoreOnly = 0,   // 파일만 묶기(가장 빠름)
    Fast = 1,        // 압축률 낮음(속도 빠름)
    Normal = 2,      // 압축률 보통(속도 보통)
    Best = 3,        // 압축률 최대(속도 느림)
    Optimal = 4,     // 최적압축(효율적 속도)
}

/// <summary>ZIP encryption choices offered by the clone.</summary>
public enum ZipCrypto
{
    None,
    Zip20,   // ZIP 2.0 호환 (ZipCrypto)
    Aes128,
    Aes256,
}

/// <summary>Filename encoding choices for read/write.</summary>
public enum NameEncoding
{
    Utf8,
    Cp949,   // 한국어 Windows 기본 (원본 "현재 OS 언어")
    EucKr,
    ShiftJis,
    Gb2312,
}

/// <summary>One entry inside an archive.</summary>
public sealed record ArchiveEntry(
    string FullName,
    long Size,
    long PackedSize,
    DateTime Modified,
    string Method,
    bool IsDirectory,
    bool Encrypted,
    string Comment = "",
    uint Crc = 0)
{
    public string Name => FullName.TrimEnd('/').Contains('/')
        ? FullName.TrimEnd('/').Substring(FullName.TrimEnd('/').LastIndexOf('/') + 1)
        : FullName.TrimEnd('/');

    public double Ratio => Size <= 0 ? 0 : Math.Max(0, 1.0 - (double)PackedSize / Size);
}

/// <summary>An opened archive: entries plus archive-level metadata.</summary>
public sealed class ArchiveModel
{
    public required string FilePath { get; init; }
    public required ArchiveFormat Format { get; init; }
    public string Comment { get; set; } = "";
    public List<ArchiveEntry> Entries { get; } = new();
    public bool IsSplit => Format == ArchiveFormat.Split;
    public int FileCount => Entries.Count(e => !e.IsDirectory);
    public long TotalSize => Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
    public long TotalPacked => Entries.Where(e => !e.IsDirectory).Sum(e => e.PackedSize);
}

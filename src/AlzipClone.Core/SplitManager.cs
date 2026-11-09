using System.Security.Cryptography;
using System.Text.Json;

namespace AlzipClone.Core;

/// <summary>Manifest for one own-format split set (&lt;base&gt;.volmanifest).</summary>
public sealed record SplitManifest(
    string SourceName,
    long SourceSize,
    string SourceSha256,
    long VolumeBytes,
    List<string> Volumes);

/// <summary>Raw chunk split/join in ALZip-style .volN naming (own container, documented).</summary>
public static class SplitManager
{
    public const string ManifestSuffix = ".volmanifest";

    public static bool IsVolumeSet(string path, out string? firstVolume)
    {
        firstVolume = null;
        string name = Path.GetFileName(path);
        string? dir = Path.GetDirectoryName(path);
        if (dir == null)
            return false;

        if (name.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var manifest = ReadManifest(path);
            if (manifest.Volumes.Count == 0)
                return false;
            firstVolume = Path.Combine(dir, manifest.Volumes[0]);
            return File.Exists(firstVolume);
        }

        var m = System.Text.RegularExpressions.Regex.Match(
            name, @"^(.*)\.vol(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success)
            return false;
        string manifestPath = Path.Combine(dir, m.Groups[1].Value + ManifestSuffix);
        if (!File.Exists(manifestPath))
            return false;
        firstVolume = path;
        return true;
    }

    public static List<string> Split(string sourcePath, string destDir, long volumeBytes, Action<ProgressInfo>? progress = null)
    {
        if (volumeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(volumeBytes));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(sourcePath);
        Directory.CreateDirectory(destDir);

        string baseName = Path.GetFileName(sourcePath);
        var volumes = new List<string>();
        using var input = File.OpenRead(sourcePath);
        using var sha = SHA256.Create();
        long total = input.Length;
        long done = 0;
        int index = 1;
        var buf = new byte[1024 * 1024];
        while (done < total || (total == 0 && index == 1))
        {
            string volName = $"{baseName}.vol{index}";
            string volPath = Path.Combine(destDir, volName);
            using (var output = File.Create(volPath))
            {
                long left = Math.Min(volumeBytes, total - done);
                if (total == 0)
                    left = 0;
                while (left > 0)
                {
                    int n = input.Read(buf, 0, (int)Math.Min(buf.Length, left));
                    if (n <= 0)
                        break;
                    output.Write(buf, 0, n);
                    sha.TransformBlock(buf, 0, n, null, 0);
                    left -= n;
                    done += n;
                }
                if (total == 0)
                    sha.TransformBlock(Array.Empty<byte>(), 0, 0, null, 0);
            }
            volumes.Add(volName);
            progress?.Invoke(new ProgressInfo(volName, index, (int)Math.Max(1, (total + volumeBytes - 1) / volumeBytes)));
            index++;
            if (total == 0)
                break;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string hash = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());

        var manifest = new SplitManifest(baseName, total, hash, volumeBytes, volumes);
        File.WriteAllText(
            Path.Combine(destDir, baseName + ManifestSuffix),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        progress?.Invoke(new ProgressInfo("", volumes.Count, volumes.Count));
        return volumes.Select(v => Path.Combine(destDir, v)).ToList();
    }

    /// <summary>Joins a set (pass any .volN or the manifest) and verifies SHA-256.</summary>
    public static string Join(string anyVolumeOrManifest, string destDir, Action<ProgressInfo>? progress = null)
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(anyVolumeOrManifest))
            ?? throw new InvalidOperationException("경로를 확인할 수 없습니다.");
        string manifestPath = anyVolumeOrManifest.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase)
            ? anyVolumeOrManifest
            : FindManifest(anyVolumeOrManifest);
        var manifest = ReadManifest(manifestPath);
        Directory.CreateDirectory(destDir);
        string dest = Path.Combine(destDir, manifest.SourceName);
        using var output = File.Create(dest);
        using var sha = SHA256.Create();
        var buf = new byte[1024 * 1024];
        int done = 0;
        foreach (var v in manifest.Volumes)
        {
            string volPath = Path.Combine(dir, v);
            progress?.Invoke(new ProgressInfo(v, done, manifest.Volumes.Count));
            using var input = File.OpenRead(volPath);
            int n;
            while ((n = input.Read(buf, 0, buf.Length)) > 0)
            {
                output.Write(buf, 0, n);
                sha.TransformBlock(buf, 0, n, null, 0);
            }
            done++;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string hash = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        progress?.Invoke(new ProgressInfo("", done, manifest.Volumes.Count));
        if (!hash.Equals(manifest.SourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"병합 검증 실패: SHA-256 불일치 (기대 {manifest.SourceSha256}, 실제 {hash})");
        return dest;
    }

    public static SplitManifest ReadManifest(string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize<SplitManifest>(File.ReadAllText(manifestPath));
        return manifest ?? throw new InvalidDataException("분할 정보가 손상되었습니다.");
    }

    private static string FindManifest(string volumePath)
    {
        string name = Path.GetFileName(volumePath);
        string? dir = Path.GetDirectoryName(volumePath);
        var m = System.Text.RegularExpressions.Regex.Match(
            name, @"^(.*)\.vol(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success || dir == null)
            throw new InvalidDataException("분할 볼륨이 아닙니다.");
        string manifestPath = Path.Combine(dir, m.Groups[1].Value + ManifestSuffix);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("분할 정보 파일을 찾을 수 없습니다.", manifestPath);
        return manifestPath;
    }

    public static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }
}

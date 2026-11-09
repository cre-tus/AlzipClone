// EGG decryption: Zip 2.0 (classic ZipCrypto stream) and WinZip-compatible
// AES-128/256 (PBKDF2-SHA1 + AES-CTR + HMAC-SHA1 auth). Implemented from the
// public EGG spec plus byte-level comparison against archives produced by
// the reference ALZip 12.37 (clean-room: no ALZip code reused).
using System.Security.Cryptography;
using System.Text;

namespace AlzipClone.Core.Egg;

internal interface IEggDecryptor
{
    byte[] TransformBlock(byte[] raw, bool isFirst, bool isLast);
}

internal static class EggCrypto
{
    public static IEggDecryptor CreateDecryptor(EggFile file, IReadOnlyList<EggBlock> blocks, string? password)
    {
        if (string.IsNullOrEmpty(password))
            throw new EggPasswordException("암호화된 파일입니다. 암호를 입력하세요.");
        return file.EncryptMethod switch
        {
            0 => new Zip2Decryptor(file, password),
            1 => new AesDecryptor(file, blocks, password, 16),
            2 => new AesDecryptor(file, blocks, password, 32),
            5 => throw new EggUnsupportedException("LEA 128bit 암호화는 공개 사양이 없어 풀 수 없습니다."),
            6 => throw new EggUnsupportedException("LEA 256bit 암호화는 공개 사양이 없어 풀 수 없습니다."),
            _ => throw new EggUnsupportedException($"지원하지 않는 EGG 암호화 방식({file.EncryptMethod})입니다."),
        };
    }

    public static byte[] Decrypt(byte[] raw, IEggDecryptor? decryptor, bool isFirst, bool isLast)
        => decryptor == null ? raw : decryptor.TransformBlock(raw, isFirst, isLast);
}

/// <summary>Classic ZipCrypto stream. EGG stores a 12-byte verifier plus the
/// expected first-block CRC32 in the Encrypt extra field.</summary>
internal sealed class Zip2Decryptor : IEggDecryptor
{
    private readonly uint[] _keys = new uint[3];
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
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

    private static uint Crc32(uint crc, byte b) => CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

    private void UpdateKeys(byte b)
    {
        _keys[0] = Crc32(_keys[0], b);
        _keys[1] = _keys[1] + (_keys[0] & 0xFF);
        _keys[1] = _keys[1] * 0x08088405u + 1;
        _keys[2] = Crc32(_keys[2], (byte)(_keys[1] >> 24));
    }

    private byte MagicByte()
    {
        uint temp = _keys[2] | 2;
        return (byte)(((temp * (temp ^ 1)) >> 8) & 0xFF);
    }

    public Zip2Decryptor(EggFile file, string password)
    {
        if (file.EncryptHeader.Length < 16)
            throw new EggFormatException("EGG Zip2.0 암호화 헤더가 짧습니다.");
        _keys[0] = 0x12345678;
        _keys[1] = 0x23456789;
        _keys[2] = 0x34567890;
        foreach (byte b in Encoding.UTF8.GetBytes(password))
            UpdateKeys(b);
        // Decrypt the 12-byte verifier; its last byte must match the
        // high byte of the stored partial CRC (mirrors the ZIP scheme).
        byte last = 0;
        for (int i = 0; i < 12; i++)
        {
            byte c = file.EncryptHeader[i];
            byte p = (byte)(c ^ MagicByte());
            UpdateKeys(p);
            last = p;
        }
        uint partial = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
            file.EncryptHeader.AsSpan(12, 4));
        if (last != (byte)(partial >> 24))
            throw new EggPasswordException("암호가 틀렸습니다.");
    }

    public byte[] TransformBlock(byte[] raw, bool isFirst, bool isLast)
    {
        var plain = new byte[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            byte p = (byte)(raw[i] ^ MagicByte());
            UpdateKeys(p);
            plain[i] = p;
        }
        return plain;
    }
}

/// <summary>WinZip-style AES for EGG. Observed framing (reference ALZip 12.37):
/// the Encrypt extra field holds salt + 2-byte verifier + 10-byte auth tag,
/// and every block stores pure CTR ciphertext (no padding, no inline tag).
/// The tag is HMAC-SHA1 over the concatenated ciphertext of all blocks.</summary>
internal sealed class AesDecryptor : IEggDecryptor
{
    private readonly byte[] _encKey;
    private readonly byte[] _authTag;
    private uint _counter; // file-stream CTR position (blocks of 16 bytes)
    private readonly HMACSHA1 _hmac;

    public AesDecryptor(EggFile file, IReadOnlyList<EggBlock> blocks, string password, int keyLen)
    {
        int saltLen = keyLen == 16 ? 8 : 16;
        if (file.EncryptHeader.Length < saltLen + 2 + 10)
            throw new EggFormatException("EGG AES 암호화 헤더가 짧습니다.");
        byte[] salt = file.EncryptHeader[..saltLen];
        byte[] pwVerify = file.EncryptHeader[saltLen..(saltLen + 2)];
        _authTag = file.EncryptHeader[(saltLen + 2)..(saltLen + 2 + 10)];

        using var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, 1000, HashAlgorithmName.SHA1);
        byte[] material = kdf.GetBytes(keyLen * 2 + 2);
        _encKey = material[..keyLen];
        byte[] authKey = material[keyLen..(keyLen * 2)];
        if (material[keyLen * 2] != pwVerify[0] || material[keyLen * 2 + 1] != pwVerify[1])
            throw new EggPasswordException("암호가 틀렸습니다.");

        _hmac = new HMACSHA1(authKey);
    }

    private byte[] CtrCrypt(byte[] data, ref uint counter)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = _encKey;
        using var enc = aes.CreateEncryptor();
        var output = new byte[data.Length];
        byte[] counterBlock = new byte[16];
        byte[] keystream = new byte[16];
        int off = 0;
        while (off < data.Length)
        {
            // WinZip-AES uses a little-endian block counter.
            counterBlock[0] = (byte)(counter & 0xFF);
            counterBlock[1] = (byte)((counter >> 8) & 0xFF);
            counterBlock[2] = (byte)((counter >> 16) & 0xFF);
            counterBlock[3] = (byte)((counter >> 24) & 0xFF);
            for (int i = 4; i < 16; i++)
                counterBlock[i] = 0;
            enc.TransformBlock(counterBlock, 0, 16, keystream, 0);
            int n = Math.Min(16, data.Length - off);
            for (int i = 0; i < n; i++)
                output[off + i] = (byte)(data[off + i] ^ keystream[i]);
            off += n;
            counter++;
        }
        return output;
    }

    public byte[] TransformBlock(byte[] raw, bool isFirst, bool isLast)
    {
        if (isFirst)
            _counter = 1;
        _hmac.TransformBlock(raw, 0, raw.Length, raw, 0);
        byte[] plain = CtrCrypt(raw, ref _counter);
        if (isLast)
        {
            _hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            byte[] full = _hmac.Hash!;
            for (int i = 0; i < 10; i++)
                if (full[i] != _authTag[i])
                    throw new EggPasswordException("AES 인증 태그가 일치하지 않습니다 (암호 오류 또는 손상).");
        }
        return plain;
    }
}

// Classic ZipCrypto stream as used by ALZ file encryption: the stored data
// span holds a 12-byte encryption header followed by the ciphertext, and
// the header's last byte must match the high byte of the file CRC.
using System.Text;

namespace AlzipClone.Core.Alz;

internal static class AlzCrypto
{
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

    public static byte[] Decrypt(byte[] stored, string password, uint expectedCrc)
    {
        if (stored.Length < 12)
            throw new AlzFormatException("ALZ 암호화 헤더가 짧습니다.");
        uint k0 = 0x12345678;
        uint k1 = 0x23456789;
        uint k2 = 0x34567890;
        static void Update(ref uint k0, ref uint k1, ref uint k2, byte b)
        {
            k0 = CrcTable[(k0 ^ b) & 0xFF] ^ (k0 >> 8);
            k1 = k1 + (k0 & 0xFF);
            k1 = k1 * 0x08088405u + 1;
            k2 = CrcTable[(k2 ^ (k1 >> 24)) & 0xFF] ^ (k2 >> 8);
        }
        static byte Magic(uint k2)
        {
            uint temp = k2 | 2;
            return (byte)(((temp * (temp ^ 1)) >> 8) & 0xFF);
        }
        foreach (byte b in Encoding.UTF8.GetBytes(password))
            Update(ref k0, ref k1, ref k2, b);
        byte last = 0;
        for (int i = 0; i < 12; i++)
        {
            byte p = (byte)(stored[i] ^ Magic(k2));
            Update(ref k0, ref k1, ref k2, p);
            last = p;
        }
        if (last != (byte)(expectedCrc >> 24))
            throw new AlzPasswordException("암호가 틀렸습니다.");
        var plain = new byte[stored.Length - 12];
        for (int i = 0; i < plain.Length; i++)
        {
            byte p = (byte)(stored[i + 12] ^ Magic(k2));
            Update(ref k0, ref k1, ref k2, p);
            plain[i] = p;
        }
        return plain;
    }
}

using System;
using System.IO;

namespace Dreynox.Delivery
{
    /// <summary>
    /// Validate embedded single-disk ZIP metadata without depending on the newer
    /// ZipArchiveEntry.ExternalAttributes API. Decompression stays in the Framework.
    /// PKWARE APPNOTE 6.3.10 sections 4.3.12 through 4.3.16.
    /// </summary>
    public static class PlayerZipMetadata
    {
        public static void Validate(Stream source)
        {
            if (source == null || !source.CanRead || !source.CanSeek)
                throw new ArgumentException("A readable seekable embedded ZIP is required.");
            long saved = source.Position;
            try
            {
                if (source.Length < 22) throw new InvalidDataException("ZIP end record is missing.");
                int tailSize = (int)Math.Min(source.Length, 22L + UInt16.MaxValue);
                long tailStart = source.Length - tailSize;
                source.Position = tailStart;
                byte[] tail = Read(source, tailSize);
                int end = -1;
                for (int i = tail.Length - 22; i >= 0; i--)
                    if (U32(tail, i) == 0x06054b50 && i + 22 + U16(tail, i + 20) == tail.Length)
                    { end = i; break; }
                if (end < 0) throw new InvalidDataException("ZIP end record or comment length is invalid.");
                if (U16(tail, end + 4) != 0 || U16(tail, end + 6) != 0)
                    throw new InvalidDataException("Multi-disk payloads are not supported.");
                ulong count = U16(tail, end + 10);
                ulong length = U32(tail, end + 12), offset = U32(tail, end + 16);
                long centralLimit = tailStart + end;
                bool zip64 = count == UInt16.MaxValue || length == UInt32.MaxValue || offset == UInt32.MaxValue;
                if (zip64)
                {
                    if (centralLimit < 20) throw new InvalidDataException("ZIP64 locator is missing.");
                    source.Position = centralLimit - 20;
                    byte[] locator = Read(source, 20);
                    if (U32(locator, 0) != 0x07064b50 || U32(locator, 4) != 0 || U32(locator, 16) != 1)
                        throw new InvalidDataException("Invalid single-disk ZIP64 locator.");
                    ulong at = U64(locator, 8);
                    if (at > (ulong)(centralLimit - 20) || (ulong)(centralLimit - 20) - at < 56)
                        throw new InvalidDataException("ZIP64 record escapes payload.");
                    source.Position = (long)at;
                    byte[] record = Read(source, 56);
                    ulong size = U64(record, 4);
                    if (U32(record, 0) != 0x06064b50 || size < 44 || size > 65536 ||
                        at + 12 + size != (ulong)(centralLimit - 20) ||
                        U32(record, 16) != 0 || U32(record, 20) != 0 || U64(record, 24) != U64(record, 32))
                        throw new InvalidDataException("Invalid ZIP64 directory record.");
                    count = U64(record, 32); length = U64(record, 40); offset = U64(record, 48);
                    centralLimit = (long)at;
                }
                else if (U16(tail, end + 8) != count)
                    throw new InvalidDataException("ZIP entry counts disagree.");
                if (count > 50000 || length > 64UL * 1024 * 1024 || offset > (ulong)centralLimit ||
                    length != (ulong)centralLimit - offset)
                    throw new InvalidDataException("ZIP central directory exceeds bounds or budget.");
                source.Position = (long)offset;
                for (ulong n = 0; n < count; n++)
                {
                    if (centralLimit - source.Position < 46) throw new InvalidDataException("Truncated ZIP central entry.");
                    byte[] header = Read(source, 46);
                    if (U32(header, 0) != 0x02014b50) throw new InvalidDataException("Invalid central entry signature.");
                    uint attributes = U32(header, 38);
                    uint kind = (attributes >> 16) & 0xf000;
                    // Check directories too: a trailing slash must not hide a symlink.
                    if ((attributes & 0x400) != 0 || (kind != 0 && kind != 0x4000 && kind != 0x8000))
                        throw new InvalidDataException("Links/special files are forbidden in Player payloads.");
                    if ((U16(header, 8) & 0x2041) != 0 || U16(header, 34) != 0 ||
                        (U16(header, 10) != 0 && U16(header, 10) != 8))
                        throw new InvalidDataException("Encrypted, split or unsupported compression in Player payload.");
                    int nameLength = U16(header, 28);
                    long following = nameLength + (long)U16(header, 30) + U16(header, 32);
                    if (nameLength == 0 || nameLength > 1024 || following > centralLimit - source.Position)
                        throw new InvalidDataException("Truncated/oversized central entry fields.");
                    source.Position += following;
                }
                if (source.Position != centralLimit)
                    throw new InvalidDataException("Central directory count/length mismatch.");
            }
            finally { source.Position = saved; }
        }
        private static byte[] Read(Stream source, int length)
        {
            byte[] value = new byte[length]; int at = 0;
            while (at < value.Length)
            {
                int read = source.Read(value, at, value.Length - at);
                if (read <= 0) throw new InvalidDataException("Truncated ZIP metadata.");
                at += read;
            }
            return value;
        }
        private static ushort U16(byte[] d, int o) { return (ushort)(d[o] | d[o + 1] << 8); }
        private static uint U32(byte[] d, int o)
        { return (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16 | d[o + 3] << 24); }
        private static ulong U64(byte[] d, int o) { return U32(d, o) | (ulong)U32(d, o + 4) << 32; }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Dreynox.Delivery;

internal static class DeliveryMetadataCases
{
    public static int Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, label) =>
        { if (!ok) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); };
        Action<Action, string> reject = (work, label) =>
        {
            try { work(); } catch (InvalidDataException) { check(true, label); return; }
            throw new Exception("Expected metadata rejection: " + label);
        };
        byte[] plain = Make("file.bin");
        using (var stream = new MemoryStream(plain))
        { stream.Position = 3; PlayerZipMetadata.Validate(stream); check(stream.Position == 3, "metadata reader restores caller position"); }
        Validate(UpgradeZip64(plain)); check(true, "single-disk ZIP64 end record accepted");
        foreach (uint attrs in new[] { 0xa1ff0000u, 0x00000400u, 0x21ff0000u, 0xc1ff0000u })
        {
            byte[] changed = PatchAttrs(plain, attrs);
            reject(() => Validate(changed), "special entry rejected: " + attrs.ToString("X"));
        }
        reject(() => Validate(PatchAttrs(Make("link/"), 0xa1ff0000)), "directory-suffix symlink cannot bypass metadata checks");
        byte[] badCount = (byte[])plain.Clone(); badCount[^12]++;
        reject(() => Validate(badCount), "entry counts disagree");
        byte[] truncated = new byte[plain.Length - 1]; Array.Copy(plain, truncated, truncated.Length);
        reject(() => Validate(truncated), "truncated footer rejected before extraction");
        byte[] encrypted = (byte[])plain.Clone(); encrypted[Central(encrypted) + 8] |= 1;
        reject(() => Validate(encrypted), "encrypted entry rejected");
        byte[] split = (byte[])plain.Clone(); split[split.Length - 18] = 1;
        reject(() => Validate(split), "multidisk footer rejected");
        byte[] oversized = (byte[])plain.Clone(); Write32(oversized, oversized.Length - 6, UInt32.MaxValue - 1);
        reject(() => Validate(oversized), "central offset escapes payload");
        byte[] unsupported = (byte[])plain.Clone(); unsupported[Central(unsupported) + 10] = 12;
        reject(() => Validate(unsupported), "unsupported compression rejected");
        byte[] zip64 = UpgradeZip64(plain); Write32(zip64, plain.Length - 22 + 16, 1);
        reject(() => Validate(zip64), "multidisk ZIP64 record rejected");
        return checks;
    }
    private static void Validate(byte[] value) { using (var stream = new MemoryStream(value)) PlayerZipMetadata.Validate(stream); }
    private static byte[] Make(string name)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        { using var item = zip.CreateEntry(name).Open(); if (!name.EndsWith("/")) item.WriteByte(42); }
        return stream.ToArray();
    }
    private static int Central(byte[] zip) { return (int)BitConverter.ToUInt32(zip, zip.Length - 6); }
    private static byte[] PatchAttrs(byte[] zip, uint value)
    { var copy = (byte[])zip.Clone(); Write32(copy, Central(copy) + 38, value); return copy; }
    private static void Write32(byte[] target, int at, uint value) { Array.Copy(BitConverter.GetBytes(value), 0, target, at, 4); }
    private static byte[] UpgradeZip64(byte[] input)
    {
        int end = input.Length - 22;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(input, 0, end);
        writer.Write(0x06064b50u); writer.Write(44UL); writer.Write((ushort)45); writer.Write((ushort)45);
        writer.Write(0u); writer.Write(0u); writer.Write((ulong)BitConverter.ToUInt16(input, end + 10));
        writer.Write((ulong)BitConverter.ToUInt16(input, end + 10));
        writer.Write((ulong)BitConverter.ToUInt32(input, end + 12)); writer.Write((ulong)BitConverter.ToUInt32(input, end + 16));
        writer.Write(0x07064b50u); writer.Write(0u); writer.Write((ulong)end); writer.Write(1u);
        writer.Write(0x06054b50u); writer.Write((ushort)0); writer.Write((ushort)0);
        writer.Write(ushort.MaxValue); writer.Write(ushort.MaxValue); writer.Write(uint.MaxValue); writer.Write(uint.MaxValue); writer.Write((ushort)0);
        return stream.ToArray();
    }
}

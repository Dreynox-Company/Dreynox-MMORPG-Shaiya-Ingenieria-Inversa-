using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>
    /// Resolves an explicit (itemtype,itemtypeid) into its authored visual image index.
    /// Column-name mapping handles original 69/70-field tables without shifting later rows.
    /// This is not an inventory, entitlement, equipment-eligibility or combat-stat implementation.
    /// </summary>
    public static class LegacyItemDefinitionParser
    {
        public static int ResolveVisualIndex(string path, long itemType, long typeId)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Item table source is required.");
            if (new FileInfo(path).Length > 64L * 1024 * 1024)
                throw new InvalidDataException("Item table exceeds its read budget.");
            return ResolveVisualIndexPlain(LegacySDataDecryptor.Decrypt(path, validateChecksum: false).Plaintext, itemType, typeId);
        }
        public static int ResolveVisualIndexPlain(byte[] data, long itemType, long typeId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > 64 * 1024 * 1024) throw new InvalidDataException("Item table exceeds its read budget.");
            if (itemType <= 0 || typeId <= 0) throw new ArgumentOutOfRangeException("Item type and TypeId must be positive.");
            using (var reader = new BinaryReader(new MemoryStream(data, false), Encoding.Unicode))
            {
                LegacyFormatPrimitives.EnsureRemaining(reader, 132);
                reader.BaseStream.Position = 128;
                int fields = LegacyFormatPrimitives.ReadCount(reader, "Item field", 512);
                var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fields; i++)
                {
                    LegacyFormatPrimitives.EnsureRemaining(reader, 1);
                    int length = reader.ReadByte() * 2;
                    LegacyFormatPrimitives.EnsureRemaining(reader, length);
                    string name = Encoding.Unicode.GetString(reader.ReadBytes(length));
                    if (string.IsNullOrWhiteSpace(name) || columns.ContainsKey(name))
                        throw new InvalidDataException("Item table has an empty or duplicate field.");
                    columns.Add(name, i);
                }
                if (!columns.TryGetValue("itemtype", out int typeColumn) ||
                    !columns.TryGetValue("itemtypeid", out int idColumn) || !columns.TryGetValue("image", out int imageColumn))
                    throw new InvalidDataException("Item table requires itemtype, itemtypeid and image columns.");
                int count = LegacyFormatPrimitives.ReadCount(reader, "Item row", 100000);
                long rowBytes = checked(fields * 8L);
                LegacyFormatPrimitives.EnsureRemaining(reader, checked(count * rowBytes));
                int matches = 0; long image = -1;
                for (int row = 0; row < count; row++)
                {
                    long start = reader.BaseStream.Position;
                    reader.BaseStream.Position = start + typeColumn * 8L; long type = reader.ReadInt64();
                    reader.BaseStream.Position = start + idColumn * 8L; long id = reader.ReadInt64();
                    if (type == itemType && id == typeId)
                    {
                        reader.BaseStream.Position = start + imageColumn * 8L; image = reader.ReadInt64(); matches++;
                    }
                    reader.BaseStream.Position = start + rowBytes;
                }
                if (reader.BaseStream.Length - reader.BaseStream.Position > 16)
                    throw new InvalidDataException("Unexpected item table trailing extension.");
                LegacyFormatPrimitives.EnsureFullyConsumed(reader, "DBItemData");
                if (matches == 0) throw new KeyNotFoundException("Original item definition absent: " + itemType + "/" + typeId);
                if (matches != 1) throw new InvalidDataException("Duplicate original item definition: " + itemType + "/" + typeId);
                if (image < 0 || image > int.MaxValue) throw new InvalidDataException("Original item image index is not usable.");
                return (int)image;
            }
        }
    }
}

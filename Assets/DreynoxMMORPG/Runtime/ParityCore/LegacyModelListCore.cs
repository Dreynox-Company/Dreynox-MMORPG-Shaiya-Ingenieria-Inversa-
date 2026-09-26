using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct LegacyModelListEntry
    {
        public readonly int MeshIndex, TextureIndex, SourceFlag;
        public LegacyModelListEntry(int mesh, int texture, int flag)
        { MeshIndex = mesh; TextureIndex = texture; SourceFlag = flag; }
    }

    /// <summary>ML2 table, not a filename convention. The third record word is preserved, not guessed.</summary>
    public sealed class LegacyModelList
    {
        public ReadOnlyCollection<string> Meshes { get; }
        public ReadOnlyCollection<string> Textures { get; }
        public ReadOnlyCollection<LegacyModelListEntry> Entries { get; }
        internal LegacyModelList(List<string> meshes, List<string> textures, List<LegacyModelListEntry> entries)
        { Meshes = meshes.AsReadOnly(); Textures = textures.AsReadOnly(); Entries = entries.AsReadOnly(); }
    }

    public static class LegacyModelListCore
    {
        public const int MaximumBytes = 4 * 1024 * 1024;
        // ps0032 0x51F210: "ML2"; 0x51F740 allocates each 12-byte selection record.
        public static LegacyModelList Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("ML2 exceeds its read budget.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (reader.ReadByte() != 'M' || reader.ReadByte() != 'L' || reader.ReadByte() != '2')
                    throw new InvalidDataException("Unsupported model-list signature; expected ML2.");
                List<string> meshes = ReadNames(reader, ".3dc");
                List<string> textures = ReadNames(reader, ".dds");
                int count = Count(reader, 65536);
                if (stream.Length - stream.Position != (long)count * 12)
                    throw new InvalidDataException("ML2 record size or trailing-data mismatch.");
                var records = new List<LegacyModelListEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    int mesh = reader.ReadInt32(), texture = reader.ReadInt32(), flag = reader.ReadInt32();
                    if (mesh < 0 || mesh >= meshes.Count || texture < 0 || texture >= textures.Count)
                        throw new InvalidDataException("ML2 selection points outside its own mesh/texture table at record " + i);
                    records.Add(new LegacyModelListEntry(mesh, texture, flag));
                }
                return new LegacyModelList(meshes, textures, records);
            }
        }
        private static int Count(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("Invalid ML2 count.");
            return count;
        }
        private static List<string> ReadNames(BinaryReader reader, string extension)
        {
            int count = Count(reader, 16384);
            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                int length = Count(reader, 512);
                if (length == 0 || reader.BaseStream.Length - reader.BaseStream.Position < length)
                    throw new InvalidDataException("Missing or truncated ML2 resource name.");
                byte[] value = reader.ReadBytes(length);
                int end = Array.IndexOf(value, (byte)0);
                if (end < 0) end = value.Length;
                for (int j = end; j < value.Length; j++)
                    if (value[j] != 0) throw new InvalidDataException("Embedded data after ML2 name terminator.");
                for (int j = 0; j < end; j++)
                    if (value[j] < 32 || value[j] > 126) throw new InvalidDataException("ML2 name is not printable ASCII.");
                string name = Encoding.ASCII.GetString(value, 0, end);
                if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || name.IndexOfAny(new[] {'/', '\\', ':', '<', '>', '|', '?', '*'}) >= 0 ||
                    name.Contains("..") || name != name.Trim())
                    throw new InvalidDataException("Invalid ML2 resource basename: " + name);
                names.Add(name);
            }
            return names;
        }
    }

    public static class LegacyDefaultAppearanceCore
    {
        public static readonly IReadOnlyList<string> Slots = Array.AsReadOnly(new[] { "upper", "lower", "hand", "foot", "face", "hair" });
        /// <summary>Unarmored body from row zero; face/hair are row indices, not mesh filename suffixes.</summary>
        public static LegacyCharacterPreviewAssetPaths Resolve(int family, int job, int sex,
            int faceIndex, int hairIndex, Func<string, byte[]> read)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (faceIndex < 0 || hairIndex < 0) throw new ArgumentOutOfRangeException(nameof(faceIndex));
            var rig = LegacyCharacterRigCore.Resolve(family, job, sex);
            string root = "DATA_Español/character/" + rig.FamilyFolder;
            var meshes = new string[6]; var textures = new string[6];
            for (int i = 0; i < 6; i++)
            {
                string source = root + "/" + rig.Prefix + "_" + Slots[i] + ".mlt";
                var table = LegacyModelListCore.Parse(read(source));
                int row = i == 4 ? faceIndex : i == 5 ? hairIndex : 0;
                if (row >= table.Entries.Count) throw new InvalidDataException("Appearance row " + row + " is absent in " + source);
                var entry = table.Entries[row];
                meshes[i] = root + "/3dc/" + table.Meshes[entry.MeshIndex];
                textures[i] = root + "/dds/" + table.Textures[entry.TextureIndex];
            }
            return new LegacyCharacterPreviewAssetPaths(rig, -1, faceIndex, hairIndex, root,
                meshes[0], meshes[1], meshes[2], meshes[3], meshes[4], meshes[5],
                textures[0], textures[1], textures[2], textures[3], textures[4], textures[5],
                root + "/ani6/" + rig.Prefix + "_019_select.ani");
        }
    }
}

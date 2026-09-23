using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Executable
{
    [Serializable]
    public sealed class PeSectionInfo
    {
        public string name;
        public uint virtualSize;
        public uint virtualAddress;
        public uint rawSize;
        public uint rawPointer;
        public uint characteristics;
    }

    [Serializable]
    public sealed class PortableExecutableInfo
    {
        public string path;
        public long fileBytes;
        public string sha256;
        public ushort machine;
        public ushort sectionCount;
        public uint timeDateStamp;
        public ushort optionalMagic;
        public uint entryPointRva;
        public ulong imageBase;
        public uint sectionAlignment;
        public uint fileAlignment;
        public uint sizeOfImage;
        public uint sizeOfHeaders;
        public ushort subsystem;
        public ushort dllCharacteristics;
        public List<PeSectionInfo> sections = new List<PeSectionInfo>();
        public List<string> importDlls = new List<string>();

        public bool IsPe32Plus => optionalMagic == 0x20B;
        public string MachineHex => "0x" + machine.ToString("X4");
    }

    [Serializable]
    public sealed class LegacyGameExeIdentity
    {
        public string id;
        public string evidence;
        public long fileBytes;
        public string sha256;
    }

    public static class LegacyGameExeBaseline
    {
        private static readonly LegacyGameExeIdentity[] Known =
        {
            new LegacyGameExeIdentity
            {
                id = "ps0032-x86-3.3.2.10",
                evidence = "Cliente PE32 x86 validado en auditoría y referencia ps0032.",
                fileBytes = 5_352_488,
                sha256 = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d"
            },
            new LegacyGameExeIdentity
            {
                id = "alternate-client-2026-09-20",
                evidence = "Cliente alternativo observado en una inspección estática posterior; mantener separado del baseline ps0032.",
                fileBytes = 8_297_632,
                sha256 = "4768f225250838787db5496ecf304753fb44a6c161b5290f06e836b83e0dd1e8"
            }
        };

        public static IReadOnlyList<LegacyGameExeIdentity> KnownBuilds => Known;

        public static LegacyGameExeIdentity Match(PortableExecutableInfo info)
        {
            if (info == null) return null;
            return Known.FirstOrDefault(x =>
                x.fileBytes == info.fileBytes &&
                string.Equals(x.sha256, info.sha256, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsExactKnownBuild(PortableExecutableInfo info) => Match(info) != null;
    }

    public static class PortableExecutableInspector
    {
        public static PortableExecutableInfo Analyze(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Ejecutable no encontrado.", path);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader r = new BinaryReader(stream))
            {
                if (ReadU16(r, 0) != 0x5A4D) throw new InvalidDataException("Falta firma MZ.");
                uint peOffset = ReadU32(r, 0x3C);
                if ((ulong)peOffset + 24 > (ulong)stream.Length) throw new InvalidDataException("e_lfanew fuera del archivo.");
                if (ReadU32(r, peOffset) != 0x00004550) throw new InvalidDataException("Falta firma PE\\0\\0.");

                long coff = peOffset + 4;
                ushort machine = ReadU16(r, coff);
                ushort sectionsCount = ReadU16(r, coff + 2);
                uint timestamp = ReadU32(r, coff + 4);
                ushort optionalSize = ReadU16(r, coff + 16);
                long optional = coff + 20;
                if (optional + optionalSize > stream.Length) throw new InvalidDataException("Optional header truncado.");
                ushort magic = ReadU16(r, optional);
                if (magic != 0x10B && magic != 0x20B) throw new InvalidDataException("Optional header PE32/PE32+ no reconocido.");

                PortableExecutableInfo info = new PortableExecutableInfo
                {
                    path = Path.GetFullPath(path),
                    fileBytes = stream.Length,
                    sha256 = FileFingerprint.Sha256(path),
                    machine = machine,
                    sectionCount = sectionsCount,
                    timeDateStamp = timestamp,
                    optionalMagic = magic,
                    entryPointRva = ReadU32(r, optional + 16),
                    imageBase = magic == 0x20B ? ReadU64(r, optional + 24) : ReadU32(r, optional + 28),
                    sectionAlignment = ReadU32(r, optional + 32),
                    fileAlignment = ReadU32(r, optional + 36),
                    sizeOfImage = ReadU32(r, optional + 56),
                    sizeOfHeaders = ReadU32(r, optional + 60),
                    subsystem = ReadU16(r, optional + 68),
                    dllCharacteristics = ReadU16(r, optional + 70)
                };

                long sectionTable = optional + optionalSize;
                for (int i = 0; i < sectionsCount; i++)
                {
                    long o = sectionTable + i * 40L;
                    if (o + 40 > stream.Length) throw new InvalidDataException("Section table truncada.");
                    info.sections.Add(new PeSectionInfo
                    {
                        name = ReadSectionName(r, o),
                        virtualSize = ReadU32(r, o + 8),
                        virtualAddress = ReadU32(r, o + 12),
                        rawSize = ReadU32(r, o + 16),
                        rawPointer = ReadU32(r, o + 20),
                        characteristics = ReadU32(r, o + 36)
                    });
                }

                uint numberOfDirectories = ReadU32(r, optional + (magic == 0x20B ? 108 : 92));
                long directories = optional + (magic == 0x20B ? 112 : 96);
                if (numberOfDirectories > 1 && directories + 16 <= optional + optionalSize)
                {
                    uint importRva = ReadU32(r, directories + 8);
                    if (importRva != 0) ReadImports(r, info, importRva);
                }
                return info;
            }
        }

        private static void ReadImports(BinaryReader r, PortableExecutableInfo info, uint importRva)
        {
            long descriptor = RvaToOffset(info, importRva);
            if (descriptor < 0) return;
            for (int i = 0; i < 512; i++)
            {
                long o = descriptor + i * 20L;
                if (o + 20 > r.BaseStream.Length) break;
                uint originalThunk = ReadU32(r, o);
                uint time = ReadU32(r, o + 4);
                uint forwarder = ReadU32(r, o + 8);
                uint nameRva = ReadU32(r, o + 12);
                uint firstThunk = ReadU32(r, o + 16);
                if (originalThunk == 0 && time == 0 && forwarder == 0 && nameRva == 0 && firstThunk == 0) break;
                long nameOffset = RvaToOffset(info, nameRva);
                if (nameOffset < 0 || nameOffset >= r.BaseStream.Length) continue;
                string dll = ReadAsciiZ(r, nameOffset, 512);
                if (!string.IsNullOrWhiteSpace(dll) && !info.importDlls.Contains(dll, StringComparer.OrdinalIgnoreCase)) info.importDlls.Add(dll);
            }
            info.importDlls.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static long RvaToOffset(PortableExecutableInfo info, uint rva)
        {
            if (rva < info.sizeOfHeaders) return rva;
            foreach (PeSectionInfo s in info.sections)
            {
                ulong start = s.virtualAddress;
                ulong span = Math.Max(s.virtualSize, s.rawSize);
                if ((ulong)rva >= start && (ulong)rva < start + span)
                    return checked((long)(s.rawPointer + ((ulong)rva - start)));
            }
            return -1;
        }

        private static string ReadSectionName(BinaryReader r, long offset)
        {
            r.BaseStream.Position = offset;
            byte[] b = r.ReadBytes(8);
            int end = Array.IndexOf(b, (byte)0);
            if (end < 0) end = b.Length;
            return Encoding.ASCII.GetString(b, 0, end);
        }

        private static string ReadAsciiZ(BinaryReader r, long offset, int max)
        {
            r.BaseStream.Position = offset;
            List<byte> b = new List<byte>();
            while (b.Count < max && r.BaseStream.Position < r.BaseStream.Length)
            {
                byte value = r.ReadByte();
                if (value == 0) break;
                if (value < 0x20 || value > 0x7E) return string.Empty;
                b.Add(value);
            }
            return Encoding.ASCII.GetString(b.ToArray());
        }

        private static ushort ReadU16(BinaryReader r, long o)
        {
            if (o < 0 || o + 2 > r.BaseStream.Length) throw new EndOfStreamException();
            r.BaseStream.Position = o; return r.ReadUInt16();
        }
        private static uint ReadU32(BinaryReader r, long o)
        {
            if (o < 0 || o + 4 > r.BaseStream.Length) throw new EndOfStreamException();
            r.BaseStream.Position = o; return r.ReadUInt32();
        }
        private static ulong ReadU64(BinaryReader r, long o)
        {
            if (o < 0 || o + 8 > r.BaseStream.Length) throw new EndOfStreamException();
            r.BaseStream.Position = o; return r.ReadUInt64();
        }
    }
}

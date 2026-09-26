using System;
using System.IO;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>Legacy filenames do not determine encoding. Never change source bytes.</summary>
    internal static class LegacyImageSignature
    {
        public static string ImportedFileName(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Image source is required.", nameof(sourcePath));
            byte[] header;
            using (var stream = File.OpenRead(sourcePath))
            using (var reader = new BinaryReader(stream)) header = reader.ReadBytes(128);
            return Path.ChangeExtension(Path.GetFileName(sourcePath), DetectExtension(header)).ToLowerInvariant();
        }
        public static string DetectExtension(byte[] header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (header.Length >= 128 && header[0] == 'D' && header[1] == 'D' && header[2] == 'S' && header[3] == ' ' && UInt32(header,4) == 124)
                return ".dds";
            if (header.Length >= 54 && header[0] == 'B' && header[1] == 'M' && UInt32(header,14) >= 40 &&
                UInt32(header,18) > 0 && UInt32(header,22) != 0 && header[26] == 1 && header[27] == 0)
                return ".bmp";
            if (IsTrueColorTga(header)) return ".tga";
            throw new InvalidDataException("Unsupported or truncated legacy image signature; filename extension is not trusted.");
        }
        private static bool IsTrueColorTga(byte[] data)
        {
            if(data.Length<18||data[1]!=0||(data[2]!=2&&data[2]!=10)||
                (data[16]!=24&&data[16]!=32)||(data[17]&0xc0)!=0)return false;
            // No color map. Alpha attribute bits may not exceed the actual pixel width.
            for(int i=3;i<=7;i++)if(data[i]!=0)return false;
            int width=data[12]|data[13]<<8,height=data[14]|data[15]<<8;
            return width>0&&height>0&&width<=4096&&height<=4096&&
                (data[17]&15)<=(data[16]==32?8:0);
        }
        /// <summary>Validate true-color TGA packet extents before handing an unchanged copy to Unity.</summary>
        public static void ValidateTgaPayload(byte[] data)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(data.Length>128*1024*1024||!IsTrueColorTga(data))throw new InvalidDataException("Unsupported TGA header.");
            int pixels=(data[12]|data[13]<<8)*(data[14]|data[15]<<8),stride=data[16]/8,at=18+data[0];
            if(at>data.Length)throw new EndOfStreamException("TGA identifier is truncated.");
            if(data[2]==2)
            {
                if((long)at+(long)pixels*stride>data.Length)throw new EndOfStreamException("TGA pixel data is truncated.");
                return;
            }
            int decoded=0;
            while(decoded<pixels)
            {
                if(at>=data.Length)throw new EndOfStreamException("TGA RLE packet is truncated.");
                int packet=data[at++],count=(packet&127)+1;
                if(count>pixels-decoded)throw new InvalidDataException("TGA RLE packet exceeds the image.");
                int bytes=(packet&128)!=0?stride:count*stride;
                if(bytes>data.Length-at)throw new EndOfStreamException("TGA RLE pixels are truncated.");
                at+=bytes;decoded+=count;
            }
            // Optional TGA extension/developer/footer bytes remain in the copied file.
            // They are not reinterpreted or silently removed by this base-image validator.
        }
        private static uint UInt32(byte[] b, int p)
        {
            return (uint)b[p] | ((uint)b[p+1] << 8) | ((uint)b[p+2] << 16) | ((uint)b[p+3] << 24);
        }
    }
}

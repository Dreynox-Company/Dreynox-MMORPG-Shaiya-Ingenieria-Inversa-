using System;
using System.IO;
using System.Threading;

namespace Dreynox.Mmorpg.LocalData
{
    public sealed class DecodedDds
    {
        public int Width, Height;
        /// <summary>RGBA8, bottom row first for Unity; only the source base mip.</summary>
        public byte[] Pixels;
    }
    /// <summary>Portable BC1/BC2/BC3 decoder. No Unity API or native dependency on worker threads.</summary>
    public static class LegacyDdsDecoder
    {
        public static DecodedDds Decode(byte[] data, CancellationToken token = default)
        {
            if (data == null || data.Length < 128 || U32(data, 0) != 0x20534444 || U32(data, 4) != 124 || U32(data, 76) != 32)
                throw new InvalidDataException("Cabecera DDS inválida.");
            int width = checked((int)U32(data, 16)), height = checked((int)U32(data, 12));
            if (width < 1 || height < 1 || width > 4096 || height > 4096)
                throw new InvalidDataException("Dimensiones DDS fuera del límite 4096.");
            if ((U32(data, 80) & 4) == 0 || U32(data, 112) != 0 || U32(data, 24) > 1)
                throw new NotSupportedException("Solo DDS 2D BC1/BC2/BC3; no arrays, cubemaps o volúmenes.");
            uint format = U32(data, 84);
            int kind = format == 0x31545844 ? 1 : format == 0x33545844 ? 3 : format == 0x35545844 ? 5 : 0;
            if (kind == 0) throw new NotSupportedException("DDS: se esperaba DXT1, DXT3 o DXT5.");
            int stride = kind == 1 ? 8 : 16, cols = (width + 3) / 4, rows = (height + 3) / 4;
            long required = 128L + (long)cols * rows * stride;
            if (required > data.Length) throw new EndOfStreamException("DDS base mip truncado.");
            byte[] pixels = new byte[checked(width * height * 4)];
            var colors = new byte[16]; var alpha = new byte[8];
            int source = 128;
            for (int by = 0; by < rows; by++)
            {
                token.ThrowIfCancellationRequested();
                for (int bx = 0; bx < cols; bx++, source += stride)
                {
                    int colorOffset = source + (kind == 1 ? 0 : 8);
                    ushort c0 = U16(data, colorOffset), c1 = U16(data, colorOffset + 2);
                    Color565(c0, colors, 0); Color565(c1, colors, 4);
                    bool fourColors = c0 > c1 || kind != 1;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        colors[8 + ch] = (byte)(fourColors ? (2 * colors[ch] + colors[4 + ch]) / 3 : (colors[ch] + colors[4 + ch]) / 2);
                        colors[12 + ch] = (byte)(fourColors ? (colors[ch] + 2 * colors[4 + ch]) / 3 : 0);
                    }
                    colors[11] = 255; colors[15] = fourColors ? (byte)255 : (byte)0;
                    uint selectors = U32(data, colorOffset + 4);
                    ulong alphaBits = 0;
                    if (kind == 5)
                    {
                        alpha[0] = data[source]; alpha[1] = data[source + 1];
                        if (alpha[0] > alpha[1])
                            for (int i = 2; i < 8; i++) alpha[i] = (byte)(((8-i)*alpha[0] + (i-1)*alpha[1]) / 7);
                        else
                        {
                            for (int i = 2; i < 6; i++) alpha[i] = (byte)(((6-i)*alpha[0] + (i-1)*alpha[1]) / 5);
                            alpha[6] = 0; alpha[7] = 255;
                        }
                        for (int i = 0; i < 6; i++) alphaBits |= (ulong)data[source + 2 + i] << (8*i);
                    }
                    for (int p = 0; p < 16; p++)
                    {
                        int x = bx*4 + p%4, y = by*4 + p/4;
                        if (x >= width || y >= height) continue;
                        int ci = (int)((selectors >> (2*p)) & 3)*4;
                        int dst = ((height - 1 - y)*width + x)*4;
                        pixels[dst] = colors[ci]; pixels[dst+1] = colors[ci+1]; pixels[dst+2] = colors[ci+2];
                        pixels[dst+3] = kind == 3 ? (byte)(((data[source+p/2] >> ((p%2)*4)) & 15)*17)
                            : kind == 5 ? alpha[(int)((alphaBits >> (3*p)) & 7)] : colors[ci+3];
                    }
                }
            }
            return new DecodedDds { Width = width, Height = height, Pixels = pixels };
        }
        private static void Color565(ushort c, byte[] target, int o)
        {
            int r=(c>>11)&31, g=(c>>5)&63, b=c&31;
            target[o]=(byte)((r<<3)|(r>>2)); target[o+1]=(byte)((g<<2)|(g>>4)); target[o+2]=(byte)((b<<3)|(b>>2)); target[o+3]=255;
        }
        private static ushort U16(byte[] d, int o) => (ushort)(d[o] | (d[o+1]<<8));
        private static uint U32(byte[] d, int o) => (uint)(d[o] | (d[o+1]<<8) | (d[o+2]<<16) | (d[o+3]<<24));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.NativeContent
{
    /// <summary>
    /// Bounded, lossless decoder of the canonical client's decrypted DB* tables.
    /// Names are length-prefixed UTF-16LE; numeric cells are signed Int64LE.
    /// Text tables have two Int64 keys followed by two length-prefixed CP1252 strings.
    /// The 128-byte prefix and zero footer are preserved, NOT given invented semantics.
    /// A parsed field is not evidence that its gameplay meaning or formula is understood.
    /// </summary>
    public sealed class NativeDataTable
    {
        public const int MaximumBytes = 64 * 1024 * 1024, MaximumRows = 100000;
        public const int MaximumColumns = 512, MaximumCells = 8000000, MaximumStringBytes = 65536;
        private static readonly Encoding UnicodeStrict = new UnicodeEncoding(false, false, true);
        private readonly byte[] prefix, footer;
        private readonly string[] columns;
        private readonly long[] numbers;
        private readonly string[] strings;
        private readonly Dictionary<string, int> byName;
        public IReadOnlyList<string> Columns { get; }
        public int RowCount { get; }
        public bool IsText { get; }
        public int NumberColumns => IsText ? 2 : columns.Length;
        public int SourceBytes { get; }
        public byte[] Prefix => (byte[])prefix.Clone();
        public byte[] Footer => (byte[])footer.Clone();

        private NativeDataTable(byte[] head, byte[] tail, string[] fields, int rows, long[] cells, string[] texts, int size)
        {
            prefix=head; footer=tail; columns=fields; RowCount=rows; numbers=cells;
            strings=texts; IsText=texts!=null; SourceBytes=size;
            Columns=new ReadOnlyCollection<string>(columns);
            byName=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<fields.Length;i++)byName.Add(fields[i],i);
        }
        public int Column(string name)
        {
            if(name==null)throw new ArgumentNullException(nameof(name));
            if(!byName.TryGetValue(name,out int result))throw new InvalidDataException("Required native field absent: "+name);
            return result;
        }
        public bool HasColumn(string name)=>name!=null&&byName.ContainsKey(name);
        public long Number(int row,string name)=>Number(row,Column(name));
        public long Number(int row,int column)
        {
            if(row<0||row>=RowCount||column<0||column>=NumberColumns)throw new ArgumentOutOfRangeException(nameof(row));
            return numbers[row*NumberColumns+column];
        }
        public string Text(int row,string name)=>Text(row,Column(name));
        public string Text(int row,int column)
        {
            if(!IsText||row<0||row>=RowCount||column<2||column>=4)throw new ArgumentOutOfRangeException(nameof(row));
            return strings[row*2+column-2];
        }
        public static NativeDataTable ReadNumeric(byte[] plaintext)=>Read(plaintext,false);
        public static NativeDataTable ReadText(byte[] plaintext)=>Read(plaintext,true);
        private static NativeDataTable Read(byte[] data,bool text)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(data.Length<136||data.Length>MaximumBytes)throw new InvalidDataException("Native table size outside budget.");
            using(var reader=new BinaryReader(new MemoryStream(data,false),Encoding.UTF8))
            {
                byte[] head=Exact(reader,128);
                int fields=Count(reader,MaximumColumns,"column");
                if(fields==0||text&&fields!=4)throw new InvalidDataException("Unexpected native table column shape.");
                var names=new string[fields];var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for(int i=0;i<fields;i++)
                {
                    int length=reader.ReadByte();
                    names[i]=UnicodeStrict.GetString(Exact(reader,length*2));
                    if(String.IsNullOrWhiteSpace(names[i])||names[i].IndexOf('\0')>=0||!seen.Add(names[i]))
                        throw new InvalidDataException("Empty or duplicate native column.");
                }
                int rows=Count(reader,MaximumRows,"row"),numericColumns=text?2:fields;
                if((long)rows*numericColumns>MaximumCells)throw new InvalidDataException("Native table cell budget exceeded.");
                // Validate the minimum on-wire size BEFORE allocating count-controlled arrays.
                Remaining(reader,(long)rows*(text?24:fields*8));
                var cells=new long[rows*numericColumns];var strings=text?new string[rows*2]:null;
                for(int row=0;row<rows;row++)
                {
                    for(int col=0;col<numericColumns;col++)cells[row*numericColumns+col]=reader.ReadInt64();
                    if(text)for(int col=0;col<2;col++)
                    {
                        int bytes=Count(reader,MaximumStringBytes,"text byte");
                        strings[row*2+col]=NativeWindows1252.Decode(Exact(reader,bytes));
                    }
                }
                long remaining=reader.BaseStream.Length-reader.BaseStream.Position;
                if(remaining>16)throw new InvalidDataException("Unparsed native table extension; refusing to discard it.");
                byte[] tail=Exact(reader,(int)remaining);
                foreach(byte b in tail)if(b!=0)throw new InvalidDataException("Unparsed nonzero native table footer.");
                return new NativeDataTable(head,tail,names,rows,cells,strings,data.Length);
            }
        }
        /// <summary>Lossless plaintext round-trip for evidence. Never writes to original DATA.</summary>
        public byte[] ToPlaintext()
        {
            using(var stream=new MemoryStream(SourceBytes))
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(prefix);writer.Write(columns.Length);
                    foreach(string name in columns)
                    {
                        byte[] encoded=UnicodeStrict.GetBytes(name);
                        writer.Write(checked((byte)(encoded.Length/2)));writer.Write(encoded);
                    }
                    writer.Write(RowCount);
                    for(int row=0;row<RowCount;row++)
                    {
                        for(int col=0;col<NumberColumns;col++)writer.Write(Number(row,col));
                        if(IsText)for(int col=2;col<4;col++)
                        {
                            byte[] encoded=NativeWindows1252.Encode(Text(row,col));writer.Write(encoded.Length);writer.Write(encoded);
                        }
                    }
                    writer.Write(footer);
                }
                return stream.ToArray();
            }
        }
        private static int Count(BinaryReader reader,int maximum,string label)
        {
            Remaining(reader,4);int value=reader.ReadInt32();
            if(value<0||value>maximum)throw new InvalidDataException("Invalid native "+label+" count.");return value;
        }
        private static byte[] Exact(BinaryReader reader,int length)
        {Remaining(reader,length);return reader.ReadBytes(length);}
        private static void Remaining(BinaryReader reader,long bytes)
        {
            if(bytes<0||reader.BaseStream.Length-reader.BaseStream.Position<bytes)
                throw new EndOfStreamException("Truncated native table.");
        }
    }

    /// <summary>Stable Windows-1252, including undefined control bytes, without OS code-page providers.</summary>
    public static class NativeWindows1252
    {
        private const string Extended="\u20ac\u0081\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u008d\u017d\u008f\u0090\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u009d\u017e\u0178";
        public static string Decode(byte[] bytes)
        {
            if(bytes==null)throw new ArgumentNullException(nameof(bytes));
            var chars=new char[bytes.Length];
            for(int i=0;i<bytes.Length;i++){byte b=bytes[i];chars[i]=b>=128&&b<160?Extended[b-128]:(char)b;}
            return new string(chars);
        }
        public static byte[] Encode(string text)
        {
            if(text==null)throw new ArgumentNullException(nameof(text));var bytes=new byte[text.Length];
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];int special=Extended.IndexOf(c);
                if(c<128||c>=160&&c<=255)bytes[i]=(byte)c;
                else if(special>=0)bytes[i]=(byte)(128+special);
                else throw new InvalidDataException("Text cannot round-trip as native Windows-1252.");
            }
            return bytes;
        }
    }
}

using Dreynox.Mmorpg.LocalData;
using System.IO;
using System.Threading;

int passed = 0;
void Check(bool condition, string text) { if (!condition) throw new Exception(text); passed++; Console.WriteLine("PASS " + text); }
void Throws<T>(Action a, string name) where T : Exception { try { a(); } catch (T) { Check(true, name); return; } throw new Exception(name); }
byte[] Header(string codec, int bytes)
{
    byte[] d = new byte[128 + bytes];
    void W(int o, uint v) { Array.Copy(BitConverter.GetBytes(v), 0, d, o, 4); }
    W(0,0x20534444); W(4,124); W(12,4); W(16,4); W(76,32); W(80,4);
    Array.Copy(System.Text.Encoding.ASCII.GetBytes(codec),0,d,84,4); return d;
}
byte[] bc1 = Header("DXT1",8); bc1[128]=0; bc1[129]=0xf8;
DecodedDds red=LegacyDdsDecoder.Decode(bc1);
Check(red.Pixels.Length == 64 && red.Pixels[0]==255 && red.Pixels[1]==0 && red.Pixels[3]==255,"BC1 red / row layout");
byte[] transparent=Header("DXT1",8); transparent[130]=0xff; transparent[131]=0xff;
for(int i=132;i<136;i++) transparent[i]=255;
Check(LegacyDdsDecoder.Decode(transparent).Pixels[3]==0,"BC1 transparent fourth color");
byte[] bc2=Header("DXT3",16);
for(int i=128;i<136;i++)bc2[i]=0x88;
bc2[136]=0;bc2[137]=0xf8;
Check(LegacyDdsDecoder.Decode(bc2).Pixels[3]==136,"BC2 four-bit alpha");
byte[] bc3=Header("DXT5",16);bc3[128]=180;bc3[129]=20;bc3[136]=0;bc3[137]=0xf8;
Check(LegacyDdsDecoder.Decode(bc3).Pixels[3]==180,"BC3 alpha endpoint");
Throws<EndOfStreamException>(()=>LegacyDdsDecoder.Decode(bc1[..^1]),"reject truncated mip");
Throws<NotSupportedException>(()=>LegacyDdsDecoder.Decode(Header("DX10",16)),"reject unimplemented DDS variant");
Throws<OperationCanceledException>(()=>LegacyDdsDecoder.Decode(bc1,new CancellationToken(true)),"DDS cancellation");
string root=Path.Combine(Path.GetTempPath(),"dx-local-test-"+Guid.NewGuid().ToString("N"));
try
{
    string data=Path.Combine(root,"DATA_Español");Directory.CreateDirectory(Path.Combine(data,"Character"));
    string file=Path.Combine(data,"Character","Test.bin");File.WriteAllBytes(file,new byte[]{1,2,3});
    var folder=new LocalDataFolder(root);
    Check(folder.Read("DATA_Español/character/test.bin",default).SequenceEqual(new byte[]{1,2,3}),"case-insensitive local corpus");
    Check(new LocalDataFolder(data).Root==data,"accept direct DATA path");
    Throws<ArgumentException>(()=>folder.Read("../test.bin",default),"reject parent traversal");
    Throws<ArgumentException>(()=>folder.Read("character/C:file.bin",default),"reject alternate streams");
    Throws<ArgumentException>(()=>folder.Read("character//Test.bin",default),"reject empty components");
    Throws<InvalidDataException>(()=>folder.Read("character/Test.bin",default,2),"enforce read budget");
    Throws<OperationCanceledException>(()=>folder.Read("character/Test.bin",new CancellationToken(true)),"IO cancellation");
    Check(File.ReadAllBytes(file).SequenceEqual(new byte[]{1,2,3}),"original bytes unchanged");
}
finally { Directory.Delete(root,true); }
Console.WriteLine("LOCAL DATA HARNESS OK: "+passed+" checks");

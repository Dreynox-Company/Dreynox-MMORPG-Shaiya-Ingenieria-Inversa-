using Dreynox.Mmorpg.ParityCore;
using System.Text;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);checks++;}
void Reject(Action work,string name)
{
    try{work();}catch(Exception ex)when(ex is InvalidDataException||ex is EndOfStreamException||ex is ArgumentException)
    {Check(true,name);return;}
    throw new Exception("Expected rejection: "+name);
}
byte[] Fixture(string mesh="body.3dc",int rowMesh=0,int rowTexture=1,int flag=7)
{
    using var stream=new MemoryStream();using var w=new BinaryWriter(stream,Encoding.ASCII,true);
    void Names(params string[] names){w.Write(names.Length);foreach(string n in names){byte[] b=Encoding.ASCII.GetBytes(n+"\0");w.Write(b.Length);w.Write(b);}}
    w.Write(Encoding.ASCII.GetBytes("ML2"));Names(mesh);Names("not-chosen.dds","authored-choice.dds");
    w.Write(1);w.Write(rowMesh);w.Write(rowTexture);w.Write(flag);w.Flush();return stream.ToArray();
}
var source=Fixture();var copy=(byte[])source.Clone();var parsed=LegacyModelListCore.Parse(source);
Check(parsed.Meshes.Count==1&&parsed.Textures.Count==2&&parsed.Entries.Count==1,"independent mesh/texture tables");
Check(parsed.Entries[0].SourceFlag==7,"third record word preserved rather than invented meaning");
Check(parsed.Textures[parsed.Entries[0].TextureIndex]=="authored-choice.dds","texture index is not mesh index");
Check(copy.SequenceEqual(source),"original bytes unchanged");
Reject(()=>LegacyModelListCore.Parse(Fixture(rowMesh:1)),"invalid mesh index");
Reject(()=>LegacyModelListCore.Parse(Fixture(rowTexture:2)),"invalid texture index");
Reject(()=>LegacyModelListCore.Parse(Fixture(rowTexture:-1)),"negative texture index");
Reject(()=>LegacyModelListCore.Parse(Fixture("../body.3dc")),"path traversal");
Reject(()=>LegacyModelListCore.Parse(Fixture("C:body.3dc")),"drive/alternate stream");
Reject(()=>LegacyModelListCore.Parse(Fixture("body.tga")),"wrong mesh format");
Reject(()=>LegacyModelListCore.Parse(source.Concat(new byte[]{0}).ToArray()),"trailing bytes never ignored");
Reject(()=>LegacyModelListCore.Parse(source[..^1]),"truncated record");
byte[] wrong=(byte[])source.Clone();wrong[2]=(byte)'1';Reject(()=>LegacyModelListCore.Parse(wrong),"unsupported ML1 explicit");
Reject(()=>LegacyModelListCore.Parse(new byte[LegacyModelListCore.MaximumBytes+1]),"bounded file memory");
var requested=new List<string>();
var appearance=LegacyDefaultAppearanceCore.Resolve(0,0,0,0,0,path=>{requested.Add(path);return Fixture();});
Check(requested.Count==6&&requested.All(p=>p.StartsWith("DATA_Español/character/human/humf_")),"six exact rig model-list resources");
Check(appearance.SetId==-1&&!appearance.UpperMesh.Contains("co_"),"default appearance is not costume set003");
Check(appearance.UpperTexture.EndsWith("authored-choice.dds"),"default character resolves authored texture row");
Reject(()=>LegacyDefaultAppearanceCore.Resolve(0,0,0,1,0,_=>Fixture()),"face selection cannot invent absent row");
Reject(()=>LegacyDefaultAppearanceCore.Resolve(0,0,0,0,-1,_=>Fixture()),"negative hair row rejected");
for(int rig=0;rig<16;rig++)
{
    var identity=LegacyCharacterRigCore.ResolveNativeRigIndex(rig);
    var resolved=LegacyDefaultAppearanceCore.Resolve(identity.Family,identity.Job,identity.Sex,0,0,_=>Fixture());
    Check(resolved.Rig.NativeRigIndex==rig,"shared model-list path resolves rig "+rig);
}
Console.WriteLine("NATIVE APPEARANCE HARNESS OK: "+checks+" checks");

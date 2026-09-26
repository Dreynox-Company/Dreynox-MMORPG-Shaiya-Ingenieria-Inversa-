using System.Text;
using Dreynox.Mmorpg.NativeContent;
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;}
void Reject(Action action,string label)
{try{action();}catch(Exception ex)when(ex is ArgumentException||ex is InvalidDataException||ex is EndOfStreamException){Check(true,label);return;}throw new Exception("Expected rejection: "+label);}
byte[] Numeric(string[] columns,long[][] rows,int tail=2)
{
    using var memory=new MemoryStream();using(var w=new BinaryWriter(memory,Encoding.UTF8,true))
    {
        w.Write(Enumerable.Range(0,128).Select(v=>(byte)v).ToArray());w.Write(columns.Length);
        foreach(string column in columns){w.Write((byte)column.Length);w.Write(Encoding.Unicode.GetBytes(column));}
        w.Write(rows.Length);foreach(var row in rows)foreach(long value in row)w.Write(value);w.Write(new byte[tail]);
    }
    return memory.ToArray();
}
byte[] TextTable(string[] columns,(long id,long variant,string name,string text)[] rows,int tail=0)
{
    using var memory=new MemoryStream();using(var w=new BinaryWriter(memory,Encoding.UTF8,true))
    {
        w.Write(new byte[128]);w.Write(columns.Length);
        foreach(string column in columns){w.Write((byte)column.Length);w.Write(Encoding.Unicode.GetBytes(column));}
        w.Write(rows.Length);foreach(var row in rows)
        {
            w.Write(row.id);w.Write(row.variant);
            foreach(string value in new[]{row.name,row.text}){byte[] encoded=NativeWindows1252.Encode(value);w.Write(encoded.Length);w.Write(encoded);}
        }
        w.Write(new byte[tail]);
    }
    return memory.ToArray();
}
string[] names={"itemtype","itemtypeid","image","level","icon","unclassified"};
var input=Numeric(names,new[]{new long[]{1,1,0,1,1,long.MinValue},new long[]{45,2,4,7,101,long.MaxValue}});
var table=NativeDataTable.ReadNumeric(input);
Check(table.RowCount==2&&table.Columns.Count==6&&table.Number(1,"unclassified")==long.MaxValue,"all signed 64-bit fields preserved");
Check(table.Number(0,"unclassified")==long.MinValue&&table.Number(0,"ICON")==1,"minimum signed field and ordinal case-independent schema");
Check(table.ToPlaintext().SequenceEqual(input),"numeric prefix/body/footer byte-exact roundtrip");
var prefix=table.Prefix;prefix[0]=255;Check(table.Prefix[0]==0,"opaque prefix cannot mutate retained table");
Array.Fill(input,(byte)0);Check(table.Number(1,"image")==4,"input mutation cannot alter parsed cells");
var text=NativeDataTable.ReadText(TextTable(new[]{"itemtype","itemtypeid","itemname","text"},new[]{(45L,2L,"Árbol de prueba","{c5}Sólo lectura{/c}\\n€"),(1L,1L,"Espada de prueba","No concede un objeto.")}));
var joined=new NativeDefinitionCatalog(table,text,false);
Check(joined.Count==2&&joined.TryItem(257,out var first)&&first.Name=="Espada de prueba","join uses native keys, not text-table row order");
Check(joined.TryItem(45<<8|2,out var second)&&second.Description=="Sólo lectura\n€","native colors converted for plain safe display");
Check(second.OriginalDescription=="{c5}Sólo lectura{/c}\\n€","raw native formatting retained");
var found=joined.Search("arbol",0,12,out int total);Check(total==1&&found[0].Key.Id==45,"accent-insensitive search");
Check(joined.Search("",1,1,out total).Count==1&&total==2,"deterministic pagination");
Check(joined.Search("no existe",0,12,out total).Count==0&&total==0,"empty search result is not a fabricated item");
Reject(()=>joined.Search(new string('x',129),0,12,out _),"bounded search query");
Reject(()=>joined.Search("",0,101,out _),"bounded page size");
Check(!joined.TryItem(0,out _)&&!joined.TryItem(65536,out _),"invalid item keys cannot wrap into catalog records");
Check(NativeDescription.Plain("<size=900>x</size> {unknown} {c123}á{/c}$nfin")=="<size=900>x</size> {unknown} á\nfin","unknown text preserved, no Unity markup conversion");
byte[] all=Enumerable.Range(0,256).Select(v=>(byte)v).ToArray();
Check(NativeWindows1252.Encode(NativeWindows1252.Decode(all)).SequenceEqual(all),"all 256 CP1252 bytes including undefined controls roundtrip");
Reject(()=>NativeWindows1252.Encode("🐼"),"unrepresentable CP1252 text is not replaced silently");
Reject(()=>NativeDataTable.ReadNumeric(new byte[135]),"short table header rejected");
Reject(()=>NativeDataTable.ReadNumeric(Numeric(new[]{"x","X"},Array.Empty<long[]>())),"duplicate case-folded columns rejected");
Reject(()=>NativeDataTable.ReadNumeric(Numeric(new[]{""},Array.Empty<long[]>())),"empty column rejected");
Reject(()=>NativeDataTable.ReadNumeric(Numeric(names,Array.Empty<long[]>(),17)),"unknown footer extension rejected");
var bad=Numeric(names,Array.Empty<long[]>());bad[^1]=1;Reject(()=>NativeDataTable.ReadNumeric(bad),"nonzero footer rejected");
var valid=Numeric(names,new[]{new long[]{1,1,0,1,1,0}});
for(int at=0;at<valid.Length-2;at++)
{int length=at;Reject(()=>NativeDataTable.ReadNumeric(valid[..length]),"truncation at "+at);}
var tooMany=(byte[])valid.Clone();BitConverter.GetBytes(513).CopyTo(tooMany,128);
Reject(()=>NativeDataTable.ReadNumeric(tooMany),"column allocation budget checked first");
var textBytes=TextTable(new[]{"itemtype","itemtypeid","itemname","text"},new[]{(1L,1L,"Nombre","Texto con acentos: áéíóú ñ € \\n")},3);
Check(NativeDataTable.ReadText(textBytes).ToPlaintext().SequenceEqual(textBytes),"text original lengths and byte encodings roundtrip");
for(int i=0;i<textBytes.Length-3;i+=7){int at=i;Reject(()=>NativeDataTable.ReadText(textBytes[..at]),"text truncation "+at);}
Reject(()=>NativeDataTable.ReadText(valid),"numeric schema cannot impersonate a text table");
var duplicateNumeric=NativeDataTable.ReadNumeric(Numeric(names,new[]{new long[]{1,1,0,1,1,0},new long[]{1,1,0,1,2,0}}));
Reject(()=>new NativeDefinitionCatalog(duplicateNumeric,text,false),"duplicate numeric identity rejected");
var duplicateText=NativeDataTable.ReadText(TextTable(new[]{"itemtype","itemtypeid","itemname","text"},new[]{(1L,1L,"A",""),(1L,1L,"B","")}));
Reject(()=>new NativeDefinitionCatalog(table,duplicateText,false),"duplicate text identity rejected");
var missingText=NativeDataTable.ReadText(TextTable(new[]{"itemtype","itemtypeid","itemname","text"},new[]{(1L,1L,"A","")}));
Reject(()=>new NativeDefinitionCatalog(table,missingText,false),"missing native text rejected rather than hidden");
var oneNumeric=NativeDataTable.ReadNumeric(valid);Reject(()=>new NativeDefinitionCatalog(oneNumeric,text,false),"orphan text row cannot be silently dropped");
var wrongId=NativeDataTable.ReadNumeric(Numeric(names,new[]{new long[]{256,1,0,1,1,0}}));
Reject(()=>new NativeDefinitionCatalog(wrongId,text,false),"item type overflow rejected");
var skillNumeric=NativeDataTable.ReadNumeric(Numeric(new[]{"skilllevel","id","level","image","point"},new[]{new long[]{1,804,80,2011,1}},13));
var skillText=NativeDataTable.ReadText(TextTable(new[]{"id","skilllevel","name","text"},new[]{(804L,1L,"Habilidad de prueba","Descripción")}));
var skills=new NativeDefinitionCatalog(skillNumeric,skillText,true);
Check(skills.TryGet(804,1,out var skill)&&skill.Value("point")==1,"skill key and column-name layout, not fixed numeric offsets");
Check(!skills.TryItem(257,out _),"a complete skill catalog is not an inventory or learned-skill grant");
Check(!NativeItemIcon.TryResolve(1,0,out _)&&!NativeItemIcon.TryResolve(1,256,out _),"invalid icon index not clamped into an unrelated item");
Check(NativeItemIcon.TryResolve(1,1,out var icon)&&icon.File=="01.dds"&&icon.X==0&&icon.Y==0,"one-based native starter sword icon");
Check(NativeItemIcon.TryResolve(1,5,out icon)&&icon.X==0&&icon.Y==32,"four-column equipment icon atlas");
Check(NativeItemIcon.TryResolve(45,101,out icon)&&icon.File=="101.dds"&&icon.X==0&&icon.Y==0,"normalized type and second 100-icon bank");
Check(NativeItemIcon.TryResolve(25,17,out icon)&&icon.File=="icon_somo.dds"&&icon.X==0&&icon.Y==32,"named registration overwrites numeric family atlas");
Check(NativeItemIcon.TryResolve(95,9,out icon)&&icon.File=="icon_rapis.dds"&&icon.Y==32,"eight-column lapis source");
Check(NativeItemIcon.TryResolve(121,1,out icon)&&icon.File=="icon_wing.dds","original wing source, not invented numerical filename");
Check(!icon.Fits(16,16)&&icon.Fits(512,512),"crop requires actual source extent");
var projection=new NativeInventoryProjection();var owned=new Dictionary<int,int>{{257,3},{0x2d02,1}};
projection.Synchronize(owned,3000);Check(projection.Count==2&&projection.Gold==3000&&projection.PageCount==1,"inventory projection reads actual supplied ownership and gold");
owned[257]=90;Check(projection.TryCell(0,out var visible)&&visible.Count==3,"inventory snapshot not aliased to mutable owner input");
Check(!projection.TryCell(2,out _)&&!projection.TryCell(-1,out _)&&!projection.SelectPage(1),"empty cells and unavailable pages cannot expose catalog items");
int before=projection.Revision;Reject(()=>projection.Synchronize(new Dictionary<int,int>{{257,0}},1),"invalid item count rejected");
Check(projection.Gold==3000&&projection.Revision==before,"failed snapshot validation is atomic");
var many=new Dictionary<int,int>();for(int i=1;i<=130;i++)many[256|i]=i;
projection.Synchronize(many,7);Check(projection.PageCount==6&&projection.SelectPage(5)&&projection.TryCell(9,out visible)&&visible.Key==386,"more than five pages retain every owned type");
projection.Synchronize(new Dictionary<int,int>(),0);Check(projection.Page==0&&projection.PageCount==1&&projection.Count==0,"shrink clamps page without granting or deleting source items");
for(int i=0;i<24;i++)Check(NativeInventoryProjection.X(i)>=19&&NativeInventoryProjection.X(i)+32<=268&&NativeInventoryProjection.Y(i)+32<=456,"native inventory cell bounds "+i);
Reject(()=>NativeInventoryProjection.X(24),"invalid cell geometry rejected");
var random=new Random(4137);
for(int test=0;test<100;test++)
{
    int count=random.Next(0,60),fields=random.Next(1,30);var rows=new long[count][];
    for(int i=0;i<count;i++){rows[i]=new long[fields];for(int j=0;j<fields;j++)rows[i][j]=random.NextInt64(long.MinValue,long.MaxValue);}
    byte[] data=Numeric(Enumerable.Range(0,fields).Select(i=>"f"+i).ToArray(),rows,random.Next(0,17));
    Check(NativeDataTable.ReadNumeric(data).ToPlaintext().SequenceEqual(data),"generated complete-table roundtrip "+test);
}
Console.WriteLine("NATIVE CONTENT CONTRACTS OK: "+checks+" checks. Synthetic malformed/round-trip tests; NOT original corpus or gameplay/render equivalence.");
if(args.Length==1)
{
    string root=Path.GetFullPath(args[0]);var tables=new NativeDataTable[4];
    string[] files={"dbitemdata.plain","dbitemtext_spn.plain","dbskilldata.plain","dbskilltext_spn.plain"};
    for(int i=0;i<4;i++)
    {
        byte[] data=File.ReadAllBytes(Path.Combine(root,files[i]));tables[i]=i%2==0?NativeDataTable.ReadNumeric(data):NativeDataTable.ReadText(data);
        Check(tables[i].ToPlaintext().SequenceEqual(data),"supplied original bytes "+files[i]);
    }
    var items=new NativeDefinitionCatalog(tables[0],tables[1],false);var originalSkills=new NativeDefinitionCatalog(tables[2],tables[3],true);
    Console.WriteLine("SUPPLIED CORPUS: "+items.Count+" item rows, "+originalSkills.Count+" skill-level rows; all four plaintext byte roundtrips verified.");
}

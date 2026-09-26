using Dreynox.Mmorpg.Networking;
int count=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);count++;}
void Reject(Action work,string name)
{
    try{work();}catch(Exception ex)when(ex is InvalidDataException||ex is ArgumentException)
    {Check(true,name);return;}
    throw new Exception("Expected native body rejection: "+name);
}
var cases=new[]{
    (new NativeQuestRequest(NativeQuestRequestKind.Accept,0x78563412,3400),"020912345678480D"),
    (new NativeQuestRequest(NativeQuestRequestKind.Complete,0x78563412,3400),"030912345678480D"),
    (new NativeQuestRequest(NativeQuestRequestKind.CompleteWithChoice,0x78563412,3400,5),"070912345678480D05"),
    (new NativeQuestRequest(NativeQuestRequestKind.Abandon,0,3400),"0809480D")};
foreach(var (request,hex) in cases)
{
    byte[] expected=Convert.FromHexString(hex);
    Check(NativeQuestRequestCodec.Encode(request).SequenceEqual(expected),"native exact field order "+request.Kind);
    var decoded=NativeQuestRequestCodec.Decode(expected);
    Check(decoded.Kind==request.Kind&&decoded.NpcRuntimeId==request.NpcRuntimeId&&decoded.QuestId==3400&&decoded.RewardChoice==request.RewardChoice,
        "native roundtrip "+request.Kind);
    Reject(()=>NativeQuestRequestCodec.Decode(expected[..^1]),"truncated "+request.Kind);
    Reject(()=>NativeQuestRequestCodec.Decode(expected.Concat(new byte[]{0}).ToArray()),"trailing field "+request.Kind);
}
var high=new NativeQuestRequest(NativeQuestRequestKind.CompleteWithChoice,uint.MaxValue,ushort.MaxValue,byte.MaxValue);
var highDecoded=NativeQuestRequestCodec.Decode(NativeQuestRequestCodec.Encode(high));
Check(highDecoded.NpcRuntimeId==uint.MaxValue&&highDecoded.QuestId==ushort.MaxValue&&highDecoded.RewardChoice==byte.MaxValue,
    "wire widths preserved without signed narrowing or invented reward limit");
Reject(()=>NativeQuestRequestCodec.Encode(default),"empty command rejected");
Reject(()=>NativeQuestRequestCodec.Decode(new byte[]{1,9,1,0}),"unsupported server-list opcode never treated as client command");
Reject(()=>new NativeQuestRequest(NativeQuestRequestKind.Abandon,1,3400),"NPC cannot leak into abandonment");
Reject(()=>new NativeQuestRequest(NativeQuestRequestKind.Accept,1,3400,1),"choice cannot leak into acceptance");
Console.WriteLine("NATIVE QUEST BODY CONTRACTS OK: "+count+" checks; no native network session executed.");

MerchantBodyCases.Run();

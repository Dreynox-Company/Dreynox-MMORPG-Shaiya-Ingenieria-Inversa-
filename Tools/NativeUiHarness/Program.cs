using Dreynox.Mmorpg.UI.Core;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.ParityCore;
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label)
{
    try{action();}catch(Exception e)when(e is ArgumentException||e is InvalidDataException||e is IOException||e is InvalidOperationException)
    {Check(true,label);return;}throw new Exception("Expected rejection: "+label);
}
var ui=new NativeQuickbarCore();
Check(ui.Snapshot().actions.Length==50&&ui.ActionAt(0,0)==1&&ui.Snapshot().actions.Count(a=>a!=0)==1,"only one actual action, not four invented skills");
foreach(bool vertical in new[]{false,true})
{
    var bar=NativeHudGeometry.Bar(vertical);
    for(int i=0;i<10;i++)
    {
        var r=NativeHudGeometry.Cell(i,vertical);
        Check(r.Width==32&&r.Height==32&&r.X>=0&&r.Y>=0&&r.X+32<=bar.Width&&r.Y+32<=bar.Height,"native cell bounds "+vertical+" / "+i);
        if(i>0)
        {
            var before=NativeHudGeometry.Cell(i-1,vertical);
            Check(vertical?r.Y-before.Y==40:r.X-before.X==40,"native 40-pixel cell stride "+vertical+" / "+i);
        }
    }
}
Check(NativeHudGeometry.ClampPixel(9999,1024,446)==578&&NativeHudGeometry.ClampPixel(-12,1024,446)==0,"integer viewport clamp");
Check(NativeHudGeometry.ClampPixel(3.5,1024,446)==4&&NativeHudGeometry.ClampPixel(10,20,53)==0,"half pixel and undersized viewport");
Reject(()=>NativeHudGeometry.ClampPixel(double.NaN,100,10),"nonfinite UI position");
Reject(()=>NativeHudGeometry.Cell(10,false),"eleventh slot rejected");
Check(ui.Move(0,0,4,9,ui.Revision)&&ui.Snapshot().actions[49]==1&&ui.ActionAt(0,0)==0,"cross-page binding move preserves action");
Check(ui.SetPage(0,4)&&ui.ActionAt(0,9)==1,"fifth native page selected");
Reject(()=>ui.SetPage(0,5),"sixth native page rejected");
int revision=ui.Revision;ui.Persist=_=>false;
Check(!ui.Rotate(0)&&!ui.Vertical(0)&&ui.Revision==revision,"failed orientation save rolls back");
Check(!ui.Move(4,9,0,0,revision)&&ui.Snapshot().actions[49]==1,"failed move save keeps original access");
ui.Persist=_=>true;Check(ui.Rotate(0)&&ui.Vertical(0),"orientation transaction succeeds");
Check(!ui.Move(4,9,0,0,revision)&&ui.Snapshot().actions[49]==1,"stale drag cannot overwrite newer configuration");
var external=ui.Snapshot();external.actions[49]=0;external.bars[0].page=0;
Check(ui.ActionAt(0,9)==1&&ui.Page(0)==4,"snapshot mutation cannot alter live state");
ui.Persist=s=>{s.actions[49]=99;return true;};Check(ui.Rotate(0)&&ui.ActionAt(0,9)==1,"persistence callback cannot mutate committed action contents");
ui.Persist=null;int calls=0;
Check(ui.Activate(0,9,100,false,_=>{calls++;return true;}),"bound action dispatch");
Check(!ui.Activate(0,9,100,false,_=>{calls++;return true;})&&calls==1,"keyboard plus click dispatches once per frame");
Check(!ui.Activate(0,9,101,true,_=>{calls++;return true;})&&calls==1,"modal ownership blocks shortcuts");
Check(!ui.Activate(0,0,102,false,_=>{calls++;return true;})&&calls==1,"empty slot cannot execute");
bool nested=true;
Check(ui.Activate(0,9,103,false,_=>{nested=ui.Activate(0,9,104,false,__=>true);return true;})&&!nested,"reentrant action dispatch rejected");
Check(!ui.Activate(0,9,105,false,_=>false)&&!ui.Activate(0,9,105,false,_=>true),"rejected action cannot retry twice in same frame");
Check(ui.ToggleNextBar(0)&&ui.VisibleBars==2&&ui.ToggleNextBar(1)&&ui.VisibleBars==3,"additional native bars enabled");
Check(ui.ToggleNextBar(0)&&ui.VisibleBars==1,"collapsing first extra bar also hides dependent bar");
Check(!ui.Activate(1,0,106,false,_=>true),"hidden bar cannot trigger input");
foreach(Action<QuickbarSave> corrupt in new Action<QuickbarSave>[] {
 s=>s.actions[0]=99,s=>s.actions=null,s=>s.bars=null,s=>s.bars[0].page=5,s=>s.visibleBars=4,s=>s.revision=-1,s=>s.schema=2})
{
    var bad=ui.Snapshot();corrupt(bad);Reject(()=>ui.Restore(bad),"invalid preference contract");
    Check(ui.ActionAt(0,9)==1,"failed restore is atomic");
}
var diskReentry=new NativeQuickbarCore();bool nestedWrite=true;
diskReentry.Persist=_=>{nestedWrite=diskReentry.Rotate(1);return true;};
Check(diskReentry.Rotate(0)&&!nestedWrite&&!diskReentry.Vertical(1),"save callback cannot start nested configuration write");
Check(!default(HudResourceValue).Available&&default(HudResourceValue).Fraction==0,"unknown vitals do not pretend to be full");
Check(new HudResourceValue(25,100).Fraction==.25f,"resource fill bound to actual supplied ratio");
Reject(()=>new HudResourceValue(1,0),"zero resource maximum rejected");
Reject(()=>new HudResourceValue(101,100),"overfull resource rejected");
var combat=new CombatCore();combat.RegisterTarget(1,300);combat.SelectTarget(1);
Check(combat.RequestAttack(95,.12,.18,.82)&&combat.AttackRemainingFraction==1,"cooldown starts with accepted attack");
combat.Tick(.5);Check(Math.Abs(combat.AttackRemainingFraction-.5)<1e-8,"cooldown reports actual phase time, not an independent timer");
combat.Tick(.5);Check(combat.AttackRemainingFraction==0&&combat.Phase==AttackPhase.Idle,"cooldown reaches zero with idle");
string temp=Path.Combine(Path.GetTempPath(),"NativeUiTests-"+Guid.NewGuid().ToString("N"));
try
{
    var store=new NativeUiLocalStore(temp);
    Check(store.Read("quickbar.json")==null,"missing preferences are distinct from corruption");
    store.Write("quickbar.json","{\"test\":\"español\"}");Check(store.Read("quickbar.json").Contains("español"),"UTF-8 preferences roundtrip");
    store.Write("quickbar.json","replacement");Check(store.Read("quickbar.json")=="replacement","atomic replacement of local UI preferences");
    Check(Directory.GetFiles(temp,"*.tmp").Length==0,"temporary writes cleaned up");
    Reject(()=>store.Write("../native-config.ini","bad"),"UI preferences cannot escape to native config");
    Reject(()=>store.Write("quickbar.json",new string('a',NativeUiLocalStore.MaximumBytes+1)),"oversized UI write rejected");
    Check(store.Read("quickbar.json")=="replacement","failed write preserves prior preferences");
    File.WriteAllBytes(Path.Combine(temp,"oversized.json"),new byte[NativeUiLocalStore.MaximumBytes+1]);
    Reject(()=>store.Read("oversized.json"),"bounded preference reading");
    if(!OperatingSystem.IsWindows())
    {
        File.CreateSymbolicLink(Path.Combine(temp,"linked.json"),Path.Combine(temp,"quickbar.json"));
        Reject(()=>store.Write("linked.json","bad"),"linked preferences rejected on write");
        Reject(()=>store.Read("linked.json"),"linked preferences rejected on read");
    }
}
finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
Console.WriteLine("NATIVE UI CONTRACTS OK: "+checks+" checks. These are logic/filesystem contracts, NOT rendered visual parity.");

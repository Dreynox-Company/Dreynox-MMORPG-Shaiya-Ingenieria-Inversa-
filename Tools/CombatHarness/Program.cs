using Dreynox.Mmorpg.ParityCore;

int checks = 0;
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS " + name); checks++;
}
foreach (int fps in new[] {30,60,144})
{
    var core = new CombatCore(); core.RegisterTarget(1,100);core.SelectTarget(1);
    Check(core.RequestAttack(25), "request " + fps);
    for(int i=0;i<fps;i++) core.Tick(1.0/fps);
    Check(core.Phase==AttackPhase.Idle && core.HitSerial==1 && core.Targets[1].Health==75,
        "one impact and complete recovery " + fps);
    Check(Math.Abs(core.GuardRemaining-7.18)<1e-8,"logical impact time " + fps);
    Check(core.RequestAttack(25) && core.AttackSerial==2 && !core.RequestAttack(25) && core.AttackSerial==2,
        "only accepted commands restart animation " + fps);
}
var longFrame = new CombatCore(); longFrame.RegisterTarget(4,100);longFrame.SelectTarget(4);
longFrame.RequestAttack(40);longFrame.Tick(1);
Check(longFrame.Targets[4].Health==60 && longFrame.HitSerial==1 && longFrame.Phase==AttackPhase.Idle,"long frame");
longFrame.Tick(9);Check(!longFrame.InCombatGuard,"guard expires without drift");
bool rejectsNan=false;try{longFrame.RequestAttack(25,double.NaN);}catch(ArgumentOutOfRangeException){rejectsNan=true;}
Check(rejectsNan,"non-finite timing rejected");
Console.WriteLine("COMBAT TIMING HARNESS OK: " + checks + " checks");

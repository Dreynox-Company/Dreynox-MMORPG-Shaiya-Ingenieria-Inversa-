using System;
using System.IO;

namespace Dreynox.Mmorpg.UI.Core
{
    [Serializable] public sealed class QuickbarViewState
    {
        public int page;
        public bool vertical;
    }
    [Serializable] public sealed class QuickbarSave
    {
        public int schema = 1, revision, visibleBars = 1;
        public int[] actions = new int[50];
        public QuickbarViewState[] bars = {new QuickbarViewState(),new QuickbarViewState{page=1},new QuickbarViewState{page=2}};
    }
    /// <summary>
    /// Native UI contract: ten cells, five pages (0x59663E..0x596692), three
    /// configurable bars. Local action 1 is a binding, NOT native opcode 1.
    /// This is UI configuration only; it cannot create items or learn skills.
    /// </summary>
    public sealed class NativeQuickbarCore
    {
        public const int Slots = 10, Pages = 5, Bars = 3, BasicAttack = 1;
        private QuickbarSave state = new QuickbarSave();
        private bool writing, dispatching;
        private long lastInputFrame = -1;
        public Func<QuickbarSave,bool> Persist {get;set;}
        public int Revision => state.revision;
        public int VisibleBars => state.visibleBars;
        public string Failure {get;private set;} = "";
        public NativeQuickbarCore() {state.actions[0]=BasicAttack;}
        public int Page(int bar) {CheckBar(bar);return state.bars[bar].page;}
        public bool Vertical(int bar) {CheckBar(bar);return state.bars[bar].vertical;}
        public int ActionAt(int bar,int slot) {CheckCell(Page(bar),slot);return state.actions[Page(bar)*Slots+slot];}
        public QuickbarSave Snapshot() => Copy(state);
        public void Restore(QuickbarSave saved)
        {
            if(writing||dispatching)throw new InvalidOperationException("UI transaction in progress.");
            Validate(saved);state=Copy(saved);lastInputFrame=-1;
        }
        public bool SetPage(int bar,int page)
        {
            CheckBar(bar);if(page<0||page>=Pages)throw new ArgumentOutOfRangeException(nameof(page));
            if(Page(bar)==page)return false;
            var next=Copy(state);next.bars[bar].page=page;return Commit(next);
        }
        public bool Rotate(int bar)
        {CheckBar(bar);var next=Copy(state);next.bars[bar].vertical=!next.bars[bar].vertical;return Commit(next);}
        public bool ToggleNextBar(int bar)
        {
            CheckBar(bar);var next=Copy(state);
            next.visibleBars=bar==Bars-1?Bars-1:state.visibleBars>bar+1?bar+1:bar+2;
            return Commit(next);
        }
        public bool Move(int sourcePage,int sourceSlot,int destinationPage,int destinationSlot,int expectedRevision)
        {
            CheckCell(sourcePage,sourceSlot);CheckCell(destinationPage,destinationSlot);
            Failure="";
            if(expectedRevision!=Revision){Failure="La barra cambió durante el arrastre.";return false;}
            int from=sourcePage*Slots+sourceSlot,to=destinationPage*Slots+destinationSlot;
            if(from==to||state.actions[from]==0)return false;
            var next=Copy(state);int previous=next.actions[to];next.actions[to]=next.actions[from];next.actions[from]=previous;
            return Commit(next);
        }
        public bool Clear(int bar,int slot)
        {
            CheckCell(Page(bar),slot);if(ActionAt(bar,slot)==0)return false;
            var next=Copy(state);next.actions[Page(bar)*Slots+slot]=0;return Commit(next);
        }
        public bool ResetBindings()
        {
            var next=Copy(state);Array.Clear(next.actions,0,next.actions.Length);next.actions[0]=BasicAttack;
            return Commit(next);
        }
        public bool Activate(int bar,int slot,long frame,bool blocked,Func<int,bool> execute)
        {
            CheckCell(Page(bar),slot);Failure="";
            if(frame<0)throw new ArgumentOutOfRangeException(nameof(frame));
            if(execute==null)throw new ArgumentNullException(nameof(execute));
            int action=ActionAt(bar,slot);
            if(blocked||bar>=VisibleBars||action==0||dispatching||frame==lastInputFrame)return false;
            // Reserve before invoking; a reentrant click or keyboard+click in the
            // same frame cannot perform the action twice, even if rejected.
            lastInputFrame=frame;dispatching=true;
            try{return execute(action);}finally{dispatching=false;}
        }
        private bool Commit(QuickbarSave next)
        {
            Failure="";
            if(writing||dispatching){Failure="Ya hay una acción de interfaz en curso.";return false;}
            writing=true;
            try
            {
                next.revision=checked(state.revision+1);Validate(next);
                if(Persist!=null&&!Persist(Copy(next))){Failure="No se pudo guardar la barra. Se conservó la configuración anterior.";return false;}
                state=Copy(next);Failure="";return true;
            }
            catch(Exception ex){Failure="No se aplicó el cambio de barra: "+ex.Message;return false;}
            finally{writing=false;}
        }
        public static void Validate(QuickbarSave value)
        {
            if(value==null||value.schema!=1||value.revision<0||value.visibleBars<1||value.visibleBars>Bars||
                value.actions==null||value.actions.Length!=Slots*Pages||value.bars==null||value.bars.Length!=Bars)
                throw new InvalidDataException("Invalid local quickbar schema.");
            foreach(int action in value.actions)if(action!=0&&action!=BasicAttack)
                throw new InvalidDataException("Unimplemented actions cannot be restored as working skills.");
            foreach(var bar in value.bars)if(bar==null||bar.page<0||bar.page>=Pages)throw new InvalidDataException("Invalid native quickbar page.");
        }
        private static QuickbarSave Copy(QuickbarSave value)
        {
            var result=new QuickbarSave{schema=value.schema,revision=value.revision,visibleBars=value.visibleBars,actions=(int[])value.actions.Clone()};
            for(int i=0;i<Bars;i++)result.bars[i]=new QuickbarViewState{page=value.bars[i].page,vertical=value.bars[i].vertical};
            return result;
        }
        private static void CheckBar(int bar){if(bar<0||bar>=Bars)throw new ArgumentOutOfRangeException(nameof(bar));}
        private static void CheckCell(int page,int slot)
        {if(page<0||page>=Pages||slot<0||slot>=Slots)throw new ArgumentOutOfRangeException(nameof(slot));}
    }
    public readonly struct NativePixelRect
    {
        public readonly int X,Y,Width,Height;
        public NativePixelRect(int x,int y,int width,int height){X=x;Y=y;Width=width;Height=height;}
    }
    public static class NativeHudGeometry
    {
        // Constructor 0x59555B: 446x53. Icon origin/stride 0x596EE4..0x596F06.
        public static NativePixelRect Bar(bool vertical)=>vertical?new NativePixelRect(0,0,53,446):new NativePixelRect(0,0,446,53);
        public static NativePixelRect Cell(int slot,bool vertical)
        {
            if(slot<0||slot>=10)throw new ArgumentOutOfRangeException(nameof(slot));
            return vertical?new NativePixelRect(7,24+40*slot,32,32):new NativePixelRect(24+40*slot,7,32,32);
        }
        public static int ClampPixel(double value,int extent,int size)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)||extent<=0||size<=0)throw new ArgumentOutOfRangeException(nameof(value));
            return (int)Math.Round(Math.Max(0,Math.Min(Math.Max(0,extent-size),value)),MidpointRounding.AwayFromZero);
        }
    }
}

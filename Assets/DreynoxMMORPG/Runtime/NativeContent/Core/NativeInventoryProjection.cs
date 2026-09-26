using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Dreynox.Mmorpg.NativeContent
{
    public readonly struct NativeOwnedItemView
    {
        public readonly int Key,Count;
        public NativeOwnedItemView(int key,int count){Key=key;Count=count;}
    }
    /// <summary>
    /// Read-only projection of the existing local journal inventory, never another ownership store.
    /// Its pages organize aggregated item TYPES, not authoritative native bag slots/stack limits.
    /// Every owned type remains reachable, including unknown definitions and more than 120 types.
    /// </summary>
    public sealed class NativeInventoryProjection
    {
        public const int Columns=6,Rows=4,Cells=Columns*Rows;
        private NativeOwnedItemView[] items=Array.Empty<NativeOwnedItemView>();
        public int Count=>items.Length;
        public int PageCount=>Math.Max(1,(items.Length+Cells-1)/Cells);
        public int Page {get;private set;}
        public long Gold {get;private set;}
        public int Revision {get;private set;}
        public IReadOnlyList<NativeOwnedItemView> Items=>new ReadOnlyCollection<NativeOwnedItemView>(items);
        public void Synchronize(IReadOnlyDictionary<int,int> owned,long gold)
        {
            if(owned==null)throw new ArgumentNullException(nameof(owned));
            if(owned.Count>65025||gold<0)throw new InvalidDataException("Invalid local inventory projection.");
            var next=new NativeOwnedItemView[owned.Count];int i=0;
            foreach(var pair in owned)
            {
                if(pair.Key<257||pair.Key>65535||(pair.Key&255)==0||pair.Value<=0)
                    throw new InvalidDataException("Invalid local owned-item identity/count.");
                next[i++]=new NativeOwnedItemView(pair.Key,pair.Value);
            }
            Array.Sort(next,(a,b)=>a.Key.CompareTo(b.Key));
            // Validate all input before replacing the visible snapshot.
            items=next;Gold=gold;Page=Math.Min(Page,PageCount-1);Revision++;
        }
        public bool SelectPage(int page)
        {if(page<0||page>=PageCount)return false;Page=page;return true;}
        public bool TryCell(int cell,out NativeOwnedItemView value)
        {
            value=default;if(cell<0||cell>=Cells)return false;int index=Page*Cells+cell;
            if(index>=items.Length)return false;value=items[index];return true;
        }
        public static int X(int cell){Check(cell);return 19+43*(cell%Columns);}
        public static int Y(int cell){Check(cell);return 301+41*(cell/Columns);}
        private static void Check(int cell){if(cell<0||cell>=Cells)throw new ArgumentOutOfRangeException(nameof(cell));}
    }
}

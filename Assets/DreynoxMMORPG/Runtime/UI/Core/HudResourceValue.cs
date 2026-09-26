using System;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Unknown is deliberately distinct from zero and from a full resource.</summary>
    public readonly struct HudResourceValue
    {
        public readonly bool Available;
        public readonly int Current,Maximum;
        public float Fraction => Available?(float)Current/Maximum:0;
        public HudResourceValue(int current,int maximum)
        {
            if(maximum<=0||current<0||current>maximum)throw new ArgumentOutOfRangeException(nameof(current));
            Current=current;Maximum=maximum;Available=true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
namespace Dreynox.Mmorpg.Interaction
{
    public static class WorldInputGate
    {
        private static readonly HashSet<int> owners = new HashSet<int>();
        public static bool IsBlocked => owners.Count > 0;
        public static void Set(Object owner,bool blocked)
        {if(owner==null)return;if(blocked)owners.Add(owner.GetInstanceID());else owners.Remove(owner.GetInstanceID());}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {owners.Clear();}
    }
}

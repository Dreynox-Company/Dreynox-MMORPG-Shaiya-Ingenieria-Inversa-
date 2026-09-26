using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.World;
using UnityEngine;

namespace Dreynox.Mmorpg.Interaction
{
    /// <summary>Revalidate a local NPC action when clicked, not just when its dialog opened.</summary>
    public static class LocalNpcInteractionGuard
    {
        public static bool Validate(ShaiyaClientActor actor,LegacyNpcRuntimeDescriptor npc,
            LegacyNpcRuntimeDescriptor current,bool uiReady,out string reason)
        {
            reason="La conversación ya no está activa.";
            if(!uiReady||actor==null||npc==null||current!=npc||!npc.gameObject.activeInHierarchy||
                !actor.gameObject.activeInHierarchy||npc.gameObject.scene!=actor.gameObject.scene)return false;
            Vector3 origin=actor.transform.position+Vector3.up;
            Vector3 line=npc.transform.position+Vector3.up-origin;
            // Same local radius as dialog opening, not a recovered server constant.
            if(line.sqrMagnitude>25f){reason="Acércate al NPC para realizar esta acción.";return false;}
            if(line.sqrMagnitude>.0001f)
                foreach(var hit in Physics.RaycastAll(origin,line.normalized,line.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(actor.transform)&&!hit.transform.IsChildOf(npc.transform))
                    {reason="La conversación está obstruida.";return false;}
            reason="";return true;
        }
    }
}

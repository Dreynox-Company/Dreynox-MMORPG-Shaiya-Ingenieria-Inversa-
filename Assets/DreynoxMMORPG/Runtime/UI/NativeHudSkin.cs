using System;
using UnityEngine;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Source rectangles are TOP LEFT pixels, not stretched POT texture bounds.</summary>
    public sealed class NativeHudSkin : ScriptableObject
    {
        public Texture2D horizontalBar, verticalBar;
        public Sprite attackIcon, health, mana, stamina, levelFrame, scrollTop, scrollMiddle, scrollBottom;
        public Sprite[] previous, next, previousVertical, nextVertical, expand, collapse, rotate, command, scrollUp, scrollDown;
        public void Validate()
        {
            if(horizontalBar==null||verticalBar==null||attackIcon==null||health==null||mana==null||stamina==null||levelFrame==null||scrollTop==null||scrollMiddle==null||scrollBottom==null)
                throw new InvalidOperationException("Original HUD art is incomplete.");
            foreach(var states in new[]{previous,next,previousVertical,nextVertical,expand,collapse,rotate,command,scrollUp,scrollDown})
                if(states==null||states.Length!=4||Array.Exists(states,s=>s==null))throw new InvalidOperationException("Original HUD button states are incomplete.");
        }
    }
}

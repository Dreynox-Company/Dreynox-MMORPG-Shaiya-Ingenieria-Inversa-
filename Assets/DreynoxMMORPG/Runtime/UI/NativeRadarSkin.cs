using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.UI
{
    public enum NativeRadarKind
    {
        Player, Npc, Monster, FriendlyPlayer, Party, EnemyPlayer,
        QuestAvailable, QuestReady, QuestLow, Gatekeeper, Warehouse,
        Blacksmith, Merchant, WeaponMerchant, ArmourMerchant, Event
    }
    [Serializable]
    public struct NativeRadarIcon
    {
        public NativeRadarKind kind;
        public Sprite sprite;
        public string source;
    }
    public sealed class NativeRadarSkin : ScriptableObject
    {
        [SerializeField] private Texture2D frame;
        [SerializeField] private Sprite zoomIn, zoomOut;
        [SerializeField] private NativeRadarIcon[] icons = Array.Empty<NativeRadarIcon>();
        public Texture2D Frame => frame;
        public Sprite ZoomIn => zoomIn;
        public Sprite ZoomOut => zoomOut;
        public IReadOnlyList<NativeRadarIcon> Icons => icons;
        public void Configure(Texture2D border, Sprite plus, Sprite minus, NativeRadarIcon[] original)
        {
            if (border == null || plus == null || minus == null || original == null)
                throw new ArgumentException("Original radar frame/buttons/icons required.");
            frame = border; zoomIn = plus; zoomOut = minus; icons = (NativeRadarIcon[])original.Clone();
            foreach (NativeRadarKind kind in Enum.GetValues(typeof(NativeRadarKind))) Get(kind);
        }
        public Sprite Get(NativeRadarKind kind)
        {
            for (int i = 0; i < icons.Length; i++)
                if (icons[i].kind == kind && icons[i].sprite != null) return icons[i].sprite;
            // No empty Image or square substitute can silently escape a missing resource.
            throw new InvalidOperationException("Original radar icon is absent: " + kind);
        }
    }
}

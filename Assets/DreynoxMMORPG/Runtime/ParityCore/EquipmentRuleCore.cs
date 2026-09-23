using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public sealed class EquipmentRuleCore
    {
        public ClientWeaponFamily MainHand { get; private set; }
        public bool HasOffHandShield { get; private set; }
        public bool HasWings { get; private set; }
        public bool HasMount { get; private set; }
        public double MountSeatHeight { get; private set; }
        public double WingLocalYawDegrees { get; private set; }
        public double WingLocalHeight { get; private set; }
        public long Revision { get; private set; }

        public bool UsesBothHands => MainHand == ClientWeaponFamily.TwoHand || MainHand == ClientWeaponFamily.Spear || MainHand == ClientWeaponFamily.Bow || MainHand == ClientWeaponFamily.Staff;

        public void EquipMainHand(ClientWeaponFamily family)
        {
            if (MainHand == family) return;
            MainHand = family;
            if (UsesBothHands) HasOffHandShield = false;
            Revision++;
        }

        public bool EquipShield()
        {
            if (UsesBothHands) return false;
            if (HasOffHandShield) return true;
            HasOffHandShield = true;
            Revision++;
            return true;
        }

        public void UnequipShield()
        {
            if (!HasOffHandShield) return;
            HasOffHandShield = false;
            Revision++;
        }

        public void EquipWings(double localYawDegrees, double localHeight)
        {
            HasWings = true;
            WingLocalYawDegrees = NormalizeDegrees(localYawDegrees);
            WingLocalHeight = localHeight;
            Revision++;
        }

        public void UnequipWings()
        {
            if (!HasWings) return;
            HasWings = false;
            WingLocalYawDegrees = 0;
            WingLocalHeight = 0;
            Revision++;
        }

        public void SetMount(bool mounted, double seatHeight = 0)
        {
            HasMount = mounted;
            MountSeatHeight = mounted ? seatHeight : 0;
            Revision++;
        }

        public double ResolveWingRootHeight(double actorGroundHeight)
        {
            return actorGroundHeight + (HasMount ? MountSeatHeight : 0) + WingLocalHeight;
        }

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            if (value > 180.0) value -= 360.0;
            if (value < -180.0) value += 360.0;
            return value;
        }
    }
}

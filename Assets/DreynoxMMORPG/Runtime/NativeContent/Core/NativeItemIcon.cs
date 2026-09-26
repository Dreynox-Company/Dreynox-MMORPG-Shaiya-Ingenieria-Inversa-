using System;

namespace Dreynox.Mmorpg.NativeContent
{
    public readonly struct NativeIconRegion
    {
        public readonly string File;
        public readonly int X,Y,Width,Height;
        public NativeIconRegion(string file,int x,int y,int width=32,int height=32)
        {File=file;X=x;Y=y;Width=width;Height=height;}
        public bool Fits(int width,int height)=>X>=0&&Y>=0&&Width>0&&Height>0&&X+Width<=width&&Y+Height<=height;
    }
    /// <summary>
    /// ps0032 game.exe 509c4a8f...: 0x5794E0 classification; 0x4E1310 UV dispatch;
    /// 0x4E11F0 sheet lookup and 0x4E0500 registrations. Icon field is ONE BASED
    /// (DEC at 0x4E0E35). Secondary equipment sheets subtract 100, not 128/256.
    /// These describe catalog icons, not dynamic rarity/cooldown/ownership overlays.
    /// </summary>
    public static class NativeItemIcon
    {
        public static int Normalize(int type)
        {
            if(type<0||type>255)throw new ArgumentOutOfRangeException(nameof(type));
            switch(type)
            {
                case 37:case 171:return 22;
                case 38:case 41:case 43:case 44:case 78:case 79:case 80:case 94:case 126:case 127:case 130:case 131:return 25;
                case 45:case 181:return 1;
                case 46:case 182:return 2;
                case 47:return 3;
                case 48:return 4;
                case 49:case 50:return 5;
                case 51:case 52:return 6;
                case 53:case 54:return 7;
                case 55:case 56:return 8;
                case 57:return 9;
                case 58:return 10;
                case 59:return 11;
                case 60:case 61:return 12;
                case 62:case 63:case 184:return 13;
                case 64:case 183:return 14;
                case 65:case 180:return 15;
                case 66:case 72:case 170:return 16;
                case 67:case 73:case 172:return 17;
                case 68:case 74:case 173:return 18;
                case 69:case 75:case 176:return 19;
                case 70:case 76:case 175:return 20;
                case 71:case 77:return 21;
                case 81:case 87:return 31;
                case 82:case 88:return 32;
                case 83:case 89:return 33;
                case 84:case 90:return 34;
                case 85:case 91:return 35;
                case 86:case 92:return 36;
                case 96:case 177:return 23;
                case 97:return 40;
                case 98:return 30;
                case 99:case 128:case 129:return 27;
                case 101:case 102:case 103:return 100;
                case 122:return 121;
                case 123:return 120;
                case 125:return 42;
                case 151:return 150;
                default:return type;
            }
        }
        private static int ColumnCount(int type)
        {
            switch(type)
            {
                case 22:case 27:case 28:case 29:case 30:case 37:case 95:case 98:case 99:case 129:return 8;
                case 25:case 38:case 41:case 42:case 43:case 44:case 78:case 79:case 80:case 94:case 100:case 101:case 102:case 103:case 120:case 121:case 122:case 123:case 125:case 126:case 127:case 128:case 130:case 131:case 150:case 151:return 16;
                default:return 4;
            }
        }
        public static bool TryResolve(int type,long originalIcon,out NativeIconRegion region)
        {
            region=default;
            if(type<1||type>255||originalIcon<1||originalIcon>255)return false;
            int index=(int)originalIcon-1,columns=ColumnCount(type);bool secondary=false;
            int normalized=Normalize(type);
            if(normalized!=30)
            {
                if(columns==8)
                {
                    if(type==99)type=28;
                    else if(type==27||type==28||type==29)
                    {type=27;if(index>=100){index-=100;secondary=true;}}
                    else if(type==37){type=22;if(index>=100){index-=100;secondary=true;}}
                }
                else if(columns==4&&index>=100){index-=100;secondary=true;}
            }
            string file;
            switch(type)
            {
                case 100: case 101:file="icon_somo2.dds";break;
                case 102: case 103:file="icon_somo3.dds";break;
                case 78: case 79: case 80:file="icon_somo1.dds";break;
                case 42:file="icon_somo.dds";break;
                case 95:file="icon_rapis.dds";break;
                case 120:file="icon_pet.dds";break;
                case 150:case 151:file="icon_DualLayer.dds";break;
                case 121:case 122:file="icon_wing.dds";break;
                case 123:file="icon_123_pet.dds";break;
                case 125:file="icon_125_mount.dds";break;
                case 128:file="icon_128_questitem.dds";break;
                case 130:file="icon_130_serviceitems.dds";break;
                default:
                    int family=Normalize(type);
                    if(family<1||family>41)return false;
                    // Several registrations overwrite array entries with named atlases.
                    // Looking only for type-number.dds is therefore incorrect.
                    if(!secondary&&family==25)file="icon_somo.dds";
                    else if(!secondary&&family==27)file="icon_quest.dds";
                    else if(!secondary&&family==28)file="icon_quest2.dds";
                    else if(!secondary&&family==30)file="icon_rapis.dds";
                    else file=(secondary?"1":"")+family.ToString("D2")+".dds";
                    break;
            }
            region=new NativeIconRegion(file,index%columns*32,index/columns*32);return true;
        }
    }
}

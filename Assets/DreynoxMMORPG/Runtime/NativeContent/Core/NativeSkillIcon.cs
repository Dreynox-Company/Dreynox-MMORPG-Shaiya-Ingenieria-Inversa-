namespace Dreynox.Mmorpg.NativeContent
{
    /// <summary>
    /// Canonical ps0032: 0x4EAEA5 reads the skill image WORD; 0x4EAEA9..CB
    /// selects image/1000 + 1 and subtracts its thousand bank. 0x4EAECD
    /// decrements/clamps the cell; 0x4EAED6 uses sixteen columns. Source
    /// registrations at 0x4E055A..C1 load icon_skill.tga and icon_skillN.dds.
    /// This resolves artwork, NOT learned-skill state, effects or damage.
    /// </summary>
    public static class NativeSkillIcon
    {
        public static bool TryResolve(long image,out NativeIconRegion region)
        {
            region=default;
            if(image<0||image>ushort.MaxValue)return false;
            int bank=(int)image/1000,index=System.Math.Max(0,(int)image%1000-1);
            // Ten native registrations exist; a missing source file is reported
            // by the importer, never replaced by another bank's first icon.
            if(bank>=10||index>=256)return false;
            string file=bank==0?"icon_skill.tga":"icon_skill"+(bank+1)+".dds";
            region=new NativeIconRegion(file,index%16*32,index/16*32);
            return true;
        }
    }
}

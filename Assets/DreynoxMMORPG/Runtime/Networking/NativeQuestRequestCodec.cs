using System;
using System.IO;

namespace Dreynox.Mmorpg.Networking
{
    public enum NativeQuestRequestKind : ushort
    {
        Accept = 0x0902,
        Complete = 0x0903,
        CompleteWithChoice = 0x0907,
        Abandon = 0x0908
    }
    /// <summary>Body at the ps0032 send boundary, including opcode, before transport/encryption.</summary>
    public readonly struct NativeQuestRequest
    {
        public readonly NativeQuestRequestKind Kind;
        // Network NPC instance identifier, never (NpcType<<16)|TypeId from content.
        public readonly uint NpcRuntimeId;
        public readonly ushort QuestId;
        public readonly byte RewardChoice;
        public NativeQuestRequest(NativeQuestRequestKind kind,uint npcRuntimeId,ushort questId,byte rewardChoice=0)
        {
            if(questId==0)throw new ArgumentOutOfRangeException(nameof(questId));
            NativeQuestRequestCodec.Length(kind);
            if(kind==NativeQuestRequestKind.Abandon&&npcRuntimeId!=0)
                throw new ArgumentException("Native abandonment has no NPC field.");
            if(kind!=NativeQuestRequestKind.CompleteWithChoice&&rewardChoice!=0)
                throw new ArgumentException("This native request has no reward-choice field.");
            Kind=kind;NpcRuntimeId=npcRuntimeId;QuestId=questId;RewardChoice=rewardChoice;
        }
    }
    /// <summary>
    /// Exact little-endian body layouts recovered at 0x695510/570/5D0/630 in
    /// canonical ps0032. Not a connected session, response decoder or arbitrary
    /// opcode fallback. Does not prepend the generic lab framer or touch a socket.
    /// </summary>
    public static class NativeQuestRequestCodec
    {
        public static byte[] Encode(NativeQuestRequest request)
        {
            request=new NativeQuestRequest(request.Kind,request.NpcRuntimeId,request.QuestId,request.RewardChoice);
            var result=new byte[Length(request.Kind)];
            Write16(result,0,(ushort)request.Kind);
            if(request.Kind==NativeQuestRequestKind.Abandon)Write16(result,2,request.QuestId);
            else
            {
                for(int i=0;i<4;i++)result[2+i]=(byte)(request.NpcRuntimeId>>(8*i));
                Write16(result,6,request.QuestId);
                if(request.Kind==NativeQuestRequestKind.CompleteWithChoice)result[8]=request.RewardChoice;
            }
            return result;
        }
        public static NativeQuestRequest Decode(byte[] body)
        {
            if(body==null)throw new ArgumentNullException(nameof(body));
            if(body.Length<2)throw new InvalidDataException("Truncated native quest opcode.");
            var kind=(NativeQuestRequestKind)Read16(body,0);
            int length=Length(kind);
            if(body.Length!=length)throw new InvalidDataException("Native quest body length does not match its opcode.");
            if(kind==NativeQuestRequestKind.Abandon)return new NativeQuestRequest(kind,0,Read16(body,2));
            uint npc=0;for(int i=0;i<4;i++)npc|=(uint)body[2+i]<<(8*i);
            return new NativeQuestRequest(kind,npc,Read16(body,6),length==9?body[8]:(byte)0);
        }
        public static int Length(NativeQuestRequestKind kind)
        {
            switch(kind)
            {
                case NativeQuestRequestKind.Accept:
                case NativeQuestRequestKind.Complete:return 8;
                case NativeQuestRequestKind.CompleteWithChoice:return 9;
                case NativeQuestRequestKind.Abandon:return 4;
                default:throw new InvalidDataException("Unsupported native quest opcode.");
            }
        }
        private static void Write16(byte[] data,int at,ushort value)
        {data[at]=(byte)value;data[at+1]=(byte)(value>>8);}
        private static ushort Read16(byte[] data,int at)=>(ushort)(data[at]|data[at+1]<<8);
    }
}

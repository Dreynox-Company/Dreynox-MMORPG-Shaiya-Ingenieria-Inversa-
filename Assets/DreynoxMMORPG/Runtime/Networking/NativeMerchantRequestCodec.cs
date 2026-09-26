using System;
using System.IO;

namespace Dreynox.Mmorpg.Networking
{
    public readonly struct NativeMerchantBuyRequest
    {
        public readonly uint NpcRuntimeId;
        public readonly byte StockIndex, Quantity;
        // Explicit opaque word read from native global 0x91FE3C. It is not a
        // local gold total, journal revision, or something callers may omit.
        public readonly uint ConversationWord;
        public NativeMerchantBuyRequest(uint npcRuntimeId,byte stockIndex,byte quantity,uint conversationWord)
        {NpcRuntimeId=npcRuntimeId;StockIndex=stockIndex;Quantity=quantity;ConversationWord=conversationWord;}
    }
    public readonly struct NativeMerchantSellRequest
    {
        public readonly byte BagIndex,SlotIndex,Quantity;
        public readonly uint NpcRuntimeId,ConversationWord;
        public NativeMerchantSellRequest(byte bagIndex,byte slotIndex,byte quantity,uint npcRuntimeId,uint conversationWord)
        {BagIndex=bagIndex;SlotIndex=slotIndex;Quantity=quantity;NpcRuntimeId=npcRuntimeId;ConversationWord=conversationWord;}
    }
    /// <summary>
    /// Exact canonical ps0032 merchant request BODY at common send 0x693880,
    /// including opcode, before transport/framing/encryption. Derived from
    /// 0x694D70/0x694DE0 and callers 0x5E4168/0x5E42A3.
    /// This does not authorize trades, infer native price rules, synthesize
    /// server replies or connect the local journal to a live native session.
    /// Zero/all-ones fields round-trip; semantic validation belongs upstream.
    /// </summary>
    public static class NativeMerchantRequestCodec
    {
        public const ushort BuyOpcode=0x0702, SellOpcode=0x0703;
        public const int BuyBytes=12,SellBytes=13;
        public static byte[] EncodeBuy(NativeMerchantBuyRequest request)
        {
            var body=new byte[BuyBytes];Write16(body,0,BuyOpcode);
            Write32(body,2,request.NpcRuntimeId);body[6]=request.StockIndex;body[7]=request.Quantity;
            Write32(body,8,request.ConversationWord);return body;
        }
        public static NativeMerchantBuyRequest DecodeBuy(byte[] body)
        {
            Require(body,BuyBytes,BuyOpcode);
            return new NativeMerchantBuyRequest(Read32(body,2),body[6],body[7],Read32(body,8));
        }
        public static byte[] EncodeSell(NativeMerchantSellRequest request)
        {
            var body=new byte[SellBytes];Write16(body,0,SellOpcode);
            body[2]=request.BagIndex;body[3]=request.SlotIndex;body[4]=request.Quantity;
            Write32(body,5,request.NpcRuntimeId);Write32(body,9,request.ConversationWord);return body;
        }
        public static NativeMerchantSellRequest DecodeSell(byte[] body)
        {
            Require(body,SellBytes,SellOpcode);
            return new NativeMerchantSellRequest(body[2],body[3],body[4],Read32(body,5),Read32(body,9));
        }
        private static void Require(byte[] body,int length,ushort opcode)
        {
            if(body==null)throw new ArgumentNullException(nameof(body));
            if(body.Length!=length)throw new InvalidDataException("Native merchant body has a different length; no prefix-only or trailing-field tolerance.");
            if((body[0]|body[1]<<8)!=opcode)throw new InvalidDataException("Unexpected native merchant request opcode.");
        }
        private static void Write16(byte[] body,int at,ushort value)
        {body[at]=(byte)value;body[at+1]=(byte)(value>>8);}
        private static void Write32(byte[] body,int at,uint value)
        {for(int i=0;i<4;i++)body[at+i]=(byte)(value>>(8*i));}
        private static uint Read32(byte[] body,int at)
        {return (uint)body[at]|(uint)body[at+1]<<8|(uint)body[at+2]<<16|(uint)body[at+3]<<24;}
    }
}

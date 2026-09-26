using Dreynox.Mmorpg.Networking;

internal static class MerchantBodyCases
{
    public static int Run()
    {
        int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);count++;}
        void Reject(Action work,string name)
        {
            try{work();}catch(Exception ex)when(ex is ArgumentException||ex is InvalidDataException)
            {Check(true,name);return;}
            throw new Exception("Expected rejection: "+name);
        }
        var buy=new NativeMerchantBuyRequest(0x12345678,0x9a,0xbc,0xdeadbeef);
        var sell=new NativeMerchantSellRequest(2,17,3,0x12345678,0xdeadbeef);
        byte[] buyGolden=Convert.FromHexString("0207785634129ABCEFBEADDE");
        byte[] sellGolden=Convert.FromHexString("030702110378563412EFBEADDE");
        Check(NativeMerchantRequestCodec.EncodeBuy(buy).SequenceEqual(buyGolden),"native 12-byte buy body golden vector");
        Check(NativeMerchantRequestCodec.EncodeSell(sell).SequenceEqual(sellGolden),"native 13-byte sell body golden vector");
        var b=NativeMerchantRequestCodec.DecodeBuy(buyGolden);var s=NativeMerchantRequestCodec.DecodeSell(sellGolden);
        Check(b.NpcRuntimeId==buy.NpcRuntimeId&&b.StockIndex==buy.StockIndex&&b.Quantity==buy.Quantity&&b.ConversationWord==buy.ConversationWord,"all native buy fields retained");
        Check(s.NpcRuntimeId==sell.NpcRuntimeId&&s.BagIndex==sell.BagIndex&&s.SlotIndex==sell.SlotIndex&&s.Quantity==sell.Quantity&&s.ConversationWord==sell.ConversationWord,"all native sell fields retained");
        for(int i=0;i<buyGolden.Length;i++){int length=i;Reject(()=>NativeMerchantRequestCodec.DecodeBuy(buyGolden[..length]),"buy truncation "+i);}
        for(int i=0;i<sellGolden.Length;i++){int length=i;Reject(()=>NativeMerchantRequestCodec.DecodeSell(sellGolden[..length]),"sell truncation "+i);}
        Reject(()=>NativeMerchantRequestCodec.DecodeBuy(buyGolden.Concat(new byte[]{0}).ToArray()),"buy extension not silently dropped");
        Reject(()=>NativeMerchantRequestCodec.DecodeSell(sellGolden.Concat(new byte[]{0}).ToArray()),"sell extension not silently dropped");
        var wrongBuy=(byte[])buyGolden.Clone();wrongBuy[0]=3;
        var wrongSell=(byte[])sellGolden.Clone();wrongSell[0]=2;
        Reject(()=>NativeMerchantRequestCodec.DecodeBuy(wrongBuy),"sell opcode cannot impersonate buy");
        Reject(()=>NativeMerchantRequestCodec.DecodeSell(wrongSell),"buy opcode cannot impersonate sell");
        Reject(()=>NativeMerchantRequestCodec.DecodeBuy(null),"null buy");Reject(()=>NativeMerchantRequestCodec.DecodeSell(null),"null sell");
        var allBuy=NativeMerchantRequestCodec.DecodeBuy(NativeMerchantRequestCodec.EncodeBuy(new NativeMerchantBuyRequest(uint.MaxValue,255,255,uint.MaxValue)));
        var allSell=NativeMerchantRequestCodec.DecodeSell(NativeMerchantRequestCodec.EncodeSell(new NativeMerchantSellRequest(255,255,255,uint.MaxValue,uint.MaxValue)));
        Check(allBuy.ConversationWord==uint.MaxValue&&allBuy.NpcRuntimeId==uint.MaxValue&&allBuy.StockIndex==255&&allBuy.Quantity==255,"buy unsigned widths preserved");
        Check(allSell.ConversationWord==uint.MaxValue&&allSell.NpcRuntimeId==uint.MaxValue&&allSell.BagIndex==255&&allSell.SlotIndex==255&&allSell.Quantity==255,"sell unsigned widths preserved");
        Check(NativeMerchantRequestCodec.DecodeBuy(NativeMerchantRequestCodec.EncodeBuy(default)).Quantity==0,"codec does not invent authorization for zero quantity");
        Check(NativeMerchantRequestCodec.DecodeSell(NativeMerchantRequestCodec.EncodeSell(default)).ConversationWord==0,"zero word preserved without source substitution");
        byte[] snapshot=(byte[])buyGolden.Clone();NativeMerchantRequestCodec.DecodeBuy(buyGolden);
        Check(buyGolden.SequenceEqual(snapshot),"native decode does not mutate source bytes");
        var random=new Random(270927);
        for(int n=0;n<100;n++)
        {
            var rawBuy=new byte[12];var rawSell=new byte[13];random.NextBytes(rawBuy);random.NextBytes(rawSell);
            rawBuy[0]=2;rawBuy[1]=7;rawSell[0]=3;rawSell[1]=7;
            Check(NativeMerchantRequestCodec.EncodeBuy(NativeMerchantRequestCodec.DecodeBuy(rawBuy)).SequenceEqual(rawBuy),"buy generated bit-exact roundtrip "+n);
            Check(NativeMerchantRequestCodec.EncodeSell(NativeMerchantRequestCodec.DecodeSell(rawSell)).SequenceEqual(rawSell),"sell generated bit-exact roundtrip "+n);
        }
        Console.WriteLine("NATIVE MERCHANT BODY CONTRACTS OK: "+count+" checks. Raw body preservation, not native transport or authoritative trades.");
        return count;
    }
}

using System;
using Dreynox.Mmorpg.NativeContent;

namespace Dreynox.Mmorpg.Commerce
{
    public static class NativeMerchantOfferFactory
    {
        // Explicit first local support set, not a recovered universal native rule.
        // Special categories (including 100/200) require separate currency/service integration.
        public static bool SupportsMerchantType(int value)=>value>=0&&value<=5;
        public static LocalMerchantOffer Resolve(NativeCatalogAsset items,int key)
        {
            if(items==null||items.IsSkills||!items.TryItem(key,out var entry))return null;
            string restriction="";
            if(items.Value(entry,"buymethod")!=0||items.Value(entry,"moneytype")!=0)
                restriction="Este objeto requiere una moneda o método de compra aún no integrado.";
            else if(items.Value(entry,"duration")!=0||items.Value(entry,"extduration")!=0||
                items.Value(entry,"itemupgrade")!=0||items.Value(entry,"spellbookdurability")!=0)
                restriction="La duración o estado individual de este objeto requiere una instancia nativa; no se comercializa como un tipo agregado.";
            return new LocalMerchantOffer(key,entry.Name,items.Value(entry,"buy"),items.Value(entry,"sell"),restriction);
        }
    }
}

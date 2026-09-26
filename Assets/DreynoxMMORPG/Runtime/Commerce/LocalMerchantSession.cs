using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dreynox.Mmorpg.Quests;

namespace Dreynox.Mmorpg.Commerce
{
    public enum LocalMerchantSide { Buy, Sell }

    /// <summary>Original template prices, not item-instance durability, taxes or premium currency.</summary>
    public sealed class LocalMerchantOffer
    {
        public int ItemKey { get; }
        public string Name { get; }
        public long Buy { get; }
        public long Sell { get; }
        public string Restriction { get; }
        public LocalMerchantOffer(int itemKey,string name,long buy,long sell,string restriction="")
        {
            if(itemKey<257||itemKey>65535||(itemKey&255)==0)throw new ArgumentOutOfRangeException(nameof(itemKey));
            ItemKey=itemKey;Name=name??"";Buy=buy;Sell=sell;Restriction=restriction??"";
        }
    }

    public sealed class LocalMerchantQuote
    {
        internal readonly LocalMerchantSession Owner;
        public LocalMerchantSide Side { get; }
        public int ItemKey { get; }
        public int Quantity { get; }
        public int JournalRevision { get; }
        public long UnitPrice { get; }
        public long Total { get; }
        public string ItemName { get; }
        internal LocalMerchantQuote(LocalMerchantSession owner,LocalMerchantSide side,LocalMerchantOffer offer,int quantity,int revision,long total)
        {
            Owner=owner;Side=side;ItemKey=offer.ItemKey;ItemName=offer.Name;Quantity=quantity;
            JournalRevision=revision;UnitPrice=side==LocalMerchantSide.Buy?offer.Buy:offer.Sell;Total=total;
        }
    }

    /// <summary>
    /// Complete local purchase/sale transaction over the existing durable journal.
    /// It never owns another wallet/inventory. Quoting has no side effects.
    /// Unmodified template buy/sell prices only; native bag capacity, discounts,
    /// quality/enchants and server-authoritative commerce are not asserted here.
    /// </summary>
    public sealed class LocalMerchantSession : IDisposable
    {
        public const int MaximumQuantity=255;
        private readonly QuestJournalCore journal;
        private readonly LocalMerchantOffer[] stock;
        private readonly Func<int,LocalMerchantOffer> lookup;
        private readonly Func<bool> contextStillValid;
        private bool closed,executing;
        private LocalMerchantQuote pending;
        public IReadOnlyList<LocalMerchantOffer> Stock { get; }
        public bool IsOpen=>!closed;
        public LocalMerchantQuote Pending=>pending;
        public int SuccessfulTrades { get; private set; }
        public LocalMerchantSession(QuestJournalCore owner,IReadOnlyList<LocalMerchantOffer> offers,
            Func<int,LocalMerchantOffer> lookupItem,Func<bool> validateContext)
        {
            journal=owner??throw new ArgumentNullException(nameof(owner));
            lookup=lookupItem??throw new ArgumentNullException(nameof(lookupItem));
            contextStillValid=validateContext??throw new ArgumentNullException(nameof(validateContext));
            if(offers==null||offers.Count>10000)throw new ArgumentOutOfRangeException(nameof(offers));
            stock=new LocalMerchantOffer[offers.Count];
            for(int i=0;i<offers.Count;i++)stock[i]=offers[i]; // Null keeps an unresolved original stock position.
            Stock=new ReadOnlyCollection<LocalMerchantOffer>(stock);
        }
        private bool Context(out string reason)
        {
            reason="";
            if(closed){reason="La tienda ya está cerrada.";return false;}
            if(executing){reason="Espera a que termine la operación actual.";return false;}
            if(!contextStillValid()) {pending=null;closed=true;reason="La conversación ya no es válida. Vuelve a hablar con el comerciante.";return false;}
            return true;
        }
        public bool QuoteBuy(int originalStockIndex,int quantity,out LocalMerchantQuote quote,out string reason)
        {
            quote=null;reason="";
            if(!Context(out reason))return false;
            pending=null;
            if(originalStockIndex<0||originalStockIndex>=stock.Length||originalStockIndex>byte.MaxValue||stock[originalStockIndex]==null)
            {reason="El objeto no pertenece a la oferta original de este comerciante.";return false;}
            return Quote(LocalMerchantSide.Buy,stock[originalStockIndex],quantity,out quote,out reason);
        }
        public bool QuoteSell(int itemKey,int quantity,out LocalMerchantQuote quote,out string reason)
        {
            quote=null;reason="";
            if(!Context(out reason))return false;
            pending=null;
            var offer=lookup(itemKey);
            if(offer==null||offer.ItemKey!=itemKey){reason="No hay una definición de venta verificada para este objeto.";return false;}
            return Quote(LocalMerchantSide.Sell,offer,quantity,out quote,out reason);
        }
        private bool Quote(LocalMerchantSide side,LocalMerchantOffer offer,int quantity,out LocalMerchantQuote quote,out string reason)
        {
            quote=null;reason="";
            if(quantity<1||quantity>MaximumQuantity){reason="La cantidad debe estar entre 1 y 255.";return false;}
            if(offer.Restriction.Length>0){reason=offer.Restriction;return false;}
            long price=side==LocalMerchantSide.Buy?offer.Buy:offer.Sell;
            if(price<=0||price>uint.MaxValue){reason="No hay precio base en oro habilitado para esta operación.";return false;}
            long total;
            try {total=checked(price*quantity);}
            catch(OverflowException){reason="El precio total excede los límites permitidos.";return false;}
            journal.Inventory.TryGetValue(offer.ItemKey,out int owned);
            if(side==LocalMerchantSide.Buy)
            {
                if(journal.Gold<total){reason="No tienes oro suficiente.";return false;}
                if(owned>int.MaxValue-quantity){reason="La cantidad supera el almacenamiento local.";return false;}
            }
            else
            {
                if(owned<quantity){reason="No tienes esa cantidad de objetos.";return false;}
                if(journal.Gold>long.MaxValue-total){reason="El oro excedería el almacenamiento local.";return false;}
            }
            quote=new LocalMerchantQuote(this,side,offer,quantity,journal.Revision,total);pending=quote;return true;
        }
        public bool Confirm(LocalMerchantQuote quote,out string reason)
        {
            reason="";
            if(!Context(out reason))return false;
            if(quote==null||quote.Owner!=this||!ReferenceEquals(quote,pending))
            {reason="La confirmación ya no corresponde a una operación pendiente.";return false;}
            if(quote.JournalRevision!=journal.Revision)
            {pending=null;reason="El inventario cambió. Selecciona y confirma de nuevo la operación.";return false;}
            executing=true;
            try
            {
                bool buy=quote.Side==LocalMerchantSide.Buy;
                if(!journal.ExchangeLocalItem(quote.JournalRevision,quote.ItemKey,buy?quote.Quantity:-quote.Quantity,
                    buy?-quote.Total:quote.Total,out reason))return false;
                pending=null;SuccessfulTrades++;return true;
            }
            finally {executing=false;}
        }
        public void CancelQuote(){if(!executing)pending=null;}
        public void Dispose(){closed=true;pending=null;}
    }
}

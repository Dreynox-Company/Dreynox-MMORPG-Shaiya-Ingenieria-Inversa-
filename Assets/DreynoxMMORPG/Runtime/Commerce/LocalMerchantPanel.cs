using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.NativeContent;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Commerce
{
    /// <summary>
    /// Original merchant stock/artwork with explicitly LOCAL base-price trades.
    /// One durable wallet/inventory; no native bag instances or fake server ACK.
    /// The quest dialogue routes its real merchant action here, preserving NPC context.
    /// </summary>
    public sealed class LocalMerchantPanel : MonoBehaviour
    {
        private sealed class Cell
        {
            public RectTransform Root;
            public Button Button;
            public RawImage Icon;
            public Text Unknown,Count;
            public NativeUiTooltip Tooltip;
        }
        [SerializeField] private NativeWorldHud hud;
        [SerializeField] private QuestWorldPanel quests;
        [SerializeField] private QuestJournalRuntime journal;
        [SerializeField] private NativeCatalogAsset items;
        [SerializeField] private Texture2D buyArtwork,sellArtwork;
        private RectTransform window,confirmation;
        private RawImage frame;
        private Text title,balance,pageLabel,status,question;
        private Button back,next,confirm,buyTab,sellTab,closeButton;
        private NativeWindowDrag windowDrag;
        private InputField quantity;
        private CanvasGroup windowGroup;
        private readonly Cell[] cells=new Cell[30];
        private readonly List<int> sellKeys=new List<int>();
        private LegacyNpcRuntimeDescriptor npc;
        private uint npcLifetime;
        private LocalMerchantSession session;
        private LocalMerchantQuote quote;
        private int page,selected=-1;
        private bool selling,dirty,closing,confirming;
        private float nextContextCheck;
        private QuestJournalCore subscribed;
        public bool Ready {get;private set;}
        public bool IsOpen=>Ready&&session!=null&&session.IsOpen&&window.gameObject.activeInHierarchy;
        public string Failure {get;private set;}="";
        public LocalMerchantQuote Pending=>quote;
        public int StockCount=>session==null?0:session.Stock.Count;
        public int VisibleCount {get;private set;}
        public LegacyNpcRuntimeDescriptor CurrentNpc=>npc;

        public void Configure(NativeWorldHud worldHud,QuestWorldPanel questPanel,QuestJournalRuntime owner,
            NativeCatalogAsset catalog,Texture2D buyFrame,Texture2D sellFrame)
        {
            if(Ready)throw new InvalidOperationException("Merchant interface already initialized.");
            hud=worldHud;quests=questPanel;journal=owner;items=catalog;buyArtwork=buyFrame;sellArtwork=sellFrame;
        }
        private IEnumerator Start()
        {
            float until=Time.realtimeSinceStartup+15;
            while(hud!=null&&journal!=null&&(!hud.Ready||!journal.Ready)&&Time.realtimeSinceStartup<until)yield return null;
            try{Initialize();}catch(Exception ex){Failure=ex.Message;Debug.LogException(ex);enabled=false;}
        }
        public void Initialize()
        {
            if(Ready)return;
            if(hud==null||!hud.Ready||hud.CanvasRoot==null||hud.PresentationSkin==null||quests==null||
                journal==null||!journal.Ready||items==null||items.IsSkills||buyArtwork==null||sellArtwork==null)
                throw new InvalidOperationException("Merchant bindings/artwork are incomplete.");
            items.Validate();Build();subscribed=journal.Journal;subscribed.Changed+=OnJournalChanged;
            quests.MerchantRequested+=OnMerchantRequested;quests.HasMerchantPanel=true;
            hud.DialogueClosed+=Close;Ready=true;window.gameObject.SetActive(false);confirmation.gameObject.SetActive(false);
        }
        private void OnMerchantRequested(LegacyNpcRuntimeDescriptor value){Open(value);}
        public bool Open(LegacyNpcRuntimeDescriptor value)
        {
            Failure="";
            if(!Ready||!journal.Ready||value==null||value.NpcType!=1||(value.Services&NpcServiceKind.Shop)==0)
                return Fail("Este NPC no es un comerciante con una oferta original vinculada.");
            if(!NativeMerchantOfferFactory.SupportsMerchantType(value.MerchantType))
                return Fail("La categoría especial de este comerciante todavía requiere integración.");
            if(!LocalNpcInteractionGuard.Validate(journal.Actor,value,hud.SelectedNpc,hud.Ready,out string reason))return Fail(reason);
            Close();npc=value;npcLifetime=npc.LifetimeGeneration;
            var offers=new List<LocalMerchantOffer>(npc.SaleItems.Count);
            foreach(var item in npc.SaleItems)offers.Add(NativeMerchantOfferFactory.Resolve(items,(item.type<<8)|item.typeId));
            session=new LocalMerchantSession(journal.Journal,offers,key=>NativeMerchantOfferFactory.Resolve(items,key),ContextValid);
            quests.SuspendForService();selling=false;page=0;selected=-1;
            window.gameObject.SetActive(true);WorldInputGate.Set(this,true);Refresh();return true;
        }
        private bool ContextValid()
        {
            return enabled&&journal!=null&&journal.Ready&&npc!=null&&npc.LifetimeGeneration==npcLifetime&&
                LocalNpcInteractionGuard.Validate(journal.Actor,npc,hud.SelectedNpc,hud.Ready,out _);
        }
        private void Update()
        {
            if(Ready&&window.gameObject.activeSelf&&session!=null&&!session.IsOpen){Close();return;}
            if(!IsOpen)return;
            if(Time.unscaledTime>=nextContextCheck)
            {
                nextContextCheck=Time.unscaledTime+.1f;
                if(!ContextValid()){AbortConversation("La tienda se cerró: el comerciante ya no está accesible.");return;}
            }
            if(Input.GetKeyDown(KeyCode.Escape))ProcessEscape();
            if(dirty&&!confirming)Refresh();
        }
        public bool ProcessEscape()
        {
            if(!IsOpen)return false;
            if(confirmation.gameObject.activeSelf)CancelConfirmation();
            else {Close();hud.CloseDialogue();}
            return true;
        }
        private void OnJournalChanged()
        {
            dirty=true;
            // An external reward/inventory change invalidates the displayed quote.
            // The session itself independently checks the same revision at commit.
            if(!confirming&&quote!=null){CancelConfirmation();Fail("El inventario cambió. Revisa otra vez la operación.");}
        }
        public void Close()
        {
            if(closing)return;closing=true;
            try
            {
                ClearFocus();session?.Dispose();session=null;npc=null;quote=null;selected=-1;
                if(window!=null)window.gameObject.SetActive(false);
                if(confirmation!=null)confirmation.gameObject.SetActive(false);
                if(windowGroup!=null)windowGroup.interactable=true;
                WorldInputGate.Set(this,false);
            }
            finally{closing=false;}
        }
        public bool ShowSelling(bool value)
        {
            if(!IsOpen)return false;
            CancelConfirmation();selling=value;page=0;selected=-1;Refresh();return true;
        }
        public bool SelectPage(int value)
        {
            int count=selling?sellKeys.Count:StockCount;
            if(!IsOpen||value<0||value>=Math.Max(1,(count+29)/30))return false;
            CancelConfirmation();page=value;Refresh();return true;
        }
        private void Refresh()
        {
            if(!IsOpen)return;
            sellKeys.Clear();foreach(var item in journal.Journal.Inventory)sellKeys.Add(item.Key);sellKeys.Sort();
            int count=selling?sellKeys.Count:StockCount;page=Math.Min(page,Math.Max(0,(count-1)/30));
            window.sizeDelta=selling?new Vector2(256,368):new Vector2(256,355);
            frame.texture=selling?sellArtwork:buyArtwork;frame.rectTransform.sizeDelta=window.sizeDelta;
            frame.uvRect=NativeRadarView.TopLeftUv(frame.texture,new Rect(0,0,256,window.sizeDelta.y));
            title.text=npc.DisplayName;balance.text="Oro: "+journal.Journal.Gold;
            pageLabel.text=(page+1)+" / "+Math.Max(1,(count+29)/30);
            back.interactable=page>0;next.interactable=(page+1)*30<count;VisibleCount=0;
            for(int i=0;i<30;i++)
            {
                int index=page*30+i;LocalMerchantOffer offer=null;
                if(index<count)offer=selling?NativeMerchantOfferFactory.Resolve(items,sellKeys[index]):session.Stock[index];
                var cell=cells[i];cell.Root.anchoredPosition=selling?
                    new Vector2(18+38*(i%6),-(42+38*(i/6))):new Vector2(15+39*(i%6),-(71+39*(i/6)));
                bool present=index<count;cell.Button.interactable=present;
                cell.Icon.enabled=false;cell.Unknown.enabled=present;cell.Count.text="";
                if(present)VisibleCount++;
                if(offer!=null)
                {
                    if(items.TryItem(offer.ItemKey,out var entry)&&items.TryIcon(entry,out var texture,out var pixels))
                    {cell.Icon.texture=texture;cell.Icon.uvRect=NativeRadarView.TopLeftUv(texture,pixels);cell.Icon.enabled=true;cell.Unknown.enabled=false;}
                    journal.Journal.Inventory.TryGetValue(offer.ItemKey,out int owned);
                    cell.Count.text=selling&&owned>1?owned.ToString():"";
                    long price=selling?offer.Sell:offer.Buy;
                    cell.Tooltip.Message=offer.Name+"\nPrecio base local: "+price+" oro\n"+offer.Restriction;
                }
                else cell.Tooltip.Message=present?"Referencia original sin definición; no se sustituye por otro objeto.":"Casilla vacía";
            }
            int tabY=selling?292:32,pageY=selling?236:266;
            buyTab.GetComponent<RectTransform>().anchoredPosition=new Vector2(11,-tabY);
            sellTab.GetComponent<RectTransform>().anchoredPosition=new Vector2(87,-tabY);
            closeButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(164,-tabY);
            back.GetComponent<RectTransform>().anchoredPosition=new Vector2(13,-pageY);
            next.GetComponent<RectTransform>().anchoredPosition=new Vector2(216,-pageY);
            pageLabel.rectTransform.anchoredPosition=new Vector2(41,-pageY);
            balance.rectTransform.anchoredPosition=new Vector2(12,selling?-264:-295);
            status.rectTransform.anchoredPosition=new Vector2(12,selling?-331:-319);
            windowDrag.Clamp();dirty=false;
        }
        public bool SelectStock(int originalStockIndex)
        {
            if(!IsOpen||originalStockIndex<0||originalStockIndex>=StockCount)return false;
            selling=false;selected=originalStockIndex;BeginConfirmation();return true;
        }
        public bool SelectOwnedItem(int key)
        {
            if(!IsOpen||!journal.Journal.Inventory.ContainsKey(key))return false;
            selling=true;selected=key;BeginConfirmation();return true;
        }
        private void SelectCell(int cell)
        {
            int index=page*30+cell;
            if(selling){if(index<sellKeys.Count)SelectOwnedItem(sellKeys[index]);}
            else SelectStock(index);
        }
        private void BeginConfirmation()
        {
            windowGroup.interactable=false;confirmation.gameObject.SetActive(true);
            quantity.SetTextWithoutNotify("1");Requote();
        }
        public bool SetQuantity(int count)
        {
            if(!IsOpen||!confirmation.gameObject.activeSelf||count<1||count>LocalMerchantSession.MaximumQuantity)return false;
            quantity.SetTextWithoutNotify(count.ToString(CultureInfo.InvariantCulture));Requote();return quote!=null;
        }
        private void Requote()
        {
            if(!IsOpen||!confirmation.gameObject.activeSelf)return;
            quote=null;session.CancelQuote();confirm.interactable=false;
            if(!int.TryParse(quantity.text,NumberStyles.None,CultureInfo.InvariantCulture,out int count)||count<1||count>255)
            {question.text="Escribe una cantidad entre 1 y 255.";return;}
            string reason;
            bool ok=selling?session.QuoteSell(selected,count,out quote,out reason):session.QuoteBuy(selected,count,out quote,out reason);
            if(!ok){question.text=reason;Failure=reason;if(!session.IsOpen)AbortConversation(reason);return;}
            question.text=(selling?"Vender ":"Comprar ")+quote.ItemName+"\n\n"+quote.Quantity+" × "+quote.UnitPrice+" = "+quote.Total+" oro";
            confirm.interactable=true;Failure="";
        }
        public bool ConfirmPending()
        {
            if(!IsOpen||quote==null||!confirmation.gameObject.activeInHierarchy||!confirm.interactable||confirming)return false;
            confirming=true;confirm.interactable=false;
            try
            {
                if(!session.Confirm(quote,out string reason))
                {
                    Fail(reason);question.text=reason;
                    if(!session.IsOpen){AbortConversation(reason);return false;}
                    // Only a failed durable save can retry this unchanged quote.
                    confirm.interactable=ReferenceEquals(session.Pending,quote)&&quote.JournalRevision==journal.Journal.Revision;
                    return false;
                }
                string result=(quote.Side==LocalMerchantSide.Buy?"Compra":"Venta")+" guardada: "+quote.ItemName+" × "+quote.Quantity+".";
                CancelConfirmation();Refresh();hud.ShowMessage(result);Failure="";status.text=result;return true;
            }
            finally {confirming=false;}
        }
        public void CancelConfirmation()
        {
            ClearFocus();quote=null;session?.CancelQuote();
            if(confirmation!=null)confirmation.gameObject.SetActive(false);
            if(windowGroup!=null)windowGroup.interactable=true;
        }
        private void AbortConversation(string reason)
        {Close();if(hud!=null)hud.CloseDialogue();Fail(reason);}
        private bool Fail(string reason){Failure=reason??"Operación no disponible.";if(status!=null)status.text=Failure;hud?.ShowMessage(Failure);return false;}
        private void ClearFocus()
        {
            var selectedObject=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selectedObject!=null&&((window!=null&&selectedObject.transform.IsChildOf(window))||
                (confirmation!=null&&selectedObject.transform.IsChildOf(confirmation))))EventSystem.current.SetSelectedGameObject(null);
            if(quantity!=null&&quantity.isFocused)quantity.DeactivateInputField();
        }
        private Button Command(RectTransform parent,string name,string label,Vector2 position,Vector2 size,Action callback)
        {
            var b=NativeUiPrimitives.Button(name,parent,hud.PresentationSkin.command,position,size,callback);
            NativeUiPrimitives.Skin(b,hud.PresentationSkin.command,true);
            NativeUiPrimitives.Text("Label",b.GetComponent<RectTransform>(),label,12,Vector2.zero,size,TextAnchor.MiddleCenter);return b;
        }
        private void Build()
        {
            window=NativeUiPrimitives.Rect("Original local merchant",hud.CanvasRoot,new Vector2(32,140),new Vector2(256,355));
            var background=window.gameObject.AddComponent<Image>();background.color=new Color(.02f,.02f,.02f,1);
            window.gameObject.AddComponent<Canvas>().overrideSorting=true;window.GetComponent<Canvas>().sortingOrder=112;
            window.gameObject.AddComponent<GraphicRaycaster>();windowGroup=window.gameObject.AddComponent<CanvasGroup>();
            frame=NativeUiPrimitives.Art("Original market frame",window,buyArtwork,new Rect(0,0,256,355),Vector2.zero);
            var handle=NativeUiPrimitives.Rect("Merchant drag handle",window,Vector2.zero,new Vector2(230,30));
            handle.gameObject.AddComponent<Image>().color=Color.clear;
            windowDrag=handle.gameObject.AddComponent<NativeWindowDrag>();windowDrag.Configure(window,"merchant",null);
            title=NativeUiPrimitives.Text("Merchant name",window,"",12,new Vector2(14,6),new Vector2(226,22),TextAnchor.MiddleCenter);
            buyTab=Command(window,"Buy tab","Comprar",new Vector2(11,32),new Vector2(73,23),()=>ShowSelling(false));
            sellTab=Command(window,"Sell tab","Vender",new Vector2(87,32),new Vector2(73,23),()=>ShowSelling(true));
            closeButton=Command(window,"Close merchant","Cerrar",new Vector2(164,32),new Vector2(80,23),()=>{Close();hud.CloseDialogue();});
            for(int i=0;i<30;i++)
            {
                int id=i;var r=NativeUiPrimitives.Rect("Merchant item "+i,window,new Vector2(15+39*(i%6),71+39*(i/6)),new Vector2(32,32));
                var hit=r.gameObject.AddComponent<Image>();hit.color=Color.clear;
                var b=r.gameObject.AddComponent<Button>();b.targetGraphic=hit;b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>SelectCell(id));
                var icon=NativeUiPrimitives.Rect("Source icon",r,Vector2.zero,new Vector2(32,32)).gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
                var missing=NativeUiPrimitives.Text("Unresolved icon",r,"?",14,Vector2.zero,new Vector2(32,32),TextAnchor.MiddleCenter);
                var count=NativeUiPrimitives.Text("Actual count",r,"",10,new Vector2(-8,20),new Vector2(40,12),TextAnchor.MiddleRight);
                cells[i]=new Cell{Root=r,Button=b,Icon=icon,Count=count,Unknown=missing,Tooltip=r.gameObject.AddComponent<NativeUiTooltip>()};
            }
            back=Command(window,"Previous merchant page","◀",new Vector2(13,266),new Vector2(25,23),()=>SelectPage(page-1));
            next=Command(window,"Next merchant page","▶",new Vector2(216,266),new Vector2(25,23),()=>SelectPage(page+1));
            pageLabel=NativeUiPrimitives.Text("Merchant page",window,"",11,new Vector2(41,266),new Vector2(174,23),TextAnchor.MiddleCenter);
            balance=NativeUiPrimitives.Text("Actual merchant wallet",window,"",12,new Vector2(12,295),new Vector2(230,20),TextAnchor.MiddleRight);
            status=NativeUiPrimitives.Text("Local merchant scope",window,"Precios base en oro · comercio local",10,new Vector2(12,319),new Vector2(230,29));
            confirmation=NativeUiPrimitives.Rect("Merchant quote modal",hud.CanvasRoot,Vector2.zero,hud.CanvasRoot.rect.size);
            confirmation.anchorMin=Vector2.zero;confirmation.anchorMax=Vector2.one;confirmation.offsetMin=confirmation.offsetMax=Vector2.zero;
            confirmation.gameObject.AddComponent<Image>().color=new Color(0,0,0,.25f);
            var layer=confirmation.gameObject.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=180;
            confirmation.gameObject.AddComponent<GraphicRaycaster>();
            var box=NativeUiPrimitives.Rect("Merchant confirmation",confirmation,new Vector2(Mathf.Max(0,(hud.CanvasRoot.rect.width-300)/2),220),new Vector2(300,206));
            box.gameObject.AddComponent<Image>().color=new Color(.075f,.06f,.04f,1);
            question=NativeUiPrimitives.Text("Quoted item and total",box,"",13,new Vector2(14,10),new Vector2(272,91),TextAnchor.MiddleCenter);
            NativeUiPrimitives.Text("Quantity caption",box,"Cantidad",12,new Vector2(28,108),new Vector2(91,27));
            var input=NativeUiPrimitives.Rect("Merchant quantity",box,new Vector2(120,109),new Vector2(118,26));input.gameObject.AddComponent<Image>().color=new Color(.015f,.015f,.015f,1);
            quantity=input.gameObject.AddComponent<InputField>();quantity.contentType=InputField.ContentType.IntegerNumber;quantity.characterLimit=3;
            quantity.textComponent=NativeUiPrimitives.Text("Quantity value",input,"1",13,new Vector2(5,0),new Vector2(108,26),TextAnchor.MiddleCenter);
            quantity.onValueChanged.AddListener(_=>Requote());
            confirm=Command(box,"Confirm merchant exchange","Confirmar",new Vector2(27,156),new Vector2(111,29),()=>ConfirmPending());
            Command(box,"Cancel merchant quote","Cancelar",new Vector2(160,156),new Vector2(111,29),CancelConfirmation);
        }
        private void OnDisable()
        {bool held=session!=null;Close();if(held&&hud!=null)hud.CloseDialogue();}
        private void OnDestroy()
        {
            Close();if(subscribed!=null)subscribed.Changed-=OnJournalChanged;
            if(quests!=null){quests.MerchantRequested-=OnMerchantRequested;quests.HasMerchantPanel=false;}
            if(hud!=null)hud.DialogueClosed-=Close;
            if(window!=null){if(Application.isPlaying)Destroy(window.gameObject);else DestroyImmediate(window.gameObject);}
            if(confirmation!=null){if(Application.isPlaying)Destroy(confirmation.gameObject);else DestroyImmediate(confirmation.gameObject);}
        }
    }
}

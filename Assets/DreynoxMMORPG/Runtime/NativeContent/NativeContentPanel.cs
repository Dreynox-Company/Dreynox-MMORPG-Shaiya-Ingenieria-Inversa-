using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.NativeContent
{
    /// <summary>
    /// Original-art inventory view of the existing LOCAL ownership journal.
    /// The separate F9 developer catalog never grants items or learns skills.
    /// All original table parsing/decryption and DDS conversion happen in Editor.
    /// </summary>
    public sealed class NativeContentPanel : MonoBehaviour
    {
        private sealed class Cell
        {
            public Button button;
            public RawImage image;
            public Text count,missing;
            public NativeUiTooltip tooltip;
        }
        [SerializeField] private NativeCatalogAsset itemCatalog,skillCatalog;
        [SerializeField] private Texture2D inventoryArtwork,skillArtwork;
        [SerializeField] private NativeWorldHud hud;
        [SerializeField] private QuestJournalRuntime journal;
        private readonly NativeInventoryProjection inventory=new NativeInventoryProjection();
        private readonly Cell[] cells=new Cell[24];
        private readonly Cell[] catalogCells=new Cell[22];
        private readonly Text[] catalogLabels=new Text[22];
        private readonly List<GameObject> ownedUi=new List<GameObject>();
        private RectTransform inventoryWindow,catalogWindow,detailsWindow;
        private Text gold,pages,inventoryHint,catalogTitle,catalogPages,details;
        private Button previousInventory,nextInventory,previousCatalog,nextCatalog;
        private InputField search;
        private Cell equippedSword;
        private LocalStarterEquipment starter;
        private EquipmentAttachmentController equipment;
        private AttachmentDefinition shownStarter;
        private QuestJournalCore subscribed;
        private NativeCatalogAsset displayedCatalog;
        private List<NativeCatalogEntry> matches=new List<NativeCatalogEntry>();
        private int catalogPage,matchCount;
        private float searchAt=-1;
        private bool initialized,inventoryDirty=true;
        public bool Ready=>initialized;
        public bool IsInventoryOpen=>inventoryWindow!=null&&inventoryWindow.gameObject.activeInHierarchy;
        public bool IsCatalogOpen=>catalogWindow!=null&&catalogWindow.gameObject.activeInHierarchy;
        public int VisibleInventoryTypes=>inventory.Count;
        public int CatalogMatches=>matchCount;
        public string Error {get;private set;}="";
        public int InspectedKey {get;private set;}
        public NativeCatalogAsset Items=>itemCatalog;
        public NativeCatalogAsset Skills=>skillCatalog;

        public void Configure(NativeWorldHud worldHud,QuestJournalRuntime owner,NativeCatalogAsset items,
            NativeCatalogAsset skills,Texture2D inventoryTexture,Texture2D skillTexture)
        {
            if(initialized)throw new InvalidOperationException("Content interface is already initialized.");
            hud=worldHud;journal=owner;itemCatalog=items;skillCatalog=skills;
            inventoryArtwork=inventoryTexture;skillArtwork=skillTexture;
        }
        private IEnumerator Start()
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(hud!=null&&journal!=null&&(!hud.Ready||!journal.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            try { Initialize(); }
            catch(Exception ex){Error=ex.Message;Debug.LogException(ex);enabled=false;}
        }
        public void Initialize()
        {
            if(initialized)return;
            if(hud==null||!hud.Ready||hud.CanvasRoot==null||hud.PresentationSkin==null||journal==null||!journal.Ready||
                itemCatalog==null||skillCatalog==null||inventoryArtwork==null||skillArtwork==null)
                throw new InvalidOperationException("Original content UI bindings are incomplete.");
            itemCatalog.Validate();skillCatalog.Validate();
            if(itemCatalog.IsSkills||!skillCatalog.IsSkills)throw new InvalidOperationException("Item and skill catalogs are reversed.");
            if(journal.Actor!=null){starter=journal.Actor.GetComponent<LocalStarterEquipment>();equipment=journal.Actor.GetComponent<EquipmentAttachmentController>();}
            BuildInventory();BuildCatalog();
            subscribed=journal.Journal;subscribed.Changed+=OwnershipChanged;hud.DialogueOpened+=NpcOpened;
            initialized=true;Close();
        }
        private void Update()
        {
            if(!Ready)return;
            bool typing=NativeUiPrimitives.TextEditing;
            if(!typing&&Input.GetKeyDown(KeyCode.I)){if(IsInventoryOpen)Close();else OpenInventory();}
            if(!typing&&(Debug.isDebugBuild||Application.isEditor)&&Input.GetKeyDown(KeyCode.F9))
            {if(IsCatalogOpen)Close();else OpenCatalog(false);}
            // Let the quest panel own L. This owner closes without consuming the
            // other window's input or depending on MonoBehaviour execution order.
            if(!typing&&Input.GetKeyDown(KeyCode.L))Close();
            if(Input.GetKeyDown(KeyCode.Escape)&&(IsInventoryOpen||IsCatalogOpen))
            {
                if(typing&&search!=null&&search.isFocused){search.DeactivateInputField();EventSystem.current?.SetSelectedGameObject(null);}
                else if(detailsWindow!=null&&detailsWindow.gameObject.activeSelf)detailsWindow.gameObject.SetActive(false);
                else Close();
            }
            if(IsInventoryOpen&&(inventoryDirty||shownStarter!=CurrentStarter()))RefreshInventory();
            if(IsCatalogOpen&&searchAt>=0&&Time.unscaledTime>=searchAt){searchAt=-1;RefreshCatalog();}
        }
        private void OwnershipChanged()
        {inventoryDirty=true;if(IsInventoryOpen&&detailsWindow!=null)detailsWindow.gameObject.SetActive(false);}
        private void NpcOpened(Dreynox.Mmorpg.World.LegacyNpcRuntimeDescriptor npc){Close();}
        public bool OpenInventory()
        {
            if(!Ready||!journal.Ready)return false;
            hud.CloseDialogue();Close();inventoryWindow.gameObject.SetActive(true);
            WorldInputGate.Set(this,true);RefreshInventory();return true;
        }
        public bool OpenCatalog(bool skills)
        {
            if(!Ready||!(Debug.isDebugBuild||Application.isEditor))return false;
            hud.CloseDialogue();Close();displayedCatalog=skills?skillCatalog:itemCatalog;catalogPage=0;
            search.SetTextWithoutNotify("");catalogWindow.gameObject.SetActive(true);
            WorldInputGate.Set(this,true);RefreshCatalog();return true;
        }
        public void Close()
        {
            if(search!=null&&search.isFocused)search.DeactivateInputField();
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&((inventoryWindow!=null&&selected.transform.IsChildOf(inventoryWindow))||
                (catalogWindow!=null&&selected.transform.IsChildOf(catalogWindow))||(detailsWindow!=null&&selected.transform.IsChildOf(detailsWindow))))
                EventSystem.current.SetSelectedGameObject(null);
            if(inventoryWindow!=null)inventoryWindow.gameObject.SetActive(false);
            if(catalogWindow!=null)catalogWindow.gameObject.SetActive(false);
            if(detailsWindow!=null)detailsWindow.gameObject.SetActive(false);
            searchAt=-1;WorldInputGate.Set(this,false);
        }
        private RectTransform Window(string name,Vector2 size,Texture2D art,Rect crop,Vector2 at,int order)
        {
            var root=NativeUiPrimitives.Rect(name,hud.CanvasRoot,at,size);ownedUi.Add(root.gameObject);
            root.gameObject.AddComponent<Image>().color=new Color(.025f,.025f,.025f,1);
            var layer=root.gameObject.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=order;
            root.gameObject.AddComponent<GraphicRaycaster>();
            if(art!=null)NativeUiPrimitives.Art("Original artwork",root,art,crop,Vector2.zero);
            var strip=NativeUiPrimitives.Rect("Window drag strip",root,Vector2.zero,new Vector2(size.x-30,30));
            strip.gameObject.AddComponent<Image>().color=Color.clear;
            var drag=strip.gameObject.AddComponent<NativeWindowDrag>();drag.Error=Report;
            drag.Configure(root,name.ToLowerInvariant().Replace(' ','-'),null);
            return root;
        }
        private Button Command(RectTransform parent,string name,string label,Vector2 at,Vector2 size,Action action)
        {
            var button=NativeUiPrimitives.Button(name,parent,hud.PresentationSkin.command,at,size,action);
            NativeUiPrimitives.Skin(button,hud.PresentationSkin.command,true);
            NativeUiPrimitives.Text("Label",button.GetComponent<RectTransform>(),label,12,Vector2.zero,size,TextAnchor.MiddleCenter);
            return button;
        }
        private Cell MakeCell(RectTransform parent,string name,Vector2 at,Action click)
        {
            var root=NativeUiPrimitives.Rect(name,parent,at,new Vector2(32,32));
            var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=hit;
            button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>click());
            var icon=NativeUiPrimitives.Rect("Original icon",root,Vector2.zero,new Vector2(32,32)).gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
            var number=NativeUiPrimitives.Text("Owned count",root,"",10,new Vector2(-8,20),new Vector2(40,12),TextAnchor.MiddleRight);
            var missing=NativeUiPrimitives.Text("Unavailable original icon",root,"?",15,Vector2.zero,new Vector2(32,32),TextAnchor.MiddleCenter);
            return new Cell{button=button,image=icon,count=number,missing=missing,tooltip=root.gameObject.AddComponent<NativeUiTooltip>()};
        }
        private static void SetCell(Cell cell,NativeCatalogAsset catalog,NativeCatalogEntry entry,int count,string context,bool present)
        {
            cell.image.enabled=false;cell.missing.enabled=false;cell.button.interactable=present;
            cell.count.text=present&&count>1?count.ToString():"";
            if(!present){cell.tooltip.Message="Casilla vacía";return;}
            if(entry!=null&&catalog.TryIcon(entry,out var texture,out var crop))
            {cell.image.texture=texture;cell.image.uvRect=NativeRadarView.TopLeftUv(texture,crop);cell.image.enabled=true;}
            else cell.missing.enabled=true;
            string description=entry==null?"Definición original no disponible.":entry.Description;
            if(description.Length>1000)description=description.Substring(0,1000)+"…";
            cell.tooltip.Message=(entry!=null?entry.Name:"Objeto sin definición")+"\n"+description+"\n"+context+
                (entry!=null&&!String.IsNullOrEmpty(entry.IconIssue)?"\n"+entry.IconIssue:"");
        }
        private void BuildInventory()
        {
            inventoryWindow=Window("Native inventory",new Vector2(284,564),inventoryArtwork,new Rect(0,0,284,564),
                new Vector2(Mathf.Max(0,hud.CanvasRoot.rect.width-498),85),110);
            NativeUiPrimitives.Text("Inventory title",inventoryWindow,"Inventario local",12,new Vector2(16,7),new Vector2(245,20),TextAnchor.MiddleCenter);
            Command(inventoryWindow,"Close inventory","×",new Vector2(261,4),new Vector2(20,20),Close);
            equippedSword=MakeCell(inventoryWindow,"Actual starter attachment",new Vector2(66,155),()=>
                {if(CurrentStarter()!=null&&itemCatalog.TryGet(1,1,out var sword))ShowDetails(itemCatalog,sword,"Adjunto visual local; no es un objeto adicional de la bolsa.");});
            for(int i=0;i<24;i++)
            {
                int cell=i;cells[i]=MakeCell(inventoryWindow,"Owned item "+i,new Vector2(NativeInventoryProjection.X(i),NativeInventoryProjection.Y(i)),()=>InspectInventoryCell(cell));
            }
            gold=NativeUiPrimitives.Text("Actual local gold",inventoryWindow,"",12,new Vector2(37,466),new Vector2(222,23),TextAnchor.MiddleRight);
            previousInventory=Command(inventoryWindow,"Previous inventory page","◀",new Vector2(15,271),new Vector2(26,23),()=>SelectInventoryPage(inventory.Page-1));
            pages=NativeUiPrimitives.Text("Inventory page",inventoryWindow,"",12,new Vector2(48,271),new Vector2(182,23),TextAnchor.MiddleCenter);
            nextInventory=Command(inventoryWindow,"Next inventory page","▶",new Vector2(242,271),new Vector2(26,23),()=>SelectInventoryPage(inventory.Page+1));
            inventoryHint=NativeUiPrimitives.Text("Inventory scope",inventoryWindow,
                "Objetos y oro del diario local.\nLas casillas agrupan tipos; no son posiciones de bolsa del servidor.",11,new Vector2(18,502),new Vector2(248,48));
        }
        public void RefreshInventory()
        {
            if(!Ready||!journal.Ready)return;
            inventory.Synchronize(journal.Journal.Inventory,journal.Journal.Gold);
            for(int i=0;i<24;i++)
            {
                bool present=inventory.TryCell(i,out var value);NativeCatalogEntry entry=null;
                if(present)itemCatalog.TryItem(value.Key,out entry);
                SetCell(cells[i],itemCatalog,entry,value.Count,present?"Cantidad local: "+value.Count+" · "+(value.Key>>8)+"/"+(value.Key&255):"",present);
            }
            shownStarter=CurrentStarter();
            itemCatalog.TryGet(1,1,out var sword);
            SetCell(equippedSword,itemCatalog,sword,1,"Adjunto visual de inicio, separado de la bolsa local.",shownStarter!=null);
            gold.text=inventory.Gold.ToString();pages.text=(inventory.Page+1)+" / "+inventory.PageCount;
            previousInventory.interactable=inventory.Page>0;nextInventory.interactable=inventory.Page+1<inventory.PageCount;
            inventoryDirty=false;
        }
        private AttachmentDefinition CurrentStarter()
        {
            if(starter==null||!starter.Equipped||equipment==null||!equipment.Has(EquipmentSlot.MainHand))return null;
            return equipment.TryGetDefinition(EquipmentSlot.MainHand,out var value)&&value==starter.Definition?value:null;
        }
        public bool SelectInventoryPage(int page)
        {if(!Ready||!inventory.SelectPage(page))return false;RefreshInventory();return true;}
        public bool InspectInventoryCell(int cell)
        {
            if(!IsInventoryOpen||!inventory.TryCell(cell,out var value))return false;
            if(!journal.Journal.Inventory.TryGetValue(value.Key,out int currentCount)||currentCount<=0)
            {RefreshInventory();Report("El objeto ya no está en el inventario local.");return false;}
            if(!itemCatalog.TryItem(value.Key,out var entry)){Report("El objeto local no tiene una definición original vinculada.");return false;}
            ShowDetails(itemCatalog,entry,"Cantidad del diario local: "+currentCount);return true;
        }
        private void BuildCatalog()
        {
            catalogWindow=Window("Native development catalog",new Vector2(500,626),skillArtwork,new Rect(0,0,500,626),
                new Vector2(Mathf.Max(0,(hud.CanvasRoot.rect.width-500)/2),75),110);
            catalogTitle=NativeUiPrimitives.Text("Catalog title",catalogWindow,"",12,new Vector2(18,7),new Vector2(445,22),TextAnchor.MiddleCenter);
            Command(catalogWindow,"Close catalog","×",new Vector2(476,4),new Vector2(20,20),Close);
            Command(catalogWindow,"Item catalog","Objetos",new Vector2(11,39),new Vector2(70,26),()=>SwitchCatalog(false));
            Command(catalogWindow,"Skill catalog","Habilidades",new Vector2(84,39),new Vector2(92,26),()=>SwitchCatalog(true));
            var field=NativeUiPrimitives.Rect("Catalog search",catalogWindow,new Vector2(183,40),new Vector2(304,24));
            field.gameObject.AddComponent<Image>().color=new Color(.03f,.025f,.02f,.95f);
            search=field.gameObject.AddComponent<InputField>();search.characterLimit=128;
            search.textComponent=NativeUiPrimitives.Text("Search text",field,"",12,new Vector2(5,2),new Vector2(294,20));
            var placeholder=NativeUiPrimitives.Text("Search placeholder",field,"Buscar nombre o ID / nivel…",12,new Vector2(5,2),new Vector2(294,20));
            placeholder.color=new Color(.7f,.7f,.7f,1);search.placeholder=placeholder;
            search.onValueChanged.AddListener(_=>{catalogPage=0;searchAt=Time.unscaledTime+.18f;if(detailsWindow!=null)detailsWindow.gameObject.SetActive(false);});
            for(int i=0;i<22;i++)
            {
                int index=i;int col=i/11,row=i%11;float x=18+241*col,y=83+45*row;
                catalogCells[i]=MakeCell(catalogWindow,"Catalog definition "+i,new Vector2(x,y),()=>InspectCatalogRow(index));
                catalogLabels[i]=NativeUiPrimitives.Text("Definition name "+i,catalogWindow,"",11,new Vector2(x+37,y-1),new Vector2(190,38));
                // Only the real 32px icon is clickable; long descriptions are inspected separately.
            }
            previousCatalog=Command(catalogWindow,"Previous definitions","◀",new Vector2(15,595),new Vector2(28,25),()=>SelectCatalogPage(catalogPage-1));
            catalogPages=NativeUiPrimitives.Text("Definition page",catalogWindow,"",11,new Vector2(50,594),new Vector2(397,25),TextAnchor.MiddleCenter);
            nextCatalog=Command(catalogWindow,"Next definitions","▶",new Vector2(457,595),new Vector2(28,25),()=>SelectCatalogPage(catalogPage+1));
            detailsWindow=Window("Original definition details",new Vector2(300,564),null,default,new Vector2(0,85),120);
            NativeUiPrimitives.Text("Definition header",detailsWindow,"Datos originales · solo lectura",12,new Vector2(8,5),new Vector2(267,24),TextAnchor.MiddleCenter);
            Command(detailsWindow,"Close details","×",new Vector2(276,4),new Vector2(20,20),()=>detailsWindow.gameObject.SetActive(false));
            var viewport=NativeUiPrimitives.Rect("Definition viewport",detailsWindow,new Vector2(9,34),new Vector2(282,518));
            viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            details=NativeUiPrimitives.Text("All native fields",viewport,"",12,Vector2.zero,new Vector2(263,518),TextAnchor.UpperLeft);
            details.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=details.rectTransform;
            viewport.gameObject.AddComponent<NativeScrollChrome>().Build(viewport,scroll,hud.PresentationSkin);
        }
        private void SwitchCatalog(bool skills)
        {
            displayedCatalog=skills?skillCatalog:itemCatalog;catalogPage=0;search.SetTextWithoutNotify("");searchAt=-1;
            detailsWindow.gameObject.SetActive(false);RefreshCatalog();
        }
        public bool SetCatalogQuery(string value)
        {
            if(!IsCatalogOpen||value==null||value.Length>128)return false;
            detailsWindow.gameObject.SetActive(false);
            search.SetTextWithoutNotify(value);catalogPage=0;searchAt=-1;RefreshCatalog();return true;
        }
        public bool SelectCatalogPage(int page)
        {
            if(!IsCatalogOpen||page<0||page>=Math.Max(1,(matchCount+21)/22))return false;
            detailsWindow.gameObject.SetActive(false);catalogPage=page;RefreshCatalog();return true;
        }
        private void RefreshCatalog()
        {
            if(displayedCatalog==null)return;
            matches=displayedCatalog.Search(search.text,catalogPage*22,22,out matchCount);
            catalogTitle.text="Catálogo de desarrollo · "+(displayedCatalog.IsSkills?"Habilidades":"Objetos");
            for(int i=0;i<22;i++)
            {
                var entry=i<matches.Count?matches[i]:null;
                SetCell(catalogCells[i],displayedCatalog,entry,1,"Definición original. No concede objetos ni aprende habilidades.",entry!=null);
                catalogLabels[i].text=entry==null?"":entry.Name+"\n"+entry.Id+"/"+entry.Variant+" · Nivel "+displayedCatalog.Value(entry,"level");
            }
            int pageCount=Math.Max(1,(matchCount+21)/22);
            catalogPages.text=matchCount+" definiciones · "+(catalogPage+1)+" / "+pageCount+" · Solo lectura";
            previousCatalog.interactable=catalogPage>0;nextCatalog.interactable=catalogPage+1<pageCount;
        }
        public bool InspectCatalogRow(int row)
        {
            if(!IsCatalogOpen||row<0||row>=matches.Count)return false;
            ShowDetails(displayedCatalog,matches[row],"Catálogo de desarrollo: no acredita propiedad ni aprendizaje.");return true;
        }
        private void ShowDetails(NativeCatalogAsset catalog,NativeCatalogEntry entry,string context)
        {
            var text=new StringBuilder(entry.Name).Append('\n').Append(entry.Id).Append('/').Append(entry.Variant)
                .Append("\n\n").Append(entry.Description).Append("\n\n").Append(context);
            if(!String.IsNullOrWhiteSpace(entry.IconIssue))text.Append("\n").Append(entry.IconIssue);
            text.Append("\n\nCAMPOS NUMÉRICOS ORIGINALES\nLos nombres no acreditan por sí solos una fórmula de juego.\n");
            for(int i=0;i<catalog.FieldCount;i++){string field=catalog.FieldName(i);text.Append('\n').Append(field).Append(": ").Append(catalog.Value(entry,field));}
            details.text=text.ToString();details.rectTransform.anchoredPosition=Vector2.zero;
            detailsWindow.gameObject.SetActive(true);InspectedKey=entry.Key;
        }
        private void Report(string reason){Error=reason??"";hud?.ShowMessage(Error);}
        private void OnDisable(){Close();}
        private void OnDestroy()
        {
            Close();if(subscribed!=null)subscribed.Changed-=OwnershipChanged;if(hud!=null)hud.DialogueOpened-=NpcOpened;
            foreach(var root in ownedUi)if(root!=null){if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);}
            ownedUi.Clear();
        }
    }
}

using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SwampKeeperMods;

public sealed class ModUI : MonoBehaviour
{
    public ModUI(IntPtr p) : base(p) { }
    internal string Status = "";
    internal bool Open;
    private bool pending;
    private float showAt;
    private CursorLockMode oldLock;
    private bool oldVisible, ownsPause;
    private Player? blockedPlayer;
    private GameObject? root, modal;
    private RectTransform? viewport, content, detailContent;
    private TMP_FontAsset? font;
    private TextMeshProUGUI? totals, count, notice, detail, hudText, hudSmall, detailName, detailType, detailSelection, detailScrollHint, warning, noResults;
    private Image? detailIcon;
    private ScrollRect? detailScroll;
    private Button? all, clear;
    private TMP_InputField? search;
    private string filter = "";
    private int hovered = -1;
    private readonly List<Button> itemButtons = new();
    private readonly List<TextMeshProUGUI> itemLabels = new();
    private readonly List<Image> itemIcons = new(), itemBackgrounds = new();
    private readonly List<GameObject> selectionMarks = new();
    private readonly string[] english = {
        "Secondary Quiver", "Large Nock", "Elastic Bowstring", "Finger Guard", "Pyeonjeon",
        "Double Fletching", "Wooden Clogs", "Taesahye", "Scent Pouch", "Light Body Charm",
        "Ginseng", "Silk Clothes", "Stabilizing Weight", "Wind Talisman", "Mutated Swamp Apple",
        "Tailring", "Tail Guard", "Ignition Compound", "Jade Bell", "Final Arrow Talisman",
        "Fang Necklace", "Arrow Husk", "Retrieval Cord", "Trident Arrow", "Portable Watchtower",
        "Elastic Arrow", "Hwacha", "Ghostcall Arrow", "Snare", "Pine Tar Jar", "Tattered Cloth",
        "Spyglass", "Cornucopia", "Spirit Stone", "Twinshot Bow", "Slaying Charm", "Powder Charge",
        "Ricochet Mail", "Straw Sandal", "Leather Glove"
    };
    private static readonly Color Dark = new(.055f,.085f,.10f,.99f), Row = new(.11f,.16f,.18f,1),
        Selected = new(.16f,.32f,.29f,1), Green = new(.78f,.94f,.65f,1), White = new(.91f,.94f,.94f,1), Muted = new(.64f,.73f,.75f,1);

    internal void SchedulePreparation() { pending = true; showAt = Time.realtimeSinceStartup + 1; }
    internal void Show()
    {
        if (Open) return;
        Open = true; oldLock = Cursor.lockState; oldVisible = Cursor.visible;
        ownsPause = TimeManager.tm && !TimeManager.tm.isPause;
        if (ownsPause) TimeManager.tm.PauseGame();
        if (Player.player)
        {
            blockedPlayer = Player.player;
            blockedPlayer.StopControl(gameObject); blockedPlayer.StopAiming(gameObject);
        }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (modal) modal!.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        Refresh();
    }
    internal void Close()
    {
        if (!Open) return;
        Open = false;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (modal) modal!.SetActive(false);
        if (blockedPlayer) { blockedPlayer!.StartControl(gameObject); blockedPlayer.StartAiming(gameObject); }
        blockedPlayer = null;
        if (ownsPause && TimeManager.tm) TimeManager.tm.ResumeGame();
        ownsPause = false;
        Cursor.lockState = oldLock; Cursor.visible = oldVisible;
    }
    [HideFromIl2Cpp]
    internal void SelectItems(IEnumerable<int> ids)
    {
        Plugin.Store.Data.PreparedItems = ids.Where(i => i >= 0 && i < 40).Distinct().OrderBy(i => i).ToArray();
        if (!Plugin.Store.Save()) Status = Plugin.Store.Warning;
        Refresh();
    }
    public void Update()
    {
        if (!root)
        {
            var source = Object.FindObjectOfType<TextMeshProUGUI>();
            if (source && source.font) { font = source.font; Build(); }
        }
        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame) { if(CoopIntegration.Active)CoopIntegration.OpenMenu?.Invoke();else if (Open) Close(); else Show(); }
        if (Open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        if (pending && Time.realtimeSinceStartup >= showAt && Plugin.InPreparation())
        {
            pending = false;
            if (Plugin.ItemsEnabled.Value) Show();
        }
        if (hudText)
        {
            hudText!.transform.parent.gameObject.SetActive(!Open && !CoopIntegration.MenuOpen && Plugin.KillsEnabled.Value && Plugin.ShowHud.Value);
            hudText.text = "LIFETIME  " + Plugin.Store.Data.LifetimeKills.ToString("N0");
            hudSmall!.text = "Run " + Plugin.RunKills.ToString("N0") + "   |   F6 mods" + (Plugin.Store.Warning.Length > 0 ? "\nSave warning: open F6" : "");
        }
        if (Open)
        {
            if (search && search!.text != filter) { filter=search.text; Refresh(); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (totals) totals!.text = "Lifetime kills  " + Plugin.Store.Data.LifetimeKills.ToString("N0") + "      This run  " + Plugin.RunKills.ToString("N0") + "      This session  " + Plugin.SessionKills.ToString("N0");
            UpdateHover();
        }
    }
    public void OnApplicationQuit() { if (Plugin.KillsEnabled.Value) Plugin.Store.Save(); }

    [HideFromIl2Cpp]
    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        return rt;
    }
    [HideFromIl2Cpp]
    private Image Background(RectTransform rt, Color color, bool block = false)
    {
        var image = rt.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = block; return image;
    }
    [HideFromIl2Cpp]
    private TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
    {
        var rt = Rect(name,parent,x,y,w,h);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.raycastTarget = false; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
    [HideFromIl2Cpp]
    private Button Button(Transform parent, string name, string label, float x, float y, float w, float h, Action action)
    {
        var rt = Rect(name,parent,x,y,w,h);
        var image = Background(rt,Row,true);
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var navigation=button.navigation; navigation.mode=Navigation.Mode.None; button.navigation=navigation;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.74f,.94f,.80f); colors.pressedColor = new Color(.55f,.8f,.65f); colors.disabledColor = new Color(.65f,.65f,.65f); button.colors = colors;
        Text(rt,"Label",label,12,0,w-24,h,16,White);
        button.onClick.AddListener((UnityAction)action);
        return button;
    }

    private void Build()
    {
        root = new GameObject("SwampKeeperModsCanvas"); Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1100,760); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();
        var hud = Rect("KillTrackerHUD",root.transform,0,0,252,84);
        hud.anchorMin = hud.anchorMax = new Vector2(1,1); hud.anchoredPosition = new Vector2(-272,-90);
        Background(hud,Dark);
        hudText = Text(hud,"Lifetime","",12,7,230,27,18,White);
        hudSmall = Text(hud,"Run","",12,36,230,42,14,Muted);
        hudSmall.enableWordWrapping = true;
        var full = Rect("ModalBlocker",root.transform,0,0,0,0);
        full.anchorMin=Vector2.zero; full.anchorMax=Vector2.one; full.offsetMin=Vector2.zero; full.offsetMax=Vector2.zero;
        Background(full,new Color(0,0,0,.60f),true); modal = full.gameObject;
        var panel=Rect("PreparationPanel",full,0,0,1000,700);
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f); panel.anchoredPosition=Vector2.zero;
        Background(panel,Dark,true);
        Background(Rect("Accent",panel,0,0,1000,3),Green);
        Text(panel,"Heading","PREPARE YOUR LOADOUT",26,18,850,36,28,Green).fontStyle=FontStyles.Bold;
        Button(panel,"Close","X",934,18,40,34,Close);
        totals=Text(panel,"Totals","",26,64,948,28,17,White);
        notice=Text(panel,"Notice","",26,104,948,26,17,White);
        Text(panel,"Instructions","Click icons to select or deselect. Hover to inspect every effect. One copy per item.",26,134,948,22,14,Muted);
        var field=Rect("Search",panel,26,170,432,34); Background(field,Row,true);
        var input=field.gameObject.AddComponent<TMP_InputField>();
        var inputText=Text(field,"InputText","",10,0,412,34,17,White);
        var placeholder=Text(field,"Placeholder","Search items...",10,0,412,34,16,Muted);
        input.textViewport=field; input.textComponent=inputText; input.placeholder=placeholder; input.characterLimit=100;
        var inputNavigation=input.navigation; inputNavigation.mode=Navigation.Mode.None; input.navigation=inputNavigation;
        search=input;
        count=Text(panel,"SelectedCount","",474,170,198,34,18,Green);
        all=Button(panel,"SelectAll","Select all",694,170,132,34,()=>SelectItems(Enumerable.Range(0,40)));
        clear=Button(panel,"Clear","Clear",836,170,138,34,()=>SelectItems(Array.Empty<int>()));
        viewport=Rect("Viewport",panel,26,222,646,400);
        viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("ItemList",viewport,0,0,646,400);
        for(int id=0;id<40;id++)
        {
            int captured=id;
            var b=Button(content,"Item"+id,"",(id%8)*82,(id/8)*82,72,72,()=>
            {
                if (!CanEdit()) return;
                var selected=Plugin.Store.Data.PreparedItems;
                SelectItems(selected.Contains(captured)?selected.Where(i=>i!=captured):selected.Append(captured));
            });
            var fallback=b.GetComponentInChildren<TextMeshProUGUI>();
            fallback.text=id.ToString("D2"); fallback.alignment=TextAlignmentOptions.Center;
            var inner=Background(Rect("SlotBackground",b.transform,3,3,66,66),Dark);
            var icon=Background(Rect("Icon",b.transform,9,9,54,54),Color.white);
            icon.preserveAspect=true;
            var mark=Rect("Selected",b.transform,54,0,18,18); Background(mark,Green);
            var markText=Text(mark,"Mark","x",0,0,18,18,14,Dark); markText.alignment=TextAlignmentOptions.Center;
            fallback.transform.SetAsLastSibling();
            itemButtons.Add(b); itemLabels.Add(fallback); itemIcons.Add(icon); itemBackgrounds.Add(inner); selectionMarks.Add(mark.gameObject);
        }
        noResults=Text(viewport,"NoResults","No matching items",0,0,646,400,20,Muted); noResults.alignment=TextAlignmentOptions.Center;
        var card=Rect("ItemDetails",panel,694,222,280,400); Background(card,Row,true);
        detailIcon=Background(Rect("Icon",card,16,16,64,64),Color.white); detailIcon.preserveAspect=true;
        detailName=Text(card,"Name","Inspect an item",96,12,168,60,21,Green); detailName.enableWordWrapping=true;
        detailName.enableAutoSizing=true; detailName.fontSizeMin=17; detailName.fontSizeMax=21;
        detailType=Text(card,"Type","",96,74,168,24,13,Muted);
        var descriptionViewport=Rect("EffectsViewport",card,16,112,248,240);
        descriptionViewport.gameObject.AddComponent<RectMask2D>(); Background(descriptionViewport,new Color(0,0,0,0),true);
        detail=Text(descriptionViewport,"Description","",0,0,248,240,17,White);
        detail.enableWordWrapping=true; detail.alignment=TextAlignmentOptions.TopLeft; detail.overflowMode=TextOverflowModes.Overflow;
        detailContent=detail.GetComponent<RectTransform>();
        detailScroll=descriptionViewport.gameObject.AddComponent<ScrollRect>(); detailScroll.viewport=descriptionViewport; detailScroll.content=detailContent;
        detailScroll.horizontal=false; detailScroll.vertical=true; detailScroll.movementType=ScrollRect.MovementType.Clamped; detailScroll.scrollSensitivity=28;
        detailSelection=Text(card,"SelectionState","",16,358,248,22,14,Green);
        detailScrollHint=Text(card,"ScrollHint","",16,380,248,18,12,Muted);
        warning=Text(panel,"SaveStatus","",26,628,948,20,13,Muted);
        Text(panel,"Footer","Your loadout is saved automatically. F6 reopens this panel.",26,650,740,34,14,Muted);
        Button(panel,"Done","Done  [F6]",794,650,180,34,Close);
        modal.SetActive(Open); Refresh();
    }
    private bool CanEdit()=>Plugin.ItemsEnabled.Value && (!Player.player || Plugin.InPreparation());
    private void Refresh()
    {
        if (!root) return;
        bool edit=CanEdit(); all!.interactable=clear!.interactable=edit;
        notice!.text=edit?"Choose your starting items. All items are free, including locked items.":"Loadout locked for this run. Change it in the preparation area.";
        count!.text=Plugin.Store.Data.PreparedItems.Length+" / 40 selected";
        int visible=0; int firstVisible=-1;
        for(int id=0;id<40;id++)
        {
            string name=Name(id);
            bool show=(id.ToString("D2")+" "+english[id]+" "+name).Contains(filter,StringComparison.OrdinalIgnoreCase);
            itemButtons[id].gameObject.SetActive(show);
            if(!show)continue;
            if(firstVisible<0)firstVisible=id;
            bool chosen=Plugin.Store.Data.PreparedItems.Contains(id);
            var rt=itemButtons[id].GetComponent<RectTransform>(); rt.anchoredPosition=new Vector2((visible%8)*82,-(visible/8)*82); visible++;
            Sprite? sprite=null;
            if(ItemManager.im) sprite=ItemManager.im.GetItem(id).icon;
            itemIcons[id].sprite=sprite; itemIcons[id].enabled=sprite;
            itemLabels[id].gameObject.SetActive(!sprite);
            itemButtons[id].GetComponent<Image>().color=chosen?Green:Row;
            itemBackgrounds[id].color=chosen?Selected:Dark;
            selectionMarks[id].SetActive(chosen);
            itemButtons[id].interactable=edit;
        }
        noResults!.gameObject.SetActive(visible==0);
        warning!.text=Status.Length>0?Status:Plugin.Store.Warning;
        SetDetail(hovered>=0 && itemButtons[hovered].gameObject.activeSelf?hovered:firstVisible);
    }
    private void UpdateHover()
    {
        if (!viewport || Mouse.current==null) return;
        InspectAt(Mouse.current.position.ReadValue());
    }
    [HideFromIl2Cpp]
    internal void InspectAt(Vector2 pos)
    {
        if(!viewport)return;
        int id=-1;
        if(RectTransformUtility.RectangleContainsScreenPoint(viewport,pos,null))
            for(int i=0;i<itemButtons.Count;i++)
                if(itemButtons[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(itemButtons[i].GetComponent<RectTransform>(),pos,null)) { id=i; break; }
        // Keep details visible when the pointer moves onto the card to read/scroll them.
        if(id>=0 && id!=hovered)SetDetail(id);
    }
    private void SetDetail(int id)
    {
        bool changed=id!=hovered;
        hovered=id; if (!detail) return;
        detailName!.text=id>=0?Name(id):"No matching items";
        detailType!.text=""; detailIcon!.enabled=false;
        if(id>=0 && ItemManager.im)
        {
            var item=ItemManager.im.GetItem(id);
            detailIcon.sprite=item.icon; detailIcon.enabled=item.icon;
            detailType.text=item.GetTypeText();
        }
        detail!.text=id>=0?ItemDescription(id):"Clear your search to see all items.";
        detailContent!.sizeDelta=new Vector2(248,Math.Max(240,detail.preferredHeight+8));
        if(changed)detailScroll!.verticalNormalizedPosition=1;
        detailSelection!.text=id<0?"":Plugin.Store.Data.PreparedItems.Contains(id)?"Selected for your next run":"Click this item's icon to select";
        detailScrollHint!.text=detailContent.sizeDelta.y>241?"Scroll here to read all effects":"";
    }
    [HideFromIl2Cpp]
    internal string ItemDescription(int id)
    {
        if(!ItemManager.im)return "Enter the preparation area to load item details.";
        var item=ItemManager.im.GetItem(id);
        var paragraphs=new List<string>();
        var general=item.GetExplain();
        if(!string.IsNullOrWhiteSpace(general))paragraphs.Add(general.Trim());
        var effects=item.GetValueExplain();
        if(effects!=null)
            foreach(var effect in effects)
                if(!string.IsNullOrWhiteSpace(effect))paragraphs.Add(effect.Trim());
        return paragraphs.Count>0?string.Join("\n\n",paragraphs):"No effect information is provided by the game.";
    }
    private string Name(int id)
    {
        try{if(ItemManager.im){var n=ItemManager.im.GetItem(id).GetName();if(!string.IsNullOrWhiteSpace(n))return n;}}catch{}
        return english[id];
    }
}

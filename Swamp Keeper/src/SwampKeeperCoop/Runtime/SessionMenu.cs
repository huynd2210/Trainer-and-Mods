using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using SwampKeeperCoop.Protocol;

namespace SwampKeeperCoop;

public sealed class SessionMenu:MonoBehaviour
{
    public SessionMenu(IntPtr p):base(p){}
    internal static SessionMenu? Instance;
    internal static bool Open {get;private set;}
    private GameObject? root,panel,shade,setup,loadout,leave;private TMP_FontAsset? font;
    private TMP_InputField? address,code;private TextMeshProUGUI? status,detail,title,selection,shortcut;
    private Image? detailIcon;private Button? ready;
    private RectTransform? detailContent;private readonly List<RectTransform> cells=new();private readonly List<Image> cellBgs=new(),cellIcons=new();
    private readonly List<TextMeshProUGUI> marks=new();private readonly HashSet<int> items=new();private readonly int[] skills={0,1};
    private int tab,hover=-1;private bool populated,wasReady,wasPeerReady;private Player? stopped;
    private int auditPhase;private float auditAt;
    private static readonly Color Background=new(.045f,.075f,.085f,1),Cell=new(.11f,.16f,.18f,1),Chosen=new(.18f,.37f,.27f,1),Light=new(.9f,.95f,.87f,1);
    public void Update()
    {
        Instance=this;
        if(!root){var sample=Object.FindObjectOfType<TextMeshProUGUI>();if(sample && sample.font){font=sample.font;Build();}}
        if(root && Environment.GetCommandLineArgs().Contains("-coop-menu-audit"))
        {
            if(auditPhase==0){Screen.SetResolution(1280,800,false);Show();auditAt=Time.realtimeSinceStartup+2;auditPhase=1;}
            else if(auditPhase==1 && Time.realtimeSinceStartup>=auditAt){ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"coop-connection-menu.png"));auditPhase=2;auditAt=Time.realtimeSinceStartup+1;}
            else if(auditPhase==2 && Time.realtimeSinceStartup>=auditAt)Application.Quit();
        }
        if(Plugin.Test && root)Gates.Get<MenuChecks>()?.Menu(this);
        if(Keyboard.current?.f7Key.wasPressedThisFrame==true)Toggle();
        if(shortcut)shortcut!.rectTransform.anchoredPosition=Plugin.Driver?.Ready==true?new Vector2(16,-770):new Vector2(16,-16);
        if(Plugin.Guest && Keyboard.current?.f6Key.wasPressedThisFrame==true)Toggle();
        if(Plugin.Guest && Plugin.Driver?.Ready==true && CustomKeyManager.cm && CustomKeyManager.cm.GetCustomKeyDown(CustomKeyCode.Inventory,true))Toggle();
        bool guestSession=Plugin.Guest && Plugin.Driver?.Ready==true;
        if(leave)leave!.SetActive(guestSession);
        // The guest's native pause menu is frozen with the rest of its game, so Esc opens this menu (and its Quit to title) instead.
        if(!Open && guestSession && Keyboard.current?.escapeKey.wasPressedThisFrame==true){Show();return;}
        if(!Open)return;
        Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
        if(status)status!.text=Plugin.Status;
        // While the link is down the connection form is what the player needs, not the loadout.
        bool connected=Plugin.Driver?.Ready==true && Plugin.Driver.LinkUp;
        if(setup)setup!.SetActive(!connected);if(loadout)loadout!.SetActive(connected);
        if(connected && !populated){foreach(int id in SwampKeeperMods.CoopIntegration.PreparedItems)items.Add(id);populated=true;Refresh();}
        if(connected)
        {
            bool localReady=Plugin.Driver!.LocalReady;
            if(localReady!=wasReady || Plugin.Driver.PeerReady!=wasPeerReady){wasReady=localReady;wasPeerReady=Plugin.Driver.PeerReady;Refresh();}
            if(ready)ready!.interactable=Plugin.Driver.IsPreparation;
            Hover();
        }
    }
    [HideFromIl2Cpp] internal void ResetForRun(){Close();populated=false;hover=-1;tab=0;items.Clear();}
    [HideFromIl2Cpp] internal void Toggle(){if(Open)Close();else Show();}
    [HideFromIl2Cpp] internal void Show()
    {
        if(!panel || Open)return;shade!.SetActive(true);panel!.SetActive(true);Open=true;
        if(Plugin.Guest && Plugin.Driver?.CanReconnect==true){address!.text=Plugin.Address;code!.text=Plugin.Code;}
        CoopHud.Instance?.Visible(false);
        if(Plugin.Driver?.Ready==true && !Plugin.Driver.IsPreparation)
        {
            items.Clear();items.UnionWith(Plugin.Driver.LocalItems());
            var equipped=Plugin.Driver.LocalSkills();for(int i=0;i<2 && i<equipped.Length;i++)skills[i]=equipped[i];Refresh();
        }
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        if(Player.player && !Plugin.Guest){stopped=Player.player;stopped.StopControl(gameObject);stopped.StopAiming(gameObject);}
        Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
    }
    [HideFromIl2Cpp] internal void Close()
    {
        Open=false;if(shade)shade!.SetActive(false);
        CoopHud.Instance?.Visible(true);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        if(stopped){stopped!.StartControl(gameObject);stopped.StartAiming(gameObject);}stopped=null;
    }
    [HideFromIl2Cpp] private RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name);var rt=go.AddComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new(0,1);rt.anchoredPosition=new(x,-y);rt.sizeDelta=new(w,h);return rt;
    }
    [HideFromIl2Cpp] private Image Bg(RectTransform rt,Color color){var image=rt.gameObject.AddComponent<Image>();image.color=color;return image;}
    [HideFromIl2Cpp] private TextMeshProUGUI Text(Transform parent,string name,string value,float x,float y,float w,float h,float size=18)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=value;t.color=Light;t.raycastTarget=false;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    [HideFromIl2Cpp] private Button Button(Transform parent,string label,float x,float y,float w,float h,Action action)
    {
        var rt=Rect(label,parent,x,y,w,h);var image=Bg(rt,Cell);var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
        Text(rt,"Label",label,12,5,w-24,h-10);b.onClick.AddListener((UnityAction)action);return b;
    }
    [HideFromIl2Cpp] private TMP_InputField Field(Transform parent,string name,string value,float x,float y,float w,string hint="")
    {
        var rt=Rect(name,parent,x,y,w,46);Bg(rt,Cell);var field=rt.gameObject.AddComponent<TMP_InputField>();
        field.textViewport=rt;field.textComponent=Text(rt,"Value","",12,8,w-24,34,22);field.characterLimit=128;field.text=value;
        if(hint.Length>0){var placeholder=Text(rt,"Placeholder",hint,12,10,w-24,30,17);placeholder.color=new(.54f,.65f,.64f,1);field.placeholder=placeholder;}
        return field;
    }
    [HideFromIl2Cpp] private void Build()
    {
        root=new GameObject("CoopSessionMenu");Object.DontDestroyOnLoad(root);
        var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32765;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1280,800);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();
        shortcut=Text(root.transform,"Shortcut","F7  ONLINE CO-OP / INVENTORY",16,16,400,26,17);
        var dim=Rect("Backdrop",root.transform,0,0,0,0);dim.anchorMin=Vector2.zero;dim.anchorMax=Vector2.one;dim.sizeDelta=Vector2.zero;Bg(dim,new Color(0,0,0,.65f));shade=dim.gameObject;
        var rt=Rect("Window",dim,0,0,1000,700);rt.anchorMin=rt.anchorMax=rt.pivot=new(.5f,.5f);rt.anchoredPosition=Vector2.zero;Bg(rt,Background);panel=rt.gameObject;
        Text(rt,"Heading","SWAMP KEEPER  /  ONLINE CO-OP",24,20,660,40,28);
        leave=Button(rt,"Quit to title",696,18,156,40,Plugin.LeaveToTitle).gameObject;leave.SetActive(false);
        Button(rt,"Close",864,18,112,40,Close);
        status=Text(rt,"Connection","F7 opens co-op. Connect from the title screen.",24,66,952,44,17);
        setup=Rect("ConnectionSetup",rt,24,122,952,550).gameObject;
        Text(setup.transform,"CodeLabel","SESSION CODE  ·  use the same code on both PCs",0,0,920,28,20);
        code=Field(setup.transform,"SessionCode",CoopListener.NewCode(),0,40,550);
        Text(setup.transform,"HostLabel","HOST A RUN",0,122,430,38,25);
        Text(setup.transform,"HostHelp","Share your IP / VPN address\nand the session code above.",0,170,430,68,19).enableWordWrapping=true;
        Button(setup.transform,"Host",0,270,180,46,()=>Start(false));
        Text(setup.transform,"GuestLabel","JOIN A RUN",480,122,440,38,25);
        Text(setup.transform,"AddressLabel","Host IP / VPN address",480,170,440,28);
        address=Field(setup.transform,"HostAddress","",480,208,440,"Enter the host's address");
        Button(setup.transform,"Join",480,270,180,46,()=>Start(true));
        Text(setup.transform,"CodeHelp","Joining? Paste the host's code into the session code field above.",0,356,920,28,17);
        Text(setup.transform,"NetworkHelp","Two PCs · same game/mod version · TCP port "+Plugin.Port+"\nFor internet play, the host needs a reachable address or a VPN connection.",0,418,920,80,17).enableWordWrapping=true;
        loadout=Rect("Preparation",rt,24,118,952,558).gameObject;
        Button(loadout.transform,"Items",0,0,150,40,()=>SetTab(0));
        Button(loadout.transform,"Skill 1",162,0,150,40,()=>SetTab(1));
        Button(loadout.transform,"Skill 2",324,0,150,40,()=>SetTab(2));
        selection=Text(loadout.transform,"Selected","",0,52,600,26,17);
        for(int i=0;i<40;i++)
        {
            int id=i;var cell=Rect("Slot"+i,loadout.transform,(i%8)*72,90+(i/8)*76,64,68);cells.Add(cell);
            var background=Bg(cell,Cell);cellBgs.Add(background);var b=cell.gameObject.AddComponent<Button>();b.targetGraphic=background;
            var icon=Rect("Icon",cell,8,5,48,48).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;cellIcons.Add(icon);
            marks.Add(Text(cell,"Selected","",8,51,52,15,12));b.onClick.AddListener((UnityAction)(Action)(()=>Select(id)));
        }
        var card=Rect("Detail",loadout.transform,600,0,352,478);Bg(card,Cell);
        detailIcon=Rect("Icon",card,18,18,72,72).gameObject.AddComponent<Image>();detailIcon.preserveAspect=true;
        title=Text(card,"Name","Hover an icon",108,18,226,76,22);title.enableWordWrapping=true;
        var viewport=Rect("DescriptionViewport",card,18,110,316,350);viewport.gameObject.AddComponent<RectMask2D>();
        detailContent=Rect("Description",viewport,0,0,316,350);
        detail=Text(detailContent,"Text","Click an item to select or deselect it.\nSkills are selected independently for each slot.",0,0,316,350,18);detail.enableWordWrapping=true;detail.overflowMode=TextOverflowModes.Overflow;
        var scroll=card.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=detailContent;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
        Button(loadout.transform,"Select all",0,484,150,40,()=>{if(Plugin.Driver?.IsPreparation!=true)return;items.UnionWith(Enumerable.Range(0,40));Refresh();});
        Button(loadout.transform,"Clear",162,484,150,40,()=>{if(Plugin.Driver?.IsPreparation!=true)return;items.Clear();Refresh();});
        ready=Button(loadout.transform,"Ready / Unready",600,492,352,44,()=>
        {
            try{Plugin.Driver!.SetReady(items.OrderBy(i=>i).ToArray(),skills,!Plugin.Driver.LocalReady);Refresh();}catch(Exception ex){Plugin.Status=ex.Message;}
        });
        Text(loadout.transform,"Help","Hover for details · click to toggle · scroll long descriptions\nWhen both are ready, enter the run together. F6 / F7 to reopen.",0,534,952,42,15);
        loadout.SetActive(false);shade.SetActive(false);
    }
    [HideFromIl2Cpp] private void Start(bool guest)
    {
        try{Plugin.BeginSession(guest,address!.text.Trim(),code!.text.Trim());Close();}
        catch(Exception ex){Plugin.Status=ex.Message;}
    }
    internal bool Built=>root;
    internal bool LoadoutVisible=>loadout && loadout!.activeInHierarchy;
    [HideFromIl2Cpp] internal Button Find(string name)=>panel!.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    [HideFromIl2Cpp] internal void Fill(string host,string session){address!.text=host;code!.text=session;}
    // Integration tests: fill the connection fields as a player would and press the real Host/Join button.
    [HideFromIl2Cpp] internal void TestConnect(bool guest,string host,string session)
    {
        if(!root)throw new InvalidOperationException("Co-op menu not built");
        Show();address!.text=host;code!.text=session;
        var button=setup!.transform.Find(guest?"Join":"Host").GetComponent<Button>();
        if(!button.interactable || !button.gameObject.activeInHierarchy)throw new InvalidOperationException("Connection button unavailable");
        button.onClick.Invoke();
    }
    [HideFromIl2Cpp] private void SetTab(int value){tab=value;hover=-1;Refresh();}
    [HideFromIl2Cpp] private void Select(int id)
    {
        if(Plugin.Driver?.IsPreparation!=true)return;
        if(tab==0){if(!items.Remove(id))items.Add(id);}else if(id<SkillManager.sm.GetSkillCount())skills[tab-1]=id;
        Plugin.Driver.Unready();Refresh();
    }
    [HideFromIl2Cpp] private void Refresh()
    {
        if(!populated || !ItemManager.im || !SkillManager.sm)return;
        int count=tab==0?40:SkillManager.sm.GetSkillCount();
        for(int i=0;i<cells.Count;i++)
        {
            cells[i].gameObject.SetActive(i<count);if(i>=count)continue;
            bool selected=tab==0?items.Contains(i):skills[tab-1]==i;
            cellBgs[i].color=selected?Chosen:Cell;marks[i].text=selected?"ON":"";
            cellIcons[i].sprite=tab==0?ItemManager.im.GetItem(i).icon:SkillManager.sm.GetSkill(i).icon;
        }
        selection!.text=Plugin.Driver!.IsPreparation?(tab==0?items.Count+" / 40 items selected":"Select ability for skill slot "+tab)+"    ·    You "+(Plugin.Driver.LocalReady?"ready":"choosing")+"  |  Partner "+(Plugin.Driver.PeerReady?"ready":"choosing"):items.Count+" owned items  ·  inspection only during a run";
    }
    [HideFromIl2Cpp] private void Hover()
    {
        if(Mouse.current==null)return;var point=Mouse.current.position.ReadValue();int id=-1;
        for(int i=0;i<cells.Count;i++)if(cells[i].gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(cells[i],point,null)){id=i;break;}
        if(id<0 || id==hover)return;Describe(id);
    }
    [HideFromIl2Cpp] private void Describe(int id)
    {
        hover=id;string body;
        if(tab==0)
        {
            var item=ItemManager.im.GetItem(id);title!.text=item.GetName();detailIcon!.sprite=item.icon;
            body=item.GetExplain()+"\n\n"+string.Join("\n",item.GetValueExplain().Where(s=>!string.IsNullOrWhiteSpace(s)));
        }
        else
        {
            var skill=SkillManager.sm.GetSkill(id);title!.text=skill.GetName();detailIcon!.sprite=skill.icon;body=skill.GetExplain()+"\n\n"+skill.GetValueExplain();
        }
        detail!.text=body.Trim();detail.rectTransform.sizeDelta=new(316,Math.Max(350,detail.preferredHeight+12));
        detailContent!.sizeDelta=detail.rectTransform.sizeDelta;detailContent.anchoredPosition=Vector2.zero;
    }
    [HideFromIl2Cpp] internal void AuditPreparation(bool guest)
    {
        if(Plugin.Driver?.IsPreparation!=true)throw new InvalidOperationException("Preparation audit started outside preparation");
        populated=true;items.Clear();SetTab(0);Show();
        cells[0].GetComponent<Button>().onClick.Invoke();
        if(!items.Contains(0))throw new InvalidOperationException("Grid click did not select item");
        cells[0].GetComponent<Button>().onClick.Invoke();
        if(items.Contains(0))throw new InvalidOperationException("Grid second click did not deselect item");
        for(int i=0;i<40;i++)
        {
            if(cellIcons[i].sprite.Pointer!=ItemManager.im.GetItem(i).icon.Pointer)throw new InvalidOperationException("Wrong native item icon "+i);
            Describe(i);if(string.IsNullOrWhiteSpace(detail!.text))throw new InvalidOperationException("Missing native hover description "+i);
        }
        items.UnionWith(guest?new[]{10,20}:new[]{0,39});
        SetTab(1);cells[guest?1:12].GetComponent<Button>().onClick.Invoke();
        SetTab(2);cells[guest?17:5].GetComponent<Button>().onClick.Invoke();
        SetTab(0);Describe(0);ready!.onClick.Invoke();
        Screen.SetResolution(1280,800,false);
        Plugin.Record("Preparation UI audit passed: 40 native icons/descriptions; actual grid select/deselect; skill choices; ready button");
    }
}

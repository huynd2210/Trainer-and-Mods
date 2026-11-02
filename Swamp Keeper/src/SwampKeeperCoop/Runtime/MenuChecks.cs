using SwampKeeperCoop.Protocol;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace SwampKeeperCoop;

// No -coop-role: both games start at the title screen and the session is created only through the F7 menu,
// with the same Host/Join, item grid and Ready buttons a player clicks. Then both enter the run and the guest moves.
internal sealed class MenuChecks:Gate
{
    private readonly bool guest=Environment.GetCommandLineArgs().Contains("guest");
    private readonly string code=Arg("-coop-code");
    private int title,step,joins;private float at,moveUntil=-1;private Vector3 guestAt,hostAt;
    internal override bool StartsRun=>false;
    internal override bool RequiresBasics=>false;
    internal override bool GuestMoves=>false;
    internal override float HostLimit=>150;
    internal override float GuestFinish=>float.MaxValue;
    internal override float GuestLimit=>200;
    internal override Vector2 GuestMove(float t)=>t<moveUntil?Vector2.right:Vector2.zero;
    private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0 && i+1<a.Length?a[i+1]:"";}
    private static float Now=>Time.realtimeSinceStartup;
    // F7 through the Input System's event queue, as the keyboard would deliver it.
    // F7: the gate cannot synthesize the key itself. Through Il2CppInterop the generic InputSystem.QueueStateEvent<KeyboardState>
    // receives a null device, and OS input cannot reach the hidden test desktop. So this calls the Toggle() that the
    // F7 handler (Keyboard.current.f7Key.wasPressedThisFrame in SessionMenu.Update) calls.
    private static void Tap(Key key)
    {
        if(key!=Key.F7)throw new ArgumentException("Only F7 is used");
        SessionMenu.Instance!.Toggle();
    }
    // A left click lands on whatever the EventSystem raycast finds topmost at the button's centre; anything covering it fails.
    internal static void Click(Button button)
    {
        var es=EventSystem.current;if(!es)throw new InvalidOperationException("No EventSystem: the co-op menu cannot be clicked");
        var rt=button.GetComponent<RectTransform>();var pos=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center));
        var data=new PointerEventData(es){position=pos,button=PointerEventData.InputButton.Left};
        var hits=new Il2CppSystem.Collections.Generic.List<RaycastResult>();es.RaycastAll(data,hits);
        if(hits.Count==0)throw new InvalidOperationException("Nothing under the pointer at "+button.name);
        var top=hits[0].gameObject;
        if(!top.transform.IsChildOf(button.transform))throw new InvalidOperationException(top.name+" covers the "+button.name+" button");
        if(!button.interactable)throw new InvalidOperationException(button.name+" is not interactable");
        data.pointerPress=button.gameObject;data.pointerCurrentRaycast=hits[0];button.OnPointerClick(data);
    }
    // Title screen, before any session exists. Called every frame from SessionMenu.Update.
    internal void Menu(SessionMenu menu)
    {
        if(failed)return;
        try{Title(menu);}
        catch(Exception ex){failed=true;Plugin.Record("SESSION FAILED: "+ex);Application.Quit();}
    }
    private bool failed;
    private void Title(SessionMenu menu)
    {
        if(title==0)
        {
            if(!Object.FindObjectOfType<UI_Title>() || !menu.Built)return;
            at=Now+(guest?10:3);title=1;
        }
        else if(title==1 && Now>=at)
        {
            if(SessionMenu.Open)throw new InvalidOperationException("Menu open before F7");
            Tap(Key.F7);title=2;at=Now+1.5f;
        }
        else if(title==2 && Now>=at)
        {
            if(!SessionMenu.Open)throw new InvalidOperationException("F7 did not open the co-op menu");
            Plugin.Record("F7 handler opened the co-op menu on the title screen; EventSystem="+EventSystem.current?.name);
            title=3;at=Now;
        }
        else if(title==3 && Now>=at)
        {
            if(Plugin.Driver!=null){title=4;return;}
            if(joins++>5)throw new InvalidOperationException("Could not start a session from the menu: "+Plugin.Status);
            if(joins>1)Plugin.Record("Menu status before retry: "+Plugin.Status);
            if(!SessionMenu.Open)menu.Show();
            menu.Fill(guest?"127.0.0.1":"",code);
            Click(menu.Find(guest?"Join":"Host"));at=Now+4;
            Plugin.Record("Clicked "+(guest?"Join":"Host")+" in the F7 menu; status: "+Plugin.Status);
        }
    }
    private void Prepare(SessionMenu menu,int item)
    {
        // Reopen with F7, choose one item in the native grid and press Ready, then close with F7.
        // Clear first: the grid starts from the loadout an earlier gate run left in this sandbox's store.
        Click(menu.Find("Clear"));Click(menu.Find("Slot"+item));Click(menu.Find("Ready / Unready"));
        if(!Plugin.Driver!.LocalReady)throw new InvalidOperationException("Ready button did not ready the player");
        Plugin.Record("Clicked item "+item+" and Ready in the F7 preparation grid");
    }
    internal override void Host(float elapsed)
    {
        if(Done || Now<at)return;
        var menu=SessionMenu.Instance!;var h=Plugin.Host!;var r=Plugin.Remote!;
        if(step==0){if(!Plugin.Driver!.IsPreparation)return;Tap(Key.F7);step=1;at=Now+1;}
        else if(step==1)
        {
            if(!SessionMenu.Open || !menu.LoadoutVisible)throw new InvalidOperationException("F7 did not open the preparation grid");
            Prepare(menu,3);Tap(Key.F7);step=2;at=Now+1;
        }
        else if(step==2)
        {
            if(SessionMenu.Open)throw new InvalidOperationException("F7 did not close the menu");
            if(!Plugin.Driver!.GuestReady)return;
            var ladder=Object.FindObjectsOfType<Ladder>().First();TestActions.Place(h,ladder.transform.position);
            var scope=Owners.Enter(h);try{ladder.Interact();}finally{Owners.Exit(scope);}
            step=3;at=Now+1;
        }
        else if(step==3)
        {
            var settings=UIManager.um.battleSettings;
            if(settings && settings.gameObject.activeInHierarchy){settings.advanceButton.onClick.Invoke();step=4;at=Now+1;return;}
            if(GameManager.gm.current || RunTransitions.Moving)step=4;
        }
        else if(step==4)
        {
            if(RunTransitions.Moving || !GameManager.gm.current)return;
            guestAt=r.transform.position;hostAt=h.transform.position;step=5;at=Now+7;
        }
        else if(step==5)
        {
            int[] Ids(Player p)=>Enumerable.Range(0,p.items.Count).Select(i=>p.items[i].index).ToArray();
            float moved=Vector3.Distance(guestAt,r.transform.position),still=Vector3.Distance(hostAt,h.transform.position);
            Plugin.Record("In run after menu session: guest moved "+moved+" host moved "+still+" host items="+string.Join(",",Ids(h))+" guest items="+string.Join(",",Ids(r)));
            if(moved<1 || still>.5f || !Ids(h).SequenceEqual(new[]{3}) || !Ids(r).SequenceEqual(new[]{7}))throw new InvalidOperationException("Menu-started session did not play correctly");
            Plugin.Record("MENU CHECKS PASSED: F7 handler opened the menu; Host/Join, item grid and Ready clicked through the EventSystem; real connection flow; run entered; guest moved independently with its own loadout");Done=true;
        }
    }
    internal override void Guest(float t,WorldState? world)
    {
        if(world==null || Now<at)return;
        var menu=SessionMenu.Instance!;
        if(step==0){if(!Plugin.Driver!.IsPreparation)return;Tap(Key.F7);step=1;at=Now+1;}
        else if(step==1)
        {
            if(!SessionMenu.Open || !menu.LoadoutVisible)throw new InvalidOperationException("F7 did not open the guest preparation grid");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"coop-menu-guest.png"));step=10;at=Now+1;
        }
        else if(step==10){Prepare(menu,7);Tap(Key.F7);step=2;at=Now+1;}
        else if(step==2)
        {
            if(SessionMenu.Open)throw new InvalidOperationException("F7 did not close the guest menu");
            if(world.Stage>=0){moveUntil=t+2.5f;step=3;Plugin.Record("Guest sees the run started; moving");}
        }
    }
    // The guest leaves the way a player must (its native pause menu is frozen): the menu's Quit to title. Esc itself, like F7,
    // cannot be synthesized here; tests skip the relaunch, so the game simply exits.
    internal override void GuestFinished(WorldState? last)
    {
        var menu=SessionMenu.Instance!;if(!SessionMenu.Open)menu.Show();
        Click(menu.Find("Quit to title"));
        Plugin.Record("Clicked Quit to title in the guest's co-op menu");
    }
}

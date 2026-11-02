using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace SwampKeeperMods;

// Only runs when the executable is explicitly launched with -swamp-mod-test.
// All kill/loadout writes are redirected to ModsSource/test-results.
public sealed class RuntimeVerification : MonoBehaviour
{
    internal static long LastResetLifetime;
    internal static int PlayerResets;
    public RuntimeVerification(IntPtr p) : base(p) { }
    private int step;
    private float next;
    private long initialKills;
    private string folder = "";
    private readonly List<string> results = new();
    private bool graphics;
    private Enemy? testEnemy;
    private int initialCapacity;
    private int resetsBeforeRestart;

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS " + message);
        Plugin.Instance.Log.LogInfo("TEST PASS " + message);
    }

    public void Update()
    {
        if (Time.realtimeSinceStartup < next) return;
        try
        {
            if (step == 0)
            {
                folder = Path.Combine(BepInEx.Paths.GameRootPath, "ModsSource", "test-results");
                graphics = !Environment.GetCommandLineArgs().Contains("-nographics");
                Application.runInBackground = true;
                initialKills = Plugin.Store.Data.LifetimeKills;
                next = Time.realtimeSinceStartup + 5;
                step++;
            }
            else if (step == 1)
            {
                var title = Object.FindObjectOfType<UI_Title>();
                if (!title) { next = Time.realtimeSinceStartup + 1; return; }
                title.StartGame();
                next = Time.realtimeSinceStartup + 5;
                step++;
            }
            else if (step == 2)
            {
                if (!Player.player || !ItemManager.im || !GameManager.gm) { next = Time.realtimeSinceStartup + 1; return; }
                Check(ItemManager.im.items.Length == 40, "Runtime catalog contains all 40 items");
                Check(Plugin.InPreparation(), "Preparation detected before run start");
                if(Environment.GetCommandLineArgs().Contains("-swamp-ui-audit"))
                {
                    var catalog=new List<object>();
                    for(int id=0;id<40;id++)
                    {
                        var item=ItemManager.im.GetItem(id);
                        string[] Values(){try{return item.GetValueExplain().ToArray();}catch{return Array.Empty<string>();}}
                        string[] Changes(){try{return item.GetChangedValue().ToArray();}catch{return Array.Empty<string>();}}
                        catalog.Add(new{id,name=item.GetName(),type=item.GetTypeText(),explain=item.GetExplain(),values=Values(),changes=Changes(),icon=(bool)item.icon});
                    }
                    File.WriteAllText(Path.Combine(folder,"item-catalog.json"),System.Text.Json.JsonSerializer.Serialize(catalog,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
                    Application.Quit(); return;
                }
                int before = Player.player.killCount;
                initialCapacity = Player.player.arrow.arrowCapacity;
                Player.player.AddKillCount(); Player.player.AddKillCount(); Player.player.AddKillCount();
                Check(Player.player.killCount == before + 3, "Original kill counter remains intact");
                Check(Plugin.Store.Data.LifetimeKills == initialKills + 3, "Native kill credit triggers exactly one persistent count");
                Check(new Storage(Plugin.Store.FilePath).Data.LifetimeKills == initialKills + 3, "Kill total survives fresh storage reload");
                Plugin.UI.Show();
                const string panel = "SwampKeeperModsCanvas/ModalBlocker/PreparationPanel/";
                Canvas.ForceUpdateCanvases();
                for(int id=0;id<40;id++)
                {
                    var slot=GameObject.Find(panel+"Viewport/ItemList/Item"+id);
                    var icon=slot.transform.Find("Icon").GetComponent<Image>();
                    if(!icon.enabled || !icon.sprite || icon.sprite.Pointer!=ItemManager.im.GetItem(id).icon.Pointer)
                        throw new InvalidOperationException("Missing/wrong game icon on item "+id);
                    Inspect(id);
                    var body=GameObject.Find(panel+"ItemDetails/EffectsViewport/Description").GetComponent<TextMeshProUGUI>().text;
                    var name=GameObject.Find(panel+"ItemDetails/Name").GetComponent<TextMeshProUGUI>().text;
                    var item=ItemManager.im.GetItem(id);
                    if(name!=item.GetName() || string.IsNullOrWhiteSpace(body))throw new InvalidOperationException("Hover failed for item "+id);
                    if(!string.IsNullOrWhiteSpace(item.GetExplain()) && !body.Contains(item.GetExplain().Trim()))throw new InvalidOperationException("Missing general description "+id);
                    foreach(var effect in item.GetValueExplain())
                        if(!string.IsNullOrWhiteSpace(effect) && !body.Contains(effect.Trim()))throw new InvalidOperationException("Missing stat description "+id);
                }
                Check(true,"All 40 grid slots bind their actual game icon and hover to complete native descriptions");
                GameObject.Find(panel + "Clear").GetComponent<Button>().onClick.Invoke();
                GameObject.Find(panel + "Viewport/ItemList/Item0").GetComponent<Button>().onClick.Invoke();
                Check(Plugin.Store.Data.PreparedItems.SequenceEqual(new[] { 0 }), "Individual native item button updates selection");
                GameObject.Find(panel + "Viewport/ItemList/Item0").GetComponent<Button>().onClick.Invoke();
                Check(Plugin.Store.Data.PreparedItems.Length==0,"Clicking a selected icon deselects it");
                GameObject.Find(panel + "SelectAll").GetComponent<Button>().onClick.Invoke();
                Check(Plugin.Store.Data.PreparedItems.Length == 40, "Native Select all button selects all items");
                GameObject.Find(panel + "Clear").GetComponent<Button>().onClick.Invoke();
                Check(Plugin.Store.Data.PreparedItems.Length == 0, "Native Clear button empties selection");
                foreach (int id in new[] { 0, 2, 10, 26 }) GameObject.Find(panel + "Viewport/ItemList/Item" + id).GetComponent<Button>().onClick.Invoke();
                var search = GameObject.Find(panel + "Search").GetComponent<TMP_InputField>();
                search.text = "39";
                next = Time.realtimeSinceStartup + .3f;
                step=20;
            }
            else if(step==20)
            {
                const string panel = "SwampKeeperModsCanvas/ModalBlocker/PreparationPanel/";
                var matching = GameObject.Find(panel + "Viewport/ItemList/Item39");
                var excluded = GameObject.Find(panel + "Viewport/ItemList/Item0");
                Check(matching && matching.activeInHierarchy && (!excluded || !excluded.activeInHierarchy), "Native search field filters the actual item controls");
                GameObject.Find(panel + "Search").GetComponent<TMP_InputField>().text = "";
                next = Time.realtimeSinceStartup + .3f; step=21;
            }
            else if(step==21)
            {
                const string panel = "SwampKeeperModsCanvas/ModalBlocker/PreparationPanel/";
                Check(GameObject.Find(panel + "Viewport/ItemList/Item0"), "Clearing native search restores item list");
                GameObject.Find(panel + "Done").GetComponent<Button>().onClick.Invoke();
                Check(!Plugin.UI.Open, "Native Done button closes the panel");
                Check(new Storage(Plugin.Store.FilePath).Data.PreparedItems.SequenceEqual(new[] { 0, 2, 10, 26 }), "Loadout selection persists");
                Plugin.UI.Show();
                if (graphics) Screen.SetResolution(1280, 800, false);
                next = Time.realtimeSinceStartup + 3;
                step=3;
            }
            else if (step == 3)
            {
                Inspect(0);
                if (graphics) ScreenCapture.CaptureScreenshot(Path.Combine(folder, "preparation.png"));
                next = Time.realtimeSinceStartup + 2;
                step=30;
            }
            else if(step==30)
            {
                Inspect(39);
                if(graphics)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"item-leather-glove.png"));
                next=Time.realtimeSinceStartup+1;step=31;
            }
            else if(step==31)
            {
                Inspect(20);
                if(graphics)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"item-long-description.png"));
                next=Time.realtimeSinceStartup+1;step=32;
            }
            else if(step==32)
            {
                var scroll=GameObject.Find("SwampKeeperModsCanvas/ModalBlocker/PreparationPanel/ItemDetails/EffectsViewport").GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                var ev=new PointerEventData(EventSystem.current);ev.scrollDelta=new Vector2(0,-20);
                scroll.OnScroll(ev);
                Check(scroll.content.anchoredPosition.y>0,"Native scroll-wheel handler reveals the rest of long item descriptions");
                if(graphics)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"item-description-scrolled.png"));
                next=Time.realtimeSinceStartup+1;step=34;
            }
            else if(step==34)
            {
                if(graphics)Screen.SetResolution(1280,720,false);
                next=Time.realtimeSinceStartup+2;step=33;
            }
            else if(step==33)
            {
                var panel=GameObject.Find("SwampKeeperModsCanvas/ModalBlocker/PreparationPanel").GetComponent<RectTransform>();
                var corners=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
                panel.GetWorldCorners(corners);
                Check(corners.All(p=>p.x>=0 && p.x<=Screen.width && p.y>=0 && p.y<=Screen.height),"Entire inventory panel fits within the 720p screen");
                Inspect(26);
                if(graphics)ScreenCapture.CaptureScreenshot(Path.Combine(folder,"item-grid-720p.png"));
                next=Time.realtimeSinceStartup+1;step=4;
            }
            else if (step == 4)
            {
                Plugin.UI.SelectItems(Enumerable.Range(0, 40));
                Plugin.UI.Close();
                GameManager.gm.MoveStage(0, true);
                Check(Plugin.Granted, "Run transition equips the loadout");
                var owned = Enumerable.Range(0, Player.player.items.Count).Select(i => Player.player.items[i].index).ToArray();
                Check(Enumerable.Range(0, 40).All(owned.Contains), "All 40 items are present in real player inventory");
                Check(Player.player.arrow.arrowCapacity > initialCapacity, "Equipped item effects increase the real arrow capacity");
                int count = Player.player.items.Count;
                Check(Plugin.GrantLoadout() && Player.player.items.Count == count, "Repeated grant is idempotent");
                Check(!Plugin.InPreparation(), "Preparation is locked after granting");
                next = Time.realtimeSinceStartup + 4;
                step++;
            }
            else if (step == 5)
            {
                if (graphics) ScreenCapture.CaptureScreenshot(Path.Combine(folder, "kill-hud.png"));
                testEnemy = EnemyManager.em.SpawnEnemy(EnemyCode.Swordman);
                Check(testEnemy, "A real pooled enemy can be spawned for death-hook verification");
                testEnemy!.ChangeState(EnemyState.Die);
                next = Time.realtimeSinceStartup + 2;
                step++;
            }
            else if (step == 6)
            {
                Check(Plugin.Store.Data.LifetimeKills == initialKills + 4, "Enemy death coroutine reaches the native kill-credit hook exactly once");
                Plugin.NewPlayer();
                Check(Plugin.RunKills == 0 && Plugin.Store.Data.LifetimeKills == initialKills + 4, "New run resets run count and retains lifetime kills");
                File.WriteAllLines(Path.Combine(folder, "runtime-report.txt"), results);
                next = Time.realtimeSinceStartup + 3;
                step++;
            }
            else if (step == 7)
            {
                Plugin.UI.Close();
                DataManager.dm.quickStart = true;
                DataManager.dm.isWell = false;
                resetsBeforeRestart=PlayerResets;
                step=8; next=Time.realtimeSinceStartup+8;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            else if(step==8)
            {
                Check(Player.player && PlayerResets>resetsBeforeRestart && Plugin.RunKills==Player.player.killCount && Plugin.Store.Data.LifetimeKills==LastResetLifetime+Plugin.RunKills && new Storage(Plugin.Store.FilePath).Data.LifetimeKills==Plugin.Store.Data.LifetimeKills, "Actual quick restart retains every prior kill and matches the new native run count");
                var owned=Enumerable.Range(0,Player.player.items.Count).Select(i=>Player.player.items[i].index).ToArray();
                Check(Plugin.Granted && Enumerable.Range(0,40).All(owned.Contains), "Quick restart equips the remembered loadout exactly once");
                Plugin.UI.Close(); DataManager.dm.quickStart=true; DataManager.dm.isWell=true;
                step=9; next=Time.realtimeSinceStartup+8;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            else if(step==9)
            {
                var owned=Enumerable.Range(0,Player.player.items.Count).Select(i=>Player.player.items[i].index).ToArray();
                Check(Plugin.Granted && Enumerable.Range(0,40).All(owned.Contains), "Well quick restart equips the remembered loadout");
                File.WriteAllLines(Path.Combine(folder,"runtime-report.txt"),results);
                Application.Quit();
            }
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex);
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "runtime-report.txt"), results);
            Plugin.Instance.Log.LogError(ex);
            Application.Quit();
        }
    }

    private static void Inspect(int id)
    {
        var slot=GameObject.Find("SwampKeeperModsCanvas/ModalBlocker/PreparationPanel/Viewport/ItemList/Item"+id).GetComponent<RectTransform>();
        var point=RectTransformUtility.WorldToScreenPoint(null,slot.TransformPoint(slot.rect.center));
        Plugin.UI.InspectAt(point);
    }
}

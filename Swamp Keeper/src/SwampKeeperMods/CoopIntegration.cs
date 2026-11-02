namespace SwampKeeperMods;

// Optional bridge; ordinary single-player behavior stays enabled when Active is false.
public static class CoopIntegration
{
    public static bool Active {get;set;}
    public static bool RenderingGuest {get;set;}
    public static Action? OpenMenu {get;set;}
    public static Func<bool>? IsMenuOpen {get;set;}
    public static bool MenuOpen=>IsMenuOpen?.Invoke()??false;
    public static bool IsLocalActor(Player player)=>!Active || (!RenderingGuest && player.name!="Coop_Player_2");
    public static int[] PreparedItems=>Plugin.Store.Data.PreparedItems.ToArray();
    public static long LifetimeKills=>Plugin.Store.Data.LifetimeKills;
    public static void SetPreparedItems(int[] items)=>Plugin.UI.SelectItems(items);
    public static void ReceiveKills(string runId,int cumulative)
    {
        if(!Active || !RenderingGuest || !Plugin.KillsEnabled.Value)return;
        long added=Plugin.Store.CreditOnlineKills(runId,cumulative);
        Plugin.SessionKills+=added;Plugin.RunKills=cumulative;
        if(Plugin.Store.Warning.Length>0)Plugin.Instance.Log.LogWarning(Plugin.Store.Warning);
    }
}

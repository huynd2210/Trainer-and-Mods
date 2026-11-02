using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace SwampKeeperCoop;

// Unity pools and delayed callbacks outlive the Player.Update that created them.
// Resolve the actor from the component, never from whichever player updated last.
internal static class Owners
{
    internal static void Reset()=>components.Clear();
    private static readonly HashSet<string> unique=ReadUnique();
    private static HashSet<string> ReadUnique()
    {
        using var stream=typeof(Owners).Assembly.GetManifestResourceStream("SwampKeeperCoop.UniqueNativeMethods.txt")!;
        using var reader=new StreamReader(stream);return reader.ReadToEnd().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
    internal static bool Patchable(MethodInfo m)=>unique.Contains(m.DeclaringType!.Name+"."+m.Name);
    private static readonly Dictionary<IntPtr,Player> components=new();
    internal static Player? Find(Component component)
    {
        if(components.TryGetValue(component.Pointer,out var owner) && owner)return owner;
        var parent=component.GetComponentInParent<Player>();
        if(parent){Register(component,parent);return parent;}
        return null;
    }
    internal static void Register(Component component,Player owner)
    {
        components[component.Pointer]=owner;
        foreach(var child in component.GetComponentsInChildren<Component>(true))components[child.Pointer]=owner;
    }
    internal static PlayerScope.State? Enter(Player? owner)
    {
        if(!owner)return null;
        Depth++;
        var state=new PlayerScope.State{Player=Player.player,Remote=Plugin.RemoteContext};
        Player.player=owner;Plugin.RemoteContext=Plugin.IsRemote(owner!);
        if(CustomKeyManager.cm)
        {
            state.Input=CustomKeyManager.cm;state.Controller=state.Input._controllerType_k__BackingField;
            state.Input._controllerType_k__BackingField=Plugin.RemoteContext?Plugin.RemoteInput.Controller:Plugin.HostController;
        }
        return state;
    }
    internal static int Depth;
    internal static void Exit(PlayerScope.State? state){if(state!=null){Depth--;if(state.Input)state.Input!._controllerType_k__BackingField=state.Controller;Player.player=state.Player;Plugin.RemoteContext=state.Remote;}}
    internal static IEnumerable<Type> EffectTypes=>typeof(Player).Assembly.GetTypes().Where(t=>typeof(Component).IsAssignableFrom(t) && (t.Name.StartsWith("Skill_") || t.Name.StartsWith("Item_")));
}

[HarmonyPatch]
internal class EffectOwnerScope
{
    static IEnumerable<MethodBase> TargetMethods()=>Owners.EffectTypes.SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
        .Where(m=>!m.IsSpecialName && m.Name!="Finalize" && !m.IsAbstract && !m.ContainsGenericParameters && Owners.Patchable(m)).Cast<MethodBase>();
    static void Prefix(Component __instance,out PlayerScope.State? __state)=>__state=Owners.Enter(Owners.Find(__instance));
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}

[HarmonyPatch]
internal class EffectCoroutineOwnerScope
{
    static IEnumerable<MethodBase> TargetMethods()=>Owners.EffectTypes.SelectMany(t=>t.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))
        .Where(t=>typeof(Component).IsAssignableFrom(t.GetProperty("__4__this")?.PropertyType))
        .Select(t=>(MethodBase?)AccessTools.Method(t,"MoveNext")).Where(m=>m!=null).Cast<MethodBase>();
    static void Prefix(object __instance,out PlayerScope.State? __state)
    {
        var component=__instance.GetType().GetProperty("__4__this")?.GetValue(__instance) as Component;
        __state=component?Owners.Enter(Owners.Find(component!)):null;
    }
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}

[HarmonyPatch(typeof(ObjectPool),nameof(ObjectPool.SpawnObject))]
internal class SpawnOwner
{
    static void Prefix(ObjectPool __instance,out PlayerScope.State? __state)=>__state=Owners.Enter(Owners.Find(__instance));
    static void Postfix(ObjectPool __instance,Component __result)
    {
        var owner=Owners.Find(__instance);
        if(__result && owner)Owners.Register(__result,owner!);
    }
    static void Finalizer(PlayerScope.State? __state)=>Owners.Exit(__state);
}

[HarmonyPatch(typeof(Player),nameof(Player.AddItem))]
internal class ItemOwner
{
    static void Prefix(Player __instance,Item item){if(item)Owners.Register(item,__instance);}
}

[HarmonyPatch(typeof(Player),nameof(Player.ChangeSkill))]
internal class SkillOwner
{
    // Native ChangeSkill does not copy: it reparents SkillManager's single instance of that skill onto the player.
    // Two actors choosing the same skill would then share one object (charge, cooldown, owner). The guest actor
    // gets its own copy instead, and the shared instance is put back exactly as it was.
    internal sealed class Shared {internal Skill? Instance;internal Transform? Parent;internal Vector3 Position,Scale,Euler;internal int Current;internal Skill? Old;}
    static void Prefix(Player __instance,int index,int pos,out Shared? __state)
    {
        __state=null;
        if(!Plugin.IsRemote(__instance) || !SkillManager.sm)return;
        var shared=SkillManager.sm.GetSkill(index);if(!shared)return;
        var t=shared.transform;
        __state=new Shared{Instance=shared,Parent=t.parent,Position=t.localPosition,Scale=t.localScale,Euler=t.localEulerAngles,Current=shared.current,Old=__instance.skills[pos]};
    }
    static void Postfix(Player __instance,int pos,Shared? __state)
    {
        if(__state?.Instance && __instance.skills[pos] && __instance.skills[pos].Pointer==__state.Instance!.Pointer)
        {
            var shared=__state.Instance!;var placed=shared.transform;
            var copy=UnityEngine.Object.Instantiate(shared,placed.parent,false);var t=copy.transform;
            t.localPosition=placed.localPosition;t.localScale=placed.localScale;t.localEulerAngles=placed.localEulerAngles;
            copy.current=shared.current;__instance.skills[pos]=copy;
            placed.SetParent(__state.Parent,false);placed.localPosition=__state.Position;placed.localScale=__state.Scale;placed.localEulerAngles=__state.Euler;shared.current=__state.Current;
            // The guest's previous copy was handed back to SkillManager's container; it belongs to no one now.
            if(__state.Old && __state.Old!.Pointer!=shared.Pointer && !SkillManager.sm.skills.Any(s=>s.Pointer==__state.Old.Pointer))UnityEngine.Object.Destroy(__state.Old.gameObject);
        }
        foreach(var skill in __instance.skills)if(skill)Owners.Register(skill,__instance);
    }
}

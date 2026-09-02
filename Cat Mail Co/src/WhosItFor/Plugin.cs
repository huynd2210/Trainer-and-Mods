using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppList = Il2CppSystem.Collections.Generic.List<DialogueData>;

namespace CatMailCo.WhosItFor;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("CatMailCo.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.catmailco.whositfor";
    public const string Name = "Who's It For?";
    public const string Version = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        RequestSettings.Initialize(Config);

        Harmony.CreateAndPatchAll(typeof(RequestDialoguePatches), Guid);

        Log.LogInfo($"{Name} {Version} loaded. Customers will say who their parcel is for.");
    }

    public override bool Unload()
    {
        Harmony.UnpatchID(Guid);
        return true;
    }
}

internal static class RequestSettings
{
    private static ConfigEntry<bool> _addCustomerName = null!;
    private static ConfigEntry<bool> _verboseLogging = null!;

    internal static bool AddCustomerName => _addCustomerName.Value;
    internal static bool VerboseLogging => _verboseLogging.Value;

    internal static void Initialize(ConfigFile config)
    {
        _addCustomerName = config.Bind("Requests", "AddCustomerName", true,
            "Make customers say their name when they would otherwise only describe the "
            + "parcel. Nothing is removed; the original description is always kept.");
        _verboseLogging = config.Bind("Diagnostics", "VerboseLogging", true,
            "Log the make-up of every request, and every name this mod adds.");
    }
}

internal static class RequestDialoguePatches
{
    /// <summary>
    /// Clue lines that describe the parcel rather than naming its owner. Only used for
    /// reporting - this mod does not remove them.
    /// </summary>
    private static readonly HashSet<DialogueType> DescriptiveClueTypes = new()
    {
        DialogueType.Size,
        DialogueType.Weight,
        DialogueType.VisualElement,
        DialogueType.StorageConstraint,
        DialogueType.BehaviorConstraint,
    };

    /// <summary>
    /// Greeting lines that a name should be slotted in after, so the customer says hello
    /// before introducing themselves.
    /// </summary>
    private static readonly HashSet<DialogueType> GreetingTypes = new()
    {
        DialogueType.Hello,
        DialogueType.HelloDeposit,
    };

    /// <summary>
    /// Purely additive: if the game built a request that never names the customer, ask the
    /// game for a name line and slot it in. The original clue lines are always left alone,
    /// so even if the added line turns out to be unhelpful the request stays exactly as
    /// solvable as it was in vanilla.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomerDialogueManager), nameof(CustomerDialogueManager.GetRequestDialogue))]
    private static void AddCustomerName(
        CustomerDialogueManager __instance,
        Il2CppList __result,
        EntityCustomer customer,
        DialogueMood mood)
    {
        if (__result == null)
            return;

        try
        {
            var before = Describe(__result);

            if (!RequestSettings.AddCustomerName)
            {
                Log($"request untouched (mod disabled) :: {before}");
                return;
            }

            if (IndexOfType(__result, DialogueType.PersonName) >= 0)
            {
                Log($"request already names the customer, left as-is :: {before}");
                return;
            }

            var nameLine = __instance.GetDialogue(DialogueType.PersonName, customer, mood);
            if (nameLine == null)
            {
                Plugin.Instance.Log.LogWarning(
                    $"No name line available for this customer; request left unchanged :: {before}");
                return;
            }

            // Trust the game's own selection rather than picking a category here: only it
            // knows whether this customer should be matched by name or by surname.
            if (nameLine._Type_k__BackingField != DialogueType.PersonName)
            {
                Plugin.Instance.Log.LogWarning(
                    "GetDialogue returned a "
                    + $"{nameLine._Type_k__BackingField} line instead of PersonName; request left unchanged.");
                return;
            }

            __result.Insert(InsertionIndex(__result), nameLine);

            Log($"added customer name [{nameLine._Key_k__BackingField} / "
                + $"{nameLine._Category_k__BackingField}] :: {before}  ->  {Describe(__result)}");
        }
        catch (System.Exception exception)
        {
            Plugin.Instance.Log.LogError($"Could not add the customer name to the request: {exception}");
        }
    }

    /// <summary>Slot the name in after any leading greeting, otherwise at the front.</summary>
    private static int InsertionIndex(Il2CppList dialogue)
    {
        var index = 0;
        while (index < dialogue.Count)
        {
            var line = dialogue[index];
            if (line == null || !GreetingTypes.Contains(line._Type_k__BackingField))
                break;

            index++;
        }

        return index;
    }

    private static int IndexOfType(Il2CppList dialogue, DialogueType type)
    {
        for (var i = 0; i < dialogue.Count; i++)
        {
            var line = dialogue[i];
            // Read the backing field rather than the property: IL2CPP reports no callers
            // for the Type getter, so it may have been stripped from the build.
            if (line != null && line._Type_k__BackingField == type)
                return i;
        }

        return -1;
    }

    /// <summary>Render a request as "verdict: Type | Type | Type" for the log.</summary>
    private static string Describe(Il2CppList dialogue)
    {
        var sequence = new System.Text.StringBuilder();
        var names = 0;
        var descriptions = 0;

        for (var i = 0; i < dialogue.Count; i++)
        {
            var line = dialogue[i];
            if (line == null)
                continue;

            var type = line._Type_k__BackingField;
            if (sequence.Length > 0)
                sequence.Append(" | ");
            sequence.Append(type);

            if (type == DialogueType.PersonName)
                names++;
            else if (DescriptiveClueTypes.Contains(type))
                descriptions++;
        }

        var verdict = names > 0 && descriptions > 0 ? "name+description"
            : names > 0 ? "name only"
            : descriptions > 0 ? "description only"
            : "no clues";

        return $"[{verdict}] {sequence}";
    }

    private static void Log(string message)
    {
        if (RequestSettings.VerboseLogging)
            Plugin.Instance.Log.LogInfo(message);
    }
}



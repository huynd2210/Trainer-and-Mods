using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
/// Loads the real game assembly (Assembly-CSharp.dll) with MetadataLoadContext and asserts
/// that every member the trainer patches or touches exists with the expected signature.
/// A signature typo here compiles fine but would fail at runtime inside Harmony — this
/// catches it before the mod ships.
///
/// Usage: dotnet run --project verify -- <ManagedDir> <Assembly-CSharp.dll path>
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: VerifySignatures <ManagedDir> <AssemblyPath>");
            return 2;
        }
        string managedDir = Path.GetFullPath(args[0]);
        string assemblyPath = Path.GetFullPath(args[1]);

        var resolverPaths = Directory.GetFiles(managedDir, "*.dll").ToList();
        var resolver = new PathAssemblyResolver(resolverPaths);

        using (var mlc = new MetadataLoadContext(resolver))
        {
            Assembly asm = mlc.LoadFromAssemblyPath(assemblyPath);
            Console.WriteLine("Loaded: " + asm.GetName().Name + " (" + assemblyPath + ")");

            // --- CampaignInterface ---
            Type ci = asm.GetType("CampaignInterface", throwOnError: false);
            Check(ci != null, "CampaignInterface type exists");

            MethodInfo checkCP = ci.GetMethod("CheckSufficientCommandPoints",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Check(checkCP != null, "CampaignInterface.CheckSufficientCommandPoints (private instance) exists");
            if (checkCP != null)
            {
                Check(checkCP.GetParameters().Length == 0, "CheckSufficientCommandPoints takes 0 params");
                Check(checkCP.ReturnType.FullName == "System.Boolean", "CheckSufficientCommandPoints returns bool (got " + checkCP.ReturnType.FullName + ")");
            }

            MethodInfo setTotal = ci.GetMethod("SetCommandTotal",
                BindingFlags.Public | BindingFlags.Instance);
            Check(setTotal != null, "CampaignInterface.SetCommandTotal (public instance) exists");
            if (setTotal != null)
            {
                Check(setTotal.GetParameters().Length == 0, "SetCommandTotal takes 0 params");
                Check(setTotal.ReturnType.FullName == "System.Void", "SetCommandTotal returns void (got " + setTotal.ReturnType.FullName + ")");
            }

            // --- CampaignManager (the singleton the patches read) ---
            Type cm = asm.GetType("CampaignManager", throwOnError: true);
            FieldInfo cmInstance = cm.GetField("instance", BindingFlags.Public | BindingFlags.Static);
            Check(cmInstance != null && cmInstance.FieldType == cm, "CampaignManager.instance static singleton exists");

            // --- CampaignData (the fields the patches mutate) ---
            Type cd = asm.GetType("CampaignData", throwOnError: true);
            FieldInfo cp = cd.GetField("commandPoints");
            Check(cp != null, "CampaignData.commandPoints exists");
            if (cp != null) Check(cp.FieldType.FullName == "System.Int32[]", "CampaignData.commandPoints is int[] (got " + cp.FieldType.FullName + ")");
            Check(cd.GetField("commandBonusPoints") != null, "CampaignData.commandBonusPoints exists");
            FieldInfo cps = cd.GetField("commandPointSpent");
            Check(cps != null, "CampaignData.commandPointSpent exists");
            if (cps != null) Check(cps.FieldType.FullName == "System.Int32[]", "CampaignData.commandPointSpent is int[] (got " + cps.FieldType.FullName + ")");

            // --- CampaignInterface fog-of-war display members (Reveal Map) ---
            MethodInfo setContactColor = ci.GetMethod("SetContactColor", BindingFlags.Public | BindingFlags.Instance);
            Check(setContactColor != null, "CampaignInterface.SetContactColor (public instance) exists");
            if (setContactColor != null)
            {
                ParameterInfo[] pc = setContactColor.GetParameters();
                Check(pc.Length == 2, "SetContactColor takes 2 params (got " + pc.Length + ")");
                if (pc.Length == 2)
                {
                    Check(pc[0].ParameterType.Name == "MobileMapObject", "SetContactColor param0 is MobileMapObject (got " + pc[0].ParameterType.Name + ")");
                    Check(pc[1].ParameterType.FullName == "System.Boolean", "SetContactColor param1 is bool (got " + pc[1].ParameterType.FullName + ")");
                }
                Check(setContactColor.ReturnType.FullName == "System.Void", "SetContactColor returns void");
            }
            FieldInfo factionColours = ci.GetField("factionColours");
            Check(factionColours != null, "CampaignInterface.factionColours exists");
            if (factionColours != null)
            {
                Check(factionColours.FieldType.IsArray && factionColours.FieldType.GetElementType().Name == "Color32",
                    "factionColours is Color32[] (got " + factionColours.FieldType.FullName + ")");
            }
            FieldInfo noContactColor = ci.GetField("noContactColor");
            Check(noContactColor != null && noContactColor.FieldType.Name == "Color32", "CampaignInterface.noContactColor is Color32");
            Check(ci.GetField("instance", BindingFlags.Public | BindingFlags.Static) != null, "CampaignInterface.instance static singleton exists");

            // --- CampaignManager campaign-map lists + intel (Reveal Map) ---
            FieldInfo otherMobile = cm.GetField("otherMobile");
            Check(otherMobile != null, "CampaignManager.otherMobile exists");
            if (otherMobile != null) Check(IsListOf(otherMobile.FieldType, "GameObjectAnimator"), "otherMobile is List<GameObjectAnimator> (got " + otherMobile.FieldType.FullName + ")");
            FieldInfo spottedContacts = cm.GetField("spottedContacts");
            Check(spottedContacts != null, "CampaignManager.spottedContacts exists");
            if (spottedContacts != null) Check(IsListOf(spottedContacts.FieldType, "MobileMapObject"), "spottedContacts is List<MobileMapObject> (got " + spottedContacts.FieldType.FullName + ")");
            MethodInfo getIntel = cm.GetMethod("GetIntelOnSeaGroup", BindingFlags.Public | BindingFlags.Instance);
            Check(getIntel != null, "CampaignManager.GetIntelOnSeaGroup (public instance) exists");
            if (getIntel != null)
            {
                ParameterInfo[] pg = getIntel.GetParameters();
                Check(pg.Length == 1 && pg[0].ParameterType.Name == "MobileMapObject", "GetIntelOnSeaGroup takes (MobileMapObject)");
                Check(IsListOf(getIntel.ReturnType, "String"), "GetIntelOnSeaGroup returns List<string> (got " + getIntel.ReturnType.FullName + ")");
            }

            // --- MobileMapObject fields used by the reveal pass ---
            Type mmo = asm.GetType("MobileMapObject", throwOnError: false);
            Check(mmo != null, "MobileMapObject type exists");
            if (mmo != null)
            {
                Check(mmo.GetField("spottedTimer") != null, "MobileMapObject.spottedTimer exists");
                Check(mmo.GetField("currentFaction") != null, "MobileMapObject.currentFaction exists");
                Check(mmo.GetField("type") != null, "MobileMapObject.type exists");
                FieldInfo intelData = mmo.GetField("intelData");
                Check(intelData != null && IsListOf(intelData.FieldType, "String"), "MobileMapObject.intelData is List<string>");
            }

            // --- Utilities.GetMaxSpotTime (keeps contacts fully spotted) ---
            Type utilities = asm.GetType("Utilities", throwOnError: false);
            Check(utilities != null, "Utilities type exists");
            if (utilities != null)
            {
                MethodInfo getMaxSpotTime = utilities.GetMethod("GetMaxSpotTime", BindingFlags.Public | BindingFlags.Static);
                Check(getMaxSpotTime != null, "Utilities.GetMaxSpotTime (public static) exists");
                if (getMaxSpotTime != null)
                {
                    ParameterInfo[] pm = getMaxSpotTime.GetParameters();
                    Check(pm.Length == 1 && pm[0].ParameterType.Name == "MobileMapObject", "GetMaxSpotTime takes (MobileMapObject)");
                    Check(getMaxSpotTime.ReturnType.FullName == "System.Single", "GetMaxSpotTime returns float");
                }
            }

            // --- Exact-intel builder (CampaignManager.GetIntelOnSeaGroup prefix) ---
            MethodInfo getShipDesignations = cm.GetMethod("GetShipDesignations", BindingFlags.Public | BindingFlags.Instance);
            Check(getShipDesignations != null, "CampaignManager.GetShipDesignations (public instance) exists");
            if (getShipDesignations != null)
            {
                ParameterInfo[] pd = getShipDesignations.GetParameters();
                Check(pd.Length == 1 && pd[0].ParameterType.Name == "MobileMapObject", "GetShipDesignations takes (MobileMapObject)");
                Check(getShipDesignations.ReturnType.IsArray && getShipDesignations.ReturnType.GetElementType().FullName == "System.Int32",
                    "GetShipDesignations returns int[] (got " + getShipDesignations.ReturnType.FullName + ")");
            }
            MethodInfo getSpriteIndex = cm.GetMethod("GetSpriteIndexFromShipSubtype", BindingFlags.Public | BindingFlags.Instance);
            Check(getSpriteIndex != null, "CampaignManager.GetSpriteIndexFromShipSubtype (public instance) exists");
            if (getSpriteIndex != null)
            {
                ParameterInfo[] ps = getSpriteIndex.GetParameters();
                Check(ps.Length == 1 && ps[0].ParameterType.Name == "UnitSubType", "GetSpriteIndexFromShipSubtype takes (UnitSubType)");
                Check(getSpriteIndex.ReturnType.FullName == "System.Int32", "GetSpriteIndexFromShipSubtype returns int");
            }

            // LanguageManager pieces used by the exact-intel builder.
            Type langManager = asm.GetType("LanguageManager", throwOnError: false);
            Check(langManager != null, "LanguageManager type exists");
            if (langManager != null)
            {
                Check(langManager.GetField("instance", BindingFlags.Public | BindingFlags.Static) != null, "LanguageManager.instance static singleton exists");
                FieldInfo generalDict = langManager.GetField("generalDictionary");
                Check(generalDict != null && IsDictOf(generalDict.FieldType, "String", "String"), "LanguageManager.generalDictionary is Dictionary<string,string>");
                FieldInfo abbreviations = langManager.GetField("unitTypeDisplayAbbreviations");
                Check(abbreviations != null && IsListOf(abbreviations.FieldType, "String"), "LanguageManager.unitTypeDisplayAbbreviations is List<string>");
            }

            // CampaignInterface pieces used by the exact-intel builder.
            MethodInfo speedDisplay = ci.GetMethod("GetMobileSeaSpeedDisplay", BindingFlags.Public | BindingFlags.Instance);
            Check(speedDisplay != null, "CampaignInterface.GetMobileSeaSpeedDisplay (public instance) exists");
            if (speedDisplay != null)
            {
                ParameterInfo[] ps2 = speedDisplay.GetParameters();
                Check(ps2.Length == 1 && ps2[0].ParameterType.FullName == "System.Single", "GetMobileSeaSpeedDisplay takes (float)");
                Check(speedDisplay.ReturnType.FullName == "System.String", "GetMobileSeaSpeedDisplay returns string");
            }
            FieldInfo mobileMapSprites = ci.GetField("mobileMapSprites");
            Check(mobileMapSprites != null && mobileMapSprites.FieldType.IsArray && mobileMapSprites.FieldType.GetElementType().Name == "Sprite",
                "CampaignInterface.mobileMapSprites is Sprite[] (got " + (mobileMapSprites != null ? mobileMapSprites.FieldType.FullName : "null") + ")");

            // EngagementManager unitDataDictionary + MobileMapObject.unitPrefabs + UnitSubType enum.
            Type eng = asm.GetType("EngagementManager", throwOnError: false);
            if (eng != null)
            {
                FieldInfo unitDict = eng.GetField("unitDataDictionary");
                Check(unitDict != null && IsDictOf(unitDict.FieldType, "String", "UnitData"), "EngagementManager.unitDataDictionary is Dictionary<string,UnitData>");
            }
            if (mmo != null)
            {
                FieldInfo unitPrefabs = mmo.GetField("unitPrefabs");
                Check(unitPrefabs != null && IsListOf(unitPrefabs.FieldType, "String"), "MobileMapObject.unitPrefabs is List<string>");
            }
            Type unitSubtype = asm.GetType("UnitSubType", throwOnError: false);
            Check(unitSubtype != null && unitSubtype.IsEnum, "UnitSubType enum exists");
            if (unitSubtype != null && unitSubtype.IsEnum)
            {
                string[] expected = { "Aircraft_Carrier", "Light_Carrier", "Battleship", "Battlecruiser", "Heavy_Cruiser",
                                      "Light_Cruiser", "Merchant", "Oiler", "Destroyer", "Destroyer_Escort", "Submarine" };
                bool all = true;
                foreach (string name in expected) if (Enum.GetNames(unitSubtype).FirstOrDefault(n => n == name) == null) all = false;
                Check(all, "UnitSubType has the expected 11 members");
            }

            // --- Tactical battle reveal (SensorManager.CanDectectUnit patch + combat pass) ---
            Type sensorManager = asm.GetType("SensorManager", throwOnError: false);
            Check(sensorManager != null, "SensorManager type exists");
            if (sensorManager != null)
            {
                MethodInfo canDetect = sensorManager.GetMethod("CanDectectUnit", BindingFlags.Public | BindingFlags.Instance);
                Check(canDetect != null, "SensorManager.CanDectectUnit (public instance) exists");
                if (canDetect != null)
                {
                    ParameterInfo[] pdc = canDetect.GetParameters();
                    Check(pdc.Length == 2 && pdc[0].ParameterType.Name == "Unit" && pdc[1].ParameterType.Name == "Unit",
                        "CanDectectUnit takes (Unit, Unit)");
                    Check(canDetect.ReturnType.FullName == "System.Boolean", "CanDectectUnit returns bool");
                }
            }
            Type unit = asm.GetType("Unit", throwOnError: false);
            Check(unit != null, "Unit type exists");
            if (unit != null)
            {
                Check(unit.GetField("detected") != null, "Unit.detected exists");
                Check(unit.GetField("previouslyDetected") != null, "Unit.previouslyDetected exists");
                Check(unit.GetField("identified") != null, "Unit.identified exists");
                Check(unit.GetField("detectedIndex") != null, "Unit.detectedIndex exists");
                Check(unit.GetField("faction") != null, "Unit.faction exists");
                Check(unit.GetField("mapUnit") != null, "Unit.mapUnit exists");
                Check(unit.GetField("unitSea") != null, "Unit.unitSea exists");
                Check(unit.GetField("unitAir") != null, "Unit.unitAir exists");
            }
            Type mapUnit = asm.GetType("MapUnit", throwOnError: false);
            if (mapUnit != null)
            {
                Check(mapUnit.GetField("mapUnitText") != null, "MapUnit.mapUnitText exists");
            }
            Type engagementData = asm.GetType("EngagementData", throwOnError: false);
            if (engagementData != null)
            {
                Check(engagementData.GetField("enemySeaDetected") != null, "EngagementData.enemySeaDetected exists");
                Check(engagementData.GetField("enemyAirDetected") != null, "EngagementData.enemyAirDetected exists");
            }
            FieldInfo otherUnits = eng != null ? eng.GetField("otherUnits") : null;
            Check(otherUnits != null && IsListOf(otherUnits.FieldType, "Unit"), "EngagementManager.otherUnits is List<Unit>");

            // --- Plugin assembly: verify the Harmony patch attributes reference real types ---
            string pluginPath = Path.Combine(args.Length > 2 ? args[2] : Path.GetDirectoryName(assemblyPath) ?? ".", "WoTSTrainer.dll");
            if (File.Exists(pluginPath))
            {
                Console.WriteLine();
                Console.WriteLine("Plugin assembly: " + pluginPath);
                try
                {
                    Assembly plugin = mlc.LoadFromAssemblyPath(Path.GetFullPath(pluginPath));
                    Check(plugin != null, "WoTSTrainer.dll loads in MetadataLoadContext");
                }
                catch (Exception ex)
                {
                    Check(false, "WoTSTrainer.dll failed to load: " + ex.Message);
                }
            }
        }

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("ALL CHECKS PASSED");
            return 0;
        }
        Console.WriteLine(failures + " CHECK(S) FAILED");
        return 1;
    }

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failures++;
    }

    private static bool IsListOf(Type type, string elementTypeName)
    {
        if (type == null || !type.IsGenericType) return false;
        if (type.GetGenericTypeDefinition().FullName != "System.Collections.Generic.List`1") return false;
        Type[] args = type.GetGenericArguments();
        return args.Length == 1 && args[0].Name == elementTypeName;
    }

    private static bool IsDictOf(Type type, string keyTypeName, string valueTypeName)
    {
        if (type == null || !type.IsGenericType) return false;
        if (type.GetGenericTypeDefinition().FullName != "System.Collections.Generic.Dictionary`2") return false;
        Type[] args = type.GetGenericArguments();
        return args.Length == 2 && args[0].Name == keyTypeName && args[1].Name == valueTypeName;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraCustomer
{
    // Every customer the game sends as an ordinary trader: the daily walk-in pools
    // (StoreClientList.generalPool and the faction pools), the display case's typed
    // customers (ShowcaseHelper.CreateClientByStockType), the supplier, specialist,
    // event, surplus/shortage and one-off ("minor") traders, and the barter customers.
    // Story, mentor, wanted, landlord, security, loan and unique characters are left
    // out on purpose: calling them out of turn would replay or break their storylines.
    // Ids missing from a future build are skipped at runtime.
    internal static class CustomerCatalog
    {
        internal class Entry
        {
            public string Id;
            public int Tier;            // 1-3 for the display case's t1/t2/t3 buyers, else 0
            public string Suffix;       // tells apart customers the game gives the same name
            public string Note;         // what they bring or want, for the tooltip
            public Func<bool> Locked;   // non-null: returns true while the game has not unlocked it
            public string LockedReason;
        }

        internal class Group
        {
            public Func<string> Label;
            public Entry[] Entries;
        }

        internal static readonly Group[] Buyers =
        {
            Text("Anything",
                E("merchant", note: "Buys any item that isn't contraband")),
            Types(new[] { "FOOD" },
                E("spacerFoodI", tier: 1), E("hungrySpacer", tier: 1), E("spacerChef", tier: 1),
                E("expeditionSupplier", tier: 2), E("privateChef", tier: 3),
                E("spacerFood", suffix: "walk-in"), E("spacerFoodVariety", suffix: "variety", note: "Wants several different foods"),
                E("foodBankBuyer", note: "Big reputation boost; your own stock sells at half price"),
                E("foodBuyer"), E("lowerLevelElder"), E("food_shortage_client"), E("lotteryWinnerMinor"),
                E("upperLevelServantHoboParty", note: "Food, treats and drinks"),
                E("secRequisitionOfficer", note: "Food and medical supplies")),
            Types(new[] { "WATER" },
                E("thirstySpacer", tier: 1), E("scavWaterShopper", tier: 1), E("waterSpace2", tier: 2), E("waterStockpiler", tier: 3),
                E("panicBuyer", suffix: "panic buyer"), E("corporateBuyer"), E("generic_water_buyer"),
                E("annoyedEngineer", note: "Food or clean water"),
                E("cluelessTouristBuyer", note: "Food, water and substances")),
            Types(new[] { "MATERIAL", "TOOL", "MODULE", "ENERGY_CELL" },
                E("lowerRepairman", tier: 1), E("scrap_byer", tier: 1), E("maintenanceWorker", tier: 1),
                E("lowerInventor", tier: 2), E("stationEngineer", tier: 3),
                E("stationEngineerMinor"), E("foreman"), E("material_shortage_client"),
                E("touristJunkSouvenirBuyer", note: "Materials, makeshift items and junk"),
                E("lowerLevelCitizenPowerBuying", suffix: "power", note: "Energy cells"),
                E("lowerLevelCitizenMaintenanceBuying", suffix: "maintenance", note: "Energy cells"),
                E("upperLevelCitizenPowerBuying", note: "Energy cells")),
            Types(new[] { "MEDICAL" },
                E("spacerMedical", tier: 1), E("sickChildCaretaker", tier: 1), E("lowLevelDoctor", tier: 1),
                E("clinicNurse", tier: 2), E("hospitalAdmin", tier: 3),
                E("pharmacist"), E("nurseMinor2"), E("sickLowers"), E("woundedMan"), E("medical_shortage_client"),
                E("cartelWoundedSoldier")),
            Types(new[] { "HOUSEHOLD_GOOD" },
                E("spacerHouseholdItem", tier: 1), E("youngSpacerHousehold", tier: 1), E("familyMother", tier: 1),
                E("citizenHousehold", tier: 2), E("upperMaid", tier: 3),
                E("spacerAmenityVariety", note: "Wants several different household goods")),
            Types(new[] { "SUBSTANCE" },
                E("desperateAddict", tier: 1), E("wornOutSpacer", tier: 2), E("upperLevelHedonist", tier: 3),
                E("upperLowerVisitor"), E("substance_shortage_client"),
                E("upperLevelVisitorMinor", note: "Substances, narcotics, makeshift and low-tech items, processed food"),
                E("miner", suffix: "sells ore", note: "Pays partly in ore"),
                E("junker", suffix: "sells scrap", note: "Pays partly in scrap"),
                E("conspiracyMinor", note: "Also sells makeshift weapons"),
                E("minerMinor", suffix: "narcotics", note: "Narcotics"),
                E("street_dealer", note: "Narcotics")),
            Text("Cigarettes and treats",
                E("patrolOfficer", note: "Cigarettes and treats")),
            Types(new[] { "WEAPON", "ARMOR", "AMMUNITION" },
                E("scavenger", tier: 1), E("selfDefenseEnthusiast", tier: 2), E("privateSecurityContractor", tier: 3),
                E("mercenaryBuyer", suffix: "regular"), E("minorMerc", suffix: "one-off"),
                E("revolutionaryBuyer", note: "Weapons, munitions and medical"),
                E("criminalBuyerMerc", note: "Weapons, armor, munitions and medical"),
                E("touristWeaponSouvenirBuyer", note: "Weapons as souvenirs"),
                E("lynching_citizen"), E("panickedShopkeeperWeaponBuyer"), E("weapon_armor_shortage_client"),
                E("lover", note: "Tools, weapons or medical")),
            Types(new[] { "FIREARM" },
                E("ll_gun_buyer", tier: 1), E("merc_gun_buyer", tier: 2), E("ul_gun_buyer", tier: 3)),
            Text("Luxury",
                E("upperLowerVisitorLuxury"), E("ulDiscerningShopper", suffix: "big spender")),
            Text("Contraband and black market",
                E("shadyMerchant", note: "Contraband and access cards, at a contraband discount"),
                E("criminalBuyerThief", note: "Access cards, medical and melee weapons"),
                E("poisonBuyer", note: "Poison and chemical supplies")),
            Text("Seeds",
                E("farmerSeedBuyer", note: "Buys seeds, sells fruit")),
        };

        private const string WaterTraderLock = "Unlocks with the water trader network upgrade.";

        internal static readonly Group[] Sellers =
        {
            Text("Cigarettes, food stamps and injectors",
                E("scavInspectable", note: "Cigarettes, food stamps or injectors; some are fake",
                    unlocked: () => PlayerStore.Instance.isStampIntroduced || PlayerStore.Instance.isVisualInspectionIntroduced
                                    || PlayerStore.Instance.isInjectorIntroduced,
                    lockedReason: "Comes once the game has introduced cigarettes, stamps or injectors."),
                E("scavStamp", note: "Food stamps; some are fake"),
                E("shadyPharma", note: "Injectors and bandages, stolen"),
                E("scalper", note: "A genuine injector, marked up")),
            Text("Substances and chemicals",
                E("lowerLevelChemist", note: "Dream dust, salve, caffeine pills; buys chemical supplies"),
                E("rareLowerLevelChemist", note: "Dream dust, injectors, pills; buys chemical supplies"),
                E("substance_surplus_client"),
                E("shadyFarmer", note: "Dream caps, coffee beans, cotton"),
                // From the Chemical Suppliers mod; hidden when it isn't installed.
                E("chemicalMerchant", note: "Alcohol, reducing agent, N-Methylol Base for Phanta, starch filler"),
                E("shadyMushroomSeller", note: "A big lot of Dream Caps and Nightmare Caps, wholesale")),
            Types(new[] { "WATER" },
                E("waterSeller", note: "Bottled water and rations"),
                E("honestBadWaterSeller", suffix: "honest"),
                E("waterSellerScav", suffix: "trader", unlocked: NetworkUpgrade.IsWaterTraderUnlocked, lockedReason: WaterTraderLock),
                E("waterHishonestSellerScav", suffix: "shady", note: "Cheap; the label may lie",
                    unlocked: NetworkUpgrade.IsWaterTraderUnlocked, lockedReason: WaterTraderLock),
                E("waterCompany", unlocked: NetworkUpgrade.IsWaterTraderUnlocked, lockedReason: WaterTraderLock),
                E("iceMiner", note: "Water; buys substances")),
            Types(new[] { "FOOD" },
                E("scavGenericFoodSeller"), E("thiefGenericFoodSeller"),
                E("scavCrate", suffix: "food crate", note: "A crate of one packed food"),
                E("peatMinorClient", suffix: "meat"),
                E("foodThief", note: "Stolen processed food"),
                E("farmer_standard", suffix: "buys seeds", note: "Fruit; buys seeds"),
                E("farmerMinor", suffix: "one-off", note: "Fruit"),
                E("food_surplus_client")),
            Types(new[] { "MATERIAL" },
                E("scavGenericMaterialSeller"), E("thiefGenericMaterialSeller"),
                E("junker", note: "Scrap, electronics, wire, plastic; buys substances"),
                E("miner", note: "Ore; buys substances"),
                E("scavHaul"), E("salvagePilot"), E("material_surplus_client")),
            Types(new[] { "MACHINE" },
                E("machine_scrapper", note: "Broken machines to repair",
                    unlocked: () => PlayerStore.Instance.IsJunkerStart() || NetworkUpgrade.IsRuinedMachineUnlocked(),
                    lockedReason: "Unlocks with the ruined machine network upgrade.")),
            Types(new[] { "MEDICAL" },
                E("scavGenericMedSeller"), E("thiefGenericMedSeller"),
                E("nurseMinor", note: "Injectors and pills, stolen"),
                E("scavBlood", note: "Blood bags; takes food or clean water"),
                E("medical_surplus_client")),
            Types(new[] { "HOUSEHOLD_GOOD" },
                E("scavGenericHouseholdSeller"), E("thiefGenericHouseholdSeller"), E("scavengerHousehold"),
                E("pettyThief", note: "Stolen food, household and medical goods")),
            Types(new[] { "FIREARM", "WEAPON" },
                E("ll_gun_seller"), E("merc_gun_seller"), E("ul_gun_seller"),
                E("lower_level_gunsmith", note: "Handmade guns and ammo"),
                E("OldScav1", note: "A handmade gun and ammo"),
                E("secSurplusMerchant", note: "Security surplus gun parts and ammo"),
                E("weapon_armor_surplus_client")),
            Text("Mixed and rare goods",
                E("scavGeneral", note: "A mixed haul"),
                E("thief", note: "Stolen premium goods, keycards, modules, loot crates"),
                E("brokeUpperLevel", note: "Upper-level gear, cheap"),
                E("lower_level_rare_merchant", note: "Rare equipment and modules"),
                E("supplier", note: "Restocks your usual goods"),
                E("hypertech_smuggler", note: "Hypertech, with contraband markup",
                    unlocked: () => NetworkUpgrade.IsUnlocked("HYPERTECH_SMUGGLER"),
                    lockedReason: "Unlocks with the hypertech smuggler network upgrade."),
                E("inventor", note: "Inventions; buys materials"),
                E("inventorStorage", note: "Storage and machine bays")),
            Text("Specialists (equipment)",
                E("retired_junker", unlocked: () => PlayerStore.Instance.IsJunkerStart() || NetworkUpgrade.IsRuinedMachineUnlocked(),
                    lockedReason: "Unlocks with the ruined machine network upgrade."),
                E("retired_water_merchant", unlocked: () => PlayerStore.Instance.IsWaterMerchantStart() || NetworkUpgrade.IsWaterTraderUnlocked(),
                    lockedReason: WaterTraderLock),
                E("retired_gunsmith", unlocked: () => PlayerStore.Instance.IsGunsmithStart() || NetworkUpgrade.IsRetiredGunsmithUnlocked(),
                    lockedReason: "Unlocks with the retired gunsmith network upgrade."),
                E("retired_farmer", unlocked: () => PlayerStore.Instance.IsFarmerStart() || NetworkUpgrade.IsRetiredFarmerUnlocked(),
                    lockedReason: "Unlocks with the retired farmer network upgrade."),
                E("retired_winemaker", unlocked: NetworkUpgrade.IsWinemakerUnlocked,
                    lockedReason: "Unlocks with the winemaker network upgrade."),
                E("retired_rancher", unlocked: () => PlayerStore.Instance.IsRancherStart() || NetworkUpgrade.IsRetiredRancherUnlocked(),
                    lockedReason: "Unlocks with the retired rancher network upgrade."),
                E("retired_aquafarmer", unlocked: NetworkUpgrade.IsAquacultureUnlocked,
                    lockedReason: "Unlocks with the aquaculture network upgrade."),
                E("retired_chemist", unlocked: () => PlayerStore.Instance.IsChemistStart() || NetworkUpgrade.IsRetiredChemistUnlocked(),
                    lockedReason: "Unlocks with the retired chemist network upgrade.")),
        };

        internal static readonly Group[] Others =
        {
            Text("Barter",
                E("addictBarter", suffix: "injectors", note: "Trades goods for an injector"),
                E("addictGeneralBarter", suffix: "narcotics", note: "Trades goods for narcotics"),
                E("addictBarterBlood", note: "Trades blood bags"),
                E("scavBarter", note: "Trades scrap and household goods"),
                E("lowerLevelChemistBarter", note: "Trades salve, pills and dream dust"),
                E("normalFoodBarter", note: "Trades for food")),
            Text("Services",
                E("scavAppraisalRandom", note: "Brings items for you to appraise",
                    unlocked: () => PlayerStore.Instance.isAppraisalIntroduced,
                    lockedReason: "Comes once the game has introduced appraisals."),
                E("shadyCustomerInformation", note: "Sells information")),
        };

        internal static bool Exists(string id) => StoreClientListDict.storeClientDict.ContainsKey(id);

        internal static IEnumerable<Entry> Available(Group group) => group.Entries.Where(e => Exists(e.Id));

        private static Group Types(string[] types, params Entry[] entries) => new Group
        {
            Label = () => string.Join(", ", types.Select(TypeHelper.GetTypeDisplayName)),
            Entries = entries,
        };

        private static Group Text(string label, params Entry[] entries) => new Group
        {
            Label = () => label,
            Entries = entries,
        };

        private static Entry E(string id, int tier = 0, string suffix = null, string note = null,
            Func<bool> unlocked = null, string lockedReason = null) => new Entry
        {
            Id = id,
            Tier = tier,
            Suffix = suffix,
            Note = note,
            Locked = unlocked == null ? null : (Func<bool>)(() => !unlocked()),
            LockedReason = lockedReason,
        };
    }
}

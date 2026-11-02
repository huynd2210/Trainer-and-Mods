using HarmonyLib;

namespace NoRentIncrease
{
    public class NoRentIncreaseMod : IMod
    {
        public void Init(ModManifest manifest) { }

        public void OnEnable() { }

        public void OnDisable() { }
    }

    // PlayerStore.RentIncrease() multiplies rentValue by the difficulty's
    // rentIncreaseMultiplier after every rent day, paid (OnRentPaid) or missed
    // (OnRentNotPaid). Skipping it keeps the rent flat; the payment itself, the
    // missed-rent debt and the one-off deals that add to rentValue are untouched.
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.RentIncrease))]
    internal static class RentIncreasePatch
    {
        private static bool Prefix() => false;
    }
}

using System;
using System.Collections.Generic;

namespace ExtraCustomer
{
    // The game's Clinic Volunteer (StoreClientListEvent.CreateClinicVolunteerClient) lays
    // Pure White injectors on the counter at the end of his dialogue, each tagged "free"
    // (a -999% market modifier, so they cost nothing). With the setting off, the volunteers
    // this mod calls keep his dialogue but the tag is taken off, so the injectors are
    // bought at their normal price. The ones the Oxymore event sends are left alone.
    internal static class ClinicVolunteer
    {
        internal const string ClientId = "clinicVolunteer";
        private const string FreeFeatureId = "free";

        internal static ConfigEntry<bool> FreeInjectors;

        internal static void Define(ModConfig config)
        {
            FreeInjectors = config.Define(
                "clinicVolunteerFree", true, "General",
                () => "Clinic Volunteer's injectors are free",
                () => "On: the Clinic Volunteer you call gives his injectors away, as in the Oxymore event. "
                      + "Off: he sells them at the normal price. Volunteers sent by the Oxymore event are not affected.");
        }

        // Wraps every end action in his dialogue chain; the setting is read when the action
        // runs, so changing it applies to volunteers already waiting in the queue.
        internal static void Prepare(StoreClient client)
        {
            var seen = new HashSet<Dialogue>();
            for (Dialogue d = client.mainDialogue; d != null && seen.Add(d); d = d.nextDialogue)
            {
                Action original = d.endAction;
                if (original == null)
                    continue;
                d.endAction = () =>
                {
                    if (FreeInjectors.Value)
                    {
                        original();
                        return;
                    }
                    List<GameItem> counter = EmporiumEntry.Instance.frontInvinvElement.childItems;
                    var before = new HashSet<GameItem>(counter);
                    original();
                    foreach (GameItem item in EmporiumEntry.Instance.frontInvinvElement.childItems)
                        if (!before.Contains(item))
                            item.RemoveItemFeatureByID(FreeFeatureId);
                };
            }
        }
    }
}

using ModShardLauncher;
using ModShardLauncher.Mods;

namespace Mercenaries;

// Hire mercenaries: talk to a tavern merc, pay a signing fee and a daily wage, and they follow you everywhere and
// fight at your side, in their own look. Right-click them for Wait here / Follow me; left-click swaps places.
// Unpaid, they leave; killed, they are gone for good.
public class Mercenaries : Mod
{
    public override string Author => "huynd2210";
    public override string Name => "Mercenaries";
    public override string Description => "Hire a tavern mercenary (500 crowns, then 50 a day) who follows you everywhere and fights at your side. Right-click them to wait or follow.";
    public override string Version => "0.1.0";
    public override string TargetVersion => "0.9.4.24";

    public override void PatchMod() => MercPatch.Apply(name => ModFiles.GetCode(name));
}

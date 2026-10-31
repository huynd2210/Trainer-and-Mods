using System;
using birdseye.Regiments;
using birdseye.Regiments.Aircraft;
using birdseye.Regiments.Dismounts;
using birdseye.Regiments.Platoons;
using birdseye.Regiments.Units;

namespace KillTracker
{
    internal enum Kind
    {
        Vehicle,
        Helicopter,
        Aircraft,
        Soldier
    }

    /// <summary>
    /// Listens to the game's death events for the length of one battle and feeds them to the
    /// battle's log. Every handler swallows its own errors: an exception escaping here would stop
    /// the game's other listeners (its own kill report included) from running.
    /// </summary>
    internal static class BattleTracker
    {
        private static BattleLog log;

        // Cached so unsubscribing removes the same delegate; the game's EventList keys on it.
        private static readonly Action<UnitCore, UnitDeathEventData> unitRemoved = OnUnitRemoved;
        private static readonly Action<PlatoonCore, BoidCore, bool> boidRemoved = OnBoidRemoved;
        private static readonly Action<PlatoonCore, PlatoonDeathEventData> platoonRemoved = OnPlatoonRemoved;
        private static readonly Action<AirUnitCore, AirUnitDeathEventData> airRemoved = OnAirUnitRemoved;

        private static int errorsLogged;

        public static void Begin()
        {
            End();
            if (!KillTrackerPlugin.Enabled.Value)
            {
                return;
            }
            Attribution.Reset();
            log = new BattleLog(DateTime.Now);
            PlatoonEvents.UnitRemoved += unitRemoved;
            PlatoonEvents.BoidRemoved += boidRemoved;
            PlatoonEvents.PlatoonRemoved += platoonRemoved;
            AirUnitEvents.AirUnitRemoved = (Action<AirUnitCore, AirUnitDeathEventData>)Delegate.Combine(AirUnitEvents.AirUnitRemoved, airRemoved);
        }

        public static void Finish(eSides winningSide, eGameResult result, eSessionEndType endType)
        {
            Guard(() => log?.Finish(winningSide, result, endType));
        }

        public static void End()
        {
            if (log == null)
            {
                return;
            }
            Guard(() => log.Close());
            log = null;
            PlatoonEvents.UnitRemoved -= unitRemoved;
            PlatoonEvents.BoidRemoved -= boidRemoved;
            PlatoonEvents.PlatoonRemoved -= platoonRemoved;
            AirUnitEvents.AirUnitRemoved = (Action<AirUnitCore, AirUnitDeathEventData>)Delegate.Remove(AirUnitEvents.AirUnitRemoved, airRemoved);
            Attribution.Reset();
        }

        public static void Tick()
        {
            if (log != null)
            {
                Guard(() => log.Tick());
            }
        }

        // Null death data means the unit left the field (undeployed, retreated), not killed.
        private static void OnUnitRemoved(UnitCore unit, UnitDeathEventData uded)
        {
            if (uded == null || log == null)
            {
                return;
            }
            Guard(() =>
            {
                PlatoonCore plt = unit.platoon;
                // An infantry squad is a "unit" too; its soldiers were already counted one by one.
                if (plt.parameters.isInfOnly)
                {
                    return;
                }
                string type = Names.Type(plt);
                Kind kind = plt.parameters.isHelicopter ? Kind.Helicopter : Kind.Vehicle;
                log.Record(kind, plt.commander, type, Names.Label(plt, type), uded.killer, uded.killingCmdr);
            });
        }

        // Service removals are soldiers mounting up or being folded into a refit, not casualties.
        private static void OnBoidRemoved(PlatoonCore plt, BoidCore boid, bool isServiceRemoval)
        {
            if (isServiceRemoval || log == null)
            {
                return;
            }
            Guard(() =>
            {
                Attribution.Shooter shooter = Attribution.Current;
                string type = Names.InfantryType(plt);
                log.Record(Kind.Soldier, plt.commander, type, Names.Label(plt, type), shooter.killer, shooter.cmdr);
            });
        }

        // Company members (slaves) die with their leader; the game reports the company once.
        private static void OnPlatoonRemoved(PlatoonCore plt, PlatoonDeathEventData pded)
        {
            if (log == null || pded == null || pded.eolState != ePltEOLState.Destroyed || plt.isSlave)
            {
                return;
            }
            Guard(() => log.RecordWipe(plt.commander, Names.Label(plt, Names.Type(plt)), pded.killer, pded.killingCmdr));
        }

        // Null death data means the aircraft flew off the map.
        private static void OnAirUnitRemoved(AirUnitCore air, AirUnitDeathEventData aded)
        {
            if (aded == null || log == null)
            {
                return;
            }
            Guard(() =>
            {
                string type = Names.AircraftType(air);
                log.Record(Kind.Aircraft, air.caller, type, type, aded.killer, aded.killingCmdr);
            });
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (errorsLogged++ < 20)
                {
                    KillTrackerPlugin.Log.LogError(e);
                }
            }
        }
    }
}

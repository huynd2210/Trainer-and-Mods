# Manual Missile Control 1.0.0

BepInEx 5 mod for Nuclear Option 0.34.1. Fly your own missiles by hand from a third-person chase camera.

## Use

1. Fire any missile (AGM, AAM, cruise missile, glide bomb).
2. Press **F6**. The view jumps behind your newest missile still in flight.
3. Steer with the controls you already fly with:
   - **Mouse / view axes** (Pan View, Tilt View) move the commanded direction the same way cockpit free-look moves your head. The camera follows it, so you look where you want the missile to go.
   - **Stick / keys** (Pitch, Roll or Yaw) swing the commanded direction at up to 35°/s. They follow your flight bindings: push forward to dive, roll right to turn right.
   - **FOV** axis zooms.
4. Put the **cross** on the target. The **diamond** shows where the missile's current path meets the ground. It converges on the cross as the missile turns. The bottom line shows weapon, speed, and range and time to the cross.
5. On impact the view stays on the explosion for 2 s, then returns to the cockpit. Press **F6** mid-flight to hand the missile back to its own seeker and return to the cockpit at once.

While you steer, your aircraft receives neutral stick and keeps its throttle. Fire, weapon, gear and eject commands are ignored. Level off before you take over.

## Behaviour

- Each physics tick, after the missile's seeker has run, the mod replaces the seeker's aim point. The missile's own autopilot, fins, turn-rate and G limits and motor do the flying. A missile can only turn as hard as the game allows.
- Under manual control a seeker cannot self-destruct the missile when it loses or misses its target. Impacts and proximity fuses still detonate it. A missile that runs out of energy falls until it hits something.
- The warhead arms 1 s after launch if the seeker has not armed it already.
- When you hand a missile back, its seeker resumes. A seeker with no target may then self-destruct it, as in the base game.

## Settings

`BepInEx/config/nuclearoption.missilecontrol.cfg` (created on first run):

| Setting | Default | |
| --- | --- | --- |
| Controls.ToggleKey | F6 | Take / release control |
| Controls.StickTurnRateDegrees | 35 | Full-stick turn rate of the command |
| Controls.LookSensitivity | 1 | Multiplier on the game's view sensitivity |
| Controls.MaxLeadDegrees | 40 | How far the command may lead the actual flight path |
| Camera.DistanceScale | 1 | Chase distance (default 3 missile lengths, at least 6 m) |
| Camera.ReturnDelaySeconds | 2 | Time spent watching the impact |
| Guidance.AimPointDistanceMetres | 10000 | Aim point distance given to the missile autopilot |

## Limits

Single player and multiplayer host only: missiles are simulated on the host, so clients get "only the host can steer missiles". Untested in multiplayer.

## Build

With the game closed and .NET 8 installed, from the game folder:

```
powershell -ExecutionPolicy Bypass -File .\ModSource\MissileControl\build.ps1 -Install
```

This builds against the installed game, runs the steering-math tests and installs to `BepInEx/plugins/MissileControl`. `package.ps1` makes the Mod, Dist (with BepInEx) and Source zips.

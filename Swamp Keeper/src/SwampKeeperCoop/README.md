# Swamp Keeper v1.05 online co-op

Two players on two Windows PCs share one run. Each has a separate native player actor with its own movement, arrows, skills, items and kills. The host's game simulates everything; the guest's game stops its own simulation, renders the host's world from snapshots and sends its input. Enemies and bosses fight both players, and the run progresses through every stage and the Well.

Player documentation is in `PLAYER-README.md` (shipped as `SwampKeeperCoop-README.md`), with `SwampKeeper-Radmin-Guide.html` for VPN play.

## Status

All acceptance gates pass as two separate game processes talking over TCP. They have not yet run on two separate PCs; see the last section.

Passed on 2026-10-20 with the final build (two game processes, private desktop): basic session, gameplay, progression, preparation, death/restart, paths, Well, bosses, effects, network, menu. 38 protocol checks pass. `dist/` holds the three zips. Not yet run: the `--install` gate (fresh game copies from `-Pack.zip`), added after the zips were built.

## How it works

- **Transport** (`Protocol/`): authenticated, version-checked TCP on port 27856. Bounded frames and queues; the latest world snapshot supersedes stale ones. No Unity API runs on network threads. Heartbeat pings measure round-trip time; 8 s of silence closes a dead (half-open) link. The same player rejoining replaces a stale link; any other player is refused.
- **Host** (`Runtime/Plugin.cs`): clones the native `Player` into a second actor. Ownership scopes (`Ownership.cs`, `OwnedEffects.cs`, `EnemyTargets.cs`) swap the native `Player.player` singleton and input source around every callback of each actor, its projectiles, skills, items, pooled helpers and coroutines. Enemies and bosses act against the nearer living player; kill credit follows the last hit.
- **Guest**: freezes native simulation and renders replicated sprites (`WorldReplication.cs`); missing sprites are transferred as PNGs.
- **Run flow** (`RunTransitions.cs`, `TeamLife.cs`): readiness-gated start, shared stage transitions, per-player death and next-stage revival, team wipe, native restart. After a transition the companion is placed on a walkable spot beside the leader and kept off temporary carriers such as the merchant's cart.
- **Swamp Keeper Mods 1.2.0** is a hard dependency: co-op uses its item-preparation UI hooks (`CoopIntegration.cs`). Its kill-tracker integration exists but is out of scope and untested in co-op.

## Acceptance gates

Each gate is a `Gate` subclass (`Runtime/Gates.cs` registry; one file per gate) that runs only under `-coop-integration-test` in the sandbox copies. `run_pair.py --<gate>` launches host and guest on a private desktop and restores the user's saves afterwards.

| Gate | Command | What passes |
|---|---|---|
| Basic session | `run_pair.py` | Independent movement and attacks over the network; world replicated with no missing sprites |
| Gameplay | `--gameplay` | 18 skills smoke-tested, 40 items isolated, shared transition |
| Progression | `--progression` | Shared transition plus runtime sprite transfer |
| Preparation | `--preparation` | Readiness gate; separate item/skill loadouts chosen in the real grid |
| Death | `--death` | Independent deaths, next-stage revival, team-wipe screen, native restart |
| Paths | `--paths` | Preparation → all 11 stages through their native exits (exit triggers, merchant after each boss) with guest and host taking turns, each boss killed in-run, run-clear screen |
| Well | `--well` | Guest enters the Well from preparation; three trials (one with a Well boss) started from the Well's button and cleared by alternating players; a single fall keeps the run going; team wipe reaches the Well's run-end screen (the Well has no exit trigger) |
| Bosses | `--boss` | Bear, Fox, Tiger: always target the nearer living player and land attacks on whichever player is near; killed by the guest with the in-game kill credited to the guest actor only; stage ends natively; Tiger reaches run clear |
| Effects | `--effects` | Each of the 40 items changes only its owner, identically on both actors; item-triggered effects and all 18 skills used by real guest network input never touch the host |
| Network | `--network` | ~250 ms RTT play through a latency proxy (host upload ~350 KiB/s); hard cut and silent stall both detected (0.1 s and 8 s); host pauses; guest rejoins via the F7 Join button; actor, items, kills and run identity preserved |
| Menu | `--menu` | No `-coop-role`: menu opened through the F7 handler's `Toggle()`, Host/Join/item/Ready clicked through EventSystem raycasts (fails if anything covers a button), real connection flow, run entered, guest moves with its own loadout |

Evidence from the passing runs is under `evidence/<gate>/`.

## Reproduction

```powershell
dotnet run --project ModsSource/tests -c Release
dotnet run --project ModsSource/SwampKeeperCoop/ProtocolTests -c Release
dotnet build ModsSource/SwampKeeperCoop/Runtime -c Release
python ModsSource/SwampKeeperCoop/run_pair.py --paths
```

Run one gate at a time (`run_one.sh` refuses to start while a game process is alive). Debug-only options `path-from=N` and `boss-from=N` skip ahead; runs using them do not save evidence.

Builds deploy only to `ModsSource/coop-investigation/sandbox`. Build Swamp Keeper Mods with `-p:GameDir=<sandbox>`: its default deploy target is the normal game install.

## Packaging

`powershell -File ModsSource/SwampKeeperCoop/package.ps1` builds both plugins into the sandbox, runs the storage and protocol tests, and writes `dist/`:

- `SwampKeeperCoop.zip`: both plugins (co-op and the Swamp Keeper Mods 1.2.0 it requires), player README, Radmin guide.
- `SwampKeeperCoop-Pack.zip`: the same plus BepInEx 6.0.0-be.789 IL2CPP x86, the Unity base libraries (offline first launch), the verified `BepInEx.cfg` and `INSTALL.txt`.
- `SwampKeeperCoop-Source.zip`: co-op, Swamp Keeper Mods, tests and tools.

No zip carries a `local.swampkeeper.coop.cfg`, so each install creates its own player identity.

## Notable fixes from the gate work

- A second native transition (e.g. a double-confirmed difficulty popup) stacked a second control lock on the guest, leaving it frozen for the stage.
- After a boss, the host rides the merchant's cart into the next stage. The guest was parented to the cart and switched off with it; it is now kept on its own parent and regrouped beside the host when the ride ends.
- Dropping an actor inside scenery made the Fox's dash-target search loop forever and froze the host. Companion placement now snaps to the navmesh.
- A silently dead link was never detected. Heartbeats now close it after 8 s, and the same guest can rejoin at once.
- While disconnected, the guest's F7 menu showed the loadout instead of the Join form it asked the player to use. It now shows Join, prefilled.
- Native `ChangeSkill` hands out SkillManager's single instance of a skill. If both players picked the same skill, they shared one object (charge, cooldown, owner); the guest's Horn emptied the host's Horn. The guest now gets its own copy.
- Arrows that items fire straight from a pool (not through `Player.ShootArrow`) had no owner, so their hits were credited to the host. Projectiles now fall back to the spawn-ownership registry.
- A send failing because the link had just died threw and ended the session instead of starting recovery. Unanswered sprite requests are now re-sent after a rejoin.
- A sprite destroyed before the guest's request arrived made the host throw while exporting it; the host now answers with a blank.
- The guest streamed input before the host was simulating, which could back up the queues during loading; input now starts with the first snapshot.
- The guest drew every sprite upside down. Il2CppInterop's generated `SpriteRenderer.flipX`/`flipY` getters marshal the native 1-byte bool as a 4-byte `BOOL`, so they read true for every sprite. Both flags are now read through the ICall with a byte return (`SpriteFlags`), and `Verify()` fails if every replica is flipped. The gates passed with this bug because they compared positions only; the guest screenshot showed it.
- The guest had no way back to the title screen: its native pause menu is frozen with the rest of its game. Esc now opens the co-op menu, whose **Quit to title** relaunches the game once this process has exited. The menu gate clicks that button; tests skip the relaunch.

## Not yet verified

- Two separate PCs over LAN, VPN or the internet. Every gate uses two processes on one PC; the network gate adds latency and faults through a local proxy.
- Hands-on play by people. Gates drive native code paths (input, interactions, triggers, damage) programmatically.
- The F7 key itself. The hidden test desktop has no keyboard, and the Input System's generic `QueueStateEvent` gets a null device through Il2CppInterop, so the menu gate calls the handler's `Toggle()` directly.
- Kill-tracker (lifetime counter) behaviour in co-op: out of scope.
- Sound on the guest (none plays) and animation fidelity beyond replicated sprites.

Validated native build: Unity 2022.3.62f2, 32-bit IL2CPP, BepInEx 6 build 789. `GameAssembly.dll` SHA256 `A9A9AE88DCF5089F9530DA833ECAB459B7213C55CA5A3CD0E0F3DB0BA1461B4B`; the runtime refuses any other binary.

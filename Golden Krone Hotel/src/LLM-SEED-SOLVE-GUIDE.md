# Golden Krone Hotel: LLM seed-solving harness

Use this guide when the user asks you to solve or explore a seed with
**omniscience, foresight, and undo/redo**. The tools below expose the installed
seed-aids harness directly. Follow `AGENTS.md` and the user's objective; use
`LLM-TOOL-GUIDE.md` for ordinary movement, menus, combat, and targeting.

This is an assisted seed-solving workflow. You can inspect hidden information
on the current floor, simulate one next movement/attack/wait, and explore
alternative histories. The harness does not contain an LLM, search algorithm,
automatic win solver, or arbitrary multi-turn simulator.

There are two distinct tool families. Ordinary `gkh_observe` / `gkh_act`
provide player knowledge and unrestricted normal inputs. Seed `gkh_seed_observe`
and **`gkh_seed_act`** provide cheats and mandatory one-move death prevention.
Use the seed action tool for every gameplay input in this workflow. Never fall
back to ordinary `gkh_act` to bypass a refused action.

Game directory:

```text
C:\Program Files (x86)\Steam\steamapps\common\Golden Krone Hotel
```

## Connect and check availability

Launch/restart the normal modded game so the updated bridge and seed aids load.
Use an ordinary, non-tutorial run. An already running session with older scripts
will not gain these endpoints merely by restarting the Python adapter.

Remote actions, menu choices, and undo/redo work with the game minimized or behind
other apps. Background support drives the real game queue without focusing the
window. Keep using the guarded seed tools; the one-move death check still runs
before any action is executed. Idle and read-only requests do not spend turns.

When the game is fully closed, `python llm-play/start-background.py` starts it
minimized without activating its window. Do not run it while an instance is
already open. The helper restores the launch manifest after startup. NW.js may
show several `gkh.exe` helper processes for one game instance.

Acquire exclusive ownership before playing: `gkh_control` with
`{"enabled":true,"exclusive":true}` or `python llm-play/gkh.py control takeover`.
Read a fresh revision afterward. Local keyboard/mouse/controller inputs (including
hover and seed-panel undo/redo) are blocked while the user watches. Remote guarded
inputs, forecasts and timeline tools still work. The Watch only panel's **Take
control** button pauses remote play and unlocks input after the accepted move
finishes. Do not automatically reclaim control after a human handoff.
Pause retains ownership; `control release` pauses and unlocks. A game restart
preserves the lock and initially pauses remote actions.

From that directory:

```powershell
python llm-play/gkh.py state
python llm-play/gkh.py seed-state
python llm-play/gkh.py seed-forecast
```

`state` is the ordinary observation, with a `seedHarness` status object added.
`seed-state` returns a normal observation under `state`, plus `seed` and
`omniscience`. `seed-forecast` reads the latest revision and computes a forecast.
To tie a forecast to a particular observation, pass its revision explicitly:

```powershell
python llm-play/gkh.py seed-forecast --revision 42
```

Replace `42` with the revision you actually read. Seed aids normally activate
automatically. If they are inactive in an idle, non-tutorial run, use the
`enable` operation described below. This starts history at the current position;
it does not reconstruct previous turns.

## MCP tools

The same stdio adapter/configuration in `LLM-PLAY.md` now offers these additional tools:

| Tool | Arguments | Result |
| --- | --- | --- |
| `gkh_seed_observe` | `{}` | Current-floor omniscience, true carried potion identities, normal state, and timeline status |
| `gkh_seed_forecast` | `{"expectedRevision":42}` | Nine next-input alternatives, plus normal state |
| `gkh_seed_act` | Same command/choose/target fields as `gkh_act`, including revision and requestId | Exact-input death check, then real input if safe |
| `gkh_seed_control` | `{"operation":"undo","expectedRevision":42,"requestId":"solve-undo-001"}` | One timeline operation and the resulting state |

Control operations are `enable`, `undo`, and `redo`. Use `gkh_seed_act` for real game
inputs. The `gkh_control` tool still pauses/enables **remote actions**, separately
from activating seed history. Solver observations/forecasts remain readable while
remote actions are paused. Respect the user's pause request.

The seed tools work independently of whether the panel's map/foresight display
toggles are on. They do not change those toggles or alter normal game visibility.
The panel hides during ordinary game menus to keep choices and ring notes
readable; tool observation/undo remain available. Ring-acquisition previews run
the pickup/menu logic without painting a UI canvas, then restore world and RNG.

For strict tool separation, configure the adapter arguments as `mcp --tool-mode
normal` or `mcp --tool-mode seed`. Normal exposes only observe/act/control;
seed exposes only seed observe/act/forecast/control and remote pause control.
The default `mcp` exposes both families. Seed-only mode requires a run already
started using ordinary tools or by the user.

## Mandatory one-move death check

`gkh_seed_act` / CLI `seed-play` / HTTP `POST /seed/action` simulate the exact
submitted input with the real engine and current RNG, then restore the world
and RNG before executing it. A fatal next move returns an error containing
`Refused: this action leads to death on the next move`. No turn, ammunition,
potion, mana, or history position is spent by a refusal. A simulation error or
unavailable check also refuses execution; it does not fall back to normal input.

The check includes movement, adjacent attacks, WAIT, aimed shots/casts, and
potion/menu choices. Opening a menu or aiming can be allowed without spending
a turn; each subsequent choice/confirmation is checked separately. It does not
predict later turns after an unfinished choice or promise a safe whole route.
It allows nonfatal damage and winning outcomes. `REST`, `shift:true`, and actions
requiring additional automatic turns are refused; use individual WAIT/movement
inputs. Redo refuses to restore a recorded dead position. Undo remains available
to recover a death caused by ordinary tools or manual play.

The seed panel disables fatal forecast buttons and checks its movement buttons
again when invoked. Ordinary keyboard/game inputs and `gkh_act` retain ordinary
death behavior. Only seed tools provide the mandatory guard.

The gameplay CLI is `python llm-play/gkh.py seed-play '<gameplay JSON>'`.
`seed-action` remains the CLI for enable/undo/redo operations.

## Read the full current floor

`gkh_seed_observe` / `seed-state` returns:

```text
state               Ordinary observation, including revision and seedHarness
seed                Current game seed value
scope               Current floor, including unseen terrain and creatures
omniscience.tiles   Every tile on that floor, with absolute x/y coordinates
omniscience.potions Carried potion labels, trueName, count, and identified flag
```

Each tile includes terrain type/name, passability, light/sunlight/moonlight, and
`visible`. It can include an item and a creature with name, HP/MP, level,
friendliness, statuses, action state, and a carried item's true name.
For floor items, `name` uses a true name when the game object supplies one;
`playerName` records the ordinary label.

Here, `visible:false` means **current, hidden information**, not stale memory.
In the ordinary `state.tiles` array, `visible:false` still means remembered,
possibly stale terrain. Keep those meanings separate. Reading omniscience does
not populate ordinary terrain memory or identify potions in the game.

Coordinates increase east/right for `x`, south/down for `y`. The player position
is `state.player.x/y`; the floor is `state.floor.index`. Do not reuse a route's
coordinates after changing floors. Omniscience is limited to the current floor;
it does not reveal unvisited future floors or make unseen targets legal. Normal
target actions still require a currently visible tile.

Plan routes using hazards, light, enemies, resources, and stairs. A tile marked
passable may still be dangerous. Re-read the map after combat, a floor transition,
or undo/redo rather than planning from a previous branch's positions.

## Read one-turn foresight

Before a real movement decision, obtain a fresh normal/seed observation and pass
its revision to `gkh_seed_forecast`. It runs the real next input from the current
RNG state, then restores the world and RNG. Preview storage/achievement effects
are suppressed. It does not consume a turn or create an undo position.

The nine candidates are the eight compass directions plus `WAIT`. Each record
names `command`, `dx`, `dy`, and `label`, with applicable outcome fields:

| Field | Meaning |
| --- | --- |
| `disabled` | This diagonal is disabled by the game's movement option; outcome fields can be absent |
| `hp` | Signed player HP change: negative means loss, positive means gain |
| `dead` | Preview reaches death |
| `damage` | Observed HP loss of the adjacent target during that candidate |
| `attacked` | Harness flags an action against an adjacent hostile target |
| `kill` | Adjacent target ends dead/destroyed |
| `noticed` | A previously unengaged hostile creature becomes engaged |
| `prompt` | Simulation stops at a menu/confirmation rather than resolving the choice |
| `blocked` | The candidate did not advance the turn counter |

Inspect missing fields rather than treating every record as a complete outcome.
A zero HP change is only a statement about that next input; it does not promise
the next several turns are safe. A prompt is an unfinished interaction, not a
fully forecast result. Compare the chosen candidate against the actual returned
state/messages after executing its `command` via `gkh_seed_act`.

Foresight covers movement, the adjacent attack triggered by movement, and wait.
It does **not** preview arbitrary spells, potion choices, stairs, revolver shots,
or whole routes. The separate mandatory guard in `gkh_seed_act` checks the exact
submitted spell/potion/stair/revolver input before playing it. Use that tool and
undo for safe experiments. A menu,
aiming mode, conversation, or other pending choice can make foresight unavailable.
Finish the interaction or undo it before requesting another forecast.

## Inspect the timeline before travelling

The normal observation's `seedHarness` object includes:

| Field | Meaning |
| --- | --- |
| `active` | Seed history is active |
| `boundary` | Harness permits stable inspection/time travel |
| `canAct` | Bridge can check a gameplay input, including closing a stable map/help view |
| `choosing`, `pending` | Interaction/unfinished action status |
| `cursor` | Zero-based current recorded position |
| `length`, `limit` | Stored positions and capacity (currently 150) |
| `canUndo`, `canRedo` | Whether that one-step operation is available |
| `canForecast` | Whether one-turn foresight is available now |
| `unavailable` | Updated harness API is absent; restart the game |

Undo/redo use the same revision and request-ID protections as ordinary actions.
Check `canUndo`/`canRedo`, send **one** operation, then inspect its returned state.
There is no jump-to-cursor operation; reach an older position by repeated,
separately observed undo calls. As capacity is exceeded, the oldest position is
dropped and indices shift, so a cursor number is not a permanent checkpoint ID.

Undo normally restores the previous recorded position. If an action has an
unfinished menu/targeting choice, undo cancels that action back to its starting
position without necessarily decrementing `cursor`. Several targeting/menu
inputs can belong to one recorded action. Undo can recover across stairs and
death. During a delayed death screen, `state.busy` may be true while
`seedHarness.canUndo` is true; the bridge permits that recovery operation.
An outstanding bridge request must still finish before another operation.

Redo restores the next recorded position. A committed world-changing action
after undo replaces the redo tail. If you want to return to the existing future,
redo before exploring a different branch. Reloading/closing the game loses
history; ordinary game saves retain seed-aid activation and RNG, not the undo tree.

**Undo does not erase request IDs.** To replay a gameplay command after undo,
generate a **new** ID and use the restored state's current revision. Reusing the
old command's payload/ID returns its cached old response and does not play it
again. Retry an identical ID only to recover a lost response for that same request.
The cache holds the last 256 accepted actions and does not survive reload.

## A practical branch-solving loop

1. Read seed state and establish the objective, current floor, player condition,
   and timeline position. Keep a short external note of the branch you are testing.
2. Inspect the whole floor to select a route or encounter worth exploring.
3. Before movement/adjacent attacks/wait, request foresight and compare HP,
   death, target damage, notice, and prompt outcomes.
4. Take one real action with the current revision and a new request ID. Resolve
   its choices and inspect the outcome; do not assume a submitted input succeeded.
5. If the branch is worth keeping, continue. If it failed, use undo to return to
   the intended position, observing after each step.
6. To restore an existing future, redo. To test another branch, submit a fresh
   gameplay action and accept that its committed outcome discards the redo tail.
7. Refresh omniscience and foresight at the restored position. Record useful
   decisions/results outside game history so they survive experimentation.

For an ability/potion/stair experiment, note starting stats, resources, floor,
turn, and cursor. Complete the real interaction, inspect the result, then undo
the recorded action(s) if you want to reject it. The harness is a single timeline
with a redo tail, not a library of independently restorable named branches.

## Shell/Python control example

The CLI also supports `seed-action` with a JSON operation payload. To avoid
native shell JSON-quoting differences, use the same import approach as the
ordinary guide. This PowerShell example deliberately sends an undo; run it only
when that is your intended action. Replace `42` with your observed revision:

```powershell
@'
import importlib.util
import json
import uuid

path = r'C:\Program Files (x86)\Steam\steamapps\common\Golden Krone Hotel\llm-play\gkh.py'
spec = importlib.util.spec_from_file_location('gkh_client', path)
gkh = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gkh)
bridge = gkh.Bridge()
action = {'type': 'seed', 'operation': 'undo',
          'expectedRevision': 42, 'requestId': uuid.uuid4().hex}
print('Action request: ' + json.dumps(action), flush=True)
print(json.dumps(bridge.call('/seed/control', action), ensure_ascii=False))
'@ | python -
```

Python reads are `bridge.call('/seed/state')` and
`bridge.call('/seed/forecast?revision=' + str(observed_revision))`.
For MCP, submit the action fields to `gkh_seed_control` **without** `type`;
the adapter adds `type:'seed'` internally.
Gameplay requests go to `/seed/action` with type command/choose/target, or to
`gkh_seed_act` with the same fields. `/action` is ordinary play and rejects
timeline operations.

## Limits and recovery

- Foresight simulations suppress external effects, but **real experimental
  actions remain real**. Achievements, save behavior, morgue exports, and
  permanent progression remain enabled. Undo restores gameplay world/inventory/
  statuses/creatures/RNG and retains earned permanent progression; it does not
  reverse already emitted external effects or refund time spent thinking.
- Undo/redo preserve gameplay RNG while rebuilding the HUD. The first bridge
  action anchors the settled starting position so startup initialization does
  not leave the initial history position stale.
- If a forecast is unavailable, check `canForecast`, `pending`, `choosing`, and
  normal state. Wait for processing, finish a choice, or undo as appropriate.
- On stale revision, observe and reconsider. On a lost reply, retry the identical
  payload/ID in the same game session/cache. On `settled:false`, observe until
  processing finishes; do not duplicate the action with a new ID.
- Do not replay an uncertain old request after reload/cache expiry. Inspect
  current state and report any unresolved ambiguity to the user.
- Ordinary read/action tools remain usable without opting into omniscience.
  Preserve the user's requested play mode and stop/pause when asked.

Automated engine checks are in `mod-tests/verify-llm-seed.py`. Setup, authentication,
normal controls, and removal are documented in `LLM-PLAY.md` and `SEED-AIDS.md`.

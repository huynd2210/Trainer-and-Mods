# Direct LLM play mod

For step-by-step instructions addressed to another LLM, read
[LLM-TOOL-GUIDE.md](LLM-TOOL-GUIDE.md).
For the seed-solving harness with omniscience, foresight, and undo/redo, read
[LLM-SEED-SOLVE-GUIDE.md](LLM-SEED-SOLVE-GUIDE.md).

Installed in `package.nw/index.html` as one extra script: `js/llm-bridge.js`.
Launch the normal Steam game / `gkh.exe`. Restart an already open game to load it.
The bridge runs locally while the game is open. It does not contain a model,
send data to a provider, require an API key, or make decisions by itself.
An LLM chooses actions using the tools below. Existing seed aids remain installed.

The game can stay minimized or behind other apps during remote play. Accepted
actions use a Node timer to advance the existing game engine when its rendering
loop is suspended; delayed menu and stair callbacks also run in the background.
The bridge never activates the window or sends desktop keyboard/mouse input.
Idle observations do not advance turns. Restart once after installing this update;
leave the game process running, then switch to your other work.

Before LLM play, acquire **exclusive control** with `control takeover`. The game
then ignores local keyboard, mouse (including hover), touch, and controller input.
You can Alt-Tab in to watch, resize/minimize it, and keep doing other work.
A small **Watch only** panel has a **Take control** button: it pauses remote
actions and unlocks your input after any accepted move finishes. This applies to
both normal and seed modes; the death guard remains exclusive to seed actions.
The lock survives reopening the game, with remote play initially paused.

For this chat, launch the game and say **“play the game using the LLM bridge.”**
The assistant can use the included CLI directly, without screen clicking or an
MCP configuration change. This plays your actual run: normal saving, death,
achievements, and progression apply to real actions.

## CLI (Python 3.9 or later, no additional packages)

Run from this game directory in PowerShell:

```powershell
python llm-play/gkh.py state
python llm-play/gkh.py control takeover
python llm-play/gkh.py action '{"type":"command","command":"WAIT"}'
python llm-play/gkh.py action '{"type":"command","command":"UP"}'
python llm-play/gkh.py action '{"type":"choose","index":0}'
python llm-play/gkh.py control pause
python llm-play/gkh.py control enable
python llm-play/gkh.py control release
```

The CLI fills in a request ID and the latest observation revision when omitted.
An LLM should supply the revision it actually used to decide, so intervening
player input makes stale actions fail. The CLI prints the exact action payload
to stderr before sending it. If a reply is lost, reuse that identical payload
and request ID; do not generate a new ID and risk taking the turn twice.

## MCP adapter for another LLM client

Configure a **stdio** MCP server with your Python executable as the command and
these arguments (the path is absolute and contains spaces, so keep it one argument):

```json
{
  "mcpServers": {
    "golden-krone-hotel": {
      "command": "C:\\Users\\Admin\\AppData\\Local\\Programs\\Python\\Python312\\python.exe",
      "args": [
        "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Golden Krone Hotel\\llm-play\\gkh.py",
        "mcp"
      ]
    }
  }
}
```

Client configuration layouts vary; use these command/args in its MCP settings.
The adapter offers `gkh_observe`, `gkh_act`, `gkh_control`, `gkh_seed_observe`,
`gkh_seed_forecast`, `gkh_seed_act`, and `gkh_seed_control`. It implements
newline-delimited JSON-RPC stdio and the tools interface described by the
[MCP transport specification](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports)
and [MCP tools specification](https://modelcontextprotocol.io/specification/2025-03-26/server/tools).

These are two separate tool families. Normal observe/act uses player knowledge
and ordinary death behavior. Seed observe/forecast/act/control adds omniscience,
undo/redo, and mandatory one-move death prevention on **every seed gameplay input**.
Use `gkh_seed_act` (CLI `seed-play`) for seed gameplay. It refuses fatal inputs,
REST/fast movement, and unavailable checks without spending resources or a turn.
Ordinary `gkh_act` has no death guard and refuses timeline cheat operations.

To expose just one family, append `--tool-mode`, `normal` or `seed` after `mcp`
in the adapter args. Default mode exposes both. Seed-only mode needs an already
started run. See the seed guide for choices, refusals, and retry handling.

Suggested instruction to the controlling LLM:

> Play Golden Krone Hotel one action at a time. Read gkh_observe before choosing
> an action. Use expectedRevision from that observation and a unique requestId.
> Wait and observe again while busy. Inspect the returned state and messages;
> settled means input processing finished, not that your intended move succeeded.
> Navigate startup, menus, hints, potions, and choices with the exposed controls.
> Avoid REST or shift movement unless you intend to spend multiple turns.

## Observations and actions

- Coordinates: `x` increases east/right, `y` increases south/down; these are
  absolute floor tile coordinates. `player.x/y` locates the player.
- Observations include state, revision, busy/enabled flags, game time, player
  stats and statuses, carried potion names using the game's identification,
  learned abilities, messages, dialogue, hints, targeting state, title entries,
  class details, rendered menu labels, and zero-based menu choice indices.
- `tiles` contains currently visible terrain and terrain previously observed
  through the bridge. `visible:false` is stale terrain memory, with no current
  item or creature information. Visible creatures include name, HP, and
  friendliness. Unknown terrain is omitted. Memory starts when the bridge first
  observes a run and is cleared on reload or when the player object is replaced.
  Existing omniscience/foresight UI does not expand bridge observations.
- `{"type":"command","command":"UP"}` sends a normal game input. Read the
  `commands` array for available names; legality still depends on game state.
  Movement attacks adjacent enemies normally. `CONTEXTUAL_ACTION` uses stairs,
  torches, and other contextual interactions. Ability commands are numbered 1–4.
- `{"type":"choose","index":0}` activates the current menu's first choice
  through the same callback as a normal menu selection.
- To aim, send `REVOLVER`, `EXAMINE`, or an ability command, then
  `{"type":"target","x":12,"y":8}`. Targeting only aims at a visible tile.
  Send `CONFIRM_TARGET` to fire, cast, or examine. `ESCAPE` cancels; a displayed
  hint can consume the first Escape, so inspect state before continuing.
  Directional spell targeting is snapped by the game's diagonal movement option.
  Direction commands during spell targeting cast immediately, as in normal play.
- REST can spend up to 50 turns; `shift:true` on movement uses the game's fast
  movement. Inputs can start animations, dialogue, or mandatory choices.
- Every action requires `expectedRevision` (integer) and `requestId` (1–80
  letters/digits/underscores/hyphens). Concurrent actions and busy game states
  are rejected. Responses wait up to 15 seconds for processing. If `settled:false`,
  poll state until idle. The last 256 action IDs are retained during this game
  session. Do not retry old IDs after a reload or after they leave this cache.
- Pause prevents new remote actions; an already accepted action completes.
  Observation continues. Pause retains an exclusive input lock; `control release`
  (or the Watch only panel's Take control button) pauses and returns local input.
  `control enable` retains the current ownership; `control takeover` explicitly
  enables remote input and acquires the lock. Without exclusive ownership, local
  play remains available. Observations include `exclusive` and `releasing` flags.

## Local HTTP interface

The game binds an OS-assigned port on `127.0.0.1` and writes a per-launch bearer
token and URL to `%LOCALAPPDATA%\GoldenKroneLLM\connection.json`. The CLI/MCP
adapter reads this automatically and ignores proxy settings for local requests.
Keep that file private. The HTTP bridge accepts:

- `GET /state`
- `POST /action` with the action JSON above
- `POST /control` with `{"enabled":false}` or `{"enabled":true}`
  retains ownership. Add `"exclusive":true` to acquire the input lock, or send
  `{"enabled":false,"exclusive":false}` to pause and release. Release waits for
  accepted work to finish; observe until `releasing:false` and `exclusive:false`.
- `GET /seed/state` for current-floor omniscience and true carried potion identities
- `GET /seed/forecast?revision=<observed revision>` for one-turn foresight
- `POST /seed/action` with gameplay action JSON, for mandatory one-move death prevention
- `POST /seed/control` with `{"type":"seed","operation":"undo"}` (or `redo`/`enable`),
  plus the usual `expectedRevision` and `requestId`

Each request needs `Authorization: Bearer <token>` and the correct localhost
Host header. Requests with an Origin header are rejected; CORS is not enabled.
The server accepts no remote connections or arbitrary code execution and limits
request bodies to 8 KiB. Errors return JSON with an `error` field. Only one game
instance is supported by the default connection file.

## Verification and removal

Automated checks use isolated headless browser storage, a Node transport fixture,
and a hidden disposable NW.js fixture app. They do not use a real saved run for
gameplay assessment. An independent agent reviews a rendered game capture.
Full live Steam gameplay and model strategy quality are separate from those checks.

To repeat engine tests, run `python -m http.server 8765 --bind 127.0.0.1 --directory package.nw`
in one terminal and `python mod-tests/verify-llm.py` in another (requires Playwright
and its Chromium installation). Transport tests use `python mod-tests/verify-llm-transport.py`
(Node required). Bundled-runtime tests use `python mod-tests/verify-llm-nw.py`.
Seed-harness engine tests use `python mod-tests/verify-llm-seed.py` with that same HTTP server.
Background engine tests use `python mod-tests/verify-llm-background.py` with the
same server and deliberately stop RAF. `python mod-tests/verify-llm-background-nw.py`
tests the full engine in a hidden disposable copy of the bundled NW.js runtime,
with separate storage, no Steam integration, and no window activation.
Exclusive-input checks use `python mod-tests/verify-llm-exclusive.py` with the
same HTTP server: real keyboard/mouse events in a headless fixture, controller
polling, locked menu choices, undo/redo, death refusal, and release during a move.
The transport test also checks exclusive control and ownership after restart.
`python mod-tests/verify-llm-ring.py` checks ring pickup with no preview canvas,
real icon rendering, undo/replay, and preservation of existing canvas pixels.

To remove the mod, close the game and remove only
`<script src="js/llm-bridge.js"></script>` from `package.nw/index.html`.
The pre-install HTML is backed up at `mod-backup/index-before-llm-bridge.html`.
Restoring that file is appropriate only if you have not edited the HTML since.
Steam updates or file verification can overwrite the script registration.

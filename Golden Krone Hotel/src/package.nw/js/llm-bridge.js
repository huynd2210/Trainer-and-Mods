/* Local, provider-independent LLM control. No model or API key is embedded. */
(function () {
    'use strict';
    if (window.gkhLLM) return;
    var commands = ['UP','DOWN','LEFT','RIGHT','NORTHWEST','NORTHEAST','SOUTHWEST','SOUTHEAST',
        'WAIT','REST','CONTEXTUAL_ACTION','CHAT','ABILITY_1','ABILITY_2','ABILITY_3','ABILITY_4',
        'REVOLVER','CYCLE_LEFT','CYCLE_RIGHT','EXAMINE','ESCAPE','CONFIRM','MENU','CHARACTER_SHEET',
        'NOTIFICATION_LOG','HOTEL_MAP','MINIMAP','BOOKS_AND_LETTERS','QUICK_POTIONS','POTIONS',
        'HELP','OPTIONS','TITLE_SCREEN','RESTART','CONFIRM_TARGET'];
    var enabled = true, pending = false, revision = 0, signature = '', run = null;
    // Ownership is deliberately outside seed snapshots: undo cannot unlock local input.
    var exclusive = false, releasing = false, persistControl = null, watchPanel = null;
    function clearLocalInput() {
        if (typeof input !== 'undefined') input.inputCache = {};
    }
    function renderControl() {
        if (!watchPanel) return;
        watchPanel.style.display = exclusive ? 'block' : 'none';
        watchPanel.querySelector('span').textContent = releasing ? 'Finishing move...' :
            (enabled ? 'LLM control - Watch only' : 'LLM paused - Watch only');
        watchPanel.querySelector('button').disabled = releasing;
        positionControl();
    }
    function positionControl() {
        if (!watchPanel) return;
        var stats = document.querySelector('#stats-container'), rect = stats && stats.getBoundingClientRect();
        watchPanel.style.top = (rect && rect.height ? rect.bottom + 10 : Math.max(20, window.innerHeight / 2)) + 'px';
    }
    function finishRelease() {
        if (!releasing || pending || busy()) return;
        exclusive = false; releasing = false; clearLocalInput();
        if (persistControl) persistControl();
        renderControl();
    }
    function waitForRelease() {
        finishRelease();
        if (releasing) defer(waitForRelease, 40);
    }
    function control(value, lock) {
        if (typeof value !== 'boolean') fail('enabled must be boolean');
        if (lock !== undefined && typeof lock !== 'boolean') fail('exclusive must be boolean');
        if (lock === false && value) fail('Releasing local control requires enabled:false');
        enabled = value;
        if (lock === true) { exclusive = true; releasing = false; clearLocalInput(); }
        if (lock === false) { releasing = true; waitForRelease(); }
        if (persistControl) persistControl();
        renderControl();
        return observe();
    }
    function localEvent(e) {
        if (!exclusive) return;
        var onRelease = watchPanel && watchPanel.contains(e.target) &&
            (e.target === watchPanel.querySelector('button') || e.target.closest && e.target.closest('button') === watchPanel.querySelector('button'));
        // Stop at window capture, before game/menu/seed-panel handlers. No OS hooks:
        // Alt-Tab, window controls, focus, resize and rendering remain available.
        e.stopImmediatePropagation();
        if (!(onRelease && /^(pointerdown|pointerup|mousedown|mouseup)$/.test(e.type))) e.preventDefault();
        if (onRelease && e.type === 'click' && !releasing) control(false, false);
    }
    ['keydown','keyup','keypress','click','dblclick','contextmenu','mousedown','mouseup','mousemove',
        'mouseover','mouseout','mouseenter','mouseleave','pointerdown','pointerup','pointermove','pointerover','pointerout',
        'pointerenter','pointerleave','pointercancel','touchstart','touchmove','touchend','touchcancel',
        'wheel','mousewheel','beforeinput','input','change','submit','paste','dragstart','drop'].forEach(function (name) {
        window.addEventListener(name, localEvent, {capture:true, passive:false});
    });
    function installWatchPanel() {
        if (!document.body || watchPanel) return;
        watchPanel = document.createElement('div');
        watchPanel.id = 'llm-watch-control';
        watchPanel.style.cssText = 'position:fixed;left:20px;z-index:2147483647;box-sizing:border-box;' +
            'width:355px;max-width:calc(38vw - 40px);padding:10px;background:#0c1016;border:1px solid #9c8250;' +
            'border-radius:6px;color:#f2ce80;font:13px Arial,sans-serif;';
        watchPanel.innerHTML = '<span></span><button type="button" style="margin-left:10px;padding:7px 10px;' +
            'border:1px solid #657487;border-radius:3px;background:#25303c;color:white;font:13px Arial,sans-serif;' +
            'cursor:pointer">Take control</button>';
        document.body.appendChild(watchPanel);
        window.addEventListener('resize', positionControl);
        var stats = document.querySelector('#stats-container');
        if (stats && typeof ResizeObserver !== 'undefined') new ResizeObserver(positionControl).observe(stats);
        renderControl();
    }
    window.addEventListener('DOMContentLoaded', installWatchPanel);
    if (document.body) installWatchPanel();
    var memories = new Map(), requests = new Map();
    var nodeTimers = typeof require === 'function' ? require('timers') : null;
    var driving = false, pumpTimer = null;
    function stopPump() {
        driving = false;
        if (pumpTimer !== null) (nodeTimers ? nodeTimers.clearInterval : window.clearInterval)(pumpTimer);
        pumpTimer = null;
    }
    function pump() {
        var A = seedHarness();
        if (!driving || A && A.simulating) return;
        // Read-only requests and idle games never advance the engine.
        if (!busy()) { stopPump(); return; }
        if (typeof advanceGameFrame === 'function' &&
            (typeof lastGameFrameAt === 'undefined' || Date.now() - lastGameFrameAt >= 70)) {
            advanceGameFrame(performance.now(), true);
        }
    }
    function startPump() {
        driving = true;
        if (pumpTimer === null) pumpTimer = (nodeTimers ? nodeTimers.setInterval : window.setInterval)(pump, 40);
    }
    function defer(callback, delay) {
        if (nodeTimers) return nodeTimers.setTimeout(callback, delay);
        var A = seedHarness();
        return A && typeof A.defer === 'function' ? A.defer(callback, delay) : window.setTimeout(callback, delay);
    }
    function seedHarness() {
        return window.seedAids && typeof seedAids.controlState === 'function' ? seedAids : null;
    }
    function plain(html) {
        var e = document.createElement('div');
        e.innerHTML = String(html || '').replace(/<br\s*\/?\s*>/gi, '\n');
        return e.textContent.trim();
    }
    function text(selector) {
        var e = document.querySelector(selector);
        return e && $(e).is(':visible') ? plain(e.innerHTML) : '';
    }
    function scalars(obj, keys) {
        var out = {};
        keys.forEach(function (k) { if (obj && ['string','number','boolean'].indexOf(typeof obj[k]) >= 0) out[k] = obj[k]; });
        return out;
    }
    function busy() {
        return typeof game === 'undefined' || game.state === 'starting' || !!(game.actionQueue && game.actionQueue.length) ||
            !!map.generating || !!menu.preventSelect || !!game.dying ||
            (typeof dramaticEntranceFrames !== 'undefined' && dramaticEntranceFrames > 0) ||
            (typeof bigOneExplosionFrames !== 'undefined' && bigOneExplosionFrames > 0);
    }
    function observe() {
        var s = {version: 1, enabled: enabled, exclusive:exclusive, releasing:releasing, busy: busy() || pending, state: game.state,
            turn: game.turnCount || 0, time: game.time, tutorial: !!game.tutorial, won: !!game.won, commands: commands,
            targeting: {magic: magic.state, forget: magic.forgetState, revolver: game.revolverState, examine: game.examineState,
                chat: game.chatState, quickPotions: game.quickPotionsState, quickPotionIndex: menu.quickPotionIndex,
                selected: game.selectedTile ? {x: game.selectedTile.i, y: game.selectedTile.j} : null},
            menu: {open: !!menu.menuOpen, id: menu.id || null, locked: !!menu.menuLocked,
                title: menu.menuOpen ? text('#modal-header') : '', text: menu.menuOpen ? text('#modal-content') : '',
                selection: menu.menuListSelection, choices: []},
            messages: (game.consoleMessages || []).slice(-20).map(function (m) { return plain(m.message); }),
            dialogue: text('#convo'), hint: hints.hintShown ? text('#hint-text') : '',
            screen: {titleSelection: title.selector ? title.selector.index : null,
                titleItems: (title.menuItems || []).map(function (i) { return i.el ? plain(i.el.html()) : ''; }),
                selectedClass: player.selectedClass, difficulty: game.difficulty,
                classDetails: game.state === 'classScreen' ? text('#class-table') : ''}};
        var harness = seedHarness();
        s.seedHarness = harness ? harness.controlState() : {active: false, unavailable: true};
        if (menu.menuListOpen) {
            s.menu.choices = Array.prototype.map.call(document.querySelectorAll('.menu-list-item'), function (e, i) {
                return {index: i, text: plain(e.innerHTML)};
            });
        }
        if (typeof pc !== 'undefined' && pc && pc.tile && map.levels && map.levels.length &&
            ['running','menu','dead','bigMap','fullscreenMiniMap','ingameHelp','ingameOptions'].indexOf(game.state) >= 0) {
            if (run !== pc) { run = pc; memories.clear(); }
            var level = map.getCurrentLevel(), tiles = [], memory = memories.get(level);
            if (!memory) { memory = new Map(); memories.set(level, memory); }
            s.floor = {index: map.levelIndex, name: plain(level.getName()), width: level.tiles.length,
                height: level.tiles[0].length};
            s.player = Object.assign(scalars(pc, ['hp','maxHp','mp','maxMp','level','strength','dexterity','intelligence']),
                scalars(player, ['hunger','apparentVampirism','gold','bullets','keys','xp','strength','dexterity','intelligence',
                    'sword','shield','armor','leggings','revolver']),
                {x: pc.tile.i, y: pc.tile.j, form: pc.getType(),
                    statuses: (pc.statuses || []).map(function (v) { return scalars(v, ['name','turnCount','turnLength']); })});
            s.potions = (potions.gamePotions || []).filter(function (p) { return p.count > 0; }).map(function (p) {
                return {name: plain(potions.getPotionName(p)), count: p.count, identified: !!p.identified};
            });
            s.abilities = (pc.getType() === 'human' ? magic.learnedSpells : abilities.learnedAbilities).map(function (v, i) {
                var spec = typeof v === 'string' ? (pc.getType() === 'human' ? magic.spells[v] : abilities.abilities[v]) : v;
                return v ? {command: 'ABILITY_' + (i + 1), name: plain(v.name || v), details: plain(spec && spec.description || '')} : null;
            }).filter(function (v) { return v !== null; });
            level.tiles.forEach(function (col) { col.forEach(function (t) {
                if (!t.canPlayerSee()) return;
                var entry = {x: t.i, y: t.j, type: t.type, name: plain(t.getName()),
                    passable: t.isPassable(), light: t.light, sunlight: t.sunlight, moonlight: t.moonlight};
                memory.set(t.i + ',' + t.j, Object.assign({}, entry));
                entry.visible = true;
                if (t.item) entry.item = {name: plain(t.item.getName()), description: plain(t.item.getDescription())};
                if (t.monster && !t.monster.isPlayer && pc.canSeeMonster(t.monster)) {
                    entry.monster = Object.assign({name: plain(t.monster.getName()), friendly: t.monster.isFriendly(pc)},
                        scalars(t.monster, ['hp','maxHp','type']));
                }
                tiles.push(entry);
            }); });
            var visible = new Set(tiles.map(function (t) { return t.x + ',' + t.y; }));
            memory.forEach(function (t, key) { if (!visible.has(key)) tiles.push(Object.assign({visible: false}, t)); });
            s.tiles = tiles;
            s.diagonalMovement = !!options.getValue('DIAGONAL_MOVEMENT');
            s.contextual = text('#contextual-button');
        }
        var next = JSON.stringify(s);
        if (next !== signature) { signature = next; revision++; }
        s.revision = revision;
        return s;
    }
    function fail(message) { throw new Error(message); }
    function seedObserve() {
        var s = observe(), A = seedHarness();
        if (!A) fail('Seed harness unavailable; restart the game to load updated seed aids');
        if (!A.active || !s.floor) fail('Seed harness requires an active non-tutorial run');
        if (pending || !s.seedHarness.canAct) fail('Game is busy; observe again before reading the seed map');
        function trueName(o) { return plain(o.getTrueName ? o.getTrueName() : o.getName ? o.getName() : o.name || o.type || ''); }
        var tiles = [];
        map.getCurrentLevel().tiles.forEach(function (col) { col.forEach(function (t) {
            var out = {x:t.i, y:t.j, type:t.type, name:trueName(t), visible:t.canPlayerSee(),
                passable:t.isPassable(), light:t.light, sunlight:t.sunlight, moonlight:t.moonlight};
            if (t.item) out.item = {name:trueName(t.item), playerName:plain(t.item.getName())};
            if (t.monster && !t.monster.isPlayer) {
                var m = t.monster;
                out.monster = Object.assign({name:trueName(m), friendly:m.isFriendly(pc),
                    statuses:(m.statuses || []).map(function (v) { return scalars(v, ['name','turnCount','turnLength']); })},
                    scalars(m, ['type','hp','maxHp','mp','maxMp','level','actionState']));
                if (m.item) out.monster.carrying = trueName(m.item);
            }
            tiles.push(out);
        }); });
        return {state:s, seed:game.seed, scope:'current floor; includes unseen tiles and creatures',
            omniscience:{tiles:tiles, potions:(potions.gamePotions || []).filter(function (p) { return p.count > 0; }).map(function (p) {
                return {name:plain(potions.getPotionName(p)), trueName:plain(potions.getTrueName(p)), count:p.count, identified:!!p.identified};
            })}};
    }
    function seedForecast(expectedRevision) {
        var s = observe(), A = seedHarness();
        if (!Number.isInteger(expectedRevision) || expectedRevision !== s.revision) fail('Stale observation; read state and use its revision');
        if (!A || !s.seedHarness.active) fail('Seed harness requires an active non-tutorial run');
        if (pending || !s.seedHarness.canForecast) fail('Forecast unavailable; wait for idle and finish or undo the current choice');
        var results = A.forecast().map(function (f) {
            return Object.assign({command:f.direction[0], dx:f.direction[1], dy:f.direction[2], label:f.direction[3]},
                scalars(f, ['disabled','hp','dead','damage','attacked','kill','noticed','prompt','blocked']));
        });
        return {revision:s.revision, state:observe(), scope:'one movement, adjacent attack, or WAIT from the current RNG', forecasts:results};
    }
    async function act(a, guarded) {
        if (!a || typeof a !== 'object' || Array.isArray(a)) fail('Action must be an object');
        if (typeof a.requestId !== 'string' || !/^[A-Za-z0-9_-]{1,80}$/.test(a.requestId)) fail('requestId is required (1-80 letters, digits, _ or -)');
        var encoded = JSON.stringify([!!guarded, a]), old = requests.get(a.requestId);
        if (old) { if (old.encoded !== encoded) fail('requestId already used with different arguments'); return old.promise; }
        if (!enabled) fail('Control is paused; use control enable first');
        var A = seedHarness(), seedState = A && A.controlState();
        if (a.type === 'seed' && !guarded) fail('Use the seed control tool for cheats; normal actions cannot undo/redo');
        if (guarded && (!A || typeof A.previewAction !== 'function')) fail('Death check unavailable; restart the game to load updated seed aids');
        if (guarded && a.type !== 'seed' && (!seedState.active || !seedState.canAct || game.tutorial)) fail('Seed actions require an active, idle non-tutorial run');
        if (a.type === 'seed' && !A) fail('Seed harness unavailable; restart the game to load updated seed aids');
        var travel = a.type === 'seed' && ['undo','redo'].indexOf(a.operation) >= 0;
        if (pending || (travel ? !seedState || !seedState.boundary : busy())) fail('Game is busy; observe again before acting');
        var before = observe();
        if (!Number.isInteger(a.expectedRevision) || a.expectedRevision !== before.revision) fail('Stale observation; read state and use its revision');
        if (a.type === 'seed') {
            if (!A) fail('Seed harness unavailable; restart the game to load updated seed aids');
            if (['enable','undo','redo'].indexOf(a.operation) < 0) fail('Seed operation must be enable, undo, or redo');
            if (a.operation === 'enable' && (game.tutorial || game.state !== 'running' || !seedState.boundary)) fail('Enable requires an idle non-tutorial run');
            if (a.operation === 'undo' && !seedState.canUndo) fail('No undo position available');
            if (a.operation === 'redo' && !seedState.canRedo) fail('No redo position available');
        } else if (a.type === 'command') {
            if (commands.indexOf(a.command) < 0) fail('Unknown or prohibited command');
            if (a.shift !== undefined && typeof a.shift !== 'boolean') fail('shift must be boolean');
            if (a.command === 'CONFIRM_TARGET' && (game.state !== 'running' || menu.menuOpen ||
                magic.state !== 'waiting' && game.revolverState !== 'aim' && game.examineState !== 'examine')) fail('No active targeting');
            if (input.defaultKeys[a.command] && input.defaultKeys[a.command].diagonal && !options.getValue('DIAGONAL_MOVEMENT')) fail('Diagonal movement is disabled');
        } else if (a.type === 'choose') {
            if (!menu.menuListOpen || !Number.isInteger(a.index) || a.index < 0 || a.index >= menu.menuList.length) fail('Invalid menu choice index');
        } else if (a.type === 'target') {
            if (game.state !== 'running' || menu.menuOpen || !['aim','examine'].includes(game.revolverState) &&
                magic.state !== 'waiting' && game.examineState !== 'examine') fail('Open revolver, ability, or examine targeting first');
            if (!Number.isInteger(a.x) || !Number.isInteger(a.y)) fail('Target coordinates must be integers');
            var t = map.getTile(a.x, a.y);
            if (!t || !t.canPlayerSee()) fail('Target must be a visible tile');
        } else fail('type must be command, choose, target, or seed');
        if (guarded && a.type === 'command' && (a.command === 'REST' || a.shift))
            fail('Seed actions require one move; use WAIT or unshifted movement');
        function executeInput() {
            if (a.type === 'choose') menu.activateMenuListItem(a.index);
            else if (a.type === 'target') {
                game.selectedTile = t;
                if (game.revolverState === 'aim') player.aimRevolver('selectedTile');
                else if (magic.state === 'waiting') {
                    var scale = tileSize * trueTileScaling * WebGlContainer.scale.x;
                    var cam = game.cameraWithScreenshake;
                    player.aimAbility((t.i - cam.x + playerCenterWidthOffset / WebGlContainer.scale.x) * scale,
                        (t.j - cam.y + playerCenterHeightOffset / WebGlContainer.scale.x) * scale);
                }
            } else input.queueCommand(a.command === 'CONFIRM_TARGET' ? (magic.state === 'waiting' ? 'CLICK' : 'CONFIRM') : a.command, {shift: !!a.shift});
        }
        pending = true;
        // Defer until the idempotency entry exists, even if a handler throws.
        var promise = Promise.resolve().then(async function () {
            try {
                if (guarded && a.type !== 'seed') {
                    var movementOnly = a.type === 'command' && !a.shift &&
                        ['UP','DOWN','LEFT','RIGHT','NORTHWEST','NORTHEAST','SOUTHWEST','SOUTHEAST','WAIT'].indexOf(a.command) >= 0;
                    // The harness also checks for pending targeting/choices;
                    // directional spells retain full-world restoration.
                    var preview = A.previewAction(executeInput, movementOnly);
                    if (preview.dead) fail('Refused: this action leads to death on the next move');
                }
                if (guarded && a.type === 'seed' && a.operation === 'redo' && A.redoLeadsToDeath())
                    fail('Refused: redo would restore a dead position');
                if (a.type !== 'seed' && A && typeof A.anchorInitialPosition === 'function') A.anchorInitialPosition();
                if (a.type === 'seed') {
                    if (a.operation === 'enable') A.enable();
                    else {
                        A.travel(a.operation === 'undo' ? -1 : 1);
                        // Normal-play terrain memory must not leak knowledge from an undone branch.
                        memories.clear();
                    }
                } else executeInput();
                startPump();
                var start = Date.now(), stable = 0;
                do {
                    await new Promise(function (resolve) {
                        defer(resolve, 40);
                    });
                    stable = busy() ? 0 : stable + 1;
                } while (stable < 3 && Date.now() - start < 15000);
                pending = false;
                finishRelease();
                return {requestId: a.requestId, settled: stable >= 3, state: observe(),
                    note: stable >= 3 ? 'Action processed; inspect state and messages for outcome.' :
                        'Still processing. Observe until idle; do not retry with a new requestId.'};
            } finally { pending = false; finishRelease(); }
        });
        requests.set(a.requestId, {encoded: encoded, promise: promise});
        if (requests.size > 256) requests.delete(requests.keys().next().value);
        return promise;
    }
    var api = window.gkhLLM = {observe: observe, act: function (a) { return act(a, false); },
        seedAct: function (a) { return act(a, true); }, seedObserve:seedObserve, seedForecast:seedForecast,
        control: control, localInputLocked: function () { return exclusive; }};
    // The browser build has the same API for isolated testing; only NW.js starts a server.
    if (typeof require === 'undefined') return;
    try {
        var http = require('http'), fs = require('fs'), path = require('path'), crypto = require('crypto');
        var home = process.env.LOCALAPPDATA || require('os').homedir();
        var dir = path.join(home, 'GoldenKroneLLM');
        if (!fs.existsSync(dir)) fs.mkdirSync(dir);
        var controlPath = path.join(dir, 'control.json');
        persistControl = function () {
            try { fs.writeFileSync(controlPath, JSON.stringify({exclusive:exclusive}), {mode:384}); }
            catch (e) { console.error('LLM ownership preference: ' + e.message); }
        };
        try {
            if (JSON.parse(fs.readFileSync(controlPath, 'utf8')).exclusive === true) {
                // Reopening a watched run stays locked and paused until its controller resumes.
                exclusive = true; enabled = false; clearLocalInput(); renderControl();
            }
        } catch (e) {}
        var token = crypto.randomBytes(32).toString('hex'), infoPath = path.join(dir, 'connection.json');
        var server = http.createServer(async function (req, res) {
            function reply(code, data) { if (!res.destroyed) { res.writeHead(code, {'Content-Type':'application/json','Cache-Control':'no-store'}); res.end(JSON.stringify(data)); } }
            if (req.headers.origin || req.headers.authorization !== 'Bearer ' + token) { reply(403, {error:'Unauthorized'}); return; }
            if (req.headers.host !== '127.0.0.1:' + server.address().port) { reply(403, {error:'Invalid Host'}); return; }
            try {
                if (req.method === 'GET' && req.url === '/state') { reply(200, observe()); return; }
                if (req.method === 'GET' && req.url === '/seed/state') { reply(200, seedObserve()); return; }
                if (req.method === 'GET' && /^\/seed\/forecast\?revision=\d+$/.test(req.url)) {
                    reply(200, seedForecast(Number(req.url.split('=')[1]))); return;
                }
                if (req.method !== 'POST' || ['/action','/control','/seed/action','/seed/control'].indexOf(req.url) < 0) { reply(404, {error:'Unknown endpoint'}); return; }
                var body = '', bytes = 0, oversized = false;
                req.setEncoding('utf8');
                req.on('data', function (part) { bytes += Buffer.byteLength(part); if (bytes > 8192) { oversized = true; } else body += part; });
                req.on('end', async function () {
                    if (oversized) { reply(413, {error:'Request too large'}); return; }
                    try {
                        var data = JSON.parse(body);
                        if (req.url === '/seed/action' && data.type === 'seed') fail('Use /seed/control for timeline operations');
                        if (req.url === '/seed/control' && data.type !== 'seed') fail('Seed control requires type seed');
                        var result = req.url === '/control' ? api.control(data.enabled, data.exclusive) : await act(data, req.url.indexOf('/seed/') === 0);
                        reply(200, result);
                    } catch (e) { reply(400, {error: e.message}); }
                });
            } catch (e) { reply(req.url.indexOf('/seed/') === 0 ? 400 : 500, {error:e.message}); }
        });
        server.requestTimeout = 20000;
        server.on('error', function (e) { api.error = e.message; console.error('LLM bridge: ' + e.message); });
        server.listen(0, '127.0.0.1', function () {
            fs.writeFileSync(infoPath, JSON.stringify({url:'http://127.0.0.1:' + server.address().port, token:token, pid:process.pid, version:1}), {mode:384});
            api.connectionPath = infoPath;
        });
        api.server = server;
        window.addEventListener('beforeunload', function () {
            stopPump();
            server.close();
            try { if (JSON.parse(fs.readFileSync(infoPath, 'utf8')).token === token) fs.unlinkSync(infoPath); } catch (e) {}
        });
    } catch (e) { api.error = e.message; console.error('LLM bridge: ' + e.message); }
})();

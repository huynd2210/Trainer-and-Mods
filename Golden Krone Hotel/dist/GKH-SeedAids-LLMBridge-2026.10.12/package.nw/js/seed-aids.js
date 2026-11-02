/* Golden Krone Hotel seed aids. Loaded by the game itself; no separate runtime.
 * Snapshots restore objects in place, preserving prototypes, aliases and closures.
 * Rendering/native resources are opaque; game logic and ROT's RNG are reversible.
 */
(function () {
    'use strict';
    var prior = window.seedAids;
    if (prior && prior.version === 2) {
        var debugOnReload = game.usedDebug;
        prior.enable(); setTimeout(function () { game.usedDebug = debugOnReload; }, 0); return;
    }
    var usedDebugBeforeInstall = game.usedDebug;
    if (prior) {
        var wasPractice = prior.active || game.seedAidsPractice;
        var progressBeforeUpgrade = [player.renown, game.pagesCollected, game.winStreaks, game.bestTimes];
        // Retire the previous hooks without restarting or restoring an older run.
        prior.reset();
        player.renown = progressBeforeUpgrade[0]; game.pagesCollected = progressBeforeUpgrade[1];
        game.winStreaks = progressBeforeUpgrade[2]; game.bestTimes = progressBeforeUpgrade[3];
        if (wasPractice) usedDebugBeforeInstall = false;
        ['seed-aids', 'sa-marks'].forEach(function (id) {
            var node = document.getElementById(id); if (node) node.remove();
        });
    }
    var A = window.seedAids = {
        version: 2, autoEnable: true,
        active: false, simulating: false, omniscience: true, foresight: true,
        history: [], cursor: -1, pending: null, forecasts: [], dirty: true,
        limit: 48, error: '', open: true
    };
    var roots = ['game', 'map', 'player', 'pc', 'magic', 'abilities', 'potions',
        'lighting', 'messaging', 'menu', 'hints', 'tutorial', 'morgue', 'input', 'options'];
    var globals = ['screenshake', 'screenshakeAngle', 'quakeCenter', 'quakeshake',
        'quakeDistance', 'dramaticEntranceFrames', 'bigOneExplosionFrames', 'dropFrames'];
    var timers = new Map(), epoch = 0;
    var nativeTimeout = window.setTimeout, nativeInterval = window.setInterval;
    var browserClearTimeout = window.clearTimeout, browserClearInterval = window.clearInterval;
    var nativeClearTimeout = browserClearTimeout, nativeClearInterval = browserClearInterval;
    // NW renderer timers can be throttled while hidden. Node timers keep menu,
    // stairs and death callbacks working without bringing the game to the front.
    if (typeof require === 'function') {
        var nodeTimers = require('timers');
        nativeTimeout = nodeTimers.setTimeout; nativeInterval = nodeTimers.setInterval;
        nativeClearTimeout = nodeTimers.clearTimeout; nativeClearInterval = nodeTimers.clearInterval;
    }
    function clearScheduled(id, interval) {
        if (A.simulating) return; // Previews must not cancel real callbacks.
        if (typeof id === 'object') (interval ? nativeClearInterval : nativeClearTimeout)(id);
        else (interval ? browserClearInterval : browserClearTimeout)(id);
        timers.delete(id);
    }
    window.clearTimeout = function (id) { clearScheduled(id, false); };
    window.clearInterval = function (id) { clearScheduled(id, true); };
    function cancelDeferred() {
        epoch++;
        timers.forEach(function (interval, id) { clearScheduled(id, interval); });
        timers.clear();
    }
    function schedule(original, interval, callback, delay, args) {
        if (!A.active || typeof callback !== 'function') return original.apply(window, [callback, delay].concat(args));
        var when = epoch, id;
        id = original.call(window, function () {
            if (!interval) timers.delete(id);
            if (when === epoch) callback.apply(window, args);
        }, delay);
        timers.set(id, interval); return id;
    }
    window.setTimeout = function (callback, delay) {
        return schedule(nativeTimeout, false, callback, delay, Array.prototype.slice.call(arguments, 2));
    };
    window.setInterval = function (callback, delay) {
        return schedule(nativeInterval, true, callback, delay, Array.prototype.slice.call(arguments, 2));
    };
    var opaqueKeys = /^(bigBuffer|_previouslySeen|howlerSounds|howlerMusic|musicObjects|sounds|managedGraphics|frameManagedGraphics|tempCanvas|tempCtx|performance)$/;
    function traversable(o) {
        if (!o || typeof o !== 'object' || o === window) return false;
        if (Array.isArray(o)) return true;
        var p = Object.getPrototypeOf(o);
        if (p === Object.prototype || p === null) return true;
        var c = o.constructor;
        // Only game-defined classes; never walk DOM, audio or PIXI internals.
        return !!(c && c.name && window[c.name] === c &&
            !/^(Date|Image|HTML|Canvas|Audio|WebGL|Howl|Howler|Object|Map|Set)/.test(c.name));
    }
    var recordCache = new WeakMap(), floorCache = new WeakMap(), lastCapturedLevel = null;
    A.capture = function () {
        var visited = new Set(), records = [], bindings = {}, floors = [];
        var currentLevel = map.getCurrentLevel();
        function visitLevel(level) {
            if (visited.has(level)) return;
            visited.add(level);
            var cached = floorCache.get(level);
            var liveReferences = cached && cached.some(function (r) {
                var o = r.object;
                return o === pc || o instanceof Level && o === currentLevel ||
                    o instanceof Tile && o.level === currentLevel ||
                    o instanceof Monster && o.tile && o.tile.level === currentLevel;
            });
            if (cached && level !== currentLevel && level !== lastCapturedLevel &&
                    !liveReferences &&
                    (!map.bigBomb || map.bigBomb.tile.level !== level)) {
                // Discovering stairs can change a remote floor's metadata
                // without ticking its tiles. Reuse its large object bank, but
                // capture the live header so previews do not erase discovery.
                var headerIndex = cached.length - 1;
                if (cached[headerIndex].object !== level) headerIndex = cached.findIndex(function (r) { return r.object === level; });
                var header = cached[headerIndex], headerKeys = Object.keys(level);
                var headerValues = headerKeys.map(function (k) { return level[k]; });
                if (headerKeys.length !== header.keys.length || headerKeys.some(function (k, i) {
                    return k !== header.keys[i] || headerValues[i] !== header.values[i];
                })) {
                    cached = cached.slice();
                    cached[headerIndex] = {object: level, keys: headerKeys, values: headerValues, length: null};
                    floorCache.set(level, cached); recordCache.set(level, cached[headerIndex]);
                }
                floors.push({level: level, records: cached}); return;
            }
            var outer = records;
            records = [];
            // Crossing stairs may link to another floor; each floor gets its own bank.
            visitObject(level);
            floorCache.set(level, records);
            floors.push({level: level, records: records});
            records = outer;
        }
        function visit(o) {
            if (!traversable(o) || visited.has(o)) return;
            if (o instanceof Level) { visitLevel(o); return; }
            visited.add(o);
            visitObject(o);
        }
        function visitObject(o) {
            var keys = Object.keys(o), values = [];
            for (var i = 0; i < keys.length; i++) {
                var v = o[keys[i]];
                values.push(v);
                if (!opaqueKeys.test(keys[i])) visit(v);
            }
            var previous = recordCache.get(o);
            if (previous && previous.length === (Array.isArray(o) ? o.length : null) &&
                    keys.length === previous.keys.length && keys.every(function (k, i) {
                        return k === previous.keys[i] && values[i] === previous.values[i];
                    })) records.push(previous);
            else {
                var record = {object: o, keys: keys, values: values,
                    length: Array.isArray(o) ? o.length : null};
                recordCache.set(o, record); records.push(record);
            }
        }
        roots.concat(globals).forEach(function (k) { bindings[k] = window[k]; visit(window[k]); });
        lastCapturedLevel = currentLevel;
        return {records: records, floors: floors, level: currentLevel, bindings: bindings, rng: ROT.RNG.getState().slice(),
            globalId: save.globalId, saveId: save.currentSaveId};
    };
    function hasLiveReferences(records, level) {
        return records.some(function (r) {
            var o = r.object;
            return o === pc || o instanceof Level && o === level ||
                o instanceof Tile && o.level === level ||
                o instanceof Monster && o.tile && o.tile.level === level;
        });
    }
    A.restore = function (s, quick) {
        function restoreRecords(records) { records.forEach(function (r) {
                var currentKeys = Object.keys(r.object);
            if (currentKeys.length !== r.keys.length || currentKeys.some(function (k, i) { return k !== r.keys[i]; })) {
                var keep = new Set(r.keys);
                currentKeys.forEach(function (k) { if (!keep.has(k)) delete r.object[k]; });
            }
            if (r.length !== null) r.object.length = r.length;
            r.keys.forEach(function (k, i) { if (r.object[k] !== r.values[i]) r.object[k] = r.values[i]; });
        }); }
        var banks = s.floors;
        if (quick) {
            // Movement previews tick only the active floor and the big bomb.
            // Departed banks can also own aliases of the player/active tiles:
            // restore those banks too, in the original order. Never apply this
            // shortcut to arbitrary inputs, stairs or timeline travel.
            if (!s.movementBanks) {
                s.movementActors = s.level.monsters.concat([pc]);
                s.movementBanks = banks.filter(function (f) {
                    return f.level === s.level || hasLiveReferences(f.records, s.level) ||
                        map.bigBomb && map.bigBomb.tile.level === f.level;
                });
            }
            // Monsters can fall while the player stays on this floor. If the
            // player changes floor, use full restoration: special encounters
            // can also modify a third floor (e.g. the Brain changes Fane).
            var destination = map.getCurrentLevel();
            if (destination === s.level) {
                var displaced = s.movementActors.filter(function (m) { return m.tile && m.tile.level !== s.level; });
                banks = displaced.length ? banks.filter(function (f) {
                    return s.movementBanks.indexOf(f) >= 0 || displaced.some(function (m) { return m.tile.level === f.level; });
                }) : s.movementBanks;
            }
        }
        s.floors.forEach(function (f) {
            // Exploring a staircase marks its destination's seenStairs even
            // before travelling. Floor headers remain reversible everywhere.
            if (quick && banks.indexOf(f) < 0) {
                var header = f.records[f.records.length - 1];
                if (!header || header.object !== f.level) header = f.records.find(function (r) { return r.object === f.level; });
                if (header) restoreRecords([header]);
                return;
            }
            restoreRecords(f.records);
            floorCache.set(f.level, f.records);
        });
        restoreRecords(s.records);
        lastCapturedLevel = s.level;
        Object.keys(s.bindings).forEach(function (k) { window[k] = s.bindings[k]; });
        ROT.RNG.setState(s.rng.slice());
        save.globalId = s.globalId;
        save.currentSaveId = s.saveId;
    };
    function ready() {
        return window.pc && pc.tile && !map.generating && !game.tutorial &&
            (game.state === 'running' || game.state === 'dead' || game.state === 'menu');
    }
    function boundary() {
        return ready() && (terminal() || (!game.actionQueue.length && !player.pathing &&
            !dramaticEntranceFrames && !bigOneExplosionFrames));
    }
    function terminal() { return game.state === 'dead' || (game.state === 'menu' && menu.previousState === 'dead'); }
    function choosing() {
        return menu.menuOpen || magic.state === 'waiting' || game.revolverState === 'aim' ||
            game.examineState === 'examine' || game.chatState === 'direction' || game.chatState === 'talking';
    }
    function bridgeBoundary() {
        return boundary() || !!(window.pc && pc.tile && !game.tutorial && !map.generating &&
            ['bigMap','fullscreenMiniMap','ingameHelp','ingameOptions'].indexOf(game.state) >= 0 &&
            !game.actionQueue.length && !player.pathing && !dramaticEntranceFrames && !bigOneExplosionFrames);
    }
    function progress() {
        return JSON.parse(JSON.stringify({renown: player.renown, pages: game.pagesCollected,
            streaks: game.winStreaks, times: game.bestTimes, weekly: game.weeklyAttempts,
            morgueFiles: morgue.morgueFiles, morgueUploads: morgue.morgueUploads}));
    }
    function restoreProgress(p) {
        player.renown = p.renown; game.pagesCollected = p.pages;
        game.winStreaks = p.streaks; game.bestTimes = p.times; game.weeklyAttempts = p.weekly;
        morgue.morgueFiles = p.morgueFiles; morgue.morgueUploads = p.morgueUploads;
    }
    A.enable = function () {
        if (A.active || !boundary() || game.state !== 'running') return;
        A.active = true;
        A.history = [A.capture()]; A.cursor = 0; A.pending = null;
        A.dirty = true; A.error = ''; render();
    };
    function redraw() {
        game.updateBuffer = true; game.undrawn = true;
        game.previousTimerDate = new Date();
        // Clear animation remnants and native cached memories after time travel.
        game.effects = []; game.captions = [];
        map.getCurrentLevel().monsters.concat([pc]).forEach(function (m) {
            m.animateTiles = []; m._cacheKey = null;
        });
        if (typeof WebGlContainer !== 'undefined') WebGlContainer.removeChildren();
        map.getCurrentLevel().wipeAndResetPreviouslySeen();
        $('#modal,#overlay,#tooltip').hide();
        menu.preventSelect = false;
        // The practice panel supplies terminal recovery. Avoid re-awarding renown
        // or scheduling the original delayed death menu when restoring a death.
        if (terminal()) { game.state = 'dead'; menu.menuOpen = false; game.dying = false; }
        else if (menu.menuOpen) menu.closeMenu(true, true);
        player.drawUI(); player.updateStats(); player.setDescriptionCaption(); potions.drawPotions(); game.updateConsole();
        A.dirty = true;
    }
    A.travel = function (delta) {
        if (!A.active || A.simulating || !boundary()) return;
        var target = delta < 0 && A.pending ? A.cursor : A.cursor + delta;
        if (target < 0 || target >= A.history.length) return;
        if (delta > 0 && A.redoLeadsToDeath()) {
            A.error = 'Refused: redo would restore a dead position';
            if (el('sa-note')) el('sa-note').textContent = A.error;
            return;
        }
        cancelDeferred();
        var earned = progress(), debugEligibility = game.usedDebug;
        A.restore(A.history[target]); A.cursor = target; A.pending = null;
        restoreProgress(earned);
        game.usedDebug = debugEligibility;
        input.commands = {}; input.inputCache = {};
        // HUD reconstruction shuffles contextual neighbors with the gameplay RNG.
        // Refresh the display without advancing the restored timeline's RNG.
        var restoredRng = ROT.RNG.getState().slice();
        try { redraw(); } finally { ROT.RNG.setState(restoredRng.slice()); }
        try { render(); } finally { ROT.RNG.setState(restoredRng.slice()); }
    };
    function commit() {
        if (!A.pending || !boundary()) return;
        if (!terminal() && choosing()) return;
        if (game.turnCount !== A.pending.turn || game.state === 'dead' ||
                map.levelIndex !== A.pending.floor || ROT.RNG.getState().some(function (v, i) { return v !== A.pending.rng[i]; })) {
            A.history.splice(A.cursor + 1);
            A.history.push(A.capture()); A.cursor++;
            if (A.history.length > A.limit) { A.history.shift(); A.cursor--; }
            A.dirty = true;
        }
        A.pending = null;
    }
    // Stable control surface for the LLM bridge, using the panel's own rules.
    A.controlState = function () {
        if (A.active && !A.simulating) commit();
        var atBoundary = !!boundary(), choice = !!choosing();
        return {active: A.active, boundary: atBoundary, canAct: !!bridgeBoundary(), choosing: choice,
            cursor: A.cursor, length: A.history.length, limit: A.limit, pending: !!A.pending,
            canUndo: !!(A.active && !A.simulating && atBoundary && (A.pending || A.cursor > 0)),
            canRedo: !!(A.active && !A.simulating && atBoundary && A.cursor + 1 < A.history.length),
            canForecast: !!(A.active && !A.simulating && atBoundary && game.state === 'running' && !choice)};
    };
    // Bridge response polling must survive cancellation of gameplay callbacks on undo.
    A.defer = function (callback, delay) { return nativeTimeout.call(window, callback, delay); };
    A.anchorInitialPosition = function () {
        // Floor construction can finish after auto-enable first captures the root.
        // Anchor the first bridge action to the actual settled starting position.
        if (A.active && !A.simulating && boundary() && !choosing() && !A.pending &&
                A.cursor === 0 && A.history.length === 1) A.history[0] = A.capture();
    };
    var processQueue = input.processQueue;
    input.processQueue = function () {
        if (A.active && !A.simulating && !A.pending && game.state === 'running') {
            var q = game.actionQueue[0];
            if (q && (q.type === 'input' || q.type === 'path' || q.type === 'slide')) {
                // The prior stable snapshot includes the entire menu/targeting action.
                A.pending = {turn: game.turnCount, floor: map.levelIndex, rng: ROT.RNG.getState().slice()};
            }
        }
        var result = processQueue.apply(this, arguments);
        if (A.active && !A.simulating) {
            if (A.pending && !choosing()) A.dirty = true;
            commit();
        }
        return result;
    };
    // Preview side effects cannot escape into the real game or its save files.
    function guard(obj, key, predicate) {
        if (typeof obj[key] !== 'function') return;
        var original = obj[key];
        obj[key] = function () { if (!predicate()) return original.apply(this, arguments); };
    }
    [ [steam, 'setCheevo'], [save, 'deleteSave'], [save, 'saveGame'], [morgue, 'saveFile'],
        [game, 'saveWinStreaks'], [game, 'saveBestTimes'], [game, 'savePagesCollected'],
        [player, 'saveRenown'] ].forEach(function (p) {
        guard(p[0], p[1], function () { return A.simulating; });
    });
    guard(storage, 'write', function () { return A.simulating; });
    ['playSound', 'playMusic', 'replaceMusic', 'vibrate', 'draw', 'updateConsole',
        'clearManagedGraphics', 'clearFrameManagedGraphics'].forEach(function (k) {
        guard(game, k, function () { return A.simulating; });
    });
    ['drawUI', 'drawXp'].forEach(function (k) {
        guard(player, k, function () { return A.simulating; });
    });
    guard(map, 'draw', function () { return A.simulating; });
    guard(potions, 'drawPotions', function () { return A.simulating; });
    // Terminal previews need only the terminal state, not the game-over UI/export.
    var gameOver = player.gameOver;
    player.gameOver = function (won) {
        if (A.simulating) { game.won = won; game.state = 'dead'; game.actionQueue = []; return; }
        return gameOver.apply(this, arguments);
    };
    var directions = [ ['NORTHWEST', -1, -1, 'NW'], ['UP', 0, -1, 'N'],
        ['NORTHEAST', 1, -1, 'NE'], ['LEFT', -1, 0, 'W'], ['WAIT', 0, 0, 'Wait'],
        ['RIGHT', 1, 0, 'E'], ['SOUTHWEST', -1, 1, 'SW'], ['DOWN', 0, 1, 'S'],
        ['SOUTHEAST', 1, 1, 'SE'] ];
    function simulate(callback, movementOnly) {
        var base = A.capture(), patched = [], previewGraphics = [];
        function patch(o, k, f) { patched.push([o, k, o[k]]); o[k] = f; }
        A.simulating = true;
        // All simulation completes synchronously. No deferred preview callbacks survive.
        patch(window, 'setTimeout', function () { return 0; });
        patch(window, 'setInterval', function () { return 0; });
        // Attacking plants or spawning offspring can construct monsters during
        // a preview. Their canvases/textures are native allocations, outside
        // the reversible object graph. Keep them out of the live graphics list
        // and dispose them when this hypothetical move is rolled back.
        patch(game, 'manageGraphic', function (graphic) {
            previewGraphics.push(graphic); return graphic;
        });
        ['html', 'text', 'css', 'attr', 'prop', 'val'].forEach(function (k) {
            var original = $.fn[k];
            patch($.fn, k, function () {
                var getter = (k === 'html' || k === 'text' || k === 'val') ?
                    arguments.length === 0 : arguments.length === 1 && typeof arguments[0] === 'string';
                return getter ? original.apply(this, arguments) : this;
            });
        });
        ['show', 'hide', 'addClass', 'removeClass', 'toggleClass', 'append', 'prepend',
            'empty', 'remove', 'fadeIn', 'fadeOut', 'animate'].forEach(function (k) {
            patch($.fn, k, function () { return this; });
        });
        try { return callback(base); } finally {
            A.restore(base, movementOnly);
            previewGraphics.forEach(function (graphic) {
                try {
                    if (graphic instanceof PIXI.Texture) {
                        var source = graphic.baseTexture && graphic.baseTexture.source;
                        graphic.destroy(source instanceof HTMLCanvasElement);
                        if (source instanceof HTMLCanvasElement) { source.width = 0; source.height = 0; }
                    } else if (graphic && typeof graphic.destroy === 'function') graphic.destroy(false);
                } catch (e) { /* Cleanup must not interrupt restoration. */ }
            });
            patched.reverse().forEach(function (p) { p[0][p[1]] = p[2]; });
            A.simulating = false;
        }
    }
    // Preview the exact bridge input, including a potion/menu choice or aimed cast.
    // Unlike the nine-way display, this also works while resolving a choice.
    A.previewAction = function (execute, movementOnly) {
        if (!A.active || A.simulating || !bridgeBoundary() || terminal()) throw new Error('Death check unavailable at this position');
        movementOnly = !!movementOnly && game.state === 'running' && !choosing();
        return simulate(function () {
            var hp = pc.hp, turn = game.turnCount, n = 0;
            execute();
            // Victory can leave presentation/tick work queued by the original
            // contextual handler. Stop at the winning outcome, as for death.
            while (game.actionQueue.length && !terminal() && !game.won) {
                if (++n > 200) throw new Error('Death check exceeded the action limit');
                // Finish one move, never explore a second gameplay turn.
                if (game.turnCount > turn && ['tick','path','slide','entranced','delay','postStairs'].indexOf(game.actionQueue[0].type) >= 0)
                    throw new Error('Death check requires a single move; use individual inputs');
                processQueue.call(input);
                if (menu.menuOpen) break;
            }
            if (game.turnCount - turn > 1) throw new Error('Death check requires a single move');
            if (!terminal() && !game.won && game.actionQueue.length && !menu.menuOpen)
                throw new Error('Death check could not finish the move');
            return {dead: !game.won && (terminal() || game.dying || pc.hp <= 0), hp: pc.hp - hp,
                turns: game.turnCount - turn, prompt: !!menu.menuOpen};
        }, movementOnly);
    };
    A.redoLeadsToDeath = function () {
        return simulate(function () {
            A.restore(A.history[A.cursor + 1]);
            return !game.won && (terminal() || game.dying || pc.hp <= 0);
        });
    };
    A.forecast = function () {
        if (!A.active || !boundary() || game.state !== 'running' || choosing()) return [];
        return simulate(function (base) {
            var results = [];
            directions.forEach(function (d) {
                A.restore(base, true);
                if (d[1] && d[2] && !options.getValue('DIAGONAL_MOVEMENT')) {
                    results.push({direction: d, disabled: true}); return;
                }
                var hp = pc.hp, turn = game.turnCount, tile = map.getTile(pc.tile.i+d[1], pc.tile.j+d[2]);
                var enemy = tile.monster !== pc ? tile.monster : null, enemyHp = enemy && enemy.hp;
                var hostile = enemy && !enemy.isFriendly(pc);
                var asleep = map.getCurrentLevel().monsters.filter(function (m) {
                    return !m.isFriendly(pc) && !m.targetLastSeen && m.actionState !== 'pursue';
                });
                input.handleCommand({command: d[0], details: {shift: false}});
                var n = 0;
                while (game.actionQueue.length && game.state === 'running' && !menu.menuOpen) {
                    if (++n > 200) throw new Error('Preview exceeded the action limit');
                    if (game.actionQueue[0].type === 'input') break;
                    processQueue.call(input);
                }
                results.push({direction: d, hp: pc.hp-hp, dead: game.state === 'dead',
                    damage: enemy ? Math.max(0, enemyHp-enemy.hp) : 0,
                    attacked: hostile && game.turnCount !== turn,
                    kill: enemy && (enemy.hp <= 0 || enemy.destroyed),
                    noticed: asleep.some(function (m) { return !!m.targetLastSeen || m.actionState === 'pursue'; }),
                    prompt: menu.menuOpen || game.state === 'menu', blocked: game.turnCount === turn});
            });
            return results;
        }, true);
    };
    // Preserve the RNG and practice flag in ordinary game saves, without persisting history.
    var generate = save.generate;
    save.generate = function () {
        var s = generate.apply(this, arguments);
        s.seedAids = {rng: ROT.RNG.getState().slice(), enabled: A.active, version: 2};
        return s;
    };
    var startLoaded = window.startLoadedGame;
    window.startLoadedGame = function () {
        A.reset();
        var result = startLoaded.apply(this, arguments);
        return result;
    };
    var loadSave = save.loadSave;
    save.loadSave = function (s) {
        var data;
        try { data = s.compressed ? JSON.parse(LZString.decompress(s.data)) : s.data; }
        catch (e) { return loadSave.apply(this, arguments); }
        var previousPc = window.pc;
        var result = loadSave.apply(this, arguments);
        if (data && data.seedAids && game.state === 'running' && pc !== previousPc) {
            // Repair only obsolete player occupants left by older preview restores.
            // They are references to the same player, not additional creatures.
            map.levels.forEach(function (level) {
                level.tiles.forEach(function (col) { col.forEach(function (tile) {
                    if (tile.monster === pc && tile !== pc.tile) tile.monster = null;
                }); });
            });
            ROT.RNG.setState(data.seedAids.rng);
            game.seedAidsPractice = false;
            if (data.seedAids.practice) game.usedDebug = false;
            if (data.seedAids.enabled || data.seedAids.practice) A.active = true;
        }
        return result;
    };
    A.reset = function () {
        cancelDeferred();
        A.active = false; A.history = []; A.cursor = -1; A.pending = null;
        A.forecasts = []; A.dirty = true; A.error = ''; game.seedAidsPractice = false;
        floorCache = new WeakMap(); recordCache = new WeakMap(); lastCapturedLevel = null;
    };
    var reallyStart = window.reallyStartGame;
    window.reallyStartGame = function () { A.reset(); return reallyStart.apply(this, arguments); };
    function el(id) { return document.getElementById(id === 'seed-aids' ? 'seed-aids-v2' : id); }
    function nameOf(o) {
        return o ? (o.getTrueName ? o.getTrueName() : o.getName ? o.getName() : o.name || o.type || '') : '';
    }
    function paintMap() {
        var level = map.getCurrentLevel(), cv = el('sa-map'), ctx = cv.getContext('2d');
        var size = level.levelSize, cell = Math.max(5, Math.floor(310/size));
        cv.width = cv.height = size*cell; ctx.imageSmoothingEnabled = false;
        ctx.fillStyle = '#080b10'; ctx.fillRect(0, 0, cv.width, cv.height);
        function sprite(index, x, y, alpha) {
            if (index === undefined || !window.ss || !ss.complete) return;
            var loc = ssLocation(index); ctx.globalAlpha = alpha;
            ctx.drawImage(ss, loc[0], loc[1], 16, 16, x*cell, y*cell, cell, cell);
        }
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) {
            var t = level.tiles[x][y];
            sprite(t.getSsIndex(), x, y, t._playerVisible ? 1 : 0.55);
            if (t.item) sprite(t.item.getSsIndex(), x, y, 1);
            if (t.monster) {
                sprite(t.monster.getSsIndex(), x, y, 1);
                ctx.globalAlpha = 1; ctx.strokeStyle = t.monster === pc ? '#74efb1' : '#e49a74';
                ctx.strokeRect(x*cell+.5, y*cell+.5, cell-1, cell-1);
            }
        }
        ctx.globalAlpha = 1; ctx.strokeStyle = '#74efb1';
        ctx.strokeRect(pc.tile.i*cell, pc.tile.j*cell, cell, cell);
        cv.onmousemove = function (e) {
            var r = cv.getBoundingClientRect();
            var x = Math.floor((e.clientX-r.left)*cv.width/r.width/cell);
            var y = Math.floor((e.clientY-r.top)*cv.height/r.height/cell);
            var t = level.tiles[x] && level.tiles[x][y];
            if (!t) return;
            el('sa-inspect').textContent = '('+x+', '+y+') '+nameOf(t)+
                (t.item ? ' \u00b7 '+nameOf(t.item) : '')+
                (t.monster ? ' \u00b7 '+nameOf(t.monster)+' \u00b7 HP '+Math.ceil(t.monster.hp)+'/'+t.monster.maxHp+
                    (t.monster.item ? ' \u00b7 carrying '+nameOf(t.monster.item) : '') : '');
        };
        el('sa-potions').textContent = potions.gamePotions.filter(function (p) { return p.count > 0; })
            .map(function (p) { return p.count+' \u00d7 '+potions.getPotionName(p)+' = '+potions.getTrueName(p); }).join('\n');
    }
    function outcome(f) {
        if (f.disabled) return 'disabled';
        if (f.dead) return 'DEATH';
        if (f.prompt) return 'confirmation';
        var parts = [];
        if (f.damage) parts.push((f.kill ? 'kill ' : 'hit ')+Math.round(f.damage*10)/10);
        else if (f.attacked) parts.push('miss');
        if (f.hp) parts.push((f.hp > 0 ? '+' : '')+Math.round(f.hp*10)/10+' HP');
        if (f.noticed) parts.push('noticed!');
        if (!parts.length) parts.push(f.blocked ? 'no turn' : 'safe');
        return parts.join(' \u00b7 ');
    }
    var lastRenderKey = null, lastMarkView = null;
    function markView() {
        var c = window.WebGlContainer, camera = game.cameraWithScreenshake || {};
        return [innerWidth, innerHeight, camera.x, camera.y,
            c && c.x, c && c.y, c && c.scale.x, c && c.scale.y].join('|');
    }
    function render(force) {
        if (!el('seed-aids')) return;
        // Poll state cheaply; do not repaint a whole floor or rebuild the DOM
        // four times a second while nothing has changed. Invalidate when a
        // move settles, an interaction changes, or display settings/size change.
        var renderKey = [A.active, A.open, A.omniscience, A.foresight, A.dirty,
            A.cursor, A.history.length, !!A.pending, A.error, game.state,
            game.turnCount, map.levelIndex, !!menu.menuOpen, choosing(), boundary(),
            magic.state, game.revolverState, innerWidth, innerHeight].join('|');
        if (!force && renderKey === lastRenderKey) {
            // Camera interpolation can continue after a turn settles. Keep
            // annotations aligned without repainting the map/panel.
            if (markView() !== lastMarkView) paintMarks();
            return;
        }
        var scrollTop = el('sa-body').scrollTop;
        // Game notes/choices must remain readable. Tools and Ctrl-Z still work
        // during menus; keep the terminal recovery panel visible after death.
        el('seed-aids').style.display = ready() && (!menu.menuOpen || terminal()) ? 'block' : 'none';
        el('sa-body').style.display = A.open ? 'block' : 'none';
        el('sa-enable').style.display = A.active ? 'none' : 'block';
        el('sa-tools').style.display = A.active ? 'block' : 'none';
        el('sa-status').textContent = A.active ? 'Seed aids \u00b7 Turn '+game.turnCount+
            ' \u00b7 '+(A.cursor+1)+'/'+A.history.length : 'Seed aids';
        el('sa-undo').disabled = (A.cursor <= 0 && !A.pending) || !boundary();
        el('sa-redo').disabled = A.cursor+1 >= A.history.length || !boundary();
        el('sa-omni').textContent = 'Omniscience: '+(A.omniscience ? 'on' : 'off');
        el('sa-fore').textContent = 'Foresight: '+(A.foresight ? 'on' : 'off');
        el('sa-knowledge').style.display = A.omniscience ? 'block' : 'none';
        el('sa-grid').style.display = A.foresight ? 'grid' : 'none';
        var visibleBody = A.open && ready() && (!menu.menuOpen || terminal()) && !document.hidden;
        if (A.active && A.dirty && boundary() && visibleBody) {
            try { A.forecasts = A.foresight ? A.forecast() : []; A.error = ''; }
            catch (e) { A.forecasts = []; A.error = 'Preview unavailable: '+e.message; console.error(e); }
            A.dirty = false;
        }
        el('sa-note').textContent = A.error || (game.state === 'dead' ? 'Undo to return before death.' :
            choosing() ? 'Finish the current choice to forecast.' :
            'One action ahead \u00b7 current RNG \u00b7 dim tiles are outside your vision');
        var grid = el('sa-grid'); grid.innerHTML = '';
        if (game.state === 'running' && !choosing()) A.forecasts.forEach(function (f) {
            var b = document.createElement('button');
            b.textContent = f.direction[3]+'\n'+outcome(f);
            b.className = f.dead || f.hp < 0 ? 'sa-danger' : f.damage ? 'sa-hit' : '';
            b.disabled = !!f.disabled || !!f.dead || !boundary();
            b.onclick = function () {
                try {
                    var outcome = A.previewAction(function () { input.queueCommand(f.direction[0], {shift: false}); }, true);
                    if (outcome.dead) throw new Error('Refused: this move leads to death');
                    input.queueCommand(f.direction[0], {shift: false});
                } catch (e) { A.error = e.message; el('sa-note').textContent = A.error; }
            };
            grid.appendChild(b);
        });
        if (A.active && A.omniscience && visibleBody) paintMap();
        paintMarks();
        el('sa-body').scrollTop = scrollTop;
        positionPanel();
        // Forecasting clears dirty; cache the resulting state rather than
        // forcing a redundant second render on the next poll.
        lastRenderKey = [A.active, A.open, A.omniscience, A.foresight, A.dirty,
            A.cursor, A.history.length, !!A.pending, A.error, game.state,
            game.turnCount, map.levelIndex, !!menu.menuOpen, choosing(), boundary(),
            magic.state, game.revolverState, innerWidth, innerHeight].join('|');
    }
    function positionPanel() {
        var panel = el('seed-aids'), potions = el('potions-sidebar');
        if (!panel) return;
        var r = potions && potions.getBoundingClientRect();
        // Reserve the entire potion column, including slots added later in the run.
        var right = r && r.width > 0 ? Math.max(12, innerWidth-r.left+12) : 210;
        var width = Math.min(334, Math.max(180, innerWidth-right-24));
        panel.style.width = width+'px'; panel.style.maxWidth = width+'px';
        panel.style.right = right+'px';
        panel.style.top = '100px'; panel.style.bottom = 'auto';
        panel.style.maxHeight = Math.max(100, innerHeight-270)+'px';
        el('sa-body').style.maxHeight = Math.max(60, innerHeight-320)+'px';
    }
    function paintMarks() {
        var cv = el('sa-marks');
        if (!cv) return;
        lastMarkView = markView();
        cv.width = innerWidth; cv.height = innerHeight;
        if (!A.active || !A.foresight || A.dirty || document.hidden || game.state !== 'running' || choosing() || !boundary()) return;
        var ctx = cv.getContext('2d'), rect = el('webgl').getBoundingClientRect();
        var tilePixels = tileSize*rect.width/WebGLRenderer.width;
        ctx.textAlign = 'center'; ctx.font = 'bold '+Math.min(12, Math.max(8, tilePixels/4))+'px Arial'; ctx.lineWidth = 3;
        function shortNumber(v) { return Math.abs(v) >= 1000 ? (v/1000).toFixed(1)+'k' : String(Math.round(v*10)/10); }
        function label(text, x, y, color) {
            ctx.strokeStyle = '#090c10'; ctx.strokeText(text, x, y);
            ctx.fillStyle = color; ctx.fillText(text, x, y);
        }
        A.forecasts.forEach(function (f) {
            if (f.disabled || !(f.dead || f.hp < 0 || f.attacked || f.damage || f.prompt || f.noticed)) return;
            var p = WebGlContainer.toGlobal(new PIXI.Point(
                (pc.tile.i+f.direction[1]+.5)*tileSize, (pc.tile.j+f.direction[2]+.5)*tileSize));
            var x = rect.left+p.x*rect.width/WebGLRenderer.width;
            var y = rect.top+p.y*rect.height/WebGLRenderer.height;
            if (f.dead) label('DEAD', x, y-10, '#ff958e');
            else if (f.prompt) label('?', x, y-10, '#ddc2ff');
            else {
                if (f.damage) label((f.kill ? 'K' : 'H')+shortNumber(f.damage), x, y-12, '#f4ce82');
                else if (f.attacked) label('miss', x, y-12, '#bdc6d2');
                if (f.hp < 0) label(shortNumber(f.hp), x, y+2, '#ff958e');
                if (f.noticed) label('!', x+tilePixels*.32, y-12, '#f4ce82');
            }
        });
    }
    $(function () {
        var style = document.createElement('style');
        style.textContent = '#seed-aids{position:fixed;right:12px;bottom:12px;width:334px;max-width:42vw;z-index:15000;color:#e6e0d5;background:rgba(13,18,25,.97);border:1px solid #8d7855;border-radius:7px;font:13px/1.4 Arial,sans-serif;box-shadow:0 4px 24px #0008}#seed-aids header{display:flex;align-items:center;justify-content:space-between;padding:9px 12px;color:#f3cd80}#sa-body{padding:0 12px 12px;max-height:70vh;overflow:auto}#seed-aids button{font:12px Arial,sans-serif;cursor:pointer;background:#25303d;color:#eee;border:1px solid #536071;border-radius:4px;padding:7px;margin:2px}#seed-aids button:disabled{opacity:.4;cursor:default}#sa-grid{grid-template-columns:repeat(3,1fr);gap:2px;margin:9px 0}#sa-grid button{white-space:pre-line;min-height:48px}#sa-grid .sa-danger{color:#ffaaa2;border-color:#b66}#sa-grid .sa-hit{color:#f4ca75}#sa-map{display:block;max-width:100%;margin:8px auto;image-rendering:pixelated}#sa-inspect{min-height:38px;color:#ddd}#sa-potions{white-space:pre-line;color:#c9b6ea}#sa-note,#sa-help{font-size:11px;color:#aeb9c5;margin:8px 0}';
        style.textContent = style.textContent.replace(/#seed-aids/g, '#seed-aids-v2');
        document.head.appendChild(style);
        var panel = document.createElement('section'); panel.id = 'seed-aids-v2';
        panel.innerHTML = '<header><b id="sa-status">Seed aids</b><button id="sa-collapse" aria-label="Collapse seed aids">\u2212</button></header><div id="sa-body"><button id="sa-enable">Enable practice aids</button><div id="sa-tools"><div><button id="sa-undo">Undo \u00b7 Ctrl Z</button><button id="sa-redo">Redo \u00b7 Ctrl Y</button></div><div><button id="sa-fore"></button><button id="sa-omni"></button></div><div id="sa-note"></div><div id="sa-grid"></div><div id="sa-knowledge"><canvas id="sa-map" aria-label="Omniscient current floor"></canvas><div id="sa-inspect">Hover the floor map to inspect tiles, items and monsters.</div><div id="sa-potions"></div></div></div><div id="sa-help">Practice aids disable achievements and permanent progression. History: last 48 positions, this session. A new action after undo replaces the redo path. Ctrl Shift F/O toggle foresight/omniscience.</div></div>';
        panel.innerHTML = panel.innerHTML.replace('Enable practice aids', 'Enable seed aids')
            .replace('Practice aids disable achievements and permanent progression. ', 'Achievements and progression remain enabled. ');
        document.body.appendChild(panel);
        var marks = document.createElement('canvas'); marks.id = 'sa-marks';
        marks.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:1001';
        document.body.appendChild(marks);
        document.addEventListener('visibilitychange', function () { lastRenderKey = null; render(); });
        var fit = document.createElement('style');
        fit.textContent = '#seed-aids{top:150px;bottom:auto;z-index:2000000;max-height:calc(100vh - 330px)}#sa-body{box-sizing:border-box;max-height:calc(100vh - 380px)}@media(max-height:720px){#seed-aids{top:105px;max-height:calc(100vh - 235px)}#sa-body{max-height:calc(100vh - 285px)}}';
        fit.textContent = fit.textContent.replace(/#seed-aids/g, '#seed-aids-v2');
        document.head.appendChild(fit);
        el('sa-enable').onclick = A.enable;
        el('sa-undo').onclick = function () { A.travel(-1); };
        el('sa-redo').onclick = function () { A.travel(1); };
        el('sa-collapse').onclick = function () { A.open = !A.open; el('sa-collapse').textContent = A.open ? '\u2212' : '+'; render(); };
        el('sa-fore').onclick = function () { A.foresight = !A.foresight; A.dirty = true; render(); };
        el('sa-omni').onclick = function () { A.omniscience = !A.omniscience; render(); };
        document.addEventListener('keydown', function (e) {
            if (!A.active || /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName) || e.target.isContentEditable) return;
            var k = (e.key || '').toLowerCase(), handled = true;
            if (e.ctrlKey && k === 'z') A.travel(e.shiftKey ? 1 : -1);
            else if (e.ctrlKey && k === 'y') A.travel(1);
            else if (e.ctrlKey && e.shiftKey && k === 'f') el('sa-fore').click();
            else if (e.ctrlKey && e.shiftKey && k === 'o') el('sa-omni').click();
            else handled = false;
            if (handled) { e.preventDefault(); e.stopImmediatePropagation(); }
        }, true);
        setInterval(function () {
            if (A.simulating) return;
            if (A.autoEnable && !A.active && boundary() && game.state === 'running') A.enable();
            if (A.active && !A.history.length && boundary()) { A.history = [A.capture()]; A.cursor = 0; }
            if (A.active) commit();
            render();
        }, 250);
        render();
        if (prior) $('#dump').hide();
        // The game's console marks evaluated commands as debug after evaluation.
        // A live installation must preserve the run's pre-install eligibility.
        setTimeout(function () { game.usedDebug = usedDebugBeforeInstall; game.seedAidsPractice = false; }, 0);
    });
}());

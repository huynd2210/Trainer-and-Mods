"""Exercise the shipped game in an isolated headless browser, never the live desktop."""
import json
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--use-gl=angle', '--use-angle=swiftshader', '--enable-webgl'])
    page = browser.new_page(viewport={"width": 1440, "height": 1000})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.route('**/*', lambda r: r.continue_() if r.request.url.startswith('http://127.0.0.1:8765/') else r.abort())
    page.goto('http://127.0.0.1:8765', wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && typeof WebGLRenderer !== "undefined"')
    page.wait_for_function('game.state === "updateSplash"', timeout=30000)
    page.evaluate("titleScreen(); game.seed='seed-aids-verification'; player.class=0; startGame(false,true)")
    page.wait_for_function('window.pc && game.state === "running" && !game.actionQueue.length && !dramaticEntranceFrames', timeout=60000)
    page.wait_for_timeout(1500)
    print(page.evaluate('({state:game.state,queue:game.actionQueue.length,menu:menu.menuOpen,pathing:player.pathing,magic:magic.state,aiming:player.aiming,entrance:dramaticEntranceFrames})'))
    page.evaluate('seedAids.enable(); seedAids.foresight=false')
    result = page.evaluate('''() => {
        const A=seedAids, checks=[];
        function assert(ok, msg) { if (!ok) throw Error(msg); checks.push(msg); }
        function fingerprint() {
            const data=save.generate(save.currentSaveId);
            // The on-screen elapsed-time clock advances between input boundaries.
            const g=JSON.parse(data.game); delete g.timer; data.game=JSON.stringify(g);
            // Save IDs and compressed field names depend on enumeration order. Decode
            // them and renumber graph references in sorted field order before comparing.
            const names=Object.fromEntries(Object.entries(data.nameDictionary).map(([k,v])=>[v,k]));
            const decoded=new Map();
            function decode(v) {
                if(typeof v==='string' && /^[{[]/.test(v)) { try { return decode(JSON.parse(v)); } catch(e) {} }
                if(!v || typeof v!=='object') return v;
                if(v.$o!==undefined) {
                    if(decoded.has(v.$o)) return decoded.get(v.$o);
                    const src=data.objectMap[v.$o], dst=Array.isArray(src)?[]:{};
                    decoded.set(v.$o,dst);
                    for(const k of Object.keys(src)) {
                        const name=Array.isArray(src)?k:names[k]||k;
                        // Time travel deliberately clears visual movement interpolation.
                        if(name!=='animateTiles') dst[name]=decode(src[k]);
                    }
                    return dst;
                }
                if(Array.isArray(v)) return v.map(decode);
                return Object.fromEntries(Object.entries(v).map(([k,v])=>[k,decode(v)]));
            }
            const root=decode(Object.fromEntries(Object.entries(data).filter(([k])=>!['objectMap','nameDictionary'].includes(k))));
            window.fpRoot=root;
            const seen=new Map(), nodes=[];
            function canonical(v) {
                if(!v || typeof v!=='object') return v;
                if(seen.has(v)) return {$ref:seen.get(v)};
                const id=nodes.length, dst=Array.isArray(v)?[]:{}; seen.set(v,id); nodes.push(dst);
                for(const k of Object.keys(v).sort()) dst[k]=canonical(v[k]);
                return {$ref:id};
            }
            canonical(root);
            return JSON.stringify({data:nodes, rng:ROT.RNG.getState(), state:game.state,
                index:map.levelIndex, visible:map.getCurrentLevel().tiles.map(col=>col.map(t=>t._playerVisible))});
        }
        function drain() { let n=0; while(game.actionQueue.length && !menu.menuOpen && game.state!=='dead') {
            if(++n>300) throw Error('queue limit'); input.processQueue();
        } }
        function action(command) { input.queueCommand(command,{shift:false}); drain(); }
        const before=fingerprint(), start=performance.now();
        const forecasts=A.forecast(), elapsed=performance.now()-start;
        assert(fingerprint()===before, 'Forecast leaves serialized world, FOV and RNG unchanged');
        assert(forecasts.length===9, 'Nine forecast directions including wait '+JSON.stringify({active:A.active,queue:game.actionQueue.length,state:game.state,menu:menu.menuOpen,magic:magic.state,path:player.pathing,entrance:dramaticEntranceFrames,aim:player.aiming}));
        const turn=game.turnCount, root=A.cursor;
        action('WAIT'); const after=fingerprint(), afterRoot=window.fpRoot;
        assert(game.turnCount>turn && A.cursor===root+1, 'Wait records one undo action');
        A.travel(-1);
        assert(game.turnCount===turn, 'Undo restores turn');
        action('WAIT');
        const replay=fingerprint();
        if(replay!==after) {
            const a=JSON.parse(after), b=JSON.parse(replay), diffs=[];
            function diff(a,b,path) {
                if(a===b || diffs.length>15) return;
                if(typeof a==='string' && typeof b==='string' && /^[{[]/.test(a)) {
                    try { return diff(JSON.parse(a),JSON.parse(b),path); } catch(e) {}
                }
                if(a && b && typeof a==='object' && typeof b==='object') {
                    for(const k of new Set([...Object.keys(a),...Object.keys(b)])) diff(a[k],b[k],path+'.'+k);
                } else diffs.push([path,String(a).slice(0,120),String(b).slice(0,120)]);
            } diff(a,b,''); throw Error('Replay mismatch '+JSON.stringify(diffs));
        }
        assert(replay===after, 'Replaying after undo produces identical world and RNG');
        A.travel(-1); A.travel(1);
        const redo=fingerprint();
        if(redo!==after) {
            const diffs=[], visited=new Map();
            function compare(a,b,path) {
                if(a===b || diffs.length>=12) return;
                if(a && b && typeof a==='object' && typeof b==='object') {
                    if(visited.get(a)?.has(b)) return;
                    if(!visited.has(a)) visited.set(a,new Set()); visited.get(a).add(b);
                    for(const k of new Set([...Object.keys(a),...Object.keys(b)])) compare(a[k],b[k],path+'.'+k);
                } else diffs.push([path,String(a).slice(0,80),String(b).slice(0,80)]);
            } compare(afterRoot,window.fpRoot,''); throw Error('Redo mismatch '+JSON.stringify(diffs));
        }
        assert(redo===after, 'Redo restores identical world and RNG');
        A.travel(-1); const oldLength=A.history.length;
        action('LEFT');
        assert(A.cursor===A.history.length-1 && A.history.length===oldLength, 'New action after undo replaces redo tail');
        // Direct graph restore: new properties, aliases, prototypes, counters and array tails.
        let snapshot=A.capture(), tile=pc.tile, originalHp=pc.hp, originalId=save.globalId;
        pc.hp=1; pc.testOnly=true; tile.testOnly=true; map.levels.push(null); save.globalId+=10;
        A.restore(snapshot);
        assert(pc.tile===tile && pc.hp===originalHp && !('testOnly' in pc) && !('testOnly' in tile)
            && save.globalId===originalId && pc instanceof Monster, 'Snapshots preserve aliases/prototypes and remove new properties');
        // Death must remain undoable, without deleting the original save or persisting progress.
        pc.hp=0.1; player.hunger=-1;
        A.history=[A.capture()]; A.cursor=0; A.pending=null;
        action('WAIT');
        assert(game.state==='dead', 'Fatal wait reaches real death');
        A.travel(-1);
        assert(game.state==='running' && pc.hp===0.1, 'Undo revives exact pre-death position');
        A.travel(1); assert(game.state==='running' && pc.hp===0.1 && A.cursor===0, 'Redo refuses a recorded death');
        pc.hp=pc.maxHp; player.hunger=100;
        // Move directly onto a real staircase to isolate and exercise the transition action.
        let stairs=map.getCurrentLevel().tiles.flat().find(t=>t.type==='staircase' &&
            t.getDestinationObject().levelIndex!==map.FANE_LEVEL && !t.collapsed);
        if(!stairs) throw Error('No test staircase');
        pc.tile.monster=null; pc.tile=stairs; stairs.monster=pc;
        A.history=[A.capture()]; A.cursor=0; A.pending=null;
        const oldFloor=map.levelIndex, oldTile=pc.tile;
        action('CONTEXTUAL_ACTION');
        assert(map.levelIndex!==oldFloor, 'Stair action changes floor');
        const newFloor=map.levelIndex, newTile=pc.tile, newRng=JSON.stringify(ROT.RNG.getState());
        A.travel(-1);
        assert(map.levelIndex===oldFloor && pc.tile===oldTile && oldTile.monster===pc, 'Undo crosses floor transition');
        A.travel(1);
        assert(map.levelIndex===newFloor && pc.tile===newTile && newTile.monster===pc
            && JSON.stringify(ROT.RNG.getState())===newRng, 'Redo crosses floor transition with exact RNG');
        // Unidentified potion information remains observation only.
        const unidentified=potions.gamePotions.find(p=>!p.identified); const id=unidentified.identified;
        const names=potions.getTrueName(unidentified);
        assert(names && unidentified.identified===id, 'True potion names do not identify gameplay inventory');
        const saved=save.generate(save.currentSaveId);
        assert(saved.seedAids.enabled && !game.usedDebug && JSON.stringify(saved.seedAids.rng)===JSON.stringify(ROT.RNG.getState()),
            'Game saves include RNG and aid flag without disabling eligibility');
        // An unfinished menu is undone as part of its action, including at the root.
        A.history=[A.capture()]; A.cursor=0; A.pending=null;
        action('MENU');
        assert(menu.menuOpen && A.pending, 'Opening a menu retains the pending action');
        A.travel(-1);
        assert(!menu.menuOpen && A.cursor===0 && game.state==='running', 'Undo cancels an unfinished menu at the root');
        action('REVOLVER');
        assert(game.revolverState==='aim' && A.pending && A.forecast().length===0,
            'Revolver aiming remains a pending choice with movement forecasts suspended');
        A.travel(-1);
        assert(game.revolverState==='idle' && A.cursor===0, 'Undo cancels revolver targeting as part of its action');
        const writes=[], write=storage.write;
        storage.write=function(k,v){writes.push(k); return write.apply(this,arguments)};
        player.saveRenown(); game.savePagesCollected(); game.saveWinStreaks(); game.saveBestTimes();
        storage.write=write;
        assert(['gkh.renown','gkh.pages','gkh.winStreaks','gkh.bestTimes'].every(k=>writes.includes(k)),
            'Actual progression writes remain enabled');
        // Put the real player next to a real hostile creature; run the same engine
        // input as the forecast and compare damage, player HP and RNG directly.
        const level=map.getCurrentLevel();
        const enemy=level.monsters.find(m=>!m.isPlayer && !m.isFriendly(pc) && !m.isInanimate()
            && m.tile.getAdjacentNeighbors().some(t=>t.isPassable() && !t.monster));
        if(!enemy) throw Error('No combat fixture');
        const beside=enemy.tile.getAdjacentNeighbors().find(t=>t.isPassable() && !t.monster);
        pc.tile.monster=null; pc.tile=beside; beside.monster=pc;
        pc.hp=pc.maxHp; player.hunger=100; player.fov();
        A.history=[A.capture()]; A.cursor=0; A.pending=null;
        const dx=enemy.tile.i-pc.tile.i, dy=enemy.tile.j-pc.tile.j;
        const forecast=A.forecast().find(f=>f.direction[1]===dx && f.direction[2]===dy);
        assert(A.forecast().find(f=>f.direction[0]==='WAIT').damage===0, 'Waiting does not report incoming damage as an outgoing attack');
        const hp=pc.hp, ehp=enemy.hp;
        action(forecast.direction[0]);
        assert(Math.abs((pc.hp-hp)-forecast.hp)<1e-8 &&
            Math.abs(Math.max(0,ehp-enemy.hp)-forecast.damage)<1e-8, 'Combat forecast matches actual incoming and outgoing damage');
        A.travel(-1);
        const roundTrip=save.generate(save.currentSaveId), expectedRng=JSON.stringify(ROT.RNG.getState());
        const previousPc=pc, expectedFloor=map.levelIndex, expectedX=pc.tile.i, expectedHp=pc.hp;
        save.loadSave({id:'headless-practice', compressed:false, data:roundTrip});
        assert(pc!==previousPc && A.active && game.state==='running' && map.levelIndex===expectedFloor
            && pc.tile.i===expectedX && pc.hp===expectedHp && JSON.stringify(ROT.RNG.getState())===expectedRng,
            'Save/load restores world, RNG and aid activation');
        A.foresight=true; A.dirty=true;
        return {checks,forecastMs:elapsed};
    }''')
    page.wait_for_timeout(1000)
    assert page.evaluate('game.state === "running" && !menu.menuOpen'), 'A discarded death callback interrupted the restored game'
    page.screenshot(path='mod-tests/verified.png')
    page.locator('#sa-map').hover(position={"x": 50, "y": 50})
    assert page.locator('#sa-inspect').inner_text().startswith('('), 'Omniscient floor hover inspection'
    page.keyboard.press('Control+Shift+o')
    assert not page.locator('#sa-knowledge').is_visible()
    page.keyboard.press('Control+Shift+o')
    assert page.locator('#sa-knowledge').is_visible()
    page.locator('#sa-collapse').click()
    assert not page.locator('#sa-body').is_visible()
    page.locator('#sa-collapse').click()
    assert page.locator('#sa-body').is_visible()
    page.wait_for_timeout(2000)
    assert page.evaluate('game.state === "running" && !menu.menuOpen'), 'No late death menu after undo'
    page.evaluate("document.getElementById('sa-body').scrollTop=99999")
    assert page.evaluate("document.getElementById('sa-help').getBoundingClientRect().bottom <= document.getElementById('sa-body').getBoundingClientRect().bottom+1"), 'Panel footer is reachable by scrolling'
    page.set_viewport_size({"width": 1280, "height": 720})
    page.wait_for_timeout(500)
    page.evaluate("document.getElementById('sa-body').scrollTop=99999")
    page.wait_for_timeout(750)
    assert page.evaluate("document.getElementById('sa-help').getBoundingClientRect().bottom <= document.getElementById('sa-body').getBoundingClientRect().bottom+1"), 'Periodic rendering preserves footer scroll position'
    page.screenshot(path='mod-tests/compact.png')
    assert not errors, errors
    print(json.dumps(result, indent=2))
    browser.close()

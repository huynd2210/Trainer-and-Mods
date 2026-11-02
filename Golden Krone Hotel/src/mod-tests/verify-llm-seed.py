"""Test the LLM seed-solving surface against the real game in isolated Chromium."""
import json
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--use-gl=angle', '--use-angle=swiftshader', '--enable-webgl'])
    page = browser.new_page(viewport={'width': 1440, 'height': 1000})
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.route('**/*', lambda r: r.continue_() if r.request.url.startswith('http://127.0.0.1:8765/') else r.abort())
    page.goto('http://127.0.0.1:8765', wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"', timeout=30000)
    page.evaluate("titleScreen();game.seed='llm-seed-solve-verification';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy && gkhLLM.observe().seedHarness.canForecast', timeout=60000)
    page.evaluate('seedAids.foresight=false;seedAids.dirty=true')
    result = page.evaluate('''async () => {
        const B=gkhLLM,A=seedAids,checks=[];
        function assert(ok,msg){if(!ok)throw Error(msg);checks.push(msg);}
        function fingerprint(){
            return JSON.stringify({turn:game.turnCount,floor:map.levelIndex,state:game.state,rng:ROT.RNG.getState(),
                pc:{x:pc.tile.i,y:pc.tile.j,hp:pc.hp,mp:pc.mp,statuses:pc.statuses.map(s=>[s.name,s.turnCount,s.turnLength])},
                player:[player.hunger,player.vampirism,player.gold,player.bullets,player.xp],
                potions:potions.gamePotions.map(p=>[p.name,p.count,p.identified]),
                tiles:map.getCurrentLevel().tiles.map(col=>col.map(t=>[t.type,t.light,t._playerVisible,
                    t.item&&t.item.getName(),t.monster&&[t.monster.name,t.monster.hp,t.monster.actionState,t.monster.targetLastSeen&&[t.monster.targetLastSeen.i,t.monster.targetLastSeen.j]]]))});
        }
        async function action(extra){const s=B.observe();return B.seedAct(Object.assign({requestId:crypto.randomUUID(),expectedRevision:s.revision},extra));}
        async function reject(extra,word){let threw=false;try{await action(extra);}catch(e){threw=e.message.includes(word);}assert(threw,'Reject '+word);}
        const before=fingerprint(), normal=B.observe(), mapData=B.seedObserve();
        const level=map.getCurrentLevel(), unknown=level.tiles.flat().find(t=>!t.canPlayerSee());
        assert(mapData.omniscience.tiles.length===level.tiles.flat().length,'Omniscience returns every current-floor tile');
        assert(unknown && mapData.omniscience.tiles.some(t=>t.x===unknown.i&&t.y===unknown.j&&!t.visible),'Omniscience includes unseen terrain');
        assert(!normal.tiles.some(t=>t.x===unknown.i&&t.y===unknown.j),'Ordinary observation does not gain omniscience');
        assert(mapData.omniscience.potions.every(p=>p.trueName&&p.count>0),'Carried potions include true identities');
        assert(fingerprint()===before,'Omniscience does not alter world, potion identification, FOV or RNG');
        const writes=[], realWrite=storage.write, realCheevo=steam.setCheevo;
        storage.write=function(){writes.push('storage');return realWrite.apply(this,arguments)};
        steam.setCheevo=function(){writes.push('cheevo');return realCheevo.apply(this,arguments)};
        const forecast=B.seedForecast(B.observe().revision);
        storage.write=realWrite;steam.setCheevo=realCheevo;
        assert(forecast.forecasts.length===9&&forecast.forecasts.some(f=>f.command==='WAIT'),'Foresight returns the nine actual next-input alternatives');
        assert(forecast.forecasts.filter(f=>f.dx&&f.dy).every(f=>f.disabled),'Disabled diagonals are reported');
        assert(fingerprint()===before && !writes.length,'Foresight restores world/FOV/RNG and performs no storage or achievement writes');
        let stale=false;try{B.seedForecast(-1);}catch(e){stale=e.message.includes('Stale');}assert(stale,'Stale forecast revisions are rejected');
        B.control(false);
        assert(B.seedObserve().omniscience.tiles.length>0&&B.seedForecast(B.observe().revision).forecasts.length===9,'Solver reads remain available while remote actions are paused');
        await reject({type:'seed',operation:'undo'},'paused');B.control(true);
        const originalTurn=game.turnCount, originalRng=JSON.stringify(ROT.RNG.getState()), c=B.observe().seedHarness.cursor;
        await action({type:'command',command:'WAIT'});const after=fingerprint(), afterTurn=game.turnCount;
        assert(B.observe().seedHarness.cursor===c+1 && afterTurn>originalTurn,'Normal actions record seed history');
        const undoPayload={type:'seed',operation:'undo',expectedRevision:B.observe().revision,requestId:'undo-one'};
        await B.seedAct(undoPayload);
        assert(game.turnCount===originalTurn&&JSON.stringify(ROT.RNG.getState())===originalRng&&B.observe().seedHarness.canRedo,'Undo restores actual turn/RNG and enables redo');
        const undoState=fingerprint();await B.seedAct(undoPayload);
        assert(fingerprint()===undoState&&B.observe().seedHarness.cursor===c,'Duplicate undo request does not undo twice');
        await action({type:'seed',operation:'redo'});
        assert(fingerprint()===after,'Redo restores identical actual world and RNG');
        await action({type:'seed',operation:'undo'});
        await action({type:'command',command:'WAIT'});
        assert(fingerprint()===after&&!B.observe().seedHarness.canRedo,'Undo/replay is deterministic and discards the redo tail');
        await action({type:'seed',operation:'undo'});
        await B.act({type:'command',command:'WAIT',expectedRevision:B.observe().revision,requestId:'compare-normal-wait'});
        assert(fingerprint()===after,'Safe guarded move has identical world/RNG outcome to ordinary input');
        await action({type:'command',command:'MENU'});
        assert(B.observe().seedHarness.pending&&!B.observe().seedHarness.canForecast,'Open menu reports pending action and suspends foresight');
        let unavailable=false;try{B.seedForecast(B.observe().revision);}catch(e){unavailable=e.message.includes('unavailable');}assert(unavailable,'Forecast rejects unfinished choices');
        const menuTurn=game.turnCount,cursor=B.observe().seedHarness.cursor;
        await action({type:'seed',operation:'undo'});
        assert(!menu.menuOpen&&game.turnCount===menuTurn&&B.observe().seedHarness.cursor===cursor,'Undo cancels a pending menu without stepping back a completed turn');
        await action({type:'command',command:'REVOLVER'});
        await action({type:'seed',operation:'undo'});
        assert(game.revolverState==='idle','Undo cancels pending targeting');
        await reject({type:'seed',operation:'redo'},'No redo');
        await reject({type:'seed',operation:'teleport'},'operation');
        await action({type:'command',command:'HOTEL_MAP'});
        assert(B.seedObserve().state.state==='bigMap','Seed-only tools can observe the hotel-map view');
        for(let i=0;i<3&&game.state!=='running';i++)await action({type:'command',command:'ESCAPE'});
        assert(game.state==='running','Seed-only tools can close the hotel-map view without a normal-tool fallback');
        await reject({type:'command',command:'REST'},'one move');
        await reject({type:'command',command:'RIGHT',shift:true},'one move');
        let noCheat=false;try{await B.act({type:'seed',operation:'undo',expectedRevision:B.observe().revision,requestId:'normal-no-cheat'});}catch(e){noCheat=e.message.includes('normal actions');}
        assert(noCheat,'Normal action tool refuses timeline cheats');
        // Establish a stable fixture at a real staircase, then cross it normally.
        const stairs=level.tiles.flat().find(t=>t.type==='staircase'&&!t.collapsed&&t.getDestinationObject().levelIndex!==map.FANE_LEVEL);
        pc.tile.monster=null;pc.tile=stairs;stairs.monster=pc;player.fov();
        A.history=[A.capture()];A.cursor=0;A.pending=null;
        const floorBefore=map.levelIndex,xBefore=pc.tile.i,yBefore=pc.tile.j;
        await action({type:'command',command:'CONTEXTUAL_ACTION'});
        const newFloor=map.levelIndex,newRng=JSON.stringify(ROT.RNG.getState());
        assert(newFloor!==floorBefore,'Real staircase changes floor');
        await action({type:'seed',operation:'undo'});
        assert(map.levelIndex===floorBefore&&pc.tile.i===xBefore&&pc.tile.j===yBefore,'Undo crosses floor boundary');
        await action({type:'seed',operation:'redo'});
        assert(map.levelIndex===newFloor&&JSON.stringify(ROT.RNG.getState())===newRng,'Redo crosses floor boundary and restores RNG');
        // Death recovery must work even during the game's delayed game-over callback.
        pc.hp=.1;player.hunger=-1;A.history=[A.capture()];A.cursor=0;A.pending=null;
        const fatalBefore=fingerprint(),fatalCursor=A.cursor,fatalHistory=A.history.length, fatalDOM=document.querySelector('#modal').innerHTML;
        const lethalPayload={type:'command',command:'WAIT',expectedRevision:B.observe().revision,requestId:'refuse-fatal'};
        let refused=false;try{await B.seedAct(lethalPayload);}catch(e){refused=e.message.includes('leads to death');}
        assert(refused,'Seed action refuses a fatal next WAIT');
        assert(fingerprint()===fatalBefore&&A.cursor===fatalCursor&&A.history.length===fatalHistory&&!A.pending&&document.querySelector('#modal').innerHTML===fatalDOM,'Fatal refusal restores world/RNG/resources/history and rendered DOM');
        refused=false;try{await B.seedAct(lethalPayload);}catch(e){refused=e.message.includes('leads to death');}
        assert(refused&&fingerprint()===fatalBefore,'Duplicate fatal request remains refused without taking a turn');
        let mismatch=false;try{await B.act(lethalPayload);}catch(e){mismatch=e.message.includes('different arguments');}
        assert(mismatch&&fingerprint()===fatalBefore,'Request cache cannot bypass refusal by switching to the normal endpoint');
        const move=Object.entries({UP:[0,-1],DOWN:[0,1],LEFT:[-1,0],RIGHT:[1,0]}).find(([key,d])=>{const t=map.getTile(pc.tile.i+d[0],pc.tile.j+d[1]);return t&&t.isPassable()&&!t.monster;});
        assert(!!move,'Lethal movement fixture has a free adjacent tile');
        await reject({type:'command',command:move[0]},'leads to death');
        assert(fingerprint()===fatalBefore,'Refused fatal movement does not move the player');
        // An aimed revolver shot must also be checked, not just the nine-way forecast.
        await action({type:'command',command:'REVOLVER'});
        const aimed=B.observe(),target=aimed.tiles.find(t=>t.visible&&t.passable&&(t.x!==aimed.player.x||t.y!==aimed.player.y));
        await action({type:'target',x:target.x,y:target.y});
        const aimedBefore=fingerprint(),bullets=player.bullets;
        await reject({type:'command',command:'CONFIRM_TARGET'},'leads to death');
        assert(player.bullets===bullets&&game.revolverState==='aim'&&fingerprint()===aimedBefore,'Fatal shot refusal preserves ammunition and targeting');
        await action({type:'seed',operation:'undo'});
        magic.learnedSpells[0]='ice';magic.spellLevels.ice=1;pc.maxMp=100;pc.mp=100;
        await action({type:'command',command:'ABILITY_1'});
        const spellBefore=fingerprint(),mp=pc.mp;
        await reject({type:'command',command:move[0]},'leads to death');
        assert(pc.mp===mp&&magic.state==='waiting'&&fingerprint()===spellBefore,'Fatal spell refusal preserves mana and aiming');
        await action({type:'seed',operation:'undo'});
        // A real potion menu choice must preserve the potion on refusal.
        const poison=potions.gamePotions.find(p=>p.name==='Volatile Poison');
        assert(!!poison,'Real Volatile Poison potion fixture is available');poison.count=1;
        if(!potions.seenPotions.includes(poison))potions.seenPotions.push(poison);
        await action({type:'command',command:'POTIONS'});
        const potionBefore=fingerprint(),potionIndex=menu.menuList.indexOf(poison);
        await reject({type:'choose',index:potionIndex},'leads to death');
        assert(poison.count===1&&menu.menuOpen&&fingerprint()===potionBefore,'Fatal potion choice preserves inventory, menu and world');
        await action({type:'seed',operation:'undo'});
        // The ordinary tool is intentionally unrestricted, even with seed aids active.
        const fatalNormal={type:'command',command:'WAIT',expectedRevision:B.observe().revision,requestId:'normal-fatal'};
        const normalDeath=B.act(fatalNormal);
        // Recovery test need not wait for the bridge's 15-second death animation timeout.
        while(game.actionQueue.length&&game.state==='running')input.processQueue();
        await normalDeath;
        assert((game.state==='dead'||menu.previousState==='dead')&&B.observe().seedHarness.canUndo,'Death retains a recoverable seed position '+JSON.stringify({state:game.state,previous:menu.previousState,hp:pc.hp,hunger:player.hunger,harness:B.observe().seedHarness}));
        await action({type:'seed',operation:'undo'});
        assert(game.state==='running'&&pc.hp===.1,'Bridge undo revives the player from the actual death/menu position');
        await reject({type:'seed',operation:'redo'},'dead position');
        const aliveBefore=fingerprint();A.travel(1);
        assert(fingerprint()===aliveBefore,'Direct seed-harness redo also refuses death');
        const originalPreview=A.previewAction;A.previewAction=function(){throw Error('injected unavailable check')};
        await reject({type:'command',command:'WAIT'},'unavailable check');A.previewAction=originalPreview;
        assert(fingerprint()===aliveBefore,'Unavailable death check refuses execution without a normal-input fallback');
        pc.hp=pc.maxHp;player.hunger=90;
        player.drawUI();player.updateStats();
        // Return a stable display fixture whose root agrees with the world.
        A.history=[A.capture()];A.cursor=0;A.pending=null;A.foresight=true;A.dirty=true;
        return {checks,observation:B.seedObserve(),forecast:B.seedForecast(B.observe().revision)};
    }''')
    page.wait_for_timeout(600)
    assert page.evaluate('game.state === "running"'), 'Discarded death callback affected recovered run'
    result['observation'] = page.evaluate('gkhLLM.seedObserve()')
    page.screenshot(path='mod-tests/llm-seed-verified.png')
    with open('mod-tests/llm-seed-observation.json','w',encoding='utf-8') as file:
        json.dump(result['observation'],file,indent=2)
    page.evaluate('pc.hp=.1;player.hunger=-1;player.drawUI();player.updateStats();seedAids.dirty=true')
    page.wait_for_timeout(600)
    page.screenshot(path='mod-tests/llm-seed-fatal-guard.png')
    fatal_render = page.evaluate('''() => ({state:gkhLLM.seedObserve(),forecasts:seedAids.forecasts.map(f=>({command:f.direction[0],dead:f.dead,disabled:f.disabled})),buttons:Array.from(document.querySelectorAll('#sa-grid button')).map(b=>({text:b.textContent,disabled:b.disabled}))})''')
    assert all(b['disabled'] for b,f in zip(fatal_render['buttons'],fatal_render['forecasts']) if f.get('dead')), 'Fatal forecast buttons must be disabled'
    assert fatal_render['forecasts'][4]['dead'] and fatal_render['buttons'][4]['disabled'], 'Fatal WAIT must be visibly disabled'
    with open('mod-tests/llm-seed-fatal-guard.json','w',encoding='utf-8') as file:
        json.dump(fatal_render,file,indent=2)
    print(json.dumps({'checks':result['checks'],'errors':errors},indent=2))
    assert not errors, errors
    browser.close()

"""Real engine with RAF stopped: tool actions must settle without window focus."""
import json
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,args=['--use-gl=angle','--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1440,'height':1000})
    errors=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('**/*',lambda r:r.continue_() if r.request.url.startswith('http://127.0.0.1:8765/') else r.abort())
    page.goto('http://127.0.0.1:8765',wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"',timeout=30000)
    page.evaluate("titleScreen();game.seed='llm-background-verification';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy',timeout=60000)
    page.evaluate('''() => {
        seedAids.foresight=false;seedAids.dirty=true;
        window.requestAnimationFrame=function(){return 0};
        const update=advanceGameFrame;window.remoteFrames=0;window.visibleFrames=0;
        advanceGameFrame=function(t,remote){if(remote)remoteFrames++;else visibleFrames++;return update(t,remote)};
    }''')
    page.wait_for_timeout(300)
    result=page.evaluate('''async () => {
        const B=gkhLLM,A=seedAids,checks=[];
        function assert(ok,msg){if(!ok)throw Error(msg);checks.push(msg)}
        async function act(extra){const s=B.observe();return B.seedAct({expectedRevision:s.revision,requestId:crypto.randomUUID(),...extra})}
        const nativeFrames=visibleFrames,turn=game.turnCount,rng=JSON.stringify(ROT.RNG.getState());
        const wait=await act({type:'command',command:'WAIT'});
        assert(wait.settled&&game.turnCount===turn+1&&remoteFrames>0,'Guarded WAIT settles through background frames with RAF stopped');
        assert(visibleFrames===nativeFrames,'Remote input needs no visible RAF frame');
        await act({type:'seed',operation:'undo'});
        assert(game.turnCount===turn&&JSON.stringify(ROT.RNG.getState())===rng,'Background undo restores exact turn/RNG');
        await act({type:'seed',operation:'redo'});
        assert(game.turnCount===turn+1,'Background redo restores the recorded move');
        await act({type:'command',command:'MENU'});
        assert(B.observe().menu.open&&B.observe().menu.choices.length>0,'Menu labels remain observable without RAF');
        await act({type:'choose',index:0});
        assert(B.observe().menu.id==='character sheet'&&(B.observe().menu.title+B.observe().menu.text).includes('Sorina'),'Menu choice executes without RAF '+JSON.stringify(B.observe().menu));
        await act({type:'command',command:'ESCAPE'});
        const level=map.getCurrentLevel(),stairs=level.tiles.flat().find(t=>t.type==='staircase'&&!t.collapsed&&t.getDestinationObject().levelIndex!==map.FANE_LEVEL);
        pc.tile.monster=null;pc.tile=stairs;stairs.monster=pc;player.fov();
        A.history=[A.capture()];A.cursor=0;A.pending=null;
        const floor=map.levelIndex;
        const crossing=await act({type:'command',command:'CONTEXTUAL_ACTION'});
        assert(crossing.settled&&map.levelIndex!==floor,'Stairs and their queued tick finish without RAF');
        await act({type:'seed',operation:'undo'});
        assert(map.levelIndex===floor,'Undo across floors works with background scheduling');
        const idleTurn=game.turnCount,idleRng=JSON.stringify(ROT.RNG.getState()),idleFrames=remoteFrames;
        B.seedObserve();B.seedForecast(B.observe().revision);
        await new Promise(r=>A.defer(r,250));
        assert(game.turnCount===idleTurn&&JSON.stringify(ROT.RNG.getState())===idleRng&&remoteFrames===idleFrames,'Idle/read-only requests do not pump frames or advance gameplay');
        pc.hp=.1;player.hunger=-1;
        const fatalTurn=game.turnCount,fatalRng=JSON.stringify(ROT.RNG.getState()),fatalFrames=remoteFrames;
        let refused=false;try{await act({type:'command',command:'WAIT'})}catch(e){refused=e.message.includes('leads to death')}
        assert(refused&&pc.hp===.1&&game.turnCount===fatalTurn&&JSON.stringify(ROT.RNG.getState())===fatalRng&&remoteFrames===fatalFrames,'Death guard refuses fatal background input without pumping/executing it');
        pc.hp=pc.maxHp;player.hunger=90;player.drawUI();player.updateStats();
        A.history=[A.capture()];A.cursor=0;A.pending=null;
        return {checks,remoteFrames,visibleFrames,observation:B.seedObserve()};
    }''')
    page.screenshot(path='mod-tests/llm-background-verified.png')
    with open('mod-tests/llm-background-observation.json','w',encoding='utf-8') as f:json.dump(result['observation'],f,indent=2)
    print(json.dumps({k:v for k,v in result.items() if k!='observation'},indent=2))
    assert not errors,errors
    browser.close()

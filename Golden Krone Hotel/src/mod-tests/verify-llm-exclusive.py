"""Exclusive ownership in the real engine; browser input is confined to headless fixtures."""
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
    page.evaluate("titleScreen();game.seed='llm-exclusive-verification';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy',timeout=60000)
    page.evaluate('''() => {
        seedAids.foresight=false;seedAids.dirty=true;
        window.checks=[];
        window.assertCheck=(ok,msg)=>{if(!ok)throw Error(msg);checks.push(msg)};
        window.toolAct=extra=>gkhLLM.seedAct({expectedRevision:gkhLLM.observe().revision,requestId:crypto.randomUUID(),...extra});
        // A held key from before takeover must not survive acquisition.
        input.inputCache.ARROWDOWN={lastIssue:0,repeat:0};
        gkhLLM.control(true,true);
        assertCheck(gkhLLM.observe().exclusive&&Object.keys(input.inputCache).length===0,'Takeover clears previously held inputs');
        window.beforeLocal=JSON.stringify([game.turnCount,pc.hp,pc.tile.i,pc.tile.j,ROT.RNG.getState(),game.selectedTile&&[game.selectedTile.i,game.selectedTile.j]]);
    }''')
    page.keyboard.down('ArrowDown');page.wait_for_timeout(400);page.keyboard.up('ArrowDown')
    page.mouse.move(700,450);page.mouse.click(700,450);page.mouse.click(750,450,button='right')
    page.locator('#menu-button').click(force=True)
    page.keyboard.press('m');page.keyboard.press('Control+z');page.keyboard.press('Control+Shift+f')
    page.evaluate('''() => {
        const previous=controller;
        controller={axes:[1,0,0,0],buttons:Array.from({length:16},()=>({pressed:true}))};
        input.handleController();input.processInputs();controller=previous;
        assertCheck(Object.keys(input.inputCache).length===0,'Controller and cached key polling ignore local presses while locked');
        assertCheck(beforeLocal===JSON.stringify([game.turnCount,pc.hp,pc.tile.i,pc.tile.j,ROT.RNG.getState(),game.selectedTile&&[game.selectedTile.i,game.selectedTile.j]])&&!menu.menuOpen&&!game.actionQueue.length,'Keyboard, board click/right-click/hover, HUD menu and seed shortcuts leave world and targeting unchanged');
    }''')
    page.evaluate('''async () => {
        const turn=game.turnCount;
        await toolAct({type:'command',command:'WAIT'});
        assertCheck(game.turnCount===turn+1&&gkhLLM.observe().exclusive,'Guarded tool action works while local input is locked');
        await toolAct({type:'seed',operation:'undo'});
        await toolAct({type:'seed',operation:'redo'});
        assertCheck(game.turnCount===turn+1&&gkhLLM.observe().exclusive,'Undo and redo retain exclusive ownership');
        window.seedLocalBefore=JSON.stringify([game.turnCount,seedAids.cursor,seedAids.foresight,ROT.RNG.getState()]);
    }''')
    page.locator('#sa-undo').click(force=True)
    page.locator('#sa-fore').click(force=True)
    page.keyboard.press('Control+z');page.keyboard.press('Control+y')
    page.evaluate('''async () => {
        assertCheck(seedLocalBefore===JSON.stringify([game.turnCount,seedAids.cursor,seedAids.foresight,ROT.RNG.getState()]),'Local seed-panel clicks and shortcuts cannot undo, redo or change foresight');
        await toolAct({type:'command',command:'MENU'});
        window.menuBefore=JSON.stringify([menu.id,menu.menuListSelection,game.turnCount]);
    }''')
    page.locator('.menu-list-item').first.click(force=True)
    page.keyboard.press('a');page.keyboard.press('Escape')
    page.evaluate('''async () => {
        assertCheck(menuBefore===JSON.stringify([menu.id,menu.menuListSelection,game.turnCount]),'Local menu click, letter selection and Escape are blocked');
        await toolAct({type:'choose',index:0});
        assertCheck(menu.id==='character sheet','Remote menu choice bypasses the local event lock');
        await toolAct({type:'command',command:'ESCAPE'});
        gkhLLM.control(false);
        assertCheck(!gkhLLM.observe().enabled&&gkhLLM.observe().exclusive,'Remote pause retains watch-only protection');
        gkhLLM.control(true,true);
        pc.hp=.1;player.hunger=-1;
        const turn=game.turnCount,rng=JSON.stringify(ROT.RNG.getState());
        let refused=false;try{await toolAct({type:'command',command:'WAIT'})}catch(e){refused=e.message.includes('leads to death')}
        assertCheck(refused&&game.turnCount===turn&&pc.hp===.1&&JSON.stringify(ROT.RNG.getState())===rng&&gkhLLM.observe().exclusive,'Fatal input is refused without changing world or exclusive ownership');
        pc.hp=pc.maxHp;player.hunger=90;player.drawUI();player.updateStats();
        seedAids.history=[seedAids.capture()];seedAids.cursor=0;seedAids.pending=null;
    }''')
    # Actual rendered watch UI, including a smaller supported window.
    for width,height in [(1440,1000),(1280,720)]:
        page.set_viewport_size({'width':width,'height':height});page.wait_for_timeout(500)
        page.screenshot(path=f'mod-tests/llm-exclusive-{width}.png')
    # Release during a move, not just while idle. The release click must never leak
    # to the game's global click handlers after it unlocks the event gate.
    page.evaluate('''() => {
        window.releaseTurn=game.turnCount;
        window.inFlight=toolAct({type:'command',command:'WAIT'});
        document.querySelector('#llm-watch-control button').click();
        assertCheck(!gkhLLM.observe().enabled&&gkhLLM.observe().exclusive&&gkhLLM.observe().releasing,'Take control immediately pauses new actions and retains lock until accepted work settles');
    }''')
    page.evaluate('''async () => {
        await inFlight;
        assertCheck(!gkhLLM.observe().exclusive&&!gkhLLM.observe().releasing&&!gkhLLM.observe().enabled&&game.turnCount===releaseTurn+1,'Take control unlocks only after accepted move; release click causes no extra turn');
        const s=gkhLLM.observe();let refused=false;
        try{await gkhLLM.seedAct({type:'command',command:'WAIT',expectedRevision:s.revision,requestId:crypto.randomUUID()})}catch(e){refused=e.message.includes('paused')}
        assertCheck(refused,'Human handoff refuses subsequent remote actions');
    }''')
    page.locator('#menu-button').click(force=True)
    page.evaluate("assertCheck(menu.menuOpen,'Local gameplay/menu input works again after release')")
    page.evaluate('''() => {
        gkhLLM.control(true,true);
        window.pointerReleaseBefore=game.turnCount;
    }''')
    page.locator('#llm-watch-control button').click()
    page.evaluate("assertCheck(!gkhLLM.observe().exclusive&&!gkhLLM.observe().enabled&&game.turnCount===pointerReleaseBefore,'Real pointer click on Take control releases ownership without a game action')")
    print(json.dumps(page.evaluate('checks'),indent=2))
    assert not errors,errors
    browser.close()

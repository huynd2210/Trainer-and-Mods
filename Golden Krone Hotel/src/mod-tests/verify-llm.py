"""Real shipped-engine tests; isolated browser storage, no Steam or desktop control."""
import json
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=['--use-gl=angle', '--use-angle=swiftshader', '--enable-webgl'])
    page = browser.new_page(viewport={'width': 1440, 'height': 1000})
    page.add_init_script("localStorage.setItem('gkh.tutorialComplete', 'true')")
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.route('**/*', lambda r: r.continue_() if r.request.url.startswith('http://127.0.0.1:8765/') else r.abort())
    page.goto('http://127.0.0.1:8765', wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"', timeout=30000)
    print('Initial state:', page.evaluate('({state:game.state,items:gkhLLM.observe().screen.titleItems})'))
    page.evaluate('gkhLLM.control(true,true)')
    # Start a run exclusively through normal menu commands exposed to the LLM.
    def command(name):
        page.wait_for_function('!gkhLLM.observe().busy', timeout=30000)
        return page.evaluate('''async name => {
            const s=gkhLLM.observe();
            return await gkhLLM.act({type:'command',command:name,expectedRevision:s.revision,requestId:crypto.randomUUID()});
        }''', name)
    command('ESCAPE')
    command('ESCAPE')
    command('CONFIRM')
    print('Before class selection:', page.evaluate('({state:game.state, title:title.state})'))
    page.evaluate("game.seed='llm-bridge-verification'")
    command('CONFIRM')
    page.wait_for_function('window.pc && game.state === "running" && !gkhLLM.observe().busy', timeout=60000)
    page.evaluate('seedAids.foresight=false; seedAids.dirty=true')
    print('Started through bridge:', page.evaluate('({state:game.state,turn:game.turnCount})'))
    result = page.evaluate('''async () => {
        const A=gkhLLM, checks=[];
        function assert(ok,msg) {if(!ok) throw Error(msg); checks.push(msg);}
        async function bad(a,word) {let failed=false;try {await A.act(a);}catch(e){failed=e.message.includes(word);}assert(failed,'Reject '+word);}
        async function action(extra) {
            const s=A.observe(); return A.act(Object.assign({requestId:crypto.randomUUID(),expectedRevision:s.revision},extra));
        }
        let s=A.observe();
        assert(s.player.hp===pc.hp && s.player.x===pc.tile.i && s.floor.index===map.levelIndex,'Observation matches actual player and floor');
        assert(s.tiles.filter(t=>t.visible).length===map.getCurrentLevel().tiles.flat().filter(t=>t.canPlayerSee()).length,'Only currently visible tiles have visible=true');
        assert(s.potions.every(p=>potions.gamePotions.some(v=>v.count===p.count && p.name===potions.getPotionName(v))),'Potion names use player identification');
        const rng=JSON.stringify(ROT.RNG.getState()), turn=game.turnCount, hp=pc.hp;
        for(let i=0;i<4;i++) A.observe();
        assert(JSON.stringify(ROT.RNG.getState())===rng && game.turnCount===turn && pc.hp===hp,'Observation does not advance turns or RNG');
        await bad({type:'command',command:'WAIT',requestId:'stale',expectedRevision:-1},'Stale');
        await bad({type:'command',command:'CONSOLE',requestId:'console',expectedRevision:A.observe().revision},'prohibited');
        await bad({type:'choose',index:-1,requestId:'negative',expectedRevision:A.observe().revision},'Invalid');
        await bad({type:'command',command:'WAIT',expectedRevision:A.observe().revision},'requestId');
        A.control(false);
        await bad({type:'command',command:'WAIT',requestId:'paused',expectedRevision:A.observe().revision},'paused');
        A.control(true);
        const payload={type:'command',command:'WAIT',requestId:'one-wait',expectedRevision:A.observe().revision};
        const first=A.act(payload);
        await bad({type:'command',command:'WAIT',requestId:'concurrent',expectedRevision:payload.expectedRevision},'busy');
        const reply=await first, after=game.turnCount;
        assert(reply.settled && after>turn,'WAIT spends a real turn and returns settled observation');
        await A.act(payload);
        assert(game.turnCount===after,'Duplicate request does not spend another turn');
        await bad(Object.assign({},payload,{command:'LEFT'}),'different arguments');
        await action({type:'command',command:'MENU'});
        s=A.observe();
        assert(s.menu.open && s.menu.choices.length===menu.menuList.length && s.menu.choices[0].text.includes('Character Sheet'),'Menu choices include rendered labels');
        await action({type:'choose',index:0});
        assert((A.observe().menu.title+A.observe().menu.text).includes('Sorina'),'Choosing a menu option executes real callback');
        await action({type:'command',command:'ESCAPE'});
        await action({type:'command',command:'POTIONS'});
        assert(A.observe().menu.open && A.observe().menu.text.length>0,'Potion menu is observable');
        await action({type:'command',command:'ESCAPE'});
        await action({type:'command',command:'REVOLVER'});
        assert(A.observe().targeting.revolver==='aim','Revolver enters real targeting');
        await action({type:'target',x:pc.tile.i,y:pc.tile.j});
        assert(game.selectedTile===pc.tile,'Target selects actual map tile');
        await bad({type:'target',x:-999,y:0,requestId:'offmap',expectedRevision:A.observe().revision},'visible');
        await action({type:'command',command:'ESCAPE'});
        await action({type:'command',command:'EXAMINE'});
        await action({type:'target',x:pc.tile.i,y:pc.tile.j});
        await action({type:'command',command:'CONFIRM'});
        await action({type:'command',command:'ESCAPE'});
        if (game.examineState==='examine') await action({type:'command',command:'ESCAPE'});
        assert(game.examineState==='idle','Examine and its help hint can be dismissed');
        // Exercise the real staircase action, isolating only fixture placement.
        const oldFloor=map.levelIndex;
        const stairs=map.getCurrentLevel().tiles.flat().find(t=>t.type==='staircase' && !t.collapsed && t.getDestinationObject().levelIndex!==map.FANE_LEVEL);
        assert(!!stairs,'Fixture has real staircase');
        pc.tile.monster=null;pc.tile=stairs;stairs.monster=pc;player.fov();
        const transition=await action({type:'command',command:'CONTEXTUAL_ACTION'});
        assert(transition.settled && map.levelIndex!==oldFloor && transition.state.floor.index===map.levelIndex,'Contextual action crosses floors and reports new floor '+JSON.stringify({oldFloor,newFloor:map.levelIndex,settled:transition.settled,state:game.state,menu:transition.state.menu,messages:transition.state.messages,target:transition.state.targeting}));
        assert(window.seedAids && seedAids.active && !game.usedDebug,'Existing seed aids remain active');
        // Combat fixture placement uses real monsters; attacks still use normal inputs.
        const enemy=map.getCurrentLevel().monsters.find(m=>!m.isPlayer && !m.isFriendly(pc) && !m.isInanimate() &&
            m.tile.getAdjacentNeighbors().some(t=>t.isPassable() && !t.monster));
        assert(!!enemy,'Fixture has a real hostile creature');
        const beside=enemy.tile.getAdjacentNeighbors().find(t=>t.isPassable() && !t.monster);
        pc.tile.monster=null;pc.tile=beside;beside.monster=pc;pc.hp=pc.maxHp;player.hunger=100;player.fov();
        const ammo=player.bullets, combatTurn=game.turnCount;
        await action({type:'command',command:'REVOLVER'});
        await action({type:'target',x:enemy.tile.i,y:enemy.tile.j});
        await action({type:'command',command:'CONFIRM_TARGET'});
        assert(player.bullets===ammo-1 && game.turnCount>combatTurn,'Aimed revolver shot spends ammunition and a turn');
        // Supply a known spell to isolate the directional casting control path.
        magic.learnedSpells[0]='ice';magic.spellLevels.ice=1;pc.maxMp=100;pc.mp=100;
        const mana=pc.mp, spellTurn=game.turnCount;
        await action({type:'command',command:'ABILITY_1'});
        assert(magic.state==='waiting','Directional spell enters targeting');
        const aim=pc.tile.getAdjacentNeighbors().find(t=>t.canPlayerSee() && t.isPassable());
        assert(!!aim,'Fixture has a visible spell direction');
        await action({type:'target',x:aim.i,y:aim.j});
        await action({type:'command',command:'CONFIRM_TARGET'});
        assert(magic.state==='idle' && pc.mp<mana && game.turnCount>spellTurn,'Target confirmation casts directional spell through real engine');
        return {checks,state:A.observe()};
    }''')
    print(json.dumps({'checks': result['checks'], 'errors': errors}, indent=2))
    if errors:
        raise AssertionError(errors)
    page.screenshot(path='mod-tests/llm-verified.png')
    with open('mod-tests/llm-observation.json', 'w', encoding='utf-8') as file:
        json.dump(result['state'], file, indent=2)
    browser.close()

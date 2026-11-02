"""Ring acquisition must simulate full gameplay without touching its UI canvas."""
import json
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,args=['--use-gl=angle','--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1440,'height':1000})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.route('**/*',lambda r:r.continue_() if r.request.url.startswith('http://127.0.0.1:8765/') else r.abort())
    page.goto('http://127.0.0.1:8765',wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"',timeout=30000)
    page.evaluate("titleScreen();game.seed='llm-ring-canvas-verification';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy',timeout=60000)
    result=page.evaluate('''async () => {
        const B=gkhLLM,A=seedAids,checks=[];
        function assert(ok,msg){if(!ok)throw Error(msg);checks.push(msg)}
        A.foresight=false;A.dirty=true;B.control(true,true);
        const level=map.getCurrentLevel();
        level.monsters.forEach(m=>{if(m.tile&&m.tile.monster===m)m.tile.monster=null});level.monsters=[];
        const dirs=[['UP',0,-1],['DOWN',0,1],['LEFT',-1,0],['RIGHT',1,0]];
        const d=dirs.find(v=>{const t=map.getTile(pc.tile.i+v[1],pc.tile.j+v[2]);return t&&t.isPassable()&&t.type==='floor'});
        assert(!!d,'Fixture has an adjacent floor');
        const tile=map.getTile(pc.tile.i+d[1],pc.tile.j+d[2]);tile.item=new Ring(tile,SOVEREIGN_RING);player.fov();
        const initialRings=JSON.stringify(game.rings),ringCount=game.ringsCollected;
        A.history=[A.capture()];A.cursor=0;A.pending=null;
        const initialRng=JSON.stringify(ROT.RNG.getState()),turn=game.turnCount;
        function fingerprint(){return JSON.stringify([game.turnCount,game.state,pc.hp,pc.tile.i,pc.tile.j,game.ringsCollected,game.rings,ROT.RNG.getState(),!!tile.item])}
        async function act(extra){return B.seedAct({expectedRevision:B.observe().revision,requestId:crypto.randomUUID(),...extra})}
        const before=fingerprint(),dom=document.body.innerHTML;
        const forecast=B.seedForecast(B.observe().revision).forecasts.find(v=>v.command===d[0]);
        assert(forecast.prompt&&!forecast.dead,'Ring pickup has a safe one-move forecast');
        assert(before===fingerprint()&&dom===document.body.innerHTML&&!document.querySelector('#ring-canvas'),'Forecast with no ring canvas restores world/RNG/DOM exactly');
        const picked=await act({type:'command',command:d[0]});
        assert(picked.settled&&menu.id==='ringNote'&&menu.menuOpen&&game.ringsCollected===ringCount+1&&!tile.item,'Guarded acquisition executes real pickup and opens ring note');
        const canvas=document.querySelector('#ring-canvas'),ctx=canvas.getContext('2d');
        assert(canvas.width===160&&ctx.getImageData(0,0,160,160).data.some((v,i)=>i%4===3&&v>0),'Actual ring icon still renders');
        await act({type:'seed',operation:'undo'});
        assert(game.turnCount===turn&&game.ringsCollected===ringCount&&JSON.stringify(game.rings)===initialRings&&tile.item&&JSON.stringify(ROT.RNG.getState())===initialRng,'Undo pending ring prompt restores pickup, inventory and RNG');
        ctx.fillStyle='#ff00ff';ctx.fillRect(0,0,160,160);
        const pixels=Array.from(ctx.getImageData(0,0,160,160).data);
        B.seedForecast(B.observe().revision);
        const afterPixels=ctx.getImageData(0,0,160,160).data;
        assert(pixels.every((v,i)=>v===afterPixels[i]),'Preview never paints into a real canvas retained from an earlier ring note');
        await act({type:'command',command:d[0]});
        assert(game.ringsCollected===ringCount+1&&menu.id==='ringNote','Guarded replay reacquires ring once');
        return {checks,turn:game.turnCount,ringCount:game.ringsCollected,menu:B.observe().menu};
    }''')
    page.wait_for_function("document.querySelector('#seed-aids-v2').style.display==='none'")
    page.screenshot(path='mod-tests/llm-ring-verified.png')
    print(json.dumps(result,indent=2))
    assert not errors,errors
    browser.close()

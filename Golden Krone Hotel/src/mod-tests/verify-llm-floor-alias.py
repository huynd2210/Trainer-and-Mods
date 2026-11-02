"""A cached departed floor must not retain stale records of mobile objects."""
import json
import sys
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,args=['--use-gl=angle','--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1440,'height':1000})
    page.on('pageerror',lambda e:print('PAGE ERROR',str(e),flush=True))
    page.goto('http://127.0.0.1:8765',wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"',timeout=30000)
    page.evaluate("seedAids.foresight=false;titleScreen();game.seed='KFKWWFHBSQAWYY';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy',timeout=60000)
    result=page.evaluate('''async(oldQuick)=>{
        const A=seedAids,B=gkhLLM,checks=[];
        A.foresight=false;B.control(true,true);
        function assert(ok,msg){if(!ok)throw Error(msg);checks.push(msg)}
        function fingerprint(){return JSON.stringify([map.levelIndex,pc.tile.level.index,pc.tile.i,pc.tile.j,pc.hp,game.turnCount,game.time,ROT.RNG.getState(),map.levels.map(l=>l.tiles.map(col=>col.map(t=>[t.monster===pc,t.monster&&t.monster.name]))),map.getCurrentLevel().monsters.map(m=>[m.name,m.tile.i,m.tile.j,m.hp,m.actionPoints])])}
        async function act(extra){return B.seedAct({expectedRevision:B.observe().revision,requestId:crypto.randomUUID(),...extra})}
        const destination=map.levels[23].tiles.flat().find(t=>t.type==='floor'&&t.level.getTile(t.i,t.j-1).type==='floor'&&t.level.getTile(t.i+1,t.j).type==='floor');
        pc.tile.monster=null;pc.tile=destination;destination.monster=pc;map.levelIndex=23;
        const fixture=map.getCurrentLevel();
        fixture.monsters.filter(m=>m!==pc).forEach(m=>{if(m.tile.monster===m)m.tile.monster=null});fixture.monsters=fixture.monsters.filter(m=>m===pc);
        map.levels[0].regressionPlayerAlias=pc;
        const right=map.getTile(pc.tile.i+1,pc.tile.j),grunt=new Grunt(right);right.monster=grunt;fixture.monsters.push(grunt);player.fov();
        if(oldQuick){const restore=A.restore;A.restore=function(s,quick){return restore.call(A,quick?Object.assign({},s,{floors:s.floors.filter(f=>f.level===s.level||f.level===map.getCurrentLevel())}):s)}}
        const initial=fingerprint(),predicted=B.seedForecast(B.observe().revision).forecasts.find(f=>f.command==='RIGHT');
        assert(predicted.attacked===true,'RIGHT forecast attacks the adjacent enemy despite a departed floor referencing the player');
        assert(initial===fingerprint(),'Cross-floor aliases preserve every tile occupant and mobile object after all forecasts');
        const aliases=fixture.tiles.flat().filter(t=>t.monster===pc);
        assert(aliases.length===1&&aliases[0]===pc.tile,'Forecast leaves exactly one player tile');
        // Create a fixture on a real lower floor, cross normally, then revisit.
        for(let crossing=0;crossing<4;crossing++){
            const level=map.getCurrentLevel();
            level.monsters.filter(m=>m!==pc).forEach(m=>{if(m.tile.monster===m)m.tile.monster=null});level.monsters=level.monsters.filter(m=>m===pc);
            const stair=level.tiles.flat().find(t=>t.type==='staircase'&&!t.collapsed&&t.getDestinationObject().levelIndex!==map.FANE_LEVEL);
            pc.tile.monster=null;pc.tile=stair;stair.monster=pc;player.fov();
            level.regressionPlayerAlias=pc;
            A.history=[A.capture()];A.cursor=0;A.pending=null;
            await act({type:'command',command:'CONTEXTUAL_ACTION'});
            const next=map.getCurrentLevel();
            next.monsters.filter(m=>m!==pc).forEach(m=>{if(m.tile.monster===m)m.tile.monster=null});next.monsters=next.monsters.filter(m=>m===pc);
            const target=next.tiles.flat().find(t=>t.type==='floor'&&!t.monster);
            pc.tile.monster=null;pc.tile=target;target.monster=pc;player.fov();
            // Prime the departed floor cache, then change a mobile root.
            A.capture();pc.hp-=2;
            const before=fingerprint();B.seedForecast(B.observe().revision);
            assert(before===fingerprint(),'Forecast preserves relocated player after crossing '+crossing);
            const snapshot=A.capture(),x=pc.tile.i,y=pc.tile.j;
            pc.hp-=1;A.restore(snapshot);
            assert(pc.tile.i===x&&pc.tile.j===y,'Snapshot restores current mobile player on crossing '+crossing);
        }
        return checks;
    }''','--old-quick' in sys.argv)
    print(json.dumps(result,indent=2));browser.close()

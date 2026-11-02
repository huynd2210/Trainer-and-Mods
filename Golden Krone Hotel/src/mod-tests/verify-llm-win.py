"""Regression: contextual forgiveness must stop its preview at victory."""
import json
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    b = p.chromium.launch(headless=True, args=['--use-gl=angle', '--use-angle=swiftshader', '--enable-webgl'])
    page = b.new_page()
    if '--old' in sys.argv:
        source = Path('package.nw/js/seed-aids.js').read_text(encoding='utf-8')
        source = source.replace(' && !game.won)', ')').replace('!terminal() && !game.won &&', '!terminal() &&')
        page.route('**/js/seed-aids.js', lambda r: r.fulfill(body=source, content_type='application/javascript'))
    page.goto('http://127.0.0.1:8765', wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"')
    page.evaluate("titleScreen();game.seed='seed-win-regression';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy', timeout=60000)
    result = page.evaluate('''async () => {
        seedAids.foresight=false;gkhLLM.control(true,true);
        map.FANE_LEVEL=map.levelIndex;game.forgivyTime=true;
        pc.tile.torch=1;pc.hp=pc.maxHp;pc.statuses=[];
        function fingerprint(){return JSON.stringify([game.state,game.won,game.forgivyTime,
            game.turnCount,pc.hp,player.gold,ROT.RNG.getState(),game.actionQueue]);}
        const before=fingerprint();let writes=0,write=storage.write;
        storage.write=function(){writes++;return write.apply(this,arguments)};
        const preview=seedAids.previewAction(()=>input.queueCommand('CONTEXTUAL_ACTION',{shift:false}));
        storage.write=write;
        if(preview.dead || preview.turns!==0 || fingerprint()!==before || writes)
            throw Error('Winning preview must stop at victory without advancing or writing live state');
        const state=gkhLLM.observe();
        const r=await gkhLLM.seedAct({type:'command',command:'CONTEXTUAL_ACTION',
            expectedRevision:state.revision,requestId:'guarded-forgiveness'});
        if(!r.state.won || r.state.player.hp<=0)throw Error('Guarded forgiveness did not win alive');
        return {preview,won:r.state.won,hp:r.state.player.hp,storageWritesDuringPreview:writes};
    }''')
    print(json.dumps(result))
    b.close()

"""Measure seed-aid work in the real engine, in an isolated browser."""
import json
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

with sync_playwright() as p:
    b = p.chromium.launch(headless=True, args=['--use-gl=angle', '--use-angle=swiftshader', '--enable-webgl'])
    page = b.new_page(viewport={'width': 1440, 'height': 1000})
    baseline = '--baseline' in sys.argv
    if baseline:
        source = Path('mod-backup/before-seed-performance/seed-aids.js').read_text(encoding='utf-8')
        page.route('**/js/seed-aids.js', lambda r: r.fulfill(body=source, content_type='application/javascript'))
    page.goto('http://127.0.0.1:8765', wait_until='domcontentloaded')
    page.wait_for_function('game.imageLoaded && game.state === "updateSplash"')
    page.evaluate("titleScreen();game.seed='seed-performance';player.class=0;startGame(false,true)")
    page.wait_for_function('window.pc && seedAids.active && !gkhLLM.observe().busy', timeout=60000)
    page.evaluate('''() => {
        window.profile={capture:[],restore:[],forecast:[],mapPaints:0,mutations:0};
        for(const k of ['capture','restore','forecast']) {
            const original=seedAids[k];seedAids[k]=function(){
                const start=performance.now();const r=original.apply(this,arguments);
                profile[k].push(performance.now()-start);return r;
            };
        }
        const ctx=document.getElementById('sa-map').getContext('2d'), fill=ctx.fillRect;
        ctx.fillRect=function(){profile.mapPaints++;return fill.apply(this,arguments)};
        new MutationObserver(m=>profile.mutations+=m.length).observe(document.getElementById('seed-aids-v2'),{childList:true,subtree:true});
    }''')
    page.wait_for_timeout(2000)
    idle = page.evaluate('JSON.parse(JSON.stringify(profile))')
    if not baseline:
        assert idle['mapPaints'] == idle['mutations'] == 0, 'Idle seed UI must perform no map paints/DOM rebuilds'
        page.evaluate('''() => {
            const liveCanvas=pc._canvas, width=liveCanvas.width;
            const graphics=game.managedGraphics.length;
            for(let i=0;i<40;i++) {
                let spawned;
                seedAids.previewAction(()=>{spawned=new BabySpider(pc.tile)});
                if(spawned._canvas.width!==0 || spawned._canvas.height!==0)
                    throw Error('Preview retained a native monster canvas');
                if(spawned._webGLTexture.baseTexture)
                    throw Error('Preview retained a native monster texture');
            }
            if(pc._canvas!==liveCanvas || liveCanvas.width!==width)
                throw Error('Preview disposed live player graphics');
            if(game.managedGraphics.length!==graphics)
                throw Error('Preview polluted live graphics ownership');
            if(seedAids.limit!==48)throw Error('Undo history memory bound missing');
        }''')
        page.evaluate('''() => {
            map.levels[29].seenStairs=true;
            seedAids.forecast();
            if(!map.levels[29].seenStairs)throw Error('Cached remote floor lost real staircase discovery');
        }''')
    measurements = page.evaluate('''() => {
        seedAids.foresight=false;
        const out=[];
        for(const index of [0,9,24]) {
            map.levelIndex=index;const level=map.getCurrentLevel();
            level.placeExistingMonster(pc,level.getTile(3,3));
            const a=performance.now(),s=seedAids.capture(),captureMs=performance.now()-a;
            const c=performance.now();seedAids.restore(s);const restoreMs=performance.now()-c;
            const f=performance.now();const predicted=seedAids.forecast();const forecastMs=performance.now()-f;
            for(const r of [...s.records,...s.floors.flatMap(f=>f.records)]) {
                if(Object.keys(r.object).length!==r.keys.length || r.keys.some((k,i)=>r.object[k]!==r.values[i]))
                    throw Error('Forecast changed snapshot object on floor '+index+' '+r.object.constructor.name+' bank '+r.object.index+' extra '+Object.keys(r.object).filter(k=>!r.keys.includes(k))+' '+r.keys.filter((k,i)=>r.object[k]!==r.values[i]).map((k)=>k+':'+String(r.object[k])).join(',').slice(0,400));
            }
            if(JSON.stringify(s.rng)!==JSON.stringify(ROT.RNG.getState()))throw Error('Forecast changed RNG');
            // Compare optimized forecasting with the former full-bank behavior.
            const restore=seedAids.restore;
            seedAids.restore=function(snapshot){return restore.call(this,snapshot,false)};
            let full;try{full=seedAids.forecast()}finally{seedAids.restore=restore}
            if(JSON.stringify(predicted)!==JSON.stringify(full))throw Error('Forecast differs from full restoration on floor '+index);
            const g=performance.now(),fastGuard=seedAids.previewAction(()=>input.queueCommand('WAIT',{shift:false}),true),guardMs=performance.now()-g;
            const h=performance.now(),fullGuard=seedAids.previewAction(()=>input.queueCommand('WAIT',{shift:false})),fullGuardMs=performance.now()-h;
            if(JSON.stringify(fastGuard)!==JSON.stringify(fullGuard))throw Error('Death check differs from full restoration');
            out.push({index,captureMs,restoreMs,forecastMs,guardMs,fullGuardMs,records:s.records.length,
                totalFloorRecords:s.floors.reduce((n,f)=>n+f.records.length,0)});
        }return out;
    }''')
    if not baseline:
        page.evaluate("seedAids.foresight=true;seedAids.dirty=true;document.getElementById('sa-collapse').click()")
        counts=page.evaluate('profile.forecast.length')
        page.wait_for_timeout(600)
        assert page.evaluate('profile.forecast.length') == counts, 'Collapsed display must not compute hidden forecasts'
        page.evaluate("document.getElementById('sa-collapse').click()")
        assert page.evaluate('profile.forecast.length') > counts, 'Reopened display must refresh forecasts'
    result={'baseline':baseline,'idle':idle,'measurements':measurements}
    Path('mod-tests/seed-performance-'+('before' if baseline else 'after')+'.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result))
    b.close()

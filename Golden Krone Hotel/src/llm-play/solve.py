"""Local seed-play controller. Uses authenticated tool endpoints only."""
import argparse
import heapq
import json
from pathlib import Path
import time
import uuid
from gkh import Bridge

BASE = Path(__file__).resolve().parent
B = Bridge()
DIRS = {'UP': (0,-1), 'DOWN': (0,1), 'LEFT': (-1,0), 'RIGHT': (1,0)}

def summary(s):
    return {k:s.get(k) for k in ('revision','enabled','exclusive','releasing','state','busy','turn','time','won','floor','player','abilities','targeting','menu','hint','contextual','seedHarness')} | {'messages':s.get('messages',[])[-8:]}

def observe():
    d = B.call('/seed/state')
    (BASE/'current-seed-state.json').write_text(json.dumps(d), encoding='utf-8')
    return d

def action(extra, d=None):
    s = (d or observe())['state']
    if not s.get('enabled') or s.get('exclusive') is not True:
        raise RuntimeError('Remote play is paused or exclusive ownership is absent; no action sent')
    a = {'expectedRevision':s['revision'],'requestId':uuid.uuid4().hex, **extra}
    with (BASE/'play-session.jsonl').open('a',encoding='utf-8') as f:
        f.write(json.dumps({'before':summary(s),'request':a})+'\n')
    endpoint = '/seed/control' if a['type']=='seed' else '/seed/action'
    try:
        result = B.call(endpoint,a)
    except RuntimeError as e:
        if 'connection failed' not in str(e).lower(): raise
        result = B.call(endpoint,a)  # Same id/payload only after a transport failure.
    latest=B.call('/state')
    if not result['settled'] or latest['busy']:
        for _ in range(200):
            s=B.call('/state')
            if not s['busy']: break
            time.sleep(.1)
        else: raise RuntimeError('Action still processing; no next action sent')
        latest=s
    result['state']=latest
    with (BASE/'play-session.jsonl').open('a',encoding='utf-8') as f:
        f.write(json.dumps({'requestId':a['requestId'],'result':summary(result['state']),'settled':result['settled']})+'\n')
    if result['state'].get('won'): print('WON',flush=True)
    return result

def traversable(t):
    return t and (t['passable'] or t['type']=='door') and t['type'] not in ('pit','lava','water','spike','ice','grid')

def paths(d, goal):
    tiles={(t['x'],t['y']):t for t in d['omniscience']['tiles']}
    p=d['state']['player'];start=(p['x'],p['y'])
    queue=[(0,start)];dist={start:0};prev={}
    while queue:
        cost,pos=heapq.heappop(queue)
        if cost!=dist[pos]:continue
        if pos==goal:break
        for command,(dx,dy) in DIRS.items():
            n=(pos[0]+dx,pos[1]+dy);t=tiles.get(n)
            if not traversable(t):continue
            if t.get('item',{}).get('name')=='Manual' and p['form']=='human' and p['hp']>p['maxHp']-1:
                continue  # Automatic reading spends an extra turn; the guard refuses it.
            m=t.get('monster');weight=4 if t['type']=='water' else 1
            if m and not m['friendly']:
                if m['type']=='obstacle' and m['name'] not in ('Chest','Door','Barrel','Chair','Table'):weight+=30
                else:weight+=2+max(0,m.get('level',p['level'])-p['level'])*8
            # Preserve health rather than walking through a hostile crowd.
            weight+=sum(.3 for dx2,dy2 in DIRS.values() if (q:=tiles.get((n[0]+dx2,n[1]+dy2))) and q.get('monster') and not q['monster']['friendly'] and q['monster']['type']!='obstacle')
            new=cost+weight
            if new<dist.get(n,float('inf')):
                dist[n]=new;prev[n]=(pos,command);heapq.heappush(queue,(new,n))
    if goal not in dist:return None
    route=[];pos=goal
    while pos!=start:
        pos,command=prev[pos];route.append(command)
    return list(reversed(route))

def inspect(d):
    print(json.dumps(summary(d['state']),indent=2))
    print('POTIONS',json.dumps(d['omniscience']['potions']))
    print('POINTS',json.dumps([t for t in d['omniscience']['tiles'] if t.get('item') or t.get('monster') or t['type']=='staircase']))

def quaff(name):
    d=observe()
    potion=next((p for p in d['omniscience']['potions'] if p['trueName']==name),None)
    if not potion:raise RuntimeError('Potion not carried: '+name)
    if not d['state']['menu']['open']:action({'type':'command','command':'POTIONS'},d)
    d=observe()
    choice=next((c for c in d['state']['menu']['choices'] if potion['name'] in c['text']),None)
    if not choice:raise RuntimeError('Potion missing from current menu')
    return action({'type':'choose','index':choice['index']},d)

def go(goal, count):
    d=observe();floor=d['state']['floor']['index'];last=None;stuck=0
    for i in range(count):
        s=d['state'];p=s['player'];pos=(p['x'],p['y'])
        if s['won'] or s['floor']['index']!=floor or pos==goal:break
        if s['menu']['open'] and s['menu']['id']=='levelUp':
            action({'type':'choose','index':0},d);d=observe();continue
        if s['menu']['open'] or not s['seedHarness']['canForecast']:
            print('CHOICE',json.dumps(summary(s)),flush=True);break
        route=paths(d,goal)
        if not route:print('NO ROUTE',goal,flush=True);break
        chosen=route[0];outcome={}
        # Every input still gets the mandatory exact-action foresight in seedAct.
        # Compare all nine alternatives when a living hostile is near the route.
        nearby=True  # Inspect fresh nine-way foresight before each movement.
        if nearby:
            try:f=B.call('/seed/forecast?revision='+str(s['revision']))
            except RuntimeError as e:
                if 'Stale observation' not in str(e):raise
                d=observe();continue  # Read-only retry; replan from the new state.
            forecasts={v['command']:v for v in f['forecasts']};outcome=forecasts[chosen]
            if outcome.get('dead') or outcome.get('disabled'):
                print('UNSAFE ROUTE',json.dumps(f['forecasts']),flush=True);break
            if outcome.get('hp',0)<-max(8,p['maxHp']*.08) or p['hp']+outcome.get('hp',0)<p['maxHp']*.3:
                print('COMBAT DECISION',json.dumps(f['forecasts']),flush=True);break
        try:result=action({'type':'command','command':chosen},d)
        except RuntimeError as e:
            if 'Stale observation' not in str(e):raise
            d=observe();continue  # Rejected before execution; make a new decision.
        q=result['state']['player']
        print(json.dumps({'step':i+1,'command':chosen,'hpChange':outcome.get('hp'),'damage':outcome.get('damage'),'turn':result['state']['turn'],'pos':[q['x'],q['y']],'hp':q['hp'],'mp':q['mp'],'form':q['form'],'messages':result['state']['messages'][-2:]}),flush=True)
        stuck=stuck+1 if (q['x'],q['y'])==pos and result['state']['turn']==s['turn'] else 0
        if stuck>=2:print('BLOCKED',flush=True);break
        d=observe()
        if q['hp']-p['hp'] < -max(8,p['maxHp']*.08):
            print('UNEXPECTED DAMAGE: inspect before continuing',flush=True);break
    print('END',json.dumps(summary(d['state'])),flush=True)
    return d

def main():
    parser=argparse.ArgumentParser();sub=parser.add_subparsers(dest='mode',required=True)
    sub.add_parser('inspect');sub.add_parser('forecast')
    a=sub.add_parser('act');a.add_argument('payload')
    c=sub.add_parser('command');c.add_argument('command')
    ch=sub.add_parser('choose');ch.add_argument('index',type=int)
    t=sub.add_parser('target');t.add_argument('x',type=int);t.add_argument('y',type=int)
    g=sub.add_parser('go');g.add_argument('x',type=int);g.add_argument('y',type=int);g.add_argument('--steps',type=int,default=12)
    sub.add_parser('undo')
    q=sub.add_parser('quaff');q.add_argument('name')
    args=parser.parse_args()
    if args.mode=='inspect':inspect(observe())
    elif args.mode=='forecast':
        d=observe();print(json.dumps(B.call('/seed/forecast?revision='+str(d['state']['revision']))['forecasts'],indent=2))
    elif args.mode=='go':go((args.x,args.y),args.steps)
    elif args.mode=='quaff':print(json.dumps(summary(quaff(args.name)['state']),indent=2))
    else:
        payload=json.loads(args.payload) if args.mode=='act' else {'type':'command','command':args.command} if args.mode=='command' else {'type':'choose','index':args.index} if args.mode=='choose' else {'type':'target','x':args.x,'y':args.y} if args.mode=='target' else {'type':'seed','operation':'undo'}
        print(json.dumps(summary(action(payload)['state']),indent=2))

if __name__=='__main__':main()

"""Observed, guarded seed-play helpers; never queues a route or bypasses refusal."""
import argparse
import json
import re
from solve import B, DIRS, observe, action, paths, quaff, summary

def brief(d):
    s=d['state']
    print(json.dumps({'floor':s.get('floor'),'player':s.get('player'),'menu':s.get('menu'),
        'messages':s.get('messages',[])[-4:]}),flush=True)

def resolve(d):
    s=d['state'];m=s['menu']
    if m['open'] and m['id']=='levelUp':
        action({'type':'choose','index':0},d);return True
    if m['open'] and m['id']=='ringNote':
        action({'type':'command','command':'CONFIRM'},d);return True
    if m['open'] and 'spell to learn' in m['title'].lower():
        preference=['HEAL','ENRAGE','DISAPPEAR','SHOCK','BLINK','CLEANSE','QUILLS','HASTE','ICE','BLAST','WARD','FOCUS','LEVITATE','TELEPORT']
        choice=min(m['choices'],key=lambda c:next((i for i,name in enumerate(preference) if re.search(r'\b'+name+r':',c['text'])),100))
        action({'type':'choose','index':choice['index']},d);return True
    return False

def useful(t,p):
    item=t.get('item')
    if not item:return False
    name=item['name']
    if name=='Blood' and p['form']!='vampire':return False
    if name in ('Grimoire','Legendary Grimoire','Manual','Primer','Master Key',"Hunter's Lens",'Soul Elixir','Blood','Demon Blood',
        'Health Potion','Holy Water','Potion of Experience','Honey','Curry Noodle Soup','Celestial Essence','Antidote',
        'Invisibility Potion','Strength Boost','Quick','Amnesia','Tome of Pain') or 'Ring' in name:return True
    if any(x in name.lower() for x in ('bullet','gold pieces')):return True
    bonus=re.search(r'\(\+(\d+)\)',name)
    if bonus:
        for part,key in [('Sword','sword'),('Shield','shield'),('Armor','armor'),('Mail','armor'),('Breast Plate','armor'),('Leggings','leggings'),('Revolver','revolver')]:
            if part in name:return int(bonus[1])>p[key]
    return False

def run(mode,steps,x=None,y=None):
    d=observe();floor=d['state']['floor']['index'];goal=(x,y) if x is not None else None
    for i in range(steps):
        s=d['state'];p=s['player'];pos=(p['x'],p['y'])
        if s['won'] or s['floor']['index']!=floor:break
        if resolve(d):d=observe();continue
        if s['menu']['open'] or not s['seedHarness']['canForecast']:
            print('NEEDS CHOICE',flush=True);break
        carried={v['trueName']:v for v in d['omniscience']['potions']}
        if p['hunger']<25 and p['form']=='human' and ('Honey' in carried or 'Curry Noodle Soup' in carried):
            quaff('Honey' if 'Honey' in carried else 'Curry Noodle Soup');d=observe();continue
        if 'Potion of Experience' in carried and p['level']<10:
            quaff('Potion of Experience');d=observe();continue
        if p['hp']<p['maxHp']*.5 and 'Health Potion' in carried:
            quaff('Health Potion');d=observe();brief(d);continue
        near=[t for t in d['omniscience']['tiles'] if t.get('monster') and not t['monster']['friendly']
            and t['monster']['type']!='obstacle' and abs(t['x']-p['x'])+abs(t['y']-p['y'])<=4]
        spells={a['name']:a['command'] for a in s.get('abilities',[])}
        statuses={v['name'] for v in p['statuses']}
        if mode!='heal' and near and p['form']=='human' and 'haste' in spells and p['mp']>=6 and 'Fast' not in statuses:
            action({'type':'command','command':spells['haste']},d);d=observe();brief(d);continue
        if mode!='heal' and near and p['form']=='human' and 'quills' in spells and p['mp']>=8 and p['hp']<p['maxHp']*.7 and 'Quills Shield' not in statuses:
            action({'type':'command','command':spells['quills']},d);d=observe();brief(d);continue
        # Samcas have one HP but inflict ranged psychic damage. Remove them
        # promptly instead of walking through their line of fire.
        samca=next((t for t in d['omniscience']['tiles'] if t['visible'] and t.get('monster',{}).get('name')=='Samca'),None)
        if samca and p['form']=='human' and p['bullets']>0:
            action({'type':'command','command':'REVOLVER'},d)
            action({'type':'target','x':samca['x'],'y':samca['y']},observe())
            action({'type':'command','command':'CONFIRM_TARGET'},observe())
            d=observe();brief(d);continue
        if mode=='loot':
            candidates=[]
            for t in d['omniscience']['tiles']:
                if useful(t,p):
                    dest=(t['x'],t['y']);r=paths(d,dest)
                    if r:candidates.append((len(r),dest,t['item']['name']))
            if not candidates:print('LOOT COMPLETE',flush=True);break
            _,goal,name=min(candidates)
        if mode=='go' and pos==goal:break
        if mode=='heal' and p['hp']>=p['maxHp']*.98:break
        f=B.call('/seed/forecast?revision='+str(s['revision']))
        fs={v['command']:v for v in f['forecasts'] if not v.get('disabled') and not v.get('dead')}
        attacks=[v for v in fs.values() if v.get('attacked')]
        route=paths(d,goal) if goal else []
        if mode=='fight':
            if not attacks:print('NO ADJACENT HOSTILE',flush=True);break
            chosen=max(attacks,key=lambda v:(v.get('kill',False),v.get('damage',0)*2+v.get('hp',0)))
        elif mode=='heal':chosen=fs.get('WAIT')
        elif route:chosen=fs.get(route[0])
        else:print('NO ROUTE',goal,flush=True);break
        if chosen and attacks and mode not in ('heal','fight'):
            best=max(attacks,key=lambda v:(v.get('kill',False),v.get('damage',0)*2+v.get('hp',0)))
            if best.get('kill') and best.get('hp',0)>=chosen.get('hp',0)-2:
                chosen=best
        minimum=3 if mode=='heal' and not attacks else max(10,p['maxHp']*.25)
        if not chosen or (p['hp']+chosen.get('hp',0)<minimum and chosen.get('hp',0)<0) or chosen.get('hp',0)<-max(10,p['maxHp']*.15):
            print('COMBAT DECISION',json.dumps(f['forecasts']),flush=True);break
        if mode=='heal' and chosen.get('hp',0)<=0 and not any(v['name'] in ('Bleeding','Poison') for v in p['statuses']):
            print('CANNOT HEAL HERE',flush=True);break
        result=action({'type':'command','command':chosen['command']},d)
        q=result['state']['player']
        if i%5==0 or chosen.get('attacked') or result['state']['messages'][-2:]!=s['messages'][-2:]:
            print(json.dumps({'step':i,'goal':goal,'command':chosen['command'],'predicted':chosen.get('hp'),
                'damage':chosen.get('damage'),'turn':result['state']['turn'],'hp':q['hp'],'pos':[q['x'],q['y']],
                'messages':result['state']['messages'][-2:]}),flush=True)
        if result['state']['turn']==s['turn'] and q['x']==p['x'] and q['y']==p['y']:
            print('BLOCKED',flush=True);d=observe();break
        d=observe()
    brief(d)

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('mode',choices=['loot','fight','heal','go','inspect'])
    ap.add_argument('x',nargs='?',type=int);ap.add_argument('y',nargs='?',type=int);ap.add_argument('--steps',type=int,default=35)
    args=ap.parse_args()
    if args.mode=='inspect':
        d=observe();brief(d)
        print('POINTS',json.dumps([t for t in d['omniscience']['tiles'] if t.get('item') or t['type']=='staircase' or t.get('monster',{}).get('type') not in (None,'obstacle')]))
        print('POTIONS',json.dumps(d['omniscience']['potions']))
    else:run(args.mode,args.steps,args.x,args.y)

import sys,json
sys.path.insert(0,'llm-play')
from solve import observe,action,paths,B,quaff
for i in range(40):
 d=observe();s=d['state'];p=s['player']
 if s['menu']['open']:break
 foes=[t for t in d['omniscience']['tiles'] if t.get('monster',{}).get('name')=='Green Man']
 if not foes:break
 q=min(foes,key=lambda t:abs(t['x']-p['x'])+abs(t['y']-p['y']));route=paths(d,(q['x'],q['y']))
 if not route:break
 cmd=route[0];f=B.call('/seed/forecast?revision='+str(s['revision']));v=next(v for v in f['forecasts'] if v['command']==cmd)
 print(i,q['x'],q['y'],q['monster']['hp'],cmd,v.get('hp'),v.get('damage'),flush=True)
 if v.get('dead') or p['hp']+v.get('hp',0)<12:break
 action({'type':'command','command':cmd},d)
print(observe()['state']['player'])

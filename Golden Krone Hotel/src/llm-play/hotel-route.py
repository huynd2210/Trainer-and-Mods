"""Navigate observed hotel stairs one guarded input at a time."""
import sys
from solve import observe,paths,action
from expedition import run
wanted=int(sys.argv[1])
for _ in range(12):
 d=observe();s=d['state'];index=s['floor']['index']
 if index==wanted:break
 if not 0<=index<=8:raise RuntimeError('Unexpected branch floor')
 next_floor=index+(1 if index<wanted else -1)
 candidates=[]
 for t in d['omniscience']['tiles']:
  if t['type']=='staircase' and t['name']==f'Staircase to Floor {next_floor+1}':
   r=paths(d,(t['x'],t['y']))
   if r is not None:candidates.append((len(r),t['x'],t['y']))
 if not candidates:raise RuntimeError('No observed route to next hotel floor')
 _,x,y=min(candidates);run('go',100,x,y)
 d=observe();p=d['state']['player']
 if (p['x'],p['y'])!=(x,y):raise RuntimeError('Route needs a combat decision')
 action({'type':'command','command':'CONTEXTUAL_ACTION'},d)
print('Reached',observe()['state']['floor'])
